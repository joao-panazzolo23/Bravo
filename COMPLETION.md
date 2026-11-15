# COMPLETION REPORT — BRAVO

Status of the implementation measured against the specifications in `prompts/`.

- **Date:** 2026-11-14
- **Sources of truth:** `prompts/01_SystemContext.md`, `prompts/02_Architecture.md`, `prompts/03_ProjectSetup.md`, `prompts/04_Recuperability.md`
- **Scope of this report:** external data acquisition, dataset processing and PostgreSQL persistence only.
- **Verification:** build, unit tests, and live end-to-end runs against the real IBGE, TSE and FGV sources and a real PostgreSQL 16 database.

Legend: **Done** · **Partial** · **Not implemented** · **Not verified**

---

## 1. Summary

The acquisition subsystem previously supported only IBGE geography, through per-row Dapper
upserts, with no TSE or FGV integration, no file downloads, and no authoritative overlap
protection. This session completed the acquisition, processing and persistence layers for
**IBGE, TSE and FGV**, added the electoral domain required to persist TSE data, and made the
import lifecycle persistent, leased and resumable.

Everything listed below was exercised against the live sources and a real database, not just
compiled. The final end-to-end run imported the official 2022 TSE section results for Acre
through the real handlers:

| Metric | Source file | Persisted |
| --- | --- | --- |
| Candidate vote rows | 237,198 | 237,198 |
| Candidate votes | 1,672,287 | 1,672,287 |
| Section statistics rows | 8,496 | 8,496 |
| Eligible voters | 2,348,888 | 2,348,888 |
| Attendees | 1,821,752 | 1,821,752 |
| Blank / null votes | 46,020 / 63,282 | 46,020 / 63,282 |
| Orphan result rows | — | 0 |
| Duplicate natural keys | — | 0 |

A second identical execution updated the same 237,198 rows and created none, confirming
idempotency.

---

## 2. Data acquisition — implemented providers and endpoints

| Provider | Dataset | Official endpoint | Format |
| --- | --- | --- | --- |
| IBGE | States | `https://servicodados.ibge.gov.br/api/v1/localidades/estados?orderBy=nome` | JSON array |
| IBGE | Municipalities | `https://servicodados.ibge.gov.br/api/v1/localidades/municipios?orderBy=nome` | JSON array |
| TSE | Candidates + election metadata | resolved from `https://dadosabertos.tse.jus.br/api/3/action/package_show?id=candidatos-{year}` → `cdn.tse.jus.br/.../consulta_cand/consulta_cand_{year}.zip` | ZIP of Latin-1 CSV |
| TSE | Polling-section votes | `package_show?id=resultados-{year}` → `votacao_secao_{year}_{UF}.zip` (state scope) or `votacao_secao_{year}_BR.zip` (national) | ZIP of Latin-1 CSV |
| TSE | Polling-section details/statistics | `package_show?id=resultados-{year}` → `detalhe_votacao_secao/detalhe_votacao_secao_{year}.zip` | ZIP of Latin-1 CSV |
| TSE | Official TSE↔IBGE municipality crosswalk | `package_show?id=codigos-oficiais-de-uf-e-municipios-segundo-o-tse-e-o-ibge` → `municipio_tse_ibge/municipio_tse_ibge.zip` | ZIP of Latin-1 CSV |
| FGV/CEPESP | IBGE↔TSE municipality crosswalk | `https://raw.githubusercontent.com/GV-CEPESP/cepespdata/main/tabelas_auxiliares/dados/codigo_municipio_ibge_tse.csv` | UTF-8 comma CSV |

Live verification of resolution and download:

```
Candidates             -> consulta_cand_2022.zip (4,444,845 bytes, sha256 4856885754fa…)
PollingSectionResults  -> votacao_secao_2022_BR.zip
PollingSectionDetails  -> detalhe_votacao_secao_2022.zip
MunicipalityCrosswalk  -> municipio_tse_ibge.zip
IBGE states            -> 2,446 bytes, 27 parsed
IBGE municipalities    -> 2,470,036 bytes, 5,571 parsed
FGV crosswalk          -> 676,817 bytes, 5,709 parsed
TSE crosswalk          -> 5,571 parsed
```

### Acquisition requirements

| Requirement | Status | Where |
| --- | --- | --- |
| Native HTTP via typed `HttpClient` | Done | `AcquisitionExtensions.RegisterProvider` + `AddHttpClient(key, …)`; providers receive `HttpClient` indirectly through `DatasetFetcher` |
| Provider-specific implementations | Done | `IbgeProvider`, `TseProvider`, `FgvProvider` |
| Streaming downloads | Done | `DatasetFetcher.DownloadAsync` streams with `ResponseHeadersRead` into a temp file in 80 KB chunks |
| Cancellation propagation | Done | `CancellationToken` flows to rate limiter, Fences pipeline, stream reads, parsing and persistence |
| Configurable timeouts | Done | `AcquisitionOptions.{Provider}.HttpTimeoutSeconds`, enforced by Fences `AddTimeout` |
| Retry + rate limiting via Fences | Done | `ProviderResiliencePipelineFactory`, `HostRateLimiter` |
| HTTP 429 + `Retry-After` | Done | Fences `DelayGenerator` reads delta and date forms, clamped to 60s |
| Do not retry permanent 4xx | Done | `ShouldHandle` = 429, 5xx, `HttpRequestException`, `TimeoutRejectedException`, `IOException` |
| Independent limits per host | Done | `HostRateLimiterRegistry` builds one limiter per allowlisted host from each provider policy |
| Bounded concurrency | Done | Per-host concurrency ceiling; IBGE states + municipalities download concurrently bounded by `MaxConcurrentDownloads` |
| Temporary storage | Done | `TemporaryFileStore` (per-execution directory, sidecar metadata, retention cleanup) |
| SSRF protection | Done | `DatasetFetcher.ValidateHost` requires HTTPS and an allowlisted host; TSE catalog URLs are validated against `cdn.tse.jus.br` before download |
| No invented endpoints | Done | TSE locations resolved from the official CKAN catalog at runtime |

---

## 3. Dataset processing

| Requirement | Status | Where |
| --- | --- | --- |
| Dataset-specific parsing | Done | `TseArchiveStreams` (5 TSE layouts), `DelimitedFileReader` (TSE + FGV), IBGE JSON DTOs |
| Source-specific parsing outside Domain | Done | All parsing in `Bravo.Infrastructure.Acquisition`; Domain has no parser or format type |
| Streaming / bounded memory | Done | `IAsyncEnumerable` + `BatchStream.Chunk`; no dataset is materialised |
| Configurable batch sizes | Done | `GeographyBatchSize`, `CandidateBatchSize`, `ResultBatchSize`, `StatisticsBatchSize` |
| Normalisation | Done | Zero-pads TSE municipality codes, uppercases UF, maps `#NULO`/`#NE`/`-1`/`-3`/`-4` to null, parses `dd/MM/yyyy`, classifies blank/null votes |
| Validation of incoming records | Done | Required columns fail fast; per-row validation yields a rejection instead of a silent drop |
| Malformed record handling | Done | `ParsedRecord<T>(T?, reference)`; rejections persisted as bounded samples in `ImportRejections` |
| Format variation across years | Done | Column-name access, optional-column list, delimiter/quote/encoding detection; verified against 2002, 2006, 2010, 2014, 2018 and 2022 layouts (the 2018+ files add `SQ_CANDIDATO`, the older ones omit it) |
| ZIP archive safety | Done | `ArchiveGuard` rejects path traversal, oversized entries and oversized total decompressed size; per-entry bounded read stream |
| No analytical processing | Done | Only ingestion; no rankings, dashboards or aggregate views |

### Parsers implemented

1. **IBGE JSON** — `StreamStates`, `StreamMunicipalities`. The municipality payload nests the
   UF under `microrregiao.mesorregiao.UF`; the DTO resolves that shape and the legacy top-level
   `UF`. (The nested shape was discovered by live verification, not assumed.)
2. **TSE candidates CSV** (`consulta_cand_{year}.zip`) — one CSV per state; emits the election
   identity, party, office, candidate and situation.
3. **TSE election metadata** — derived from the candidate archive (the source publishes election,
   round and contest on every candidacy row).
4. **TSE polling-section votes** (`votacao_secao_{year}_{UF}.zip`) — vote facts, blank/null
   classification, polling-place data.
5. **TSE polling-section details** (`detalhe_votacao_secao_{year}.zip`) — turnout statistics.
6. **TSE official crosswalk** (`municipio_tse_ibge.zip`).
7. **FGV crosswalk** (UTF-8 comma CSV).

---

## 4. Database persistence

### Repositories and services (all in Infrastructure)

| Type | Responsibility |
| --- | --- |
| `GeographyPersistenceService` | States, municipalities, TSE↔IBGE crosswalk; readiness probe |
| `ElectoralPersistenceService` | Elections, rounds, contests, parties, candidates; geographic lookup |
| `ElectoralResultsPersistenceService` | Zones, places, sections, candidate results, turnout statistics |
| `ImportExecutionRepository` | Lifecycle, lease claim/renew, dispatch query, status probe |
| `ImportCheckpointRepository` | Latest checkpoint and completed dataset keys |
| `ImportRejectionRepository` | Bounded rejection samples |
| `DatasetProvenanceRepository` | Provenance, latest successful acquisition |

### COPY and batch strategies

- Every large batch is loaded into a **session-local temporary staging table with
  `NpgsqlBinaryImporter` (binary COPY)** and merged with **one set-based statement**.
- One explicit transaction per batch (truncate staging → COPY → merge → commit). No transaction
  is held during download or parsing.
- No individual `INSERT` per imported electoral record: an Acre results import of 247,818 facts
  used 59 batches and 59 merge statements.
- Bulk path example (`PollingSectionResults`): staging COPY → CTE that resolves state, contest,
  candidate and section → `INSERT … ON CONFLICT … DO UPDATE` keyed by
  `(ElectoralContestId, PollingSectionId, CandidateId)`.
- Geography and candidate merges use the same COPY+merge pattern keyed on `IbgeCode`,
  `(ElectionId, TseSequential)` and `TseCode`.
- EF Core remains the choice for domain/operational records (import executions, checkpoints,
  provenance, rejections); Dapper for the set-based SQL. No EF Core is used for electoral bulk
  ingestion.

### Schema changes (migration `AddElectoralAndImportReliability`)

New tables: `Elections`, `ElectionRounds`, `ElectoralContests`, `PoliticalParties`,
`Candidates`, `ElectoralZones`, `PollingPlaces`, `PollingSections`, `PollingSectionResults`,
`PollingSectionStatistics`, `MunicipalityTseCodes`, `ImportRejections`.

`ImportExecutions` gained `RecordsRejected`, `RecordsSkipped`, `BatchesCommitted`,
`BytesDownloaded`, `RecoveryAttempts`, `HeartbeatAt`, `LeaseExpiresAt`, and a generated
`ScopeKey` column.

`DatasetProvenances` gained `ETag` and `SourceLastModifiedAt`; `Scope` and `DatasetIdentifier`
were widened.

Natural keys and unique constraints:

| Table | Unique constraint |
| --- | --- |
| `ImportExecutions` | partial `(OperationType, ScopeKey)` where status is active — authoritative overlap guard |
| `ImportCheckpoints` | `(ExecutionId, DatasetKey)` |
| `States` / `Municipalities` | `IbgeCode` |
| `MunicipalityTseCodes` | `TseCode` |
| `Elections` | `TseCode` |
| `ElectionRounds` | `(ElectionId, Number)` |
| `ElectoralContests` | `(ElectionRoundId, OfficeCode, StateId)` with `NULLS NOT DISTINCT` |
| `PoliticalParties` | `Number` |
| `Candidates` | `(ElectionId, TseSequential)` |
| `ElectoralZones` | `(StateId, Number)` |
| `PollingPlaces` | `(MunicipalityId, ZoneNumber, TseCode)` |
| `PollingSections` | `(StateId, ZoneNumber, Number)` |
| `PollingSectionResults` | `(ElectoralContestId, PollingSectionId, CandidateId)` |
| `PollingSectionStatistics` | `(ElectoralContestId, PollingSectionId)` |

All official codes are stored as text where leading zeros matter (`TseCode`, `TseSequential`,
`ZoneNumber`, `Number`, `Number` on parties remains numeric because it has no leading zeros).

Indexes were added only for the ingestion and referential paths that exist today
(contest/section/candidate lookup, election/round, candidate per election, party per election).
No analytical indexes, partitioning or materialised views were added, per `01` §12.

---

## 5. Import reliability and recovery

| Requirement | Status | Notes |
| --- | --- | --- |
| Persistent execution state | Done | PostgreSQL is authoritative; the channel only dispatches persisted ids |
| Prevent overlapping imports | Done | Partial unique index (database-level) + advisory query; duplicate start returns 409 |
| Multi-instance safety | Done | `TryClaim` performs a conditional lease update; a second instance cannot claim a live lease. Verified: first claim `true`, second `false`, reclaim after expiry `true` |
| Persistent checkpoints | Done | Unique per `(ExecutionId, DatasetKey)`, written only after a committed batch |
| Recovery after restart | Done | `GetDispatchableIds` re-dispatches pending executions and running executions whose lease expired; live leases are skipped. Verified against the database |
| Resume from checkpoint | Done | Completed dataset keys are read at startup of each handler and skipped |
| Safe cancellation | Done | `DELETE /import/{id}` sets `CancellationRequested`; a watcher polls the database every 10s and cancels the pipeline token; terminal states recorded as `Cancelled` |
| Dataset provenance | Done | Provider, dataset id, source URL, filename, content hash, size, ETag, last-modified, version, format, execution id |
| Content hash / file size | Done | SHA-256 computed while streaming; size enforced against `MaxDownloadBytes` |
| Temporary file cleanup | Done | Successful executions delete their files; a retention service removes abandoned downloads. Verified: 0 remaining files after a run |
| Error visibility | Done | Counters on the execution, bounded rejection samples with source reference |
| Idempotency | Done | Copy + `ON CONFLICT` natural keys; identical re-run updated all rows and inserted none |

### Dataset dependencies

- FGV crosswalk depends on IBGE municipalities: the handler checks `GetReadiness` and fails with
  a clear message instead of silently skipping rows (verified both paths).
- TSE results depend on the section geography: the vote file is always processed first and
  creates zones, places and sections in the same batches.
- TSE results resolve their election, round and contests from the source rows, so a results
  import does not require a prior metadata import.

---

## 6. Architectural constraints

- **Domain** references only `Bravo.Shared`. No Npgsql, Dapper or EF Core type.
- **Application** defines provider and persistence contracts only; `AcquisitionOptions` lives in
  Application because the batch sizes are orchestration concerns. No database type leaked: the
  unique-violation-to-conflict translation happens in `UnityOfWork`.
- **Infrastructure** holds HTTP, rate limiting, resilience, parsing, archive handling, temp
  storage, COPY staging, SQL and repositories.
- **Presentation** only composes DI and exposes the existing endpoints.
- No Polly, no new package references (only pinned the already-used EF Relational package to
  remove a pre-existing version-conflict warning).
- No generic repository; repositories remain boundary-focused.
- Existing endpoints, commands and the execution lifecycle were preserved.

### One deliberate interface change

`IIbgeProvider` previously streamed parsed records straight from the HTTP response. It now
downloads to temporary storage and parses from the file, as required by `04` §3 ("store large
downloads in temporary storage", "calculate checksums", "reuse verified temporary downloads").
The change is confined to the acquisition contracts; no controller or command changed.

---

## 7. Files modified or added

**Domain**
- `Acquisition/Entities/ImportExecution.cs` (counters, lease, heartbeat, resume)
- `Acquisition/Entities/DatasetProvenance.cs` (ETag, last-modified, bounded metadata)
- `Acquisition/Entities/ImportRejection.cs` *(new)*
- `Acquisition/Enums/ImportOperationType.cs` (FGV crosswalk)
- `Electoral/Entities/{Election, ElectionRound, ElectoralContest, PoliticalParty, Candidate, ElectoralZone, PollingPlace, PollingSection, PollingSectionResult, PollingSectionStatistics}.cs` *(new)*
- `Geography/Entities/MunicipalityTseCode.cs` *(new)*

**Application**
- `Acquisition/Providers/{IIbgeProvider, ITseProvider, IFgvProvider, TseRecords, TsePollingSectionRecord}.cs`
- `Acquisition/Services/{IGeographyPersistenceService, IElectoralPersistenceService, IElectoralResultsPersistenceService, ITemporaryFileStore, IImportExecutionQueue, ImportExecutionRunner, BatchStream}.cs`
- `Acquisition/Repositories/{IImportExecutionRepository, IImportCheckpointRepository, IImportRejectionRepository, IDatasetProvenanceRepository}.cs`
- `Acquisition/Handlers/{IbgeImportHandler, FgvCrosswalkImportHandler, TseCandidatesImportHandler, TseResultsImportHandler, StartImportHandler}.cs`
- `Acquisition/Scopes/ImportScope.cs` *(new)*
- `Acquisition/Options/AcquisitionOptions.cs` *(new)*
- `Acquisition/Validators/StartImportCommandValidator.cs`
- `Extensions/ApplicationExtensions.cs`

**Infrastructure**
- `Acquisition/Http/DatasetFetcher.cs` *(new)*
- `Acquisition/Providers/{IbgeProvider, TseProvider, TseArchiveStreams, FgvProvider, Ibge*Dto}.cs`
- `Acquisition/Parsing/DelimitedFileReader.cs` *(new)*
- `Acquisition/Storage/{TemporaryFileStore, TemporaryFileCleanupService, ArchiveGuard}.cs` *(new)*
- `Acquisition/RateLimiting/HostRateLimiter.cs` *(new; replaces `IbgeRateLimiter`)*
- `Acquisition/Resilience/ProviderResiliencePipelineFactory.cs` *(new; replaces the IBGE-only factory)*
- `Acquisition/Persistence/{BulkPersistenceSession, BulkCopy, GeographyPersistenceService, ElectoralPersistenceService, ElectoralResultsPersistenceService}.cs`
- `Acquisition/Repositories/*.cs`, `Acquisition/Queue/ImportBackgroundService.cs`, `Extensions/AcquisitionExtensions.cs`
- `PostgreSQL/Context/PostgreSQLContext.cs`, `Mappings/ElectoralMaps/*`, `Mappings/GeographyMaps/MunicipalityTseCodeMap.cs`, `Mappings/AcquisitionMaps/*`, `Data/UnityOfWork.cs`
- `PostgreSQL/Migrations/20261114230214_AddElectoralAndImportReliability.cs` *(new)*

**Presentation**
- `Controllers/ImportController.cs` (scope documentation only; endpoints preserved)
- `appsettings.json` (per-provider acquisition policy, limits, batch sizes)

**Tests**
- `Acquisition/{HostRateLimiterTests, DatasetFetcherTests, TseArchiveParsingTests, ImportScopeAndStorageTests}.cs` *(new)*
- `Acquisition/StartImportHandlerTests.cs` (scopes and normalisation)
- Removed `IbgeProviderTests`, `IbgeRateLimiterTests`, `IbgeImportHandlerTests` — they targeted
  types that no longer exist and were partly commented out.

---

## 8. Build and test results

```
dotnet build Bravo.sln     → 0 errors, 2 warnings (both pre-existing NU1510 in Bravo.Infrastructure.Auth)
dotnet test UnitTests      → 53 passed, 0 failed
dotnet ef database update  → applied AddAcquisitionAndGeography + AddElectoralAndImportReliability
```

Live verification (real sources, real PostgreSQL 16):

- Catalog resolution: 4/4 TSE datasets resolved to their official CDN URLs.
- IBGE: 27 states and 5,571 municipalities downloaded, hashed and parsed.
- FGV: 676,817-byte crosswalk downloaded and 5,709 rows parsed; 1 row is a documented `#NULO`
  gap and 139 rows reference TSE codes without a current IBGE municipality (expected historical
  gap, reported as skipped rather than fabricated).
- TSE candidates 2022: 543 candidates persisted, 0 rejected, idempotent re-run.
- TSE results 2022 round 1 Acre: 237,198 facts and 8,496 statistic rows in 16s, 0 skipped,
  0 rejected, totals identical to the source, 0 orphans, idempotent re-run.
- Lease: claim/reject/reclaim-after-expiry verified.
- Cancellation before start: recorded as `Cancelled` with the reason.
- Dependency guard: FGV before IBGE fails with a clear message.
- Temp cleanup: 0 files remaining after successful execution.
- DI: all 16 acquisition registrations resolved from a real container.

---

## 9. Remaining incomplete requirements

These are the items from the four prompts that remain outside what was implemented. They are
either explicitly out of scope for this task or require a decision.

1. **Electoral consultation APIs, analytical queries, rankings, dashboards, materialised
   views** — explicitly out of scope for this task and for `01` §12. Not implemented.
2. **Geographic history** (`MunicipalityHistoricalIdentity`, `MunicipalityRelationship`,
   `GeographicBoundary`, validity intervals) — `01` §3.2 asks for evaluation, not
   implementation. Not implemented; the model stores current IBGE identity plus the TSE↔IBGE
   crosswalk, which is the provable current state.
3. **Historical geographic divisions** (regions, meso/micro, districts) — only the meso/micro
   names are retained on `MunicipalityTseCodes` as source metadata. No separate division tables.
4. **Record-level resume inside one file** — `04` §2 explicitly allows restarting a file when
   record-level recovery is unreliable. Resumption is per dataset (the unit that is
   checkpointed), which matches the official file structure.
5. **Conditional requests in practice for TSE** — the downloader sends `If-None-Match` /
   `If-Modified-Since` and handles 304, and ETag/last-modified are persisted per acquisition.
   The handlers do not yet feed the previous validators into the next execution, so an
   unchanged TSE file is downloaded again. IBGE sends no validators.
6. **Integration test project** — the existing integration tests require Docker/Testcontainers,
   which is unavailable in this environment (`/var/run/docker.sock` permission denied). The
   persistence behaviour was instead verified with an external harness against local PostgreSQL;
   those harnesses are not committed as tests. Adding committed PostgreSQL integration tests
   remains to be done.
7. **Distributed execution across many instances** — leases are correct for multi-instance
   safety, but there is no fairness or back-pressure between instances beyond the database.
   Not required by the specifications.

### Specification conflict (reported, not invented)

`01_SystemContext.md` §12 forbids endpoints, controllers, handlers, commands/queries, TSE file
import pipelines and TSE API integration. `03_ProjectSetup.md` and `04_Recuperability.md`
require exactly those. The two cannot both be satisfied. The implementation follows `03`/`04`,
because they are the acquisition specifications and this task's scope is acquisition and
persistence.

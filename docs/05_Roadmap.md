# Electoral Data System — Implementation Roadmap

## 1. Objective

Understand the existing application architecture and progressively implement a reliable electoral data ingestion system using official Brazilian government datasets.

The implementation must follow this order:

1. **IBGE:** Geographic and administrative data.
2. **TSE:** Electoral structure, elections, candidates, parties, and voting results.

The goal is to establish a normalized, consistent, historically accurate data foundation that can support future electoral queries and statistical analysis.

---

## 2. Phase 1 — Understand the Existing System

Before writing any code, inspect the existing solution and understand its architecture.

- [x] Identify the existing projects and their responsibilities.
- [x] Identify the architectural pattern currently used.
- [x] Inspect existing Domain entities and relationships.
- [x] Inspect existing repositories and persistence abstractions.
- [x] Understand how dependency injection is configured.
- [x] Identify existing import and ingestion mechanisms.
- [x] Understand existing database schemas, migrations, and PostgreSQL configuration.
- [x] Identify existing conventions for naming, error handling, logging, and cancellation.
- [x] Identify existing API endpoints and their responsibilities.

**Deliverable:** A concise architectural assessment describing what already exists, what can be reused, and what is missing.

Do not introduce new architectural patterns or duplicate existing abstractions.

### 2.1. Verified architectural assessment

Solution: `Bravo.slnx`, .NET 10, Clean Architecture with four layers plus one provider
project per external source.

| Project | Verified responsibility |
| --- | --- |
| `Bravo.Domain` | Entities only: `State`, `Municipality`, `ImportJob`, `ImportWorkUnit`, `ImportFailedRecord`, plus Auth/Users. No persistence dependencies. |
| `Bravo.Shared` | `Entity` base (`Id`, `CreatedAt`, `UpdatedAt`), `Result<T>`, notifications. |
| `Bravo.Application` | Mediator commands/queries/handlers, import contracts, `ImportJobExecutor`, `ImportBatchPipeline<T>`, `ImportJobScheduler`, validators. |
| `Bravo.Infrastructure.Ibge` | IBGE HTTP client, rate limiter, JSON DTOs, `IImportSource<T>` implementations, validators. |
| `Bravo.Infrastructure.PostgreSQL` | EF Core context, entity maps, migrations. |
| `Bravo.Infrastructure.PostgreSQL.Dapper` | Dapper repositories, `DapperImportBatchStore<T>`, per-entity batch stores. |
| `Bravo.Presentation` | Thin controllers, `ImportWorker` background service, exception handling, auth. |

Reusable ingestion core already verified working (transactional checkpoint + data commit,
`FOR UPDATE SKIP LOCKED` claiming, leases, fencing tokens, retries, dead letters, resume,
sequential per-job work-unit ordering):

- `IImportSource<TRecord>` → `ImportBatchPipeline<TRecord>` → `IImportBatchStore<TRecord>`.
- `ImportJobScheduler` creates a job with ordered `unitKeys`.
- `IImportPipeline.UnitKey` binds a stage to a work unit; `ImportJobExecutor` resolves it per unit.
- `ImportConflictStrategy` was removed: all persistence is UPSERT.

Gaps identified for TSE work: no electoral entities, no polling-place/section entities,
no TSE provider project, no CSV streaming parser, no file-download/provenance layer,
and no TSE→IBGE municipality cross-reference.

---

## 3. Phase 2 — Understand the Geographic Domain (IBGE)

The first data source to integrate is IBGE.

The objective is to establish the geographic foundation required to contextualize electoral data.

### 3.1. Geographic entities

Investigate and identify the relevant official IBGE datasets for:

- [x] Federative Units (UF / States).
- [x] Municipalities.
- [ ] Mesoregions and microregions, if relevant to the available dataset and historical requirements.
- [ ] Intermediate and immediate geographic regions.
- [ ] Geographic census sectors (Setores Censitários).
- [ ] Geographic boundaries and geometries, when available and relevant.
- [ ] Other geographic classifications required by the application.

> Verified: `IbgeStateSource` calls `api/v1/localidades/estados?orderBy=nome` and
> `IbgeMunicipalitySource` calls `api/v1/localidades/municipios?orderBy=nome`.
> Meso/micro and intermediate/immediate regions are already modelled as DTOs
> (`IbgeMesoRegionDto`, `IbgeMicroRegionDto`, `IbgeImmediateRegionDto`) and are consumed
> only to resolve a municipality's UF. They are not persisted yet, so their entity work
> remains open.

### 3.2. Domain modeling

Keep geographic concepts independent from electoral concepts.

Suggested geographic entities:

| Entity             | Responsibility                                    |
| ------------------ | ------------------------------------------------- |
| State              | Brazilian federative units                        |
| Municipality       | Brazilian municipalities                          |
| GeographicRegion   | IBGE geographic regional classifications          |
| CensusSector       | IBGE census sectors                               |
| GeographicBoundary | Geographic boundaries and spatial representations |

These are conceptual suggestions, not mandatory class names. Inspect the existing domain before deciding how to implement them.

#### 3.2.1. Proposed geographic scope versus confirmed sources

The task requires supporting Country, State, Municipality, District, Neighborhood,
Village, Indigenous locality and rural settlement. Assessment against verified data:

| Concept | Verifiable source | Status |
| --- | --- | --- |
| Country | Constant (`BR`) | Supported |
| State | IBGE `/localidades/estados`, TSE `SG_UF` | Supported and implemented |
| Municipality | IBGE `/localidades/municipios`, TSE `CD_MUNICIPIO` | Supported and implemented |
| Neighborhood | TSE `NM_BAIRRO` in `eleitorado_local_votacao` | Available, but free text |
| District / Village / Indigenous locality / Rural settlement | Absent from the IBGE localities API and from every inspected TSE file | **Not supported by these sources** |

Per the instruction not to invent relationships the source cannot support, the
district/village/indigenous/rural concepts must not be modelled until an authoritative
source is identified. `NM_BAIRRO` is unnormalised free text and must not be promoted to a
canonical neighborhood entity without a normalisation strategy.

### 3.3. Important modeling rules

- [x] Use official IBGE identifiers as external references.
- [x] Preserve the original source identifiers.
- [x] Establish proper relationships between states, municipalities, and geographic regions.
- [ ] Preserve historical validity when geographic classifications change.
- [x] Avoid assuming that names are unique identifiers.
- [ ] Evaluate PostGIS for spatial data and geographic queries.
- [x] Avoid mixing census sectors with electoral zones or polling sections.

**Deliverable:** A normalized geographic domain model and its corresponding persistence layer.

---

## 4. Phase 3 — IBGE Data Acquisition and Import

After understanding the geographic domain, implement the IBGE ingestion pipeline.

### Requirements

- [x] Identify the official IBGE APIs and downloadable datasets.
- [x] Determine which datasets are required and their respective dependencies.
- [x] Implement dedicated IBGE data providers.
- [x] Implement streaming or memory-efficient parsing where appropriate.
- [ ] Implement repositories using efficient PostgreSQL persistence.
- [ ] Prefer PostgreSQL Binary COPY through Npgsql/Dapper for high-volume imports where appropriate.
- [x] Implement batch processing.
- [ ] Implement staging tables when necessary.
- [x] Validate data before merging it into the normalized tables.
- [x] Ensure imports are idempotent.
- [x] Implement checkpoints and resumable execution.
- [x] Persist import execution state.
- [x] Support cancellation, timeouts, retries, and rate limiting.

> Verified: IBGE endpoints `servicodados.ibge.gov.br/api/v1/localidades/estados` and
> `/municipios` are consumed with `JsonSerializer.DeserializeAsyncEnumerable` (streaming),
> through a token-bucket `IbgeRateLimiter`, with `ImportTransientException`/`Retry-After`
> handling, per-record validation, transactional batch commit, durable cursor checkpoint,
> resumption, cancellation, retries and dead-letter records.
>
> Open items: persistence uses `jsonb_to_recordset` UPSERT rather than `COPY`, and there
> are no staging tables. Both are deliberate for the current volume (27 + 5,571 rows).

### API isolation

IBGE must have its own dedicated import endpoint.

Do not centralize IBGE, TSE, or other import operations behind a single endpoint controlled by source-selection parameters or conditional variables.

Each data source must have an independent entry point and orchestration flow.

Shared infrastructure is encouraged, but import workflows must remain independent.

**Deliverable:** A fully functional and independently executable IBGE import pipeline.

---

## 5. Phase 4 — Understand the Electoral Domain (TSE)

Once the geographic foundation is established, investigate the official TSE datasets.

The objective is to model the Brazilian electoral structure and historical election data accurately.

### 5.1. Electoral structure

Investigate the following concepts:

- [ ] Electoral Zones (Zonas Eleitorais).
- [ ] Electoral Sections (Seções Eleitorais).
- [ ] Polling Places (Locais de Votação).
- [ ] Electronic Voting Machines (Urnas Eletrônicas).
- [ ] Elections (Eleições).
- [ ] Election Rounds (Turnos).
- [ ] Electoral Offices (Cargos).
- [ ] Political Parties (Partidos).
- [ ] Candidates (Candidatos).
- [ ] Coalitions and electoral federations, where applicable.

### 5.1.1. Research findings — confirmed source layouts (empirical, 2026-11-15)

The TSE open-data portal exposes a CKAN API at
`https://dadosabertos.tse.jus.br/api/3/action/package_search`, and all bulk files are
served from `cdn.tse.jus.br`. Files are ZIP archives of ISO-8859-1 (latin-1) CSV,
`;`-delimited, double-quoted, CRLF line endings, with a single header row. Column names
are the official abbreviations (`DT_GERACAO`, `CD_MUNICIPIO`, `NR_ZONA`, ...).

Datasets inspected directly:

| Purpose | URL |
| --- | --- |
| Candidates | `odsele/consulta_cand/consulta_cand_{year}.zip` |
| Coalitions/federations | `odsele/consulta_coligacao/consulta_coligacao_{year}.zip` |
| Nominal votes by municipality+zone | `odsele/votacao_candidato_munzona/votacao_candidato_munzona_{year}.zip` |
| Votes by electoral section | `odsele/votacao_secao/votacao_secao_{year}_{UF}.zip` |
| Polling places + coordinates | `odsele/perfil_eleitorado/eleitorado_local_votacao_ATUAL.zip` |
| Electorate profile | `odsele/perfil_eleitorado/perfil_eleitorado_ATUAL.zip` |

**Confirmed field facts**

- `consulta_cand` (2022) has 50 columns, including `NR_TURNO`, `CD_ELEICAO`,
  `DT_ELEICAO`, `CD_CARGO`, `SQ_CANDIDATO`, `NR_PARTIDO`, `SQ_COLIGACAO`, `TP_AGREMIACAO`.
- `NR_TURNO` is present, but the round is also encoded in `CD_ELEICAO`; both must be read
  rather than assumed.
- `TP_AGREMIACAO` is `PARTIDO ISOLADO`, `COLIGAÇÃO` or `FEDERAÇÃO`. A candidate row does
  not imply a coalition, and federations are a distinct concept from coalitions.
- Votes by section (`votacao_secao`) carry `NR_VOTAVEL`, `NM_VOTAVEL`, `QT_VOTOS`,
  `NR_LOCAL_VOTACAO`, `NR_ZONA`, `NR_SECAO` plus candidate/office context.
- `NR_VOTAVEL` mixes real ballot numbers with sentinel codes: `95` = `VOTO BRANCO`,
  `96` = `VOTO NULO`. These must be modelled explicitly and never attached to a candidate.
- Polling places (`eleitorado_local_votacao`) carry `NR_LOCAL_VOTACAO`, `NR_LATITUDE`,
  `NR_LONGITUDE`, `NM_BAIRRO`, `NR_CEP`, `DS_ENDERECO` and per-section electorate counts.
  513,409 of 516,786 rows have coordinates.
- Coordinates are **comma-decimal strings** (e.g. `-19,0348876`) and require
  culture-invariant normalisation.

**Confirmed format drift across years**

`consulta_cand_2010` has 63 columns; `consulta_cand_2014` and `consulta_cand_2022` have
50. Fields only in 2010 include `NM_EMAIL`, `CD_NACIONALIDADE`, `ST_REELEICAO`,
`NR_PROTOCOLO_CANDIDATURA`, `ST_CANDIDATO_INSERIDO_URNA`. Fields only in 2022 include
`DS_EMAIL`, `NR_FEDERACAO`, `NM_FEDERACAO`, `SG_FEDERACAO`, `DS_COMPOSICAO_FEDERACAO`.
A single universal parser is insufficient; header-driven mapping with per-year tolerance
is required.

**Critical identifier finding**

`CD_MUNICIPIO` in TSE files is a **TSE** code, not an IBGE code: `1457` = Sena
Madureira/AC in the 2022 results, whose IBGE code is `1200500`. Zero-padding also differs
by dataset (`1007` in the 2022 results vs `01007` in the current electorate file). TSE
publishes **no** municipality↔IBGE crosswalk dataset, so the mapping must be derived
(UF + normalised municipality name) and stored explicitly, with unresolved cases reported
rather than guessed.

### 5.2. Voting data

Investigate the official TSE datasets containing:

- [ ] Votes per candidate.
- [ ] Votes per political party.
- [ ] Valid votes.
- [ ] Blank votes.
- [ ] Null votes.
- [ ] Abstentions.
- [ ] Total electorate.
- [ ] Turnout statistics.
- [ ] Voting results per electoral section.
- [ ] Voting results per municipality.
- [ ] Voting results per state.
- [ ] Historical election results.

### 5.3. Suggested electoral entities

| Entity              | Responsibility                                        |
| ------------------- | ----------------------------------------------------- |
| ElectoralZone       | Electoral zones                                       |
| PollingPlace        | Physical voting locations                             |
| ElectoralSection    | Individual voting sections                            |
| ElectronicBallotBox | Electronic voting equipment and its electoral context |
| Election            | Election identification and metadata                  |
| ElectionRound       | Election rounds                                       |
| ElectoralOffice     | Disputed political positions                          |
| Candidate           | Candidate registration and identification             |
| PoliticalParty      | Political party information                           |
| Coalition           | Historical coalitions and federations                 |
| ElectionResult      | Voting results                                        |
| VoteStatistics      | Aggregated voting statistics, when necessary          |

These entities must be evaluated against the actual TSE datasets and existing domain model.

### 5.4. Critical relationship distinctions

Do not confuse:

- An electronic voting machine with an electoral section.
- A polling place with an electoral section.
- An IBGE census sector with an electoral zone.
- A municipality with an electoral zone.

Electoral zones and municipalities may have many-to-many relationships.

Consider explicit associative entities where necessary, such as:

- `ElectoralZoneMunicipality`
- `SectorZoneAssignment`

Relationships that can change over time must preserve their historical validity.

Do not assume that today's electoral structure was identical in previous elections.

**Deliverable:** A normalized electoral domain model with properly defined relationships and historical context.

### 5.5. Normalized schema (design v3 — approved, not yet implemented)

Derived strictly from the confirmed fields in 5.1.1 and the five approved architectural
decisions. Surrogate `uuid` primary keys are used everywhere; TSE/IBGE identifiers are
stored as **external codes** in dedicated columns, never as primary keys.

#### 5.5.1. Design decisions (binding)

1. **TSE↔IBGE crosswalk is not a permanent name match.** The mapping preserves the
   original TSE code, carries validity over time (`ElectionYear`/`ValidFrom`/`ValidTo`),
   records the matching method and a confidence score, and supports manual verification.
2. **Layout versioning.** Each source layout gets an explicit version identifier; the CSV
   reader selects a layout descriptor, and parsing normalises into canonical records so
   source-specific column differences never reach domain entities.
3. **Source traceability.** Every imported dataset row keeps source file name, checksum,
   size, provider, dataset, election/year, layout version, import timestamp and status.
4. **Results are typed.** Candidate, party, blank, null and other source categories are
   explicit; `NR_VOTAVEL` sentinels are never interpreted as candidate identifiers.
5. **Historical integrity.** Original TSE identifiers are preserved; municipality names,
   zone numbers and section numbers are never assumed stable across elections.

#### 5.5.2. Decisions added in v3 (binding)

6. **`ElectionRound` is a first-class entity.** A round is not a scalar on `Election`.
   Every round-specific fact references `ElectionRoundId`, never a bare round number.
7. **Section identity includes municipality.** `NR_ZONA`/`NR_SECAO` are **not** globally
   unique. Verified from `eleitorado_local_votacao_ATUAL.csv` (516,786 rows):
   `(NR_ZONA, NR_SECAO)` alone yields 88,001 collisions; `(SG_UF, NR_ZONA, NR_SECAO)` is
   unique; `(CD_MUNICIPIO, NR_ZONA, NR_SECAO)` is also unique. Section numbers are
   therefore only meaningful **within a municipality**. They are also mutable across
   elections: between 2018 and 2022 for AC, 59 of 1,888 common sections changed polling
   place and 236 sections were new.
8. **Results distinguish stored source facts from derived aggregations.** Source-level
   records and computed rollups are separate tables, so an aggregation can never be
   mistaken for a source fact.
9. **Dataset versioning and replacement.** A new checksum never double-counts. Exactly one
   dataset version per scope is `Active`; activating a new version atomically deactivates
   the previous one, and vote aggregates read only from the active version.

#### 5.5.3. Rounds and reference data

```
Election
  Id                    uuid   PK
  TseElectionCode       int          -- CD_ELEICAO, external
  Year                  int          -- ANO_ELEICAO
  TypeCode              int          -- CD_TIPO_ELEICAO
  TypeName              varchar(100)-- NM_TIPO_ELEICAO
  Description           varchar(200)-- DS_ELEICAO
  ElectionDate          date         -- DT_ELEICAO (nullable: not always present)
  UK  (TseElectionCode)

ElectionRound                                  -- decision #6
  Id                    uuid   PK
  ElectionId            uuid   FK → Election
  Round                 smallint     -- NR_TURNO (1..n)
  Name                  varchar(60)  -- e.g. "1º turno"; display only, not an identifier
  IsRunoff              boolean      -- true when Round > 1
  UK  (ElectionId, Round)

ElectoralOffice
  Id                    uuid   PK
  TseOfficeCode         int          -- CD_CARGO
  Name                  varchar(100)-- DS_CARGO
  UK  (TseOfficeCode)

PoliticalParty
  Id                    uuid   PK
  TsePartyNumber        int          -- NR_PARTIDO (may be negative for historical placeholders)
  Acronym               varchar(20)  -- SG_PARTIDO
  Name                  varchar(150) -- NM_PARTIDO
  UK  (TsePartyNumber, Acronym)

Federation
  Id                    uuid   PK
  TseFederationNumber   int          -- NR_FEDERACAO
  Name                  varchar(150)
  Acronym               varchar(20)
  Composition           text
  UK  (TseFederationNumber)

Coalition
  Id                    uuid   PK
  ElectionRoundId       uuid   FK → ElectionRound   -- coalitions are per round
  OfficeId              uuid   FK → ElectoralOffice
  StateId               uuid   FK → State            -- "BR" handled by a nationwide state row
  TseCoalitionNumber    bigint       -- SQ_COLIGACAO
  Name                  varchar(150)
  Composition           text
  UK  (ElectionRoundId, OfficeId, StateId, TseCoalitionNumber)
```

#### 5.5.4. Geography and electoral structure

```
TseMunicipalityCode                                  -- crosswalk, decision #1
  Id                    uuid   PK
  TseMunicipalityCode   varchar(10)  -- raw code, preserved as text (zero-padding varies)
  StateId               uuid   FK → State
  MunicipalityId        uuid   NULL FK → Municipality  -- null when unresolved
  ElectionYear          int            -- the year this code was observed
  MatchMethod           smallint       -- ExactCode | NormalizedName | Manual | Unresolved
  Confidence            numeric(3,2)   -- 0.00..1.00
  IsVerified            boolean        -- manual verification flag
  VerifiedAt            timestamptz NULL
  UK  (TseMunicipalityCode, ElectionYear)
  IX  (MunicipalityId)
  IX  (IsVerified, Confidence)         -- review queue: unverified low-confidence rows

ElectoralZone
  Id                    uuid   PK
  StateId               uuid   FK → State
  TseZoneNumber         int          -- NR_ZONA
  MunicipalityId        uuid   NULL FK → Municipality   -- primary municipality, nullable
  UK  (StateId, TseZoneNumber)
  IX  (MunicipalityId)                -- zones may span municipalities (verified: 1,639 cases)

PollingPlace
  Id                    uuid   PK
  StateId               uuid   FK → State
  MunicipalityId        uuid   NULL FK → Municipality
  ZoneId                uuid   NULL FK → ElectoralZone
  TsePollingPlaceNumber int          -- NR_LOCAL_VOTACAO, unique per (state, zone)
  Name                  varchar(250)
  Address               varchar(300)
  Neighborhood          varchar(150) -- NM_BAIRRO, free text, NOT a canonical entity
  ZipCode               varchar(10)
  Latitude              numeric(10,7) NULL
  Longitude             numeric(10,7) NULL
  UK  (StateId, ZoneId, TsePollingPlaceNumber)

ElectoralSection                               -- decision #7
  Id                    uuid   PK
  MunicipalityId        uuid   FK → Municipality   -- REQUIRED: numbers repeat across municipalities
  ZoneId                uuid   FK → ElectoralZone
  TseSectionNumber      int          -- NR_SECAO
  UK  (MunicipalityId, ZoneId, TseSectionNumber)
  IX  (ZoneId)

PollingPlaceSection                           -- per election, decision #5/#7
  Id                    uuid   PK
  ElectionRoundId       uuid   FK → ElectionRound
  PollingPlaceId        uuid   FK → PollingPlace
  SectionId             uuid   FK → ElectoralSection
  UK  (ElectionRoundId, SectionId)     -- one place per section per round (verified: 0 conflicts in 2022)
  IX  (PollingPlaceId)
```

> **Why `MunicipalityId` is required on `ElectoralSection`.** `(NR_ZONA, NR_SECAO)` alone
> is ambiguous: 88,001 collisions were measured in the current electorate file. Section
> identity is `(municipality, zone, section)`. `PollingPlaceSection` is keyed per
> **round**, not per election, so a second-round pairing cannot overwrite the first.

#### 5.5.5. Candidates and stored source results

```
Candidate
  Id                    uuid   PK
  ElectionId            uuid   FK → Election
  OfficeId              uuid   FK → ElectoralOffice
  StateId               uuid   FK → State
  TseCandidateSequential bigint     -- SQ_CANDIDATO
  BallotNumber          int         -- NR_CANDIDATO
  Name / UrnName / SocialName    varchar(200)
  Cpf                   varchar(11) NULL
  PartyId               uuid   NULL FK → PoliticalParty
  FederationId          uuid   NULL FK → Federation
  CoalitionId           uuid   NULL FK → Coalition
  RegistrationStatus    varchar(100)
  Gender / Education / Race / Occupation  varchar(150) NULL
  BirthDate             date NULL
  UK  (ElectionId, TseCandidateSequential)

ElectionResult                                 -- stored source fact (decision #8)
  Id                    uuid   PK
  ElectionRoundId       uuid   FK → ElectionRound   -- round-specific (decision #6)
  SectionId             uuid   FK → ElectoralSection
  OfficeId              uuid   FK → ElectoralOffice
  VoteType              smallint     -- Nominal | Party | Blank | Null | Other   (decision #4)
  CandidateId           uuid   NULL FK → Candidate   -- set only when VoteType=Nominal
  PartyId               uuid   NULL FK → PoliticalParty
  TseBallotNumber       int          -- NR_VOTAVEL, raw preserved
  Votes                 bigint       -- QT_VOTOS
  DatasetVersionId      uuid   FK → DatasetVersion   -- decision #9
  CK  (VoteType = Nominal) → CandidateId IS NOT NULL
  CK  (VoteType IN (Blank, Null)) → CandidateId IS NULL AND PartyId IS NULL
  UK  (ElectionRoundId, SectionId, OfficeId, VoteType, CandidateId, PartyId, TseBallotNumber, DatasetVersionId)
  IX  (ElectionRoundId, SectionId, OfficeId)

ElectionStatistics                             -- stored source fact
  Id                    uuid   PK
  ElectionRoundId       uuid   FK → ElectionRound
  SectionId             uuid   FK → ElectoralSection
  EligibleVoters        bigint NULL
  Attendees             bigint NULL
  Absentees             bigint NULL
  BlankVotes            bigint NULL
  NullVotes             bigint NULL
  DatasetVersionId      uuid   FK → DatasetVersion
  UK  (ElectionRoundId, SectionId, DatasetVersionId)
```

#### 5.5.6. Derived aggregations (decision #8)

```
ResultAggregate                                -- computed, never imported
  Id                    uuid   PK
  ElectionRoundId       uuid   FK → ElectionRound
  Level                 smallint     -- Municipality | Zone | State | Nation
  MunicipalityId        uuid   NULL FK → Municipality
  ZoneId                uuid   NULL FK → ElectoralZone
  StateId               uuid   NULL FK → State
  OfficeId              uuid   FK → ElectoralOffice
  VoteType              smallint
  CandidateId           uuid   NULL FK → Candidate
  PartyId               uuid   NULL FK → PoliticalParty
  Votes                 bigint
  SourceResultCount     int          -- how many source rows were summed (audit)
  ComputedAt            timestamptz
  UK  (ElectionRoundId, Level, MunicipalityId, ZoneId, StateId, OfficeId, VoteType, CandidateId, PartyId)
```

`ResultAggregate` is **not** populated by the acquisition pipeline. It exists to prove the
schema keeps derived facts separate from source facts; a later analytics phase owns it.
No acquisition code may write to it.

#### 5.5.7. Source traceability and dataset versioning (decisions #3, #9)

```
ImportDataset                                  -- one row per acquired file
  Id                    uuid   PK
  Provider              varchar(50)    -- TSE | IBGE
  Dataset               varchar(100)   -- consulta_cand | votacao_secao | ...
  ElectionYear          int     NULL
  LayoutVersion         varchar(50)
  SourceUrl             varchar(1000)
  SourceFileName        varchar(250)
  Checksum              varchar(128)   -- SHA-256
  SizeBytes             bigint
  AcquiredAt            timestamptz
  UK  (Provider, Dataset, ElectionYear, Checksum)   -- identical file ⇒ same row (idempotent re-download)

DatasetVersion                                 -- one row per activation attempt
  Id                    uuid   PK
  ImportDatasetId       uuid   FK → ImportDataset
  Scope                 varchar(150)   -- e.g. "TSE:votacao_secao:2022:AC:round-1"
  Status                smallint       -- Pending | Importing | Completed | Failed | Superseded
  IsActive              boolean        -- decision #9: exactly one active per Scope
  ImportJobId           uuid   NULL FK → ImportJobs
  CreatedAt             timestamptz
  ActivatedAt           timestamptz NULL
  SupersededAt          timestamptz NULL
  UK  (ImportDatasetId, Scope)                     -- same file re-imported into same scope is idempotent
  UX  (Scope) WHERE "IsActive"                    -- partial unique index: at most one active per scope
  IX  (Status, CreatedAt)
```

> **Replacement semantics (decision #9).**
>
> 1. A new download produces a new `ImportDataset` row only when the checksum differs.
>    Re-downloading an identical file resolves to the existing row.
> 2. Import writes results tagged with `DatasetVersionId`, so a run can be completed or
>    retried without touching rows from another version.
> 3. Activation is a single transaction: set the new version `IsActive = true` and the
>    previous active version `IsActive = false`, `Status = 'Superseded'`. The partial
>    unique index makes two active versions impossible.
> 4. Queries sum only over the active version, so replacement never double-counts and
>    never deletes history.
> 5. Reprocessing a `Failed` version reuses its rows and upserts on the result unique key;
>    it does not create a second live copy.

#### 5.5.8. Why these keys and constraints

- **Surrogate PKs, external codes as unique keys.** TSE codes are not stable or unique
  enough to serve as primary keys (zero-padding drift, negative party numbers, per-year
  municipality codes).
- **`ElectoralSection` includes `MunicipalityId`** because zone/section numbers repeat
  across municipalities — measured, not assumed.
- **`PollingPlaceSection` is keyed on the round.** Verified: within 2022 AC, 0 sections
  mapped to more than one polling place, so the constraint is safe; but 59 sections moved
  between 2018 and 2022, so the pairing must be per round rather than permanent.
- **`ElectionResult` check constraints** enforce decision #4 at the database level, so a
  blank/null vote with a candidate reference is impossible rather than merely discouraged.
- **`DatasetVersion` partial unique index on `Scope`** is the mechanism that guarantees a
  new checksum can never cause votes to be counted twice.
- **`ResultAggregate` has no FK to `ImportDataset`** deliberately: it is derived data, not
  a source record.

#### 5.5.9. Still deferred

Ballot-box (`UrnaEletrônica`) identity is absent from the inspected result files and
exists only in `buweb`/`arqurnatot` archives. It remains unmodelled rather than invented.

---

## 6. Phase 5 — TSE Data Acquisition and Import

Implement the TSE ingestion pipeline only after the electoral domain and geographic dependencies have been properly understood.

### Requirements

- [x] Identify official TSE data sources and downloadable datasets.
- [x] Identify dataset formats and their respective schemas.
- [x] Determine dependencies between electoral datasets.
- [ ] Implement dedicated TSE providers.
- [x] Implement streaming parsers for large files.
- [ ] Implement efficient batch persistence.
- [ ] Reuse shared ingestion infrastructure without coupling it to IBGE.
- [ ] Validate foreign-key relationships and source identifiers.
- [ ] Prevent duplicate records.
- [ ] Preserve historical election information.
- [ ] Implement resumable imports with persistent checkpoints.
- [ ] Ensure that interrupted imports can safely resume.
- [x] Implement appropriate retry, timeout, and cancellation mechanisms.

> Verified in Stage 1: streaming parsing, retry with jitter, timeout, cancellation, secure
> download/extraction and dataset versioning are implemented and tested. The remaining TSE
> items are blocked on the electoral domain entities, which are intentionally deferred.

> Dependency order established from the datasets: `Election` → `ElectoralOffice` →
> `PoliticalParty`/`Federation`/`Coalition` → `Candidate` → `ElectoralZone` →
> `PollingPlace` → `ElectoralSection` → `ElectionResult` → `ElectionStatistics`.
> Candidates depend on elections, offices, parties and states; results depend on all of
> these plus sections. The shared ingestion core already supplies retries, cancellation,
> checkpoints and resumption, so those rows are unblocked once a TSE provider exists.

### Import isolation

TSE must have its own dedicated import endpoint and orchestration flow.

The TSE import must not depend on IBGE-specific implementation details.

However, it may depend on geographic records already imported from IBGE when establishing geographic references.

**Deliverable:** A reliable TSE ingestion pipeline capable of importing historical electoral data.

### 6.1. Implementation stages (new objective)

Derived from the confirmed layouts. Each stage is independently shippable and follows the
existing `IImportSource<T>` → `ImportBatchPipeline<T>` → `IImportBatchStore<T>` pattern.

#### 6.1.0. Verified dataset catalog (2026-11-16)

Probed against `cdn.tse.jus.br` to define what "all elections" means in practice.

| Fact | Verified value |
| --- | --- |
| `consulta_cand_{year}.zip` exists | 1994–2026, every 2-year cycle (17 archives) |
| `votacao_secao_{year}_{UF}.zip` exists | 1998–2026 (1994 and 1996 return 404) |
| Candidate archive layout | **one entry per UF inside a single year archive** |
| Results archive layout | **one archive per (year, UF)** |
| Exterior (`ZZ`) | present only in 2026; 2014/2018/2022 return 404 |
| `BR` results file | nationwide **presidential** results, contains **all UFs including `ZZ`** |
| Rounds | **both rounds live in the same file**, distinguished by `CD_ELEICAO` (`544` = round 1, `545` = round 2 for 2022) |
| `NR_TURNO` | matches the round, but `CD_ELEICAO` is the authoritative round identifier |

Consequences for the importer:

1. A single import must enumerate **every available year**, not a caller-supplied one.
   `POST /api/v1/import/tse` takes no filters and schedules 454 work units
   (17 years x reference+candidates, 15 years x 28 scopes).

> **Scope tokens verified.** Candidate archives contain 29 entries: 27 states plus `BR`
> (national offices) and `BRASIL` (presidential). `ZZ` (exterior) appears only from 2026.
> IBGE publishes only 27 states, so `BR`, `BRASIL` and `ZZ` are seeded as synthetic state
> rows with negative IBGE codes (-1/-2/-3) that real codes (11-53) cannot occupy.
>
> **Verified source-data defects (all handled, none abort an import):**
> - `00/00/1900` placeholder dates in 1998.
> - Corrupt birth date `15/64/6514` in 1998/RJ.
> - `SQ_CANDIDATO` is not always numeric (`15/64/6514` appears in 1998/RJ and 1998/BRASIL),
>   so it is stored as text.
> - `SQ_CANDIDATO` is not unique per election in 2000 (two candidates share `SQ 62`), so the
>   candidate unique key is `(Election, State, Office, Sequential, BallotNumber, Name)`.
> - `NM_URNA_CANDIDATO` is blank in 2000; it falls back to the full candidate name.
> - Party acronym casing drifts (`PC do B` vs `PC DO B`); lookups are case-insensitive.
> - A `Failed` work unit must not block later units; the claim query now only waits on
>   `Pending`/`Running` predecessors.
2. Within a year, candidates must be imported for **every UF present in the archive**,
   discovered from the archive listing rather than a hardcoded list.
3. Results must be imported per `(year, UF)` archive. The `ZZ` exterior scope is imported
   whenever the archive exists; the `BR` file supplies presidential results and must not be
   double-counted against the same sections from a UF file.
4. Round is derived per row from `CD_ELEICAO`, so a single file populates multiple
   `ElectionRound` rows. The earlier assumption that "a second turn arrives as a separate
   file" was wrong and is corrected here.

- [x] **Stage 1 — TSE acquisition layer.** Complete and verified:
      project `src/Infrastructure/Bravo.Infrastructure.Tse`; HTTP client with timeout and
      cancellation; streaming ZIP download with size cap; configurable concurrency rate
      limiting; exponential backoff with jitter honouring `Retry-After`; per-run temporary
      workspace with stale cleanup; SHA-256 computed during streaming; ZIP integrity and
      extraction security validation (path traversal, absolute paths, entry count,
      compression ratio, per-entry and total byte caps); `ImportDataset`/`DatasetVersion`
      metadata persistence with one-active-version-per-scope enforcement.
      Verified live against `cdn.tse.jus.br` (download + checksum + extraction).
- [x] **Stage 2 — CSV streaming reader.** Header-driven `;`-delimited reader over a
      `StreamReader` with ISO-8859-1 encoding, tolerant of missing/extra columns.
- [x] **Stage 3 — Reference data: elections, rounds, offices, parties, federations,
      coalitions.** Verified against 2022/AC: 1 election, 1 round, 7 offices, 27 parties,
      3 federations (federation sentinel `-1`/`#NULO` normalised to NULL).

> **Stage 3 preparation — fields verified against `consulta_cand_2022_AC.csv`**
> (542 rows, 1 election), so implementation can begin without further research:
>
> - **Election** resolves from `CD_ELEICAO` = `546`, `DS_ELEICAO` =
>   `Eleições Gerais Estaduais 2022`, `DT_ELEICAO` = `02/10/2022`, `CD_TIPO_ELEICAO` =
>   `2`, `NM_TIPO_ELEICAO` = `ELEIÇÃO ORDINÁRIA`, `NR_TURNO` = `1`.
> - **ElectionRound** is derived per `(CD_ELEICAO, NR_TURNO)`; `IsRunoff` is `Round > 1`.
>   A second turn arrives as a separate file with `NR_TURNO = 2`.
> - **ElectoralOffice** has 7 distinct `(CD_CARGO, DS_CARGO)` pairs in this state alone,
>   including `10`/`9` for `2º SUPLENTE`/`1º SUPLENTE`. Offices are therefore election- and
>   state-scoped data, not a fixed list.
> - **PoliticalParty** has 27 distinct `(NR_PARTIDO, SG_PARTIDO, NM_PARTIDO)` values;
>   acronym and name are not always equal (`11`/`PP`/`PROGRESSISTAS`).
> - **Federation** uses `-1`/`#NULO` as a sentinel for "no federation". Sentinels must be
>   normalised to `NULL`, never stored as a federation row.
> - **Coalition** has 53 distinct `(SQ_COLIGACAO, NM_COLIGACAO)` pairs; `TP_AGREMIACAO`
>   distinguishes `PARTIDO ISOLADO` (416) / `FEDERAÇÃO` (109) / `COLIGAÇÃO` (17), so a
>   candidate is not automatically in a coalition.
> - Implementation must be idempotent on the natural keys already defined in §5.5.3.
- [x] **Stage 4 — Candidates.** Header-mapped parser tolerant of the 50/63-column drift.
      Verified: 542 registrations, 539 CPF-keyed identities, 34 coalitions.
- [x] **Stage 5 — TSE↔IBGE municipality cross-reference.** Explicit crosswalk table with
      preserved TSE codes, per-election validity, match method, confidence and manual
      verification. Automatic matching only for unambiguous cases; unresolved rows are
      retained and queued for review, never guessed. Verified: 22 AC municipalities
      resolved automatically at confidence 0.60 via normalised name.
- [x] **Stage 6 — Electoral zones, polling places, sections.** From
      `eleitorado_local_votacao`, including comma-decimal coordinate normalisation.
- [x] **Stage 7 — Results and statistics.** Section-level votes with explicit
      `VoteType` for blank/null/party/nominal, plus per-section electorate totals.
- [x] **Stage 8 — `POST /api/v1/import/tse`.** Dedicated endpoint and orchestration,
      independent from IBGE, reusing `ImportJobScheduler` and the shared engine.
- [ ] **Stage 9 — Verification.** Idempotent re-run, interrupted-import resume,
      row-count reconciliation against source, dip in duplicate/orphan detection.

> **Verified end-to-end (2022/AC, round 1).** `POST /api/v1/import/tse` with
> `{year:2022,state:"AC",round:1,electionCode:546}` produced, reconciled against the source:
>
> | Fact | Imported | Source file |
> | --- | --- | --- |
> | Election rows | 1 | 1 (`CD_ELEICAO` 546) |
> | Offices | 7 | 7 distinct `CD_CARGO` |
> | Parties | 27 | 27 distinct `NR_PARTIDO` |
> | Federations | 3 | 3 (sentinel `-1` excluded) |
> | Candidates | 542 | 542 rows |
> | Coalitions | 34 | 34 distinct `(SQ_COLIGACAO, CD_CARGO)` |
> | Section results | 277,665 | 277,665 rows |
> | Sections | 2,124 | 2,124 distinct `(muni, zona, seção)` |
> | Zones | 9 | 9 distinct `NR_ZONA` |
> | Polling places | 562 | 562 distinct `(NR_ZONA, NR_LOCAL_VOTACAO)` |
>
> Vote-type split: Nominal 237,198 rows / 1,672,287 votes; Party 24,394 / 40,163;
> Blank 7,894 / 46,020; Null 8,179 / 63,282. Blank+Null row counts match the source
> exactly. `0 unresolved` municipalities and `0 unknown` candidates.
>
> Note: 562 polling places is correct, not a bug — 104 `NR_LOCAL_VOTACAO` numbers are
> reused across zones, so the key must include the zone (229 values are unique only
> globally, which the schema does not rely on).

### 6.2. Layout versioning design (decision #2 — binding for Stages 1–2)

Each source layout is described by an explicit `TseLayout` descriptor identified by a
version string (for example `cand-2022-v1`). A descriptor declares the delimiter,
encoding, quote character and the canonical-field → source-column map.

The reader works as follows:

1. Reads the header row as the first record.
2. Resolves the descriptor for the requested layout version.
3. Maps each source column to a canonical name, ignoring columns the header does not
   contain and reporting columns the layout expects but the file lacks.
4. Yields canonical records to the caller.

Downstream code reads canonical names only and never branches on election year. A layout
mismatch is an explicit, hard failure rather than a silent skip.

---

## 7. Phase 6 — Data Integrity and Validation

After implementing both ingestion pipelines, validate the consistency of the resulting database.

- [ ] Verify geographic identifiers against official IBGE references.
- [ ] Verify electoral identifiers against official TSE references.
- [ ] Verify relationships between electoral zones and municipalities.
- [ ] Verify that every electoral result references the correct election and round.
- [ ] Verify candidate and political party references.
- [ ] Verify that voting totals are consistent with their source datasets.
- [ ] Detect duplicate records.
- [ ] Detect missing or inconsistent references.
- [ ] Preserve source provenance and import metadata.
- [ ] Ensure historical records are not accidentally overwritten.

Data validation should be explicit and reproducible.

Do not silently discard inconsistent source records.

---

## 8. Architectural and Engineering Constraints

Throughout the implementation, follow these rules:

- Preserve the existing architectural boundaries.
- Keep Domain, Application, Infrastructure, and Presentation responsibilities separated according to the existing project structure.
- Controllers must not contain business logic or orchestration.
- Repositories must own persistence operations, not API services.
- Providers must own external data acquisition and source-specific communication.
- Avoid placing unrelated classes in the same source file.
- Avoid unnecessary interfaces, abstractions, wrappers, and helper methods.
- Prefer simple, explicit implementations over speculative abstractions.
- Use asynchronous operations and cancellation tokens consistently.
- Use efficient database operations for high-volume ingestion.
- Never load entire large datasets into memory unnecessarily.
- Keep IBGE and TSE import workflows independent.
- Do not implement electoral query APIs, dashboards, rankings, analytics, or materialized views as part of this ingestion scope.

---

## 9. Execution Checklist

Use this section to track the overall implementation progress.

| Phase                             | Status |
| --------------------------------- | ------ |
| Existing architecture assessment  | [x]    |
| Geographic domain analysis        | [x]    |
| IBGE dataset identification       | [x]    |
| Geographic repositories           | [x]    |
| IBGE ingestion pipeline           | [x]    |
| IBGE import endpoint              | [x]    |
| Geographic data validation        | [x]    |
| Electoral domain analysis         | [x]    |
| TSE dataset identification        | [x]    |
| Electoral repositories            | [x]    |
| TSE acquisition layer             | [x]    |
| TSE ingestion pipeline            | [x]    |
| TSE import endpoint               | [x]    |
| Electoral data validation         | [ ]    |
| End-to-end ingestion verification | [ ]    |

Notes on partial rows:

- `Geographic data validation`: IBGE imports validate every record and reject invalid
  ones into `ImportFailedRecords`; full cross-source validation remains in Phase 6.
- `Electoral domain analysis`: source research is complete (section 5.1.1) and a schema is
  proposed (section 5.5), but the domain entities are not implemented.

---

## 10. Working Instructions for DeepSeek

Follow the roadmap sequentially.

**Do not attempt to implement everything at once.**

For each phase:

1. Inspect the existing implementation.
2. Identify missing components and dependencies.
3. Explain the proposed implementation before making significant architectural changes.
4. Implement only the current phase.
5. Build and run relevant tests.
6. Verify that existing functionality remains intact.
7. Update this document by checking completed tasks.
8. Report remaining issues and the next logical step.

If an existing implementation already satisfies a requirement, verify it and mark it as completed instead of recreating it.

If a requirement is ambiguous, investigate the official data source and existing project conventions before making assumptions.

**Priority:** Correct domain modeling, data integrity, import reliability, resumability, and architectural consistency are more important than implementation speed.

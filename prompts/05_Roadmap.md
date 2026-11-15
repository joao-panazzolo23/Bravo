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

- [ ] Identify the existing projects and their responsibilities.
- [ ] Identify the architectural pattern currently used.
- [ ] Inspect existing Domain entities and relationships.
- [ ] Inspect existing repositories and persistence abstractions.
- [ ] Understand how dependency injection is configured.
- [ ] Identify existing import and ingestion mechanisms.
- [ ] Understand existing database schemas, migrations, and PostgreSQL configuration.
- [ ] Identify existing conventions for naming, error handling, logging, and cancellation.
- [ ] Identify existing API endpoints and their responsibilities.

**Deliverable:** A concise architectural assessment describing what already exists, what can be reused, and what is missing.

Do not introduce new architectural patterns or duplicate existing abstractions.

---

## 3. Phase 2 — Understand the Geographic Domain (IBGE)

The first data source to integrate is IBGE.

The objective is to establish the geographic foundation required to contextualize electoral data.

### 3.1. Geographic entities

Investigate and identify the relevant official IBGE datasets for:

- [ ] Federative Units (UF / States).
- [ ] Municipalities.
- [ ] Mesoregions and microregions, if relevant to the available dataset and historical requirements.
- [ ] Intermediate and immediate geographic regions.
- [ ] Geographic census sectors (Setores Censitários).
- [ ] Geographic boundaries and geometries, when available and relevant.
- [ ] Other geographic classifications required by the application.

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

### 3.3. Important modeling rules

- [ ] Use official IBGE identifiers as external references.
- [ ] Preserve the original source identifiers.
- [ ] Establish proper relationships between states, municipalities, and geographic regions.
- [ ] Preserve historical validity when geographic classifications change.
- [ ] Avoid assuming that names are unique identifiers.
- [ ] Evaluate PostGIS for spatial data and geographic queries.
- [ ] Avoid mixing census sectors with electoral zones or polling sections.

**Deliverable:** A normalized geographic domain model and its corresponding persistence layer.

---

## 4. Phase 3 — IBGE Data Acquisition and Import

After understanding the geographic domain, implement the IBGE ingestion pipeline.

### Requirements

- [ ] Identify the official IBGE APIs and downloadable datasets.
- [ ] Determine which datasets are required and their respective dependencies.
- [ ] Implement dedicated IBGE data providers.
- [ ] Implement streaming or memory-efficient parsing where appropriate.
- [ ] Implement repositories using efficient PostgreSQL persistence.
- [ ] Prefer PostgreSQL Binary COPY through Npgsql/Dapper for high-volume imports where appropriate.
- [ ] Implement batch processing.
- [ ] Implement staging tables when necessary.
- [ ] Validate data before merging it into the normalized tables.
- [ ] Ensure imports are idempotent.
- [ ] Implement checkpoints and resumable execution.
- [ ] Persist import execution state.
- [ ] Support cancellation, timeouts, retries, and rate limiting.

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

---

## 6. Phase 5 — TSE Data Acquisition and Import

Implement the TSE ingestion pipeline only after the electoral domain and geographic dependencies have been properly understood.

### Requirements

- [ ] Identify official TSE data sources and downloadable datasets.
- [ ] Identify dataset formats and their respective schemas.
- [ ] Determine dependencies between electoral datasets.
- [ ] Implement dedicated TSE providers.
- [ ] Implement streaming parsers for large files.
- [ ] Implement efficient batch persistence.
- [ ] Reuse shared ingestion infrastructure without coupling it to IBGE.
- [ ] Validate foreign-key relationships and source identifiers.
- [ ] Prevent duplicate records.
- [ ] Preserve historical election information.
- [ ] Implement resumable imports with persistent checkpoints.
- [ ] Ensure that interrupted imports can safely resume.
- [ ] Implement appropriate retry, timeout, and cancellation mechanisms.

### Import isolation

TSE must have its own dedicated import endpoint and orchestration flow.

The TSE import must not depend on IBGE-specific implementation details.

However, it may depend on geographic records already imported from IBGE when establishing geographic references.

**Deliverable:** A reliable TSE ingestion pipeline capable of importing historical electoral data.

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
| Existing architecture assessment  | [ ]    |
| Geographic domain analysis        | [ ]    |
| IBGE dataset identification       | [ ]    |
| Geographic repositories           | [ ]    |
| IBGE ingestion pipeline           | [ ]    |
| IBGE import endpoint              | [ ]    |
| Geographic data validation        | [ ]    |
| Electoral domain analysis         | [ ]    |
| TSE dataset identification        | [ ]    |
| Electoral repositories            | [ ]    |
| TSE ingestion pipeline            | [ ]    |
| TSE import endpoint               | [ ]    |
| Electoral data validation         | [ ]    |
| End-to-end ingestion verification | [ ]    |

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

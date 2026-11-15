# Historical Electoral Analysis Platform — Domain, Persistence, and Large-Scale Data Architecture

## 1. Project Context and Main Objective

We are developing a Brazilian historical electoral and geographic analysis platform.

The primary objective is to enable longitudinal analysis of Brazilian elections, allowing users to query, compare, and aggregate electoral behavior across municipalities, electoral zones, polling places, and polling sections over time.

Examples of analyses the platform should eventually support:

- How a municipality voted in every presidential election.
- How a candidate's vote count and vote share evolved across elections.
- Which municipalities, electoral zones, and polling sections had the highest vote counts for a candidate.
- Comparisons of electoral behavior between municipalities.
- Historical changes in candidate and political party vote shares.
- Aggregation of results by municipality, state, and region.
- Analysis of electoral behavior across different geographic areas.
- Comparisons between first-round and second-round results.
- Historical comparisons involving municipalities and their IBGE geographic identities.
- Large-scale batch analysis involving millions or potentially billions of electoral result records.

The system must be designed primarily for historical data analysis, not merely for conventional CRUD operations.

The architecture must support both reliable data ingestion and computationally intensive analytical workloads.

---

## 2. Historical Scope

The initial scope focuses on Brazilian presidential elections conducted after the country's return to democracy and involving electronic voting machines.

Priority:

1. Presidential elections from 2002 onward, when electronic voting was implemented nationwide.
2. The 1998 presidential election, accounting for partial adoption of electronic voting and known dataset coverage limitations.
3. Earlier elections may be investigated and incorporated in the future.

The 1996 election was a municipal election, not a presidential election. Do not treat it as part of the initial presidential election dataset.

**Important:** Never assume all historical datasets have the same geographic granularity or coverage.

Data availability must be evaluated separately for each election, round, region, and dataset.

The initial scope is exclusively focused on presidential elections. The domain may support future expansion to state, municipal, and legislative elections, but do not implement unnecessary complexity for those scenarios now.

---

## 3. Geographic Domain and Granularity

The reference geographic structure for electoral data is:

- State.
- Municipality.
- Electoral Zone.
- Polling Place.
- Polling Section.

Polling sections should be the smallest storage unit for electoral results whenever that information is available in the original source.

Higher-level results should be derived through aggregation whenever possible and semantically correct.

For example:

- Polling section results can be aggregated by polling place.
- Polling place results can be aggregated by electoral zone.
- Electoral zone results can be aggregated by municipality.
- Municipal results can be aggregated by state and nationwide.

However, do not assume that this hierarchy is always complete or available for every historical election.

### 3.1. Independent Geographic Divisions

Do not confuse:

- Municipality with electoral zone.
- Electoral zone with polling place.
- Polling place with polling section.
- Neighborhood with census tract.
- Census tract with electoral zone.

An electoral zone may cover multiple municipalities, and a municipality may contain multiple electoral zones.

Do not model an electoral zone as necessarily belonging to a single municipality.

Evaluate an appropriate relational representation for these relationships, considering historical changes.

### 3.2. Geographic History and IBGE Integration

The system must support historical identification of geographic entities.

Consider:

- Municipal emancipation.
- Territorial subdivision.
- Territorial incorporation.
- Name changes.
- Changes to official codes.
- Changes to territorial boundaries.

Historical analysis must distinguish between the administrative identity used at the time and potential territorial equivalence with a current geographic unit.

The system will eventually incorporate IBGE municipality data to enable more advanced geographic analysis.

Evaluate the need for structures such as:

- MunicipalityHistoricalIdentity.
- MunicipalityRelationship.
- GeographicBoundary.
- Other entities justified by the existing model.

Do not implement entities merely in anticipation of possible future requirements.

Official IBGE and TSE codes must be stored as strings to preserve leading zeros.

**Important:** IBGE municipality integration is a future requirement. Design the current model so that it can accommodate this integration without introducing unnecessary dependencies or implementing the entire integration now.

---

## 4. Electoral Domain Model

Evaluate the separation of the following concepts:

- Election: the electoral event.
- ElectionRound: the election round.
- ElectoralContest: the specific electoral contest, initially presidential.
- Candidate: the candidate.
- PoliticalParty: the political party.
- ElectoralZone: the electoral zone.
- PollingPlace: the polling place.
- PollingSection: the polling section.

Do not assume that every entity must be an Aggregate Root or have its own repository.

Repository decisions must respect the existing architecture and actual business rules.

### 4.1. Electoral Results

Electoral results must not be stored as properties of Candidate, Municipality, or Election.

An electoral result represents a historical fact associated with an electoral event, geographic unit, candidate, and data source.

Evaluate a structure similar to:

**PollingSectionResult**

- ElectionId.
- ElectionRoundId.
- ElectoralContestId.
- PollingSectionId.
- CandidateId.
- Votes.
- SourceId.
- ImportBatchId.

This is only a conceptual reference. Adapt it to the actual domain model and relational integrity requirements.

The system must also support results originally provided at municipality, electoral zone, or another geographic level, particularly when polling-section data is unavailable.

Avoid unnecessarily duplicating facts that can be derived through aggregation.

Do not use a polymorphic geographic foreign key without proper referential integrity.

If results at multiple geographic levels must coexist, evaluate a relational design that preserves valid foreign keys and clearly identifies the original granularity.

### 4.2. Candidate Votes and Turnout Statistics

Do not repeat the same eligible voter, turnout, and absentee totals in every candidate vote record.

Separate candidate voting facts from general electoral statistics for a geographic unit.

Evaluate structures similar to:

**PollingSectionStatistics**

- ElectionId.
- ElectionRoundId.
- PollingSectionId.
- EligibleVoters.
- Attendees.
- Absentees.
- BlankVotes.
- NullVotes.

Fields must reflect the information actually provided by the source. Do not assume all fields are available in every historical dataset.

Consider that blank and null votes may be reported differently depending on the election and the original TSE dataset structure.

---

## 5. Data Provenance and Import Architecture

The platform must preserve the original granularity of imported data.

Never automatically transform municipality-level results into polling-section results or infer information that does not exist in the source.

We need to identify:

- Election.
- Election round.
- Geographic coverage.
- Original data granularity.
- Data source.
- Import batch.
- Dataset version.
- Known coverage limitations.

Evaluate the need for structures such as:

- DataSource.
- ImportBatch.
- ImportedDataset.
- DatasetCoverage.
- ImportError.

These names are suggestions, not strict requirements.

Traceability is essential. It must be possible to identify where a result originated and which import process inserted it.

### 5.1. Bulk Import Requirements

The architecture must be prepared for future bulk imports of large TSE datasets.

Consider:

- Idempotent imports.
- Duplicate detection.
- Relationship validation.
- Referential integrity.
- Import error tracking.
- Data provenance preservation.
- Reprocessing of import batches.
- Compatibility with different election years and dataset formats.
- Large transaction management.
- Efficient staging and validation.
- Bulk insertion and upsert strategies.

Do not implement the entire import pipeline in this phase.

However, the persistence architecture must not make large-scale ingestion unnecessarily difficult.

---

## 6. Critical Requirement: Large-Scale Data Processing

This is one of the most important architectural requirements.

The platform is expected to process millions of electoral result records and may eventually reach hundreds of millions or billions of records, depending on the number of elections, geographic granularity, and future expansions.

Do not design the persistence layer exclusively around conventional CRUD workloads.

The architecture must distinguish between:

1. Domain-oriented persistence.
2. Bulk data ingestion.
3. Analytical queries.
4. Large-scale aggregation and reporting.

These workloads have different performance and implementation requirements.

### 6.1. ORM and Data Access Strategy

Evaluate and implement a hybrid data access strategy using the following technologies:

**Entity Framework Core**

Use EF Core as the primary ORM for:

- Domain-oriented persistence.
- Geographic entities.
- Elections, rounds, contests, candidates, and parties.
- Data sources and import metadata.
- Conventional CRUD operations.
- Relational configuration.
- Change tracking when necessary.
- Migrations and schema management.
- Transactional operations involving domain entities.

EF Core should be used where its change tracking, relationship management, LINQ translation, and domain-oriented persistence provide meaningful value.

Do not use EF Core to individually materialize or persist millions of analytical result records.

**Dapper**

Evaluate Dapper as the primary data access mechanism for SQL-intensive read workloads, particularly:

- Historical comparisons.
- Candidate rankings.
- Vote aggregations.
- Municipal comparisons.
- Electoral evolution analysis.
- Complex joins across large datasets.
- Grouping by election, round, municipality, zone, and section.
- Queries that require precise control over SQL execution.
- Analytical projections that do not require domain entity materialization.

Dapper should allow us to write explicit, optimized PostgreSQL queries without introducing unnecessary ORM materialization and change-tracking overhead.

Do not assume Dapper is automatically faster than EF Core for every query. The actual SQL, execution plan, network transfer, result materialization, and database indexing are more important.

**Npgsql**

Use Npgsql directly when PostgreSQL-specific functionality or low-level database access is necessary.

In particular, evaluate:

- PostgreSQL COPY.
- NpgsqlBinaryImporter.
- Efficient bulk insertion.
- Parameterized commands.
- PostgreSQL-specific data types and operations.
- Efficient transaction handling.

For large imports, prefer PostgreSQL COPY or an equivalent bulk ingestion strategy instead of inserting records individually through EF Core.

### 6.2. Do Not Force One Technology to Handle Every Workload

Do not force all persistence operations through EF Core.

Do not force all persistence operations through Dapper either.

The choice must be based on the workload:

| Workload                     | Preferred approach |
| ---------------------------- | ------------------ |
| Domain-oriented CRUD         | EF Core            |
| Relational configuration     | EF Core            |
| Migrations                   | EF Core            |
| Bulk ingestion               | Npgsql COPY        |
| Large analytical queries     | Dapper + SQL       |
| Complex aggregations         | PostgreSQL SQL     |
| High-volume batch updates    | Set-based SQL      |
| Domain entity tracking       | EF Core            |
| Lightweight read projections | Dapper             |

This is the expected starting strategy, but validate it against the actual project architecture.

### 6.3. Avoid Row-by-Row Processing

For large datasets, avoid:

- Loading millions of entities into memory.
- Calling SaveChangesAsync repeatedly for individual records.
- Executing one INSERT statement per record.
- Materializing complete object graphs for analytical queries.
- Performing aggregations in application memory when PostgreSQL can perform them efficiently.
- Using tracking queries when tracking is unnecessary.
- N+1 query patterns.
- Excessive object allocations during batch processing.

Prefer:

- Set-based SQL.
- Bulk operations.
- Streaming where appropriate.
- Database-side aggregation.
- Projection into lightweight result types.
- Pagination or keyset pagination for large result sets.
- Efficient transaction boundaries.
- Staging tables when they simplify bulk ingestion and validation.

The application should avoid loading entire historical datasets into RAM just to perform an aggregation.

---

## 7. PostgreSQL and Large-Scale Analytical Architecture

PostgreSQL is the current database and must remain the primary relational database in this phase.

However, the system must be designed with potential analytical workloads involving billions of records in mind.

Evaluate the following techniques and determine which are justified by the expected workload.

### 7.1. Indexing

Evaluate indexes for common access patterns:

- Election + round + polling section.
- Election + round + municipality.
- Election + candidate.
- Municipality + election.
- Electoral zone + election.
- Polling place + election.
- Candidate + municipality + election.

Consider composite indexes, covering indexes, partial indexes, and other PostgreSQL indexing strategies when justified.

Do not create indexes indiscriminately.

Every index introduces storage and write overhead, which is particularly relevant during bulk imports.

Explain why each important index exists.

### 7.2. Partitioning

Evaluate PostgreSQL table partitioning for large electoral result tables.

Potential partitioning dimensions include:

- Election year.
- Election identifier.
- Another dimension justified by query patterns and data distribution.

Do not partition merely because the system might eventually contain billions of rows.

Analyze:

- Expected partition sizes.
- Query pruning.
- Index maintenance.
- Bulk ingestion.
- Partition creation and lifecycle management.
- Uniqueness constraints and foreign-key implications.

If partitioning is not justified yet, explain the conditions under which it should be introduced.

### 7.3. Aggregations and Materialized Data

Evaluate whether recurring analytical queries should use:

- Materialized views.
- Pre-aggregated tables.
- Incrementally maintained aggregates.
- Summary tables by municipality, state, election, or candidate.

Do not calculate every historical aggregate from the most granular data on every request if the workload makes that impractical.

However, do not duplicate every possible aggregation in advance.

Propose a strategy that balances:

- Query performance.
- Storage overhead.
- Import complexity.
- Refresh cost.
- Data consistency.
- Historical corrections and reprocessing.

### 7.4. OLTP and OLAP Separation

Evaluate the distinction between transactional and analytical workloads.

PostgreSQL can serve both purposes initially, but do not assume that one relational database will remain the optimal analytical engine at every scale.

Consider future integration with analytical technologies such as:

- DuckDB.
- ClickHouse.
- A dedicated analytical warehouse.
- Columnar storage formats such as Parquet.

Do not introduce these technologies now without a concrete justification.

Explain when a separate analytical engine would become worthwhile and how the current architecture could support that evolution.

The transactional PostgreSQL database should remain the authoritative source of operational and normalized data unless a future architectural decision explicitly changes this.

---

## 8. Repository and Query Architecture

After auditing the project, determine how repository contracts and query services should be organized.

Do not create a repository for every entity automatically.

Consider the difference between:

- Aggregate persistence.
- Reference data retrieval.
- Analytical read models.
- Bulk ingestion operations.
- Infrastructure-specific database operations.

Do not force analytical SQL queries into domain repositories if they do not belong there.

If the existing architecture supports a separate read/query abstraction, evaluate using it for Dapper-based analytical access.

Maintain clear boundaries between Domain, Application, and Infrastructure.

Do not expose Npgsql, Dapper, DbConnection, or EF Core implementation details in the Domain layer.

Repository interfaces and query contracts must be placed according to the existing architectural conventions.

Avoid generic abstractions that hide useful SQL capabilities or create unnecessary complexity.

---

## 9. Existing Architecture Audit

Before writing code, investigate the project to understand:

- Current layer organization.
- Separation between Domain, Application, Infrastructure, and Presentation.
- Existing repository patterns.
- Existing interfaces.
- Repository implementation conventions.
- Current Entity Framework Core and Dapper usage.
- DbContext configuration.
- PostgreSQL configuration.
- Entity and relationship configuration.
- Naming conventions.
- Dependency injection patterns.
- Transaction handling.
- Migration strategy.
- Aggregate and domain entity conventions.
- Existing result and error-handling patterns.
- Existing database connection management.
- Existing testing conventions.

Use existing conventions when appropriate.

Do not introduce a second architectural pattern without justification.

If the current architecture contains inconsistencies that prevent a correct implementation, explain the problem and propose the necessary correction before proceeding.

Do not assume the current implementation is optimal merely because it already exists.

---

## 10. Implementation Requirements

After completing the audit and validating the proposed model, implement the required persistence infrastructure.

The implementation may include:

1. Repository interfaces.
2. Repository implementations.
3. EF Core configurations.
4. Dapper query implementations where justified.
5. PostgreSQL relationships and constraints.
6. Relevant indexes.
7. Dependency injection registrations.
8. Database connection management.
9. Transaction handling.
10. Migrations, if necessary.
11. Tests for persistence behavior.

Do not automatically introduce Dapper into every repository.

Use EF Core for domain-oriented persistence and Dapper for analytical SQL workloads where it provides a meaningful architectural or implementation advantage.

Use Npgsql directly for bulk operations when necessary.

Keep database access efficient without compromising domain integrity.

---

## 11. Performance and Memory Management

Performance matters primarily because of data volume and batch processing, not because we need to optimize every ordinary CRUD operation prematurely.

Pay special attention to:

- Database round trips.
- Query execution plans.
- Memory consumption.
- Object materialization.
- Change tracking.
- Network transfer.
- Batch sizes.
- Transaction duration.
- Lock contention.
- Index maintenance.
- Data ingestion throughput.

Do not load complete datasets into memory unnecessarily.

Use streaming or bounded batches where appropriate.

For analytical queries, return only the fields required by the analysis.

Avoid unnecessary entity graph loading.

Use `AsNoTracking()` for EF Core read operations that do not require change tracking.

Use explicit SQL for operations where it provides better control.

Do not assume that application-level parallelism always improves database throughput. Consider PostgreSQL connection limits, I/O contention, locking, and query concurrency.

If possible, provide a lightweight benchmark or measurement plan comparing the proposed approaches for representative ingestion and query workloads.

Do not invent benchmark results.

---

## 12. Scope Restrictions

Do NOT implement at this very moment:

- Endpoints.
- Controllers.
- Handlers.
- DTOs.
- Application Commands or Queries.
- User interfaces.
- Dashboards.
- Reports.
- Full analytical features.
- TSE file import pipelines.
- External integration with TSE APIs.
- Municipal, state, or legislative election functionality.
- Full support for elections before the country's return to democracy.
- A separate OLAP database without a concrete requirement.
- Premature distributed infrastructure.
- Unnecessary caching systems.
- Premature database partitioning.

These things will be made from 2 to 4 md files.

Do not create fictional data to fill historical gaps.

The architecture must allow future expansion, but the implementation must remain focused on the persistence requirements of the initial scope.

---

## 13. Testing and Validation

Implement or update tests according to the project's existing conventions.

Prioritize validation of:

- Relationships between municipalities and electoral zones.
- Relationships between polling places and polling sections.
- Result associations with elections, rounds, and candidates.
- Referential integrity.
- Historical identification of geographic units.
- Result uniqueness.
- Idempotency where applicable.
- EF Core configurations.
- Dapper query correctness.
- PostgreSQL compatibility.
- Bulk operation correctness.
- Transaction boundaries.
- Migrations.
- Large-query behavior where practical.

Do not create artificial tests that merely reproduce the implementation.

Tests must validate actual domain rules, invariants, and persistence behavior.

---

## 14. Mandatory Execution Workflow

Execute the work in the following order.

### Phase 1 — Architecture Audit

Analyze the existing architecture, domain models, and persistence layer.

Present:

- Current architecture.
- Identified problems.
- Existing EF Core and Dapper usage.
- Entities and relationships that need modification.
- Justified new entities.
- Aggregates and transactional boundaries.
- Repositories that are actually necessary.
- Appropriate separation between transactional and analytical workloads.
- Potential scalability bottlenecks.

### Phase 2 — Domain and Persistence Proposal

Present the proposed final structure, including:

- Entities.
- Relationships.
- Primary and foreign keys.
- Unique constraints.
- Important indexes.
- Geographic history strategy.
- Data granularity and provenance strategy.
- EF Core responsibilities.
- Dapper responsibilities.
- Npgsql bulk operation responsibilities.
- Query and repository architecture.
- Partitioning considerations.
- Potential future analytical architecture.

**Do not begin implementation before presenting this proposal and identifying ambiguities that could significantly affect the model.**

Do not ask questions about decisions that can be objectively resolved by analyzing the existing codebase.

### Phase 3 — Implementation

After validating the proposed model, implement the repositories, mappings, configurations, query infrastructure, and required integrations.

Respect the existing project structure.

Do not implement features explicitly excluded from this phase.

### Phase 4 — Validation

Run tests and build the project.

Verify:

- EF Core configurations.
- Dapper queries.
- PostgreSQL compatibility.
- Migrations.
- Referential integrity.
- Transaction behavior.
- Relevant performance characteristics.

Fix the issues found.

### Phase 5 — Final Report

Provide:

- What was implemented.
- What was changed.
- Which repositories and query components were created.
- Which entities were modified or added.
- How EF Core, Dapper, and Npgsql are used.
- Which scalability considerations were addressed.
- Which optimizations were intentionally deferred.
- Remaining limitations.
- Test and build results.
- Remaining work required to implement TSE data ingestion.

---

## 15. Quality Criteria

The implementation must prioritize:

- DDD applied with sound judgment.
- Clean Architecture.
- SOLID principles.
- Low coupling.
- Referential integrity.
- Historical consistency.
- Data provenance.
- Efficient bulk ingestion.
- Efficient analytical queries.
- Appropriate relational modeling.
- Memory-efficient processing.
- Scalability without premature complexity.
- Maintainability.
- Ease of future expansion.

Do not sacrifice domain integrity for artificial generalization.

Do not force every operation through a single ORM.

Do not use EF Core for workloads that are fundamentally bulk-processing or analytical SQL operations.

Do not introduce Dapper or low-level database access where EF Core already provides a clear and maintainable solution.

Do not optimize based on assumptions alone. Evaluate actual query patterns, execution plans, and expected data volumes.

The goal is to build a specialized foundation for Brazilian historical electoral analysis, starting with presidential elections conducted using electronic voting machines, preserving the most detailed granularity available in official datasets, and preparing the persistence architecture for potentially billions of records.

**Understand the project before implementing. Justify abstractions before introducing them. Preserve data provenance. Choose the right persistence mechanism for each workload. Design for large-scale analysis without introducing premature infrastructure.**

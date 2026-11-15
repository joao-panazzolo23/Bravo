# Official Data Acquisition and API Population

## Objective

Design and implement an on-demand data acquisition mechanism within the existing API.

The API must retrieve official datasets directly from authoritative sources such as:

- **TSE** — electoral data, elections, candidates, political parties, electoral zones, polling places, polling sections, and election results.
- **IBGE** — Brazilian states, municipalities, official geographic identifiers, and available historical geographic information.
- **FGV and other reputable institutions** — complementary historical datasets when relevant and justified.

The goal is to populate and update the API's database using official external sources, without relying on manually downloaded files or external scripts as the primary ingestion mechanism.

All HTTP requests, downloading, parsing, validation, and persistence must happen within this application.

## 1. On-Demand Execution

Data acquisition must be explicitly triggered through an API controller.

These datasets are predominantly historical or static. Therefore, implementing periodic background synchronization is unnecessary and undesirable.

The expected workflow is:

1. An authorized client calls a dedicated controller endpoint.
2. The controller delegates the operation to an Application use case.
3. The Application coordinates the acquisition process.
4. Infrastructure retrieves the required datasets from official sources.
5. Retrieved data is parsed, validated, normalized, and persisted.
6. The API returns an execution summary.

Do not introduce recurring jobs, cron schedules, hosted services, or automatic startup synchronization.

The operation must be explicitly initiated by an API request.

Consider exposing separate operations for different data domains rather than creating one endpoint that indiscriminately downloads everything.

For example:

- Synchronize IBGE geographic reference data.
- Acquire TSE election metadata.
- Acquire election results for a specific election and round.
- Acquire complementary historical datasets.

The API should allow the caller to specify the intended scope of an operation.

## 2. External HTTP Communication

Use the native .NET HTTP infrastructure:

- `IHttpClientFactory` for HTTP client management.
- `System.Threading.RateLimiting` for request throttling and concurrency control.
- Custom bounded retry and backoff handling where necessary.
- Proper cancellation support through `CancellationToken`.

**Do not use Polly or Polly-based resilience packages.**

Do not introduce unnecessary third-party HTTP or resilience libraries.

### Rate Limiting and Anti-Blocking Strategy

A critical requirement is avoiding excessive requests that could result in temporary blocking, throttling, or denial of access by external providers.

Implement responsible, configurable request management.

Requirements:

- Maintain independent rate limits for each external host.
- Avoid sending large bursts of concurrent requests.
- Use conservative default request rates.
- Support configurable minimum delays between requests.
- Limit concurrent downloads per host.
- Respect HTTP `429 Too Many Requests` responses.
- Respect the `Retry-After` response header whenever provided.
- Implement bounded exponential backoff with jitter for transient failures.
- Avoid retrying permanent errors, such as most `4xx` responses.
- Support request cancellation.
- Apply reasonable connection and request timeouts.
- Avoid downloading the same dataset repeatedly when its local version is already current.
- Never attempt to bypass provider restrictions, access controls, or anti-bot mechanisms.

Do not assume that every provider has the same rate limits. Each source must have its own configurable policy.

If a provider does not expose a documented API, investigate its official downloadable datasets and supported access mechanisms before implementing scraping.

Prefer official bulk-download endpoints over making thousands of individual requests.

## 3. Source Abstraction

External providers must not be tightly coupled to the domain model.

Create appropriate abstractions for external data acquisition, such as source-specific clients or providers.

Examples:

- TSE data provider.
- IBGE data provider.
- FGV data provider.

Each provider should be responsible for understanding its own external data formats, endpoints, download mechanisms, and source-specific limitations.

Do not expose HTTP responses, external DTOs, JSON structures, CSV records, or provider-specific implementation details to the Domain layer.

External data must be transformed into application/domain-compatible representations before being persisted.

Do not create unnecessary generic abstractions that make source-specific behavior difficult to implement.

## 4. Data Acquisition Pipeline

The acquisition process should follow a clear sequence:

1. Resolve the official dataset and its download location.
2. Retrieve the dataset using the appropriate provider.
3. Validate the HTTP response and downloaded content.
4. Parse the original format (CSV, ZIP, JSON, or other supported formats).
5. Validate records and required identifiers.
6. Normalize external values into the application's internal representation.
7. Persist data efficiently.
8. Record execution status and acquisition metadata.

Support large datasets without loading entire files into memory unnecessarily.

Prefer streaming, bounded batches, and bulk-oriented persistence mechanisms.

Do not use EF Core to insert millions of records individually.

Reuse the existing EF Core, Dapper, and PostgreSQL infrastructure according to their established responsibilities. Use efficient bulk-loading techniques when appropriate.

## 5. Data Integrity and Idempotency

Running the same acquisition operation multiple times must not create duplicate records or corrupt existing data.

Requirements:

- Define stable external identifiers for imported entities.
- Use appropriate unique constraints.
- Implement idempotent persistence.
- Distinguish between inserting new records and updating existing records.
- Preserve historical information when the source represents historical records.
- Do not overwrite information blindly when the source has different coverage or semantics.
- Handle incomplete datasets explicitly.
- Never fabricate missing historical information.

A failed acquisition must not leave the database in an inconsistent state.

Consider staging tables or equivalent mechanisms when they materially improve validation and atomicity for large imports.

## 6. Import Tracking and Observability

The system must provide visibility into each acquisition execution.

Record relevant metadata, including:

- Data source.
- Dataset identification.
- Requested scope.
- Execution start and completion timestamps.
- Execution status.
- Number of downloaded records.
- Number of inserted records.
- Number of updated records.
- Number of skipped records.
- Number of rejected records.
- Relevant errors and failure details.
- Source version, publication date, or dataset reference when available.

Avoid storing enormous error payloads or entire downloaded files in ordinary database records.

The API should return a useful execution summary and make failures diagnosable.

## 7. Clean Architecture Responsibilities

Respect the existing Clean Architecture implementation and project structure.

**Domain**

- Contains the actual domain concepts and business invariants.
- Must not depend on HTTP, external providers, file formats, or database implementation details.

**Application**

- Defines acquisition use cases.
- Coordinates the import workflow.
- Defines the necessary provider and persistence contracts.
- Handles execution orchestration and cancellation.

**Infrastructure**

- Implements TSE, IBGE, FGV, and other external providers.
- Implements HTTP communication and rate limiting.
- Handles downloading, decompression, parsing, and external format conversion.
- Implements persistence using the existing database infrastructure.

**Presentation**

- Exposes controller endpoints to initiate acquisition operations.
- Handles authorization, request validation, and HTTP responses.
- Must not contain downloading, parsing, or persistence logic.

Do not place the entire ingestion workflow inside controllers or infrastructure services that bypass Application.

## 8. Execution Safety

Because acquisition operations may involve very large datasets and long-running downloads:

- Prevent accidental simultaneous executions of the same import scope.
- Define an appropriate execution lifecycle.
- Support cancellation without leaving partially committed data.
- Ensure that a failed operation can be safely retried.
- Avoid keeping unnecessary database transactions open while downloading external files.
- Consider returning an execution identifier when the operation is too long for a normal synchronous HTTP request.
- If asynchronous execution is necessary, keep its orchestration inside the API. Do not introduce an external worker or separate ingestion application.

Do not claim that an operation has completed before persistence has actually succeeded.

## 9. Implementation Constraints

Before implementing anything:

1. Inspect the existing solution, project structure, dependency injection, database configuration, repositories, and existing abstractions.
2. Understand the current domain model and its relationships.
3. Identify existing functionality that can be reused.
4. Identify the official datasets and access mechanisms available from TSE, IBGE, FGV, and other relevant institutions.
5. Verify dataset formats, identifiers, coverage, and publication/update characteristics.

Do not duplicate existing infrastructure.

Do not unnecessarily restructure the solution.

Do not introduce new projects, abstractions, interfaces, or dependencies without a concrete justification.

Do not implement every possible historical dataset immediately. Prioritize the datasets required by the current electoral analysis scope.

Do not use fucking try catches everywhere. Do not use it at all.

After implementation:

- Build the entire solution.
- Verify dependency injection registrations.
- Validate the HTTP acquisition workflow.
- Test rate limiting and retry behavior.
- Test cancellation and failure handling.
- Test repeated imports for idempotency.
- Verify database integrity after successful and unsuccessful executions.

## 10. Concurrent Acquisition and Incremental Processing

The ingestion system must support efficient acquisition of multiple independent datasets without processing every request sequentially.

Implement an Application-level import handler responsible for orchestrating the acquisition workflow.

### Concurrent Requests

The handler should be able to initiate multiple independent downloads concurrently when the datasets do not depend on one another.

Requirements:

- Use asynchronous execution throughout the acquisition pipeline.
- Use bounded concurrency rather than unrestricted `Task.WhenAll`.
- Apply independent concurrency and rate-limit policies per external host.
- Allow different providers or independent datasets to be downloaded concurrently.
- Do not serialize unrelated downloads unnecessarily.
- Respect provider restrictions and avoid excessive request bursts.
- Propagate cancellation to all ongoing operations.
- Avoid downloading the same dataset more than once during overlapping executions.

Use `Task.WhenAll` when appropriate for a known, bounded collection of independent download tasks. For larger or dynamically discovered collections, use an appropriate bounded-concurrency mechanism.

### Streaming with IAsyncEnumerable

Provider implementations should expose asynchronous streaming interfaces whenever the source format and acquisition mechanism allow it.

Prefer `IAsyncEnumerable<T>` and `yield return` for incremental delivery of parsed records or batches.

Examples of appropriate contracts:

- Stream parsed records from a downloaded dataset.
- Stream validated batches ready for persistence.
- Report acquisition progress through a separate execution/progress mechanism.

The Application handler must consume these streams using `await foreach`.

Do not require providers to materialize entire datasets into `List<T>` or other in-memory collections.

Do not use `Task<IEnumerable<T>>` as a substitute for streaming when processing large datasets.

Use `[EnumeratorCancellation]` where appropriate and propagate `CancellationToken` correctly.

### Pipeline and Backpressure

The acquisition workflow should separate:

1. Downloading.
2. Decompression and parsing.
3. Validation and normalization.
4. Batch persistence.

Where useful, allow these stages to operate concurrently through a bounded producer-consumer pipeline, such as `System.Threading.Channels`.

Requirements:

- Keep memory usage bounded.
- Apply backpressure when persistence cannot keep up with parsing.
- Avoid unbounded queues.
- Avoid excessive database concurrency.
- Flush incomplete batches at the end of processing.
- Ensure that failures in any stage are propagated to the orchestration handler.
- Ensure that cancellation stops producers and consumers cleanly.

Do not introduce a complex pipeline when a simple asynchronous stream is sufficient.

### Bulk Persistence

Persist records in bounded batches instead of issuing one database operation per record.

Use the existing persistence infrastructure and choose the appropriate mechanism for each workload:

- Dapper for efficient SQL-based batch operations.
- Npgsql bulk-copy capabilities such as `COPY` when appropriate.
- EF Core for conventional aggregate persistence and operations that benefit from its change tracking.

Do not load entire datasets into memory before persistence.

### HTTP Timeout and Long-Running Operations

Do not assume that a complete import must fit within a single HTTP request timeout.

The controller should initiate the import and use the application's execution lifecycle appropriately.

For long-running operations, prefer returning an execution identifier and allowing the client to query execution status and progress.

Do not keep an HTTP request open merely to stream millions of imported records or wait for every independent download to finish.

The import itself must continue within the API's application process, with explicit cancellation and failure handling. Do not introduce an external worker or separate ingestion application.

### Progress Reporting

Expose meaningful progress information, such as:

- Current dataset.
- Current provider.
- Download status.
- Number of records processed.
- Number of records persisted.
- Number of batches completed.
- Current execution stage.
- Errors and final execution status.

Keep progress reporting separate from the dataset's `IAsyncEnumerable<T>` stream. Do not mix control messages, progress notifications, and domain records into one loosely typed stream.

The final design should provide concurrent acquisition, incremental processing, bounded memory usage, and efficient persistence without introducing unnecessary complexity.

## Final Architectural Principle

This is an **on-demand data acquisition subsystem**, not a periodic synchronization service.

The API is responsible for initiating, coordinating, executing, and tracking the acquisition of official external datasets.

The design must prioritize responsible access to external providers, data provenance, correctness, idempotency, efficient bulk persistence, and compatibility with the existing Clean Architecture.

Do not optimize for unnecessary abstraction. Implement the simplest architecture that correctly supports large historical datasets and future expansion.

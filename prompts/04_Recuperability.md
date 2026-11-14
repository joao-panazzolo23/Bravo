# Import Reliability, Recovery, and Data Integrity

Extend the existing on-demand data acquisition subsystem with the following reliability and operational requirements.

The goal is to ensure that large historical datasets can be downloaded, processed, and persisted efficiently, even when failures, interruptions, provider changes, or API restarts occur.

Respect the existing Clean Architecture and previously established acquisition architecture. Do not duplicate existing functionality or introduce unnecessary abstractions.

## 1. Import Execution Lifecycle

Introduce a persistent execution lifecycle for import operations.

An import execution must have:

- A unique execution identifier.
- Requested operation and scope.
- Current status.
- Creation and start timestamps.
- Completion timestamp.
- Current execution stage.
- Progress information.
- Failure details when applicable.
- Cancellation information.

Support meaningful execution states, such as:

- Pending.
- Running.
- Completed.
- CompletedWithErrors.
- Failed.
- CancellationRequested.
- Cancelled.

The API must expose endpoints to:

- Start an import execution.
- Retrieve its status.
- Retrieve its progress.
- Request cancellation.

The controller must not perform the acquisition or processing itself.

### Internal Execution Mechanism

Use an internal execution mechanism within the existing API process.

A suitable approach is:

- A persistent PostgreSQL-backed execution queue.
- An internal `BackgroundService` that consumes pending executions.
- Application handlers responsible for coordinating the actual import operations.

Do not use an in-memory-only queue as the source of truth.

Do not use fire-and-forget `Task.Run` from controllers.

Do not introduce an external worker, separate ingestion application, message broker, or recurring scheduler unless an existing architectural requirement explicitly justifies it.

The API must recover pending or interrupted executions after restarting.

If multiple API instances are possible, prevent two instances from claiming and executing the same job simultaneously.

## 2. Checkpointing and Resumable Imports

Large imports must support recovery without unnecessarily restarting completed work.

Implement a checkpoint mechanism at the appropriate dataset, file, and batch levels.

Track:

- Completed datasets.
- Completed files.
- Successfully persisted batches.
- Current processing position when it can be reliably recovered.
- Relevant source version or checksum.
- Last successful checkpoint.

Requirements:

- Persist checkpoints only after the corresponding work has been successfully committed.
- Resume from the last valid checkpoint whenever possible.
- Avoid duplicate records during retries or recovery.
- Distinguish an interrupted execution from a completed execution.
- Support safely retrying failed files or batches.
- Do not assume that every source format supports reliable record-level resumption.

When record-level recovery is unreliable, restart processing from the beginning of the affected file while relying on idempotent persistence.

## 3. Temporary File Management

Large datasets must not be downloaded entirely into application memory.

Implement a temporary file management strategy.

Requirements:

- Download files using streaming HTTP operations.
- Store large downloads in temporary storage.
- Support large ZIP and CSV files without loading their complete contents into memory.
- Validate downloaded file size and integrity.
- Calculate checksums when appropriate.
- Avoid unnecessarily downloading identical datasets again.
- Reuse verified temporary downloads when safe.
- Clean up temporary files after successful processing.
- Define a retention and cleanup policy for failed or interrupted executions.

Do not trust file extensions or HTTP content types as sufficient proof of file validity.

Validate actual file structure before processing.

Protect against malformed archives, excessive decompression ratios, unexpectedly large files, and insufficient disk space.

Temporary storage must be configurable.

## 4. Dataset Versioning and Provenance

The system must preserve the origin and version of imported information.

For each acquired dataset, track relevant metadata:

- Data provider.
- Dataset identifier.
- Original source URL.
- Original filename.
- Acquisition timestamp.
- Source publication or update date, when available.
- File checksum.
- File size.
- Dataset format.
- Dataset version or reference.
- Import execution identifier.
- Processing status.

Detect when the source dataset has changed.

Distinguish between:

- A new import attempt.
- A repeated acquisition of an unchanged dataset.
- A newly published dataset version.
- A corrected or replaced historical dataset.

Never silently overwrite historical information without preserving sufficient provenance to identify its origin and version.

Do not assume that every provider exposes reliable version metadata. Use checksums and available source information when necessary.

## 5. Dataset Dependencies and Execution Planning

Import operations must account for dependencies between datasets.

For example, election results may depend on previously imported:

- Elections.
- Election rounds.
- Candidates.
- Political parties.
- States and municipalities.
- Electoral zones.
- Polling places.
- Polling sections.

Implement an appropriate dependency resolution mechanism.

Requirements:

- Identify required datasets before starting dependent imports.
- Allow independent datasets to execute concurrently.
- Do not start dependent processing before required references are available.
- Avoid serializing unrelated acquisition operations.
- Detect missing references and unresolved external identifiers.
- Provide clear failure information when dependencies are unavailable.

A dependency graph or simpler explicit dependency definitions may be used depending on actual complexity.

Do not introduce a generic workflow engine unnecessarily.

## 6. Source Format Evolution

Official datasets may differ between election years, providers, and publication versions.

The acquisition pipeline must account for:

- Different CSV headers.
- Different column ordering.
- Encoding variations.
- Different delimiters.
- Date and numeric format variations.
- ZIP archives containing multiple files.
- Missing or additional columns.
- Changes in official identifiers.
- Source-specific documentation and README files.

Use source-specific parsers or version-aware parsing strategies where appropriate.

Do not assume a universal parser will correctly process every historical dataset.

Parsing errors must identify the source file and relevant record or position whenever possible.

Never silently discard records because they do not match an expected format.

## 7. Data Integrity and Partial Failures

Define clear transaction and failure boundaries.

Requirements:

- Use bounded database transactions.
- Commit checkpoints only after successful persistence.
- Avoid keeping transactions open during HTTP downloads or long parsing operations.
- Support safe retries of failed batches.
- Prevent duplicate records.
- Record rejected records and validation failures.
- Distinguish recoverable errors from fatal errors.
- Avoid publishing incomplete datasets as fully imported.

Use staging tables when validating an entire dataset before making its contents available is necessary.

For datasets that can safely be imported incrementally, use idempotent batch persistence.

Do not assume every dataset requires the same transaction strategy.

## 8. Coverage and Data Quality

The system must preserve information about dataset coverage and quality.

Track relevant information such as:

- Election and round.
- Original source granularity.
- Geographic scope.
- Number of expected records, when available.
- Number of downloaded records.
- Number of successfully parsed records.
- Number of persisted records.
- Number of rejected records.
- Missing geographic or electoral references.
- Known limitations documented by the source.

Do not infer missing historical data.

Do not assume that every election has the same coverage or granularity.

Do not compare datasets as if they were equivalent when their original granularity, coverage, or semantics differ.

Preserve the original source granularity and the information necessary to assess completeness.

## 9. Resource Protection and Security

Protect the API and external providers during acquisition.

Requirements:

- Restrict import endpoints to authorized users.
- Prevent duplicate overlapping executions for the same import scope.
- Validate requested dataset identifiers and parameters.
- Prevent arbitrary user-supplied URLs from being fetched.
- Protect against SSRF.
- Define maximum download sizes and decompressed sizes.
- Define resource limits for parsing and persistence.
- Avoid unbounded task creation and queues.
- Avoid exhausting database connections.
- Support cancellation and controlled shutdown.

Use explicit allowlisted provider hosts and approved source locations.

## 10. Observability and Diagnostics

Provide structured logs and measurable execution progress.

Track metrics for individual pipeline stages:

**Download**
- Download duration.
- Bytes downloaded.
- Transfer speed.
- HTTP retries.
- HTTP failures.

**Parsing**
- Processing duration.
- Records parsed.
- Records per second.
- Parsing errors.

**Validation**
- Records validated.
- Rejected records.
- Unresolved references.
- Validation failures.

**Persistence**
- Batches committed.
- Records inserted.
- Records updated.
- Records skipped.
- Persistence duration.
- Records per second.

**Execution**
- Total duration.
- Current stage.
- Recovery attempts.
- Cancellation status.
- Final execution status.

Use structured logging and correlate logs using the execution identifier.

Keep progress reporting separate from `IAsyncEnumerable<T>` data streams. Dataset records and operational status are different concerns.

Avoid excessive progress writes to PostgreSQL. Use sensible reporting intervals or batch updates.

## 11. Performance and Recovery Testing

Implement or extend tests to verify:

- Recovery after API process interruption.
- Resumption from valid checkpoints.
- Idempotent repeated imports.
- Concurrent imports of independent datasets.
- Prevention of duplicate overlapping executions.
- HTTP rate limiting and `429` handling.
- `Retry-After` behavior.
- Network interruptions.
- Download timeouts.
- Cancellation during downloads.
- Cancellation during parsing.
- Cancellation during persistence.
- Malformed archives and oversized files.
- Source format changes.
- Database failures during batch persistence.
- Bounded memory usage when processing large datasets.

Validate the complete execution lifecycle, not just individual provider methods.

## 12. Architectural Constraints

Respect the existing architecture.

**Application**
- Defines import execution use cases.
- Coordinates acquisition, dependencies, checkpoints, and progress.
- Defines contracts for execution tracking and external providers.

**Infrastructure**
- Implements persistent execution storage.
- Implements checkpoint persistence.
- Implements temporary file management.
- Implements provider-specific acquisition and parsing.
- Implements database bulk persistence.

**Presentation**
- Exposes authorized endpoints to initiate and manage executions.
- Returns execution identifiers and status information.
- Does not contain acquisition or persistence logic.

**Domain**
- Contains domain invariants and concepts only when they genuinely belong to the business domain.
- Must not become a container for operational infrastructure concerns such as HTTP retries, temporary files, or background execution.

Do not introduce entities into the Domain merely because a database table exists.

## 13. Final Implementation Requirements

Before implementing:

1. Inspect the existing solution and previous acquisition implementation.
2. Identify which requirements are already satisfied.
3. Reuse existing abstractions and infrastructure.
4. Identify missing capabilities.
5. Implement only what is necessary.

After implementation:

- Build the complete solution.
- Verify dependency injection.
- Test execution recovery.
- Test checkpoint consistency.
- Test concurrent execution.
- Test idempotency.
- Test cancellation and shutdown.
- Verify that incomplete imports cannot be reported as successful.
- Validate resource consumption with representative large datasets.

## Final Principle

The acquisition subsystem must behave as a reliable, resumable, observable, and resource-bounded import system.

It must remain entirely within the existing API application while supporting concurrent acquisition, incremental processing, efficient bulk persistence, recovery after failures, and traceable historical data provenance.

Prioritize correctness and recoverability over unnecessary architectural complexity.

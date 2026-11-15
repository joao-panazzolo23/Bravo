using Bravo.Application.Acquisition.Services;

namespace Bravo.Application.Acquisition.Providers;

/// <summary>
/// Provider contract for TSE open-data packages. A provider resolves a dataset to its
/// official download location, downloads it to temporary storage and parses it as a stream.
/// </summary>
public interface ITseProvider
{
    /// <summary>
    /// Resolves a dataset available on the TSE open-data portal.
    /// </summary>
    Task<TseDatasetInfo> ResolveDataset(TseDatasetKind kind, int year, CancellationToken ct = default);

    /// <summary>
    /// Resolves a dataset for a specific state. State-scoped datasets use the per-state
    /// archive rather than the national one, which would mix contests and coverage.
    /// </summary>
    Task<TseDatasetInfo> ResolveDataset(TseDatasetKind kind, int year, string? state, CancellationToken ct = default);

    /// <summary>
    /// Downloads the dataset archive to temporary storage.
    /// </summary>
    Task<CachedDownload> Download(Guid executionId, TseDatasetInfo dataset, CancellationToken ct = default);

    /// <summary>
    /// Streams election metadata records (elections, rounds and contests) from the archive.
    /// </summary>
    IAsyncEnumerable<ParsedRecord<TseElectionMetadataRecord>> StreamElectionMetadata(string filePath, TseDatasetInfo dataset, string? state, CancellationToken ct = default);

    /// <summary>
    /// Streams candidate records from the archive.
    /// </summary>
    IAsyncEnumerable<ParsedRecord<TseCandidateRecord>> StreamCandidates(string filePath, TseDatasetInfo dataset, string? state, CancellationToken ct = default);

    /// <summary>
    /// Streams candidate vote records from the polling-section archive.
    /// </summary>
    IAsyncEnumerable<ParsedRecord<TsePollingSectionResultRecord>> StreamPollingSectionResults(string filePath, TseDatasetInfo dataset, string? state, CancellationToken ct = default);

    /// <summary>
    /// Streams turnout statistics from the polling-section detail archive.
    /// </summary>
    IAsyncEnumerable<ParsedRecord<TsePollingSectionStatisticsRecord>> StreamPollingSectionStatistics(string filePath, TseDatasetInfo dataset, string? state, CancellationToken ct = default);

    /// <summary>
    /// Streams the official TSE-to-IBGE municipality crosswalk.
    /// </summary>
    IAsyncEnumerable<ParsedRecord<TseMunicipalityCrosswalkRecord>> StreamMunicipalityCrosswalk(string filePath, CancellationToken ct = default);
}

public record TseMunicipalityCrosswalkRecord(
    string StateAbbreviation,
    string TseCode,
    int IbgeCode,
    string? TseName,
    string? IbgeName);

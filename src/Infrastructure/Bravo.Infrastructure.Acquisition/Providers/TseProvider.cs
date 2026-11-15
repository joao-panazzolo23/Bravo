using Bravo.Application.Acquisition.Services;
using System.Text.Json;
using Bravo.Application.Acquisition.Providers;
using Bravo.Infrastructure.Acquisition.Http;
using Bravo.Application.Acquisition.Options;

namespace Bravo.Infrastructure.Acquisition.Providers;

/// <summary>
/// TSE open-data provider.
///
/// Dataset locations are not hardcoded: the official CKAN catalog at
/// <c>dadosabertos.tse.jus.br</c> is queried for the package of each election year, the
/// package is searched for the resource whose official name matches the requested dataset,
/// and the resulting resource URL is validated against the provider host allowlist before
/// any download.
/// </summary>
public sealed class TseProvider(DatasetFetcher fetcher, ProviderOptions policy) : ITseProvider
{
    private const string CatalogPackageShow = "api/3/action/package_show?id={0}";
    private const long MaxCatalogBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<TseDatasetInfo> ResolveDataset(TseDatasetKind kind, int year, CancellationToken ct = default) =>
        await ResolveDataset(kind, year, null, ct);

    public async Task<TseDatasetInfo> ResolveDataset(
        TseDatasetKind kind,
        int year,
        string? state,
        CancellationToken ct = default)
    {
        var (packageId, identifier, displayName, format) = kind switch
        {
            TseDatasetKind.ElectionMetadata => ($"candidatos-{year}", $"tse-election-metadata-{year}", $"TSE election metadata {year}", "CSV/ZIP"),
            TseDatasetKind.Candidates => ($"candidatos-{year}", $"tse-candidates-{year}", $"TSE candidates {year}", "CSV/ZIP"),
            TseDatasetKind.PollingSectionResults => ($"resultados-{year}", $"tse-polling-section-results-{year}", $"TSE polling section results {year}", "CSV/ZIP"),
            TseDatasetKind.PollingSectionDetails => ($"resultados-{year}", $"tse-polling-section-details-{year}", $"TSE polling section details {year}", "CSV/ZIP"),
            TseDatasetKind.MunicipalityCrosswalk => ("codigos-oficiais-de-uf-e-municipios-segundo-o-tse-e-o-ibge", "tse-municipality-crosswalk", "TSE/IBGE municipality crosswalk", "CSV/ZIP"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported dataset kind.")
        };

        var catalogUri = new Uri(new Uri(policy.BaseAddress), string.Format(CatalogPackageShow, packageId));
        var json = await fetcher.GetStringAsync(catalogUri, MaxCatalogBytes, ct);

        CatalogResponse? catalog;
        try
        {
            catalog = JsonSerializer.Deserialize<CatalogResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"TSE catalog response for '{packageId}' is not valid JSON.", ex);
        }

        if (catalog?.Result?.Resources is not { Count: > 0 } resources)
        {
            throw new InvalidOperationException($"TSE catalog package '{packageId}' has no resources.");
        }

        var resource = kind switch
        {
            TseDatasetKind.ElectionMetadata => FindCandidatesResource(resources),
            TseDatasetKind.Candidates => FindCandidatesResource(resources),
            TseDatasetKind.PollingSectionResults => FindSectionVotesResource(resources, state),
            TseDatasetKind.PollingSectionDetails => FindResource(resources, "Detalhe da apuração por seção eleitoral"),
            TseDatasetKind.MunicipalityCrosswalk => FindResource(resources, "Códigos oficiais"),
            _ => null
        };

        if (resource?.Url is null || !Uri.TryCreate(resource.Url, UriKind.Absolute, out var sourceUri))
        {
            throw new InvalidOperationException(
                $"TSE catalog package '{packageId}' does not expose the expected resource for {kind}.");
        }

        DatasetFetcher.ValidateHost(sourceUri, policy);

        var scopedIdentifier = state is null ? identifier : $"{identifier}-{state.ToUpperInvariant()}";

        return new TseDatasetInfo(
            kind,
            scopedIdentifier,
            state is null ? displayName : $"{displayName} ({state.ToUpperInvariant()})",
            sourceUri,
            resource.Format ?? format,
            year,
            catalog.Result.MetadataModified);
    }

    /// <summary>
    /// The candidates package publishes a single national archive whose entries are split by
    /// state; the national "Candidatos" resource is authoritative.
    /// </summary>
    private static TseResource? FindCandidatesResource(IReadOnlyList<TseResource> resources) =>
        resources
            .Where(r => r.Name is not null
                        && r.Name.Equals("Candidatos", StringComparison.OrdinalIgnoreCase)
                        && r.Url is not null)
            .FirstOrDefault()
        ?? FindResource(resources, "Candidatos");

    /// <summary>
    /// Polling-section votes are published both as a national presidential archive and as
    /// per-state archives. When a state is requested, the state archive must be used: the
    /// national archive contains only presidential contests and would resolve contests that
    /// were never imported for that state.
    /// </summary>
    private static TseResource? FindSectionVotesResource(IReadOnlyList<TseResource> resources, string? state)
    {
        if (!string.IsNullOrWhiteSpace(state))
        {
            var prefix = $"{state.ToUpperInvariant()} - Votação por seção eleitoral";
            var stateResource = resources.FirstOrDefault(r =>
                r.Name is not null
                && r.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && r.Url is not null);

            if (stateResource is not null)
            {
                return stateResource;
            }
        }

        return FindResource(resources, "Presidente - Votação por seção eleitoral");
    }

    private static TseResource? FindResource(IReadOnlyList<TseResource> resources, string namePrefix) =>
        resources
            .Where(r => r.Name is not null
                        && r.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase)
                        && r.Url is not null)
            .OrderByDescending(r => r.LastModified ?? string.Empty)
            .FirstOrDefault();

    public async Task<CachedDownload> Download(Guid executionId, TseDatasetInfo dataset, CancellationToken ct = default)
    {
        var downloaded = await fetcher.DownloadAsync(executionId, dataset.DatasetIdentifier, dataset.SourceUri, null, ct);
        return downloaded ?? throw new InvalidOperationException(
            $"TSE dataset '{dataset.DatasetIdentifier}' was reported as unmodified but no local copy exists.");
    }

    public IAsyncEnumerable<ParsedRecord<TseElectionMetadataRecord>> StreamElectionMetadata(
        string filePath,
        TseDatasetInfo dataset,
        string? state = null,
        CancellationToken ct = default) =>
        TseArchiveStreams.ReadElectionMetadata(filePath, dataset, state, ct);

    public IAsyncEnumerable<ParsedRecord<TseCandidateRecord>> StreamCandidates(
        string filePath,
        TseDatasetInfo dataset,
        string? state = null,
        CancellationToken ct = default) =>
        TseArchiveStreams.ReadCandidates(filePath, dataset, state, ct);

    public IAsyncEnumerable<ParsedRecord<TsePollingSectionResultRecord>> StreamPollingSectionResults(
        string filePath,
        TseDatasetInfo dataset,
        string? state = null,
        CancellationToken ct = default) =>
        TseArchiveStreams.ReadPollingSectionResults(filePath, dataset, state, ct);

    public IAsyncEnumerable<ParsedRecord<TsePollingSectionStatisticsRecord>> StreamPollingSectionStatistics(
        string filePath,
        TseDatasetInfo dataset,
        string? state = null,
        CancellationToken ct = default) =>
        TseArchiveStreams.ReadPollingSectionStatistics(filePath, dataset, state, ct);

    public IAsyncEnumerable<ParsedRecord<TseMunicipalityCrosswalkRecord>> StreamMunicipalityCrosswalk(
        string filePath,
        CancellationToken ct = default) =>
        TseArchiveStreams.ReadMunicipalityCrosswalk(filePath, ct);

    private sealed class CatalogResponse
    {
        public bool Success { get; set; }
        public CatalogResult? Result { get; set; }
    }

    private sealed class CatalogResult
    {
        public string? Name { get; set; }
        public string? Title { get; set; }
        public string? MetadataModified { get; set; }
        public List<TseResource>? Resources { get; set; }
    }

    private sealed class TseResource
    {
        public string? Name { get; set; }
        public string? Format { get; set; }
        public string? Url { get; set; }
        public string? LastModified { get; set; }
    }
}

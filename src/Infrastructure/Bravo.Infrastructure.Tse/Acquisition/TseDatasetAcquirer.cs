using Bravo.Domain.Electoral.Services;
using Bravo.Infrastructure.Tse.Clients;
using Bravo.Infrastructure.Tse.Extraction;
using Bravo.Infrastructure.Tse.Options;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Tse.Acquisition;

internal sealed class TseDatasetAcquirer(
    TseHttpClient client,
    TseArchiveExtractor extractor,
    IOptions<TseOptions> options) : ITseDatasetAcquirer
{
    private const string CandidatePrefix = "consulta_cand_";

    public async Task<IReadOnlyList<AcquiredCandidateFile>> AcquireCandidatesAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        var tempDirectory = options.Value.TempDirectory;

        var downloaded = await client.DownloadCandidateArchiveAsync(
            year, tempDirectory, cancellationToken);

        var destination = Path.Combine(
            tempDirectory,
            $"cand-{year}-{Guid.CreateVersion7():N}");

        var extracted = extractor.Extract(
            downloaded.FilePath,
            destination,
            entryFilter: name => IsCandidateEntry(name, year));

        var layoutVersion = CandidateLayoutFor(year);

        var files = new List<AcquiredCandidateFile>(extracted.Count);

        foreach (var file in extracted)
        {
            var state = ExtractState(Path.GetFileNameWithoutExtension(file.FilePath));
            var extension = Path.GetExtension(file.FilePath);

            var located = Path.Combine(destination, $"{CandidatePrefix}{year}_{state}{extension}");

            if (!string.Equals(file.FilePath, located, StringComparison.Ordinal))
                File.Move(file.FilePath, located, overwrite: true);

            files.Add(new AcquiredCandidateFile(
                state,
                new AcquiredDataset(
                    located,
                    Path.GetFileName(located),
                    file.SizeBytes,
                    downloaded.Sha256,
                    downloaded.SourceUrl,
                    layoutVersion)));
        }

        return files;
    }

    public async Task<AcquiredDataset> AcquireResultsAsync(
        int year,
        string state,
        CancellationToken cancellationToken = default)
    {
        var upper = state.ToUpperInvariant();
        var tempDirectory = options.Value.TempDirectory;

        var downloaded = await client.DownloadSectionResultsArchiveAsync(
            year, upper, tempDirectory, cancellationToken);

        var destination = Path.Combine(
            tempDirectory,
            $"results-{year}-{upper}-{Guid.CreateVersion7():N}");

        var extracted = extractor.Extract(
            downloaded.FilePath,
            destination,
            entryFilter: name => name.EndsWith($"_{upper}.csv", StringComparison.OrdinalIgnoreCase));

        var match = extracted.FirstOrDefault()
            ?? throw new TseArchiveException(
                $"Archive '{downloaded.SourceUrl}' contains no entry for state '{upper}'.");

        return new AcquiredDataset(
            match.FilePath,
            Path.GetFileName(match.FilePath),
            match.SizeBytes,
            downloaded.Sha256,
            downloaded.SourceUrl,
            SectionResultLayoutFor(year));
    }

    private static bool IsCandidateEntry(string entryName, int year)
        => Path.GetFileName(entryName).StartsWith($"{CandidatePrefix}{year}_", StringComparison.OrdinalIgnoreCase)
           && entryName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Extracts the scope token from a name such as <c>consulta_cand_2022_SP</c>. The token is
    /// a state acronym, or one of the national scopes <c>BR</c> / <c>BRASIL</c>.
    /// </summary>
    private static string ExtractState(string fileNameWithoutExtension)
    {
        var prefix = fileNameWithoutExtension.LastIndexOf('_');

        return fileNameWithoutExtension[(prefix + 1)..].ToUpperInvariant();
    }

    private static string CandidateLayoutFor(int year) => year switch
    {
        >= 2014 and <= 2026 => "cand-2022-v1",
        >= 2006 and <= 2012 => "cand-2010-v1",
        >= 1994 and <= 2004 => "cand-2010-v1",
        _ => throw new NotSupportedException($"No candidate layout is registered for {year}.")
    };

    private static string SectionResultLayoutFor(int year) => year switch
    {
        >= 1998 and <= 2026 => "vot-secao-2022-v1",
        _ => throw new NotSupportedException($"No section result layout is registered for {year}.")
    };
}

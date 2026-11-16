using Bravo.Application.Electoral.Contracts;
using Bravo.Application.Importing;
using Bravo.Infrastructure.Tse.Downloads;
using Bravo.Infrastructure.Tse.Extraction;
using Bravo.Infrastructure.Tse.Options;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Tse.Acquisition;

internal sealed class TseDatasetAcquirer(
    TseFileDownloader downloader,
    TseArchiveExtractor extractor,
    IOptions<TseOptions> options) : ITseDatasetAcquirer
{
    public Task<AcquiredDataset> AcquireCandidatesAsync(
        TseImportScope scope,
        CancellationToken cancellationToken = default)
        => AcquireAsync(
            scope,
            $"estatistica/sead/odsele/consulta_cand/consulta_cand_{scope.Year}.zip",
            $"consulta_cand_{scope.Year}.zip",
            $"_{scope.State}.csv",
            CandidateLayoutFor(scope.Year),
            cancellationToken);

    public Task<AcquiredDataset> AcquireResultsAsync(
        TseImportScope scope,
        CancellationToken cancellationToken = default)
        => AcquireAsync(
            scope,
            $"estatistica/sead/odsele/votacao_secao/votacao_secao_{scope.Year}_{scope.State}.zip",
            $"votacao_secao_{scope.Year}_{scope.State}.zip",
            $"_{scope.State}.csv",
            SectionResultLayoutFor(scope.Year),
            cancellationToken);

    private async Task<AcquiredDataset> AcquireAsync(
        TseImportScope scope,
        string relativeUrl,
        string archiveName,
        string entrySuffix,
        string layoutVersion,
        CancellationToken cancellationToken)
    {
        var downloaded = await downloader.DownloadAsync(relativeUrl, archiveName, cancellationToken);

        var extractDirectory = Path.Combine(
            options.Value.TempDirectory,
            $"extract-{Guid.CreateVersion7():N}");

        var extracted = extractor.Extract(
            downloaded.FilePath,
            extractDirectory,
            entryFilter: name => name.EndsWith(entrySuffix, StringComparison.OrdinalIgnoreCase));

        var match = extracted.FirstOrDefault()
            ?? throw new TseArchiveException(
                $"Archive '{relativeUrl}' contains no entry ending with '{entrySuffix}'.");

        return new AcquiredDataset(
            match.FilePath,
            Path.GetFileName(match.FilePath),
            match.SizeBytes,
            downloaded.Sha256,
            downloaded.SourceUrl,
            layoutVersion);
    }

    private static string CandidateLayoutFor(int year) => year switch
    {
        2022 or 2014 => "cand-2022-v1",
        2010 or 2006 => "cand-2010-v1",
        _ => throw new NotSupportedException($"No candidate layout is registered for {year}.")
    };

    private static string SectionResultLayoutFor(int year) => year switch
    {
        2022 => "vot-secao-2022-v1",
        _ => throw new NotSupportedException($"No section result layout is registered for {year}.")
    };
}

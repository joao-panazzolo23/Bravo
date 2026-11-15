using Bravo.Application.Acquisition.Services;
using System.Runtime.CompilerServices;
using Bravo.Application.Acquisition.Providers;
using Bravo.Infrastructure.Acquisition.Http;
using Bravo.Application.Acquisition.Options;
using Bravo.Infrastructure.Acquisition.Parsing;
using Bravo.Infrastructure.Acquisition.Storage;

namespace Bravo.Infrastructure.Acquisition.Providers;

/// <summary>
/// FGV (CEPESP) provider. The dataset used by the pipeline is the official IBGE/TSE
/// municipality crosswalk published as an auxiliary table of CEPESP/FGV.
/// The file is a plain UTF-8 CSV with a header row.
/// </summary>
public sealed class FgvProvider(DatasetFetcher fetcher, ProviderOptions policy) : IFgvProvider
{
    private const string DatasetKey = "fgv-municipality-crosswalk";
    private const string CrosswalkPath = "GV-CEPESP/cepespdata/main/tabelas_auxiliares/dados/codigo_municipio_ibge_tse.csv";

    private static readonly string[] RequiredColumns =
    [
        "CODIGO_UF",
        "UF",
        "COD_MUN_TSE",
        "COD_MUN_IBGE"
    ];

    public Task<FgvDatasetInfo> GetDatasetInfo(CancellationToken ct = default)
    {
        var info = new FgvDatasetInfo(
            DatasetKey,
            "FGV/CEPESP IBGE-TSE municipality crosswalk",
            new Uri(new Uri(policy.BaseAddress), CrosswalkPath),
            "CSV",
            null);

        return Task.FromResult(info);
    }

    public async Task<CachedDownload> Download(Guid executionId, FgvDatasetInfo dataset, CancellationToken ct = default)
    {
        var downloaded = await fetcher.DownloadAsync(executionId, DatasetKey, dataset.SourceUri, null, ct);
        return downloaded ?? throw new InvalidOperationException("FGV crosswalk was reported as unmodified but no local copy exists.");
    }

    public async IAsyncEnumerable<ParsedRecord<FgvMunicipalityRecord>> StreamMunicipalities(
        string filePath,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            16384,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using var reader = await DelimitedFileReader.OpenAsync(
            stream,
            Path.GetFileName(filePath),
            RequiredColumns,
            encoding: System.Text.Encoding.UTF8,
            ct: ct);

        CsvRow? row;
        while ((row = await reader.ReadAsync(ct)) is not null)
        {
            var reference = $"line {reader.LineNumber}";

            var state = row.Value.GetOptional(reader.Columns, "UF");
            var tseCode = row.Value.GetOptional(reader.Columns, "COD_MUN_TSE");
            var ibgeCode = TseValueParser.ToInt(row.Value.GetOptional(reader.Columns, "COD_MUN_IBGE"));

            if (state is null || tseCode is null || ibgeCode is null)
            {
                yield return new ParsedRecord<FgvMunicipalityRecord>(null, reference);
                continue;
            }

            yield return new ParsedRecord<FgvMunicipalityRecord>(
                new FgvMunicipalityRecord(
                    state,
                    tseCode,
                    ibgeCode.Value,
                    row.Value.GetOptional(reader.Columns, "NOME_MUNICIPIO"),
                    TseValueParser.ToInt(row.Value.GetOptional(reader.Columns, "CODIGO_UF")),
                    row.Value.GetOptional(reader.Columns, "NOME_MESO"),
                    row.Value.GetOptional(reader.Columns, "NOME_MICRO")),
                $"{state}/{tseCode}");
        }
    }
}

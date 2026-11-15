using System.IO.Compression;
using Bravo.Application.Acquisition.Providers;
using Bravo.Infrastructure.Acquisition.Parsing;
using Bravo.Infrastructure.Acquisition.Storage;

namespace Bravo.Infrastructure.Acquisition.Providers;

/// <summary>
/// Parsers for the TSE open-data archives.
///
/// The archives contain one CSV per state plus national aggregate files (BR, BRASIL).
/// Only the per-state files are consumed for state-partitioned datasets, otherwise every
/// record would be counted twice. Datasets published as a single national file
/// (presidential section votes) fall back to the only CSV entry available.
///
/// Columns are addressed by name because their order and availability vary between
/// election years. Optional columns are declared explicitly; a required column that is
/// absent aborts the dataset instead of silently shifting the values.
/// </summary>
public static class TseArchiveStreams
{
    private static readonly HashSet<string> StateAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT",
        "PA", "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO"
    };

    private static readonly string[] ElectionColumns =
    [
        "ANO_ELEICAO", "CD_TIPO_ELEICAO", "NM_TIPO_ELEICAO", "NR_TURNO", "CD_ELEICAO",
        "DS_ELEICAO", "DT_ELEICAO", "CD_CARGO", "DS_CARGO"
    ];

    private static readonly string[] CandidateColumns =
    [
        "ANO_ELEICAO", "CD_TIPO_ELEICAO", "NM_TIPO_ELEICAO", "NR_TURNO", "CD_ELEICAO",
        "DS_ELEICAO", "DT_ELEICAO", "SG_UF", "CD_CARGO", "DS_CARGO",
        "SQ_CANDIDATO", "NR_CANDIDATO", "NM_CANDIDATO", "NM_URNA_CANDIDATO",
        "NR_PARTIDO", "SG_PARTIDO", "NM_PARTIDO"
    ];

    private static readonly string[] SectionResultColumns =
    [
        "ANO_ELEICAO", "CD_TIPO_ELEICAO", "NM_TIPO_ELEICAO", "CD_ELEICAO", "DS_ELEICAO",
        "DT_ELEICAO", "NR_TURNO", "SG_UF", "CD_MUNICIPIO", "NR_ZONA",
        "NR_SECAO", "CD_CARGO", "DS_CARGO", "NR_VOTAVEL", "NM_VOTAVEL", "QT_VOTOS",
        "NR_LOCAL_VOTACAO"
    ];

    private static readonly string[] SectionStatisticsColumns =
    [
        "ANO_ELEICAO", "CD_TIPO_ELEICAO", "NM_TIPO_ELEICAO", "CD_ELEICAO", "DS_ELEICAO",
        "DT_ELEICAO", "NR_TURNO", "SG_UF", "CD_MUNICIPIO", "NR_ZONA",
        "NR_SECAO", "CD_CARGO", "DS_CARGO", "QT_APTOS", "QT_COMPARECIMENTO",
        "QT_ABSTENCOES", "QT_VOTOS_NOMINAIS", "QT_VOTOS_BRANCOS", "QT_VOTOS_NULOS",
        "NR_LOCAL_VOTACAO"
    ];

    private static readonly string[] CrosswalkColumns =
    [
        "SG_UF", "CD_MUNICIPIO_TSE", "CD_MUNICIPIO_IBGE"
    ];

    public static async IAsyncEnumerable<ParsedRecord<TseElectionMetadataRecord>> ReadElectionMetadata(
        string filePath,
        TseDatasetInfo dataset,
        string? stateFilter = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Metadata is derived from the candidate files, which publish the election, round and
        // contest of every registered candidacy. The distinct set is small, so a dictionary
        // bounded by the number of contests is enough while streaming the archive.
        var elections = new Dictionary<string, MutableElection>(StringComparer.Ordinal);

        await foreach (var (row, reader, reference) in ReadRows(filePath, ElectionColumns, stateFilter, ct))
        {
            var year = TseValueParser.ToInt(row.Get(reader.Columns, "ANO_ELEICAO"));
            var electionCode = row.Get(reader.Columns, "CD_ELEICAO");
            var round = TseValueParser.ToInt(row.Get(reader.Columns, "NR_TURNO"));
            var officeCode = TseValueParser.ToInt(row.Get(reader.Columns, "CD_CARGO"));
            var officeName = row.Get(reader.Columns, "DS_CARGO");
            var typeCode = TseValueParser.ToInt(row.Get(reader.Columns, "CD_TIPO_ELEICAO"));
            var typeName = row.Get(reader.Columns, "NM_TIPO_ELEICAO");
            var name = row.Get(reader.Columns, "DS_ELEICAO");
            var date = TseValueParser.ToDate(row.Get(reader.Columns, "DT_ELEICAO"));
            var state = row.GetOptional(reader.Columns, "SG_UF");

            if (year is null || electionCode is null || round is null
                || officeCode is null || officeName is null || name is null || date is null)
            {
                yield return new ParsedRecord<TseElectionMetadataRecord>(null, reference);
                continue;
            }

            if (!elections.TryGetValue(electionCode, out var election))
            {
                election = new MutableElection(
                    electionCode,
                    year.Value,
                    typeCode ?? 0,
                    typeName ?? string.Empty,
                    name,
                    date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    round.Value);
                elections[electionCode] = election;
            }

            election.AddContest(officeCode.Value, officeName, NormalizeState(state));
        }

        foreach (var election in elections.Values)
        {
            yield return new ParsedRecord<TseElectionMetadataRecord>(
                new TseElectionMetadataRecord(
                    election.TseCode,
                    election.Year,
                    election.TypeCode,
                    election.TypeName,
                    election.Name,
                    election.ElectionDate,
                    election.Round,
                    election.Contests.Select(c => new TseContestRecord(c.OfficeCode, c.OfficeName, c.State)).ToList()),
                $"election {election.TseCode}");
        }
    }

    public static async IAsyncEnumerable<ParsedRecord<TseCandidateRecord>> ReadCandidates(
        string filePath,
        TseDatasetInfo dataset,
        string? stateFilter = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var required = CandidateColumns;
        await foreach (var (row, reader, reference) in ReadRows(filePath, required, stateFilter, ct))
        {
            var electionCode = row.Get(reader.Columns, "CD_ELEICAO");
            var year = TseValueParser.ToInt(row.Get(reader.Columns, "ANO_ELEICAO"));
            var round = TseValueParser.ToInt(row.Get(reader.Columns, "NR_TURNO"));
            var electionName = row.Get(reader.Columns, "DS_ELEICAO");
            var electionDate = TseValueParser.ToDate(row.Get(reader.Columns, "DT_ELEICAO"));
            var typeCode = TseValueParser.ToInt(row.Get(reader.Columns, "CD_TIPO_ELEICAO"));
            var typeName = row.Get(reader.Columns, "NM_TIPO_ELEICAO");
            var sequential = row.Get(reader.Columns, "SQ_CANDIDATO");
            var number = row.Get(reader.Columns, "NR_CANDIDATO");
            var name = row.Get(reader.Columns, "NM_CANDIDATO");
            var ballotName = row.GetOptional(reader.Columns, "NM_URNA_CANDIDATO");
            var officeCode = TseValueParser.ToInt(row.Get(reader.Columns, "CD_CARGO"));
            var officeName = row.Get(reader.Columns, "DS_CARGO");
            var partyNumber = TseValueParser.ToInt(row.Get(reader.Columns, "NR_PARTIDO"));
            var partyAcronym = row.GetOptional(reader.Columns, "SG_PARTIDO");
            var partyName = row.GetOptional(reader.Columns, "NM_PARTIDO");
            var state = NormalizeState(row.GetOptional(reader.Columns, "SG_UF"));

            if (electionCode is null || year is null || round is null || electionName is null
                || electionDate is null || sequential is null || number is null || name is null
                || officeCode is null || officeName is null || partyNumber is null
                || partyAcronym is null || state is null)
            {
                yield return new ParsedRecord<TseCandidateRecord>(null, reference);
                continue;
            }

            yield return new ParsedRecord<TseCandidateRecord>(
                new TseCandidateRecord(
                    electionCode,
                    year.Value,
                    electionName,
                    electionDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    round.Value,
                    typeCode ?? 0,
                    typeName ?? string.Empty,
                    sequential,
                    number,
                    name,
                    string.IsNullOrWhiteSpace(ballotName) ? name : ballotName,
                    officeCode.Value,
                    officeName,
                    partyNumber.Value,
                    partyAcronym,
                    partyName ?? partyAcronym,
                    state,
                    TseValueParser.ToInt(row.GetOptional(reader.Columns, "CD_SITUACAO_CANDIDATURA")),
                    row.GetOptional(reader.Columns, "DS_SITUACAO_CANDIDATURA"),
                    TseValueParser.ToDate(row.GetOptional(reader.Columns, "DT_NASCIMENTO"))),
                $"{electionCode}/{state}/{sequential}");
        }
    }

    public static async IAsyncEnumerable<ParsedRecord<TsePollingSectionResultRecord>> ReadPollingSectionResults(
        string filePath,
        TseDatasetInfo dataset,
        string? stateFilter = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var (row, reader, reference) in ReadRows(filePath, SectionResultColumns, stateFilter, ct))
        {
            var election = ReadElectionIdentity(row, reader);
            var state = NormalizeState(row.Get(reader.Columns, "SG_UF"));
            var municipality = NormalizeMunicipalityCode(row.Get(reader.Columns, "CD_MUNICIPIO"));
            var zone = row.Get(reader.Columns, "NR_ZONA");
            var section = row.Get(reader.Columns, "NR_SECAO");
            var officeCode = TseValueParser.ToInt(row.Get(reader.Columns, "CD_CARGO"));
            var officeName = row.Get(reader.Columns, "DS_CARGO");
            var votableNumber = row.Get(reader.Columns, "NR_VOTAVEL");
            var votableName = row.Get(reader.Columns, "NM_VOTAVEL");
            var votes = TseValueParser.ToInt(row.Get(reader.Columns, "QT_VOTOS"));
            var placeCode = row.GetOptional(reader.Columns, "NR_LOCAL_VOTACAO");

            if (election is null || state is null || municipality is null || zone is null || section is null
                || officeCode is null || officeName is null || votableNumber is null
                || votableName is null || votes is null || placeCode is null)
            {
                yield return new ParsedRecord<TsePollingSectionResultRecord>(null, reference);
                continue;
            }

            var isBlank = votableNumber is "95" || votableName.Equals("VOTO BRANCO", StringComparison.OrdinalIgnoreCase);
            var isNull = votableNumber is "96" || votableName.Equals("VOTO NULO", StringComparison.OrdinalIgnoreCase);
            var sequential = row.GetOptional(reader.Columns, "SQ_CANDIDATO");

            yield return new ParsedRecord<TsePollingSectionResultRecord>(
                new TsePollingSectionResultRecord(
                    election,
                    state,
                    municipality,
                    zone,
                    section,
                    placeCode,
                    row.GetOptional(reader.Columns, "NM_LOCAL_VOTACAO"),
                    row.GetOptional(reader.Columns, "DS_LOCAL_VOTACAO_ENDERECO"),
                    officeCode.Value,
                    officeName,
                    votableNumber,
                    votableName,
                    sequential,
                    votes.Value,
                    isBlank,
                    isNull),
                $"{state}/{zone}/{section}/{officeCode}/{votableNumber}");
        }
    }

    public static async IAsyncEnumerable<ParsedRecord<TsePollingSectionStatisticsRecord>> ReadPollingSectionStatistics(
        string filePath,
        TseDatasetInfo dataset,
        string? stateFilter = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var (row, reader, reference) in ReadRows(filePath, SectionStatisticsColumns, stateFilter, ct))
        {
            var election = ReadElectionIdentity(row, reader);
            var state = NormalizeState(row.Get(reader.Columns, "SG_UF"));
            var municipality = NormalizeMunicipalityCode(row.Get(reader.Columns, "CD_MUNICIPIO"));
            var zone = row.Get(reader.Columns, "NR_ZONA");
            var section = row.Get(reader.Columns, "NR_SECAO");
            var officeCode = TseValueParser.ToInt(row.Get(reader.Columns, "CD_CARGO"));
            var officeName = row.Get(reader.Columns, "DS_CARGO");
            var placeCode = row.GetOptional(reader.Columns, "NR_LOCAL_VOTACAO");

            if (election is null || state is null || municipality is null || zone is null || section is null
                || officeCode is null || officeName is null || placeCode is null)
            {
                yield return new ParsedRecord<TsePollingSectionStatisticsRecord>(null, reference);
                continue;
            }

            yield return new ParsedRecord<TsePollingSectionStatisticsRecord>(
                new TsePollingSectionStatisticsRecord(
                    election,
                    state,
                    municipality,
                    zone,
                    section,
                    placeCode,
                    row.GetOptional(reader.Columns, "NM_LOCAL_VOTACAO"),
                    row.GetOptional(reader.Columns, "DS_LOCAL_VOTACAO_ENDERECO"),
                    officeCode.Value,
                    officeName,
                    TseValueParser.ToInt(row.GetOptional(reader.Columns, "QT_APTOS")),
                    TseValueParser.ToInt(row.GetOptional(reader.Columns, "QT_COMPARECIMENTO")),
                    TseValueParser.ToInt(row.GetOptional(reader.Columns, "QT_ABSTENCOES")),
                    TseValueParser.ToInt(row.GetOptional(reader.Columns, "QT_VOTOS_NOMINAIS")),
                    TseValueParser.ToInt(row.GetOptional(reader.Columns, "QT_VOTOS_BRANCOS")),
                    TseValueParser.ToInt(row.GetOptional(reader.Columns, "QT_VOTOS_NULOS")),
                    TseValueParser.ToInt(row.GetOptional(reader.Columns, "QT_VOTOS_LEGENDA"))),
                $"{state}/{zone}/{section}/{officeCode}");
        }
    }

    public static async IAsyncEnumerable<ParsedRecord<TseMunicipalityCrosswalkRecord>> ReadMunicipalityCrosswalk(
        string filePath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var (row, reader, reference) in ReadRows(filePath, CrosswalkColumns, null, ct))
        {
            var state = NormalizeState(row.Get(reader.Columns, "SG_UF"));
            var tseCode = NormalizeMunicipalityCode(row.Get(reader.Columns, "CD_MUNICIPIO_TSE"));
            var ibgeCode = TseValueParser.ToInt(row.Get(reader.Columns, "CD_MUNICIPIO_IBGE"));

            if (state is null || tseCode is null || ibgeCode is null)
            {
                yield return new ParsedRecord<TseMunicipalityCrosswalkRecord>(null, reference);
                continue;
            }

            yield return new ParsedRecord<TseMunicipalityCrosswalkRecord>(
                new TseMunicipalityCrosswalkRecord(
                    state,
                    tseCode,
                    ibgeCode.Value,
                    row.GetOptional(reader.Columns, "NM_MUNICIPIO_TSE"),
                    row.GetOptional(reader.Columns, "NM_MUNICIPIO_IBGE")),
                $"{state}/{tseCode}");
        }
    }

    /// <summary>
    /// Streams rows across every eligible CSV entry of the archive. Each entry is bounded by
    /// the configured decompressed-size limit.
    /// </summary>
    private static async IAsyncEnumerable<(CsvRow Row, DelimitedFileReader Reader, string Reference)> ReadRows(
        string filePath,
        IReadOnlyCollection<string> requiredColumns,
        string? stateFilter,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var archive = ZipFile.OpenRead(filePath);
        var entries = SelectEntries(archive, stateFilter);
        if (entries.Count == 0)
        {
            throw new InvalidDataException($"Archive '{Path.GetFileName(filePath)}' has no CSV entries.");
        }

        foreach (var entry in entries)
        {
            await using var bounded = ArchiveGuard.OpenBounded(entry, long.MaxValue);
            await using var reader = await DelimitedFileReader.OpenAsync(bounded, entry.FullName, requiredColumns, ct: ct);

            CsvRow? row;
            while ((row = await reader.ReadAsync(ct)) is not null)
            {
                ct.ThrowIfCancellationRequested();
                yield return (row.Value, reader, $"{entry.FullName}:{reader.LineNumber}");
            }
        }
    }

    /// <summary>
    /// Chooses the per-state entries. When the archive contains only national files, those
    /// files are used instead. Aggregate files are never combined with state files.
    /// </summary>
    public static IReadOnlyList<ZipArchiveEntry> SelectEntries(ZipArchive archive, string? stateFilter = null)
    {
        var csv = ArchiveGuard.GetCsvEntries(archive, long.MaxValue);

        var perState = csv
            .Where(e => TryGetStateSuffix(e, out var suffix) && StateAbbreviations.Contains(suffix))
            .ToList();

        if (perState.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(stateFilter))
            {
                var filtered = perState
                    .Where(e => TryGetStateSuffix(e, out var suffix)
                                && string.Equals(suffix, stateFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (filtered.Count == 0)
                {
                    throw new InvalidDataException(
                        $"Archive '{Path.GetFileName(perState[0].FullName)}' has no file for state '{stateFilter}'.");
                }

                return filtered;
            }

            return perState;
        }

        // National archives (a single CSV for the whole country, or BR/BRASIL aggregates). When
        // a state was requested it is filtered while parsing; aggregate files are never mixed
        // with per-state files, which would count every vote twice.
        return csv
            .Where(e => !e.FullName.Contains("BRASIL", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool TryGetStateSuffix(ZipArchiveEntry entry, out string state)
    {
        state = string.Empty;
        var name = Path.GetFileNameWithoutExtension(entry.Name);
        var underscore = name.LastIndexOf('_');
        if (underscore < 0 || underscore == name.Length - 1)
        {
            return false;
        }

        var suffix = name[(underscore + 1)..];
        if (suffix.Length != 2)
        {
            return false;
        }

        state = suffix.ToUpperInvariant();
        return true;
    }

    /// <summary>
    /// The official TSE municipality code has five digits, but the vote files publish it
    /// without leading zeros ("1392") while the crosswalk publishes it padded ("01392").
    /// Normalizing to five digits is required to join results against the crosswalk.
    /// </summary>
    private static string? NormalizeMunicipalityCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var value = code.Trim();
        return value.Length < 5 ? value.PadLeft(5, '0') : value;
    }

    /// <summary>
    /// Reads the official election identity published on every electoral row.
    /// </summary>
    private static TseElectionIdentity? ReadElectionIdentity(CsvRow row, DelimitedFileReader reader)
    {
        var electionCode = row.Get(reader.Columns, "CD_ELEICAO");
        var year = TseValueParser.ToInt(row.Get(reader.Columns, "ANO_ELEICAO"));
        var round = TseValueParser.ToInt(row.Get(reader.Columns, "NR_TURNO"));
        var name = row.Get(reader.Columns, "DS_ELEICAO");
        var date = TseValueParser.ToDate(row.Get(reader.Columns, "DT_ELEICAO"));

        if (electionCode is null || year is null || round is null || name is null || date is null)
        {
            return null;
        }

        return new TseElectionIdentity(
            electionCode,
            year.Value,
            TseValueParser.ToInt(row.GetOptional(reader.Columns, "CD_TIPO_ELEICAO")) ?? 0,
            row.GetOptional(reader.Columns, "NM_TIPO_ELEICAO") ?? string.Empty,
            name,
            date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            round.Value);
    }

    private static string? NormalizeState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        var value = state.Trim().ToUpperInvariant();
        return StateAbbreviations.Contains(value) ? value : null;
    }

    private sealed class MutableElection(
        string tseCode,
        int year,
        int typeCode,
        string typeName,
        string name,
        DateTime electionDate,
        int round)
    {
        public string TseCode { get; } = tseCode;
        public int Year { get; } = year;
        public int TypeCode { get; } = typeCode;
        public string TypeName { get; } = typeName;
        public string Name { get; } = name;
        public DateTime ElectionDate { get; } = electionDate;
        public int Round { get; } = round;
        public List<(int OfficeCode, string OfficeName, string? State)> Contests { get; } = [];

        public void AddContest(int officeCode, string officeName, string? state)
        {
            if (Contests.Any(c => c.OfficeCode == officeCode
                                  && c.OfficeName == officeName
                                  && string.Equals(c.State, state, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            Contests.Add((officeCode, officeName, state));
        }
    }
}

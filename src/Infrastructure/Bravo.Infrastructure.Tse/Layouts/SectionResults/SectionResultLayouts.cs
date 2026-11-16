using System.Text;
using Bravo.Infrastructure.Tse.Layouts;

namespace Bravo.Infrastructure.Tse.Layouts.SectionResults;

public static class SectionResultLayouts
{
    public const string Version2022 = "vot-secao-2022-v1";

    private static readonly Encoding Latin1 = Encoding.Latin1;

    private static readonly IReadOnlyDictionary<string, string> Columns2022 =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ANO_ELEICAO"] = Canonical.Year,
            ["CD_TIPO_ELEICAO"] = Canonical.ElectionTypeCode,
            ["NM_TIPO_ELEICAO"] = Canonical.ElectionTypeName,
            ["NR_TURNO"] = Canonical.Round,
            ["CD_ELEICAO"] = Canonical.ElectionCode,
            ["DS_ELEICAO"] = Canonical.ElectionDescription,
            ["DT_ELEICAO"] = Canonical.ElectionDate,
            ["SG_UF"] = Canonical.StateAcronym,
            ["CD_MUNICIPIO"] = Canonical.MunicipalityCode,
            ["NM_MUNICIPIO"] = Canonical.MunicipalityName,
            ["NR_ZONA"] = Canonical.ZoneNumber,
            ["NR_SECAO"] = Canonical.SectionNumber,
            ["CD_CARGO"] = Canonical.OfficeCode,
            ["DS_CARGO"] = Canonical.OfficeName,
            ["NR_VOTAVEL"] = Canonical.BallotNumber,
            ["NM_VOTAVEL"] = Canonical.BallotName,
            ["QT_VOTOS"] = Canonical.Votes,
            ["NR_LOCAL_VOTACAO"] = Canonical.PollingPlaceNumber,
            ["SQ_CANDIDATO"] = Canonical.CandidateSequential,
            ["NM_LOCAL_VOTACAO"] = Canonical.PollingPlaceName,
            ["DS_LOCAL_VOTACAO_ENDERECO"] = Canonical.PollingPlaceAddress
        };

    private static readonly string[] RequiredFields =
    [
        Canonical.Year,
        Canonical.ElectionCode,
        Canonical.Round,
        Canonical.StateAcronym,
        Canonical.MunicipalityCode,
        Canonical.ZoneNumber,
        Canonical.SectionNumber,
        Canonical.OfficeCode,
        Canonical.BallotNumber,
        Canonical.Votes
    ];

    public static TseLayout V2022() => TseLayout.Create(
        Version2022,
        delimiter: ';',
        quote: '"',
        encoding: Latin1,
        columns: Columns2022,
        requiredCanonicalFields: RequiredFields);

    public static TseLayout Get(string version) => version switch
    {
        Version2022 => V2022(),
        _ => throw new ArgumentOutOfRangeException(
            nameof(version),
            version,
            "Unknown section result layout version.")
    };
}

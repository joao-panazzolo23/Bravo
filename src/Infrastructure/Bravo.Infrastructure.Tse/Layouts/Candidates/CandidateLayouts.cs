using System.Text;
using Bravo.Infrastructure.Tse.Layouts;

namespace Bravo.Infrastructure.Tse.Layouts.Candidates;

public static class CandidateLayouts
{
    public const string Version2022 = "cand-2022-v1";
    public const string Version2010 = "cand-2010-v1";

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
            ["CD_CARGO"] = Canonical.OfficeCode,
            ["DS_CARGO"] = Canonical.OfficeName,
            ["SQ_CANDIDATO"] = Canonical.CandidateSequential,
            ["NR_CANDIDATO"] = Canonical.BallotNumber,
            ["NM_CANDIDATO"] = Canonical.CandidateName,
            ["NM_URNA_CANDIDATO"] = Canonical.UrnName,
            ["NM_SOCIAL_CANDIDATO"] = Canonical.SocialName,
            ["NR_CPF_CANDIDATO"] = Canonical.Cpf,
            ["DS_EMAIL"] = Canonical.Email,
            ["CD_SITUACAO_CANDIDATURA"] = Canonical.RegistrationStatusCode,
            ["DS_SITUACAO_CANDIDATURA"] = Canonical.RegistrationStatus,
            ["TP_AGREMIACAO"] = Canonical.AggregationType,
            ["NR_PARTIDO"] = Canonical.PartyNumber,
            ["SG_PARTIDO"] = Canonical.PartyAcronym,
            ["NM_PARTIDO"] = Canonical.PartyName,
            ["NR_FEDERACAO"] = Canonical.FederationNumber,
            ["NM_FEDERACAO"] = Canonical.FederationName,
            ["SG_FEDERACAO"] = Canonical.FederationAcronym,
            ["SQ_COLIGACAO"] = Canonical.CoalitionNumber,
            ["NM_COLIGACAO"] = Canonical.CoalitionName,
            ["DS_COMPOSICAO_COLIGACAO"] = Canonical.CoalitionComposition,
            ["DT_NASCIMENTO"] = Canonical.BirthDate,
            ["CD_GENERO"] = Canonical.GenderCode,
            ["DS_GENERO"] = Canonical.Gender,
            ["CD_GRAU_INSTRUCAO"] = Canonical.EducationCode,
            ["DS_GRAU_INSTRUCAO"] = Canonical.Education,
            ["CD_COR_RACA"] = Canonical.RaceCode,
            ["DS_COR_RACA"] = Canonical.Race,
            ["CD_OCUPACAO"] = Canonical.OccupationCode,
            ["DS_OCUPACAO"] = Canonical.Occupation,
            ["CD_SIT_TOT_TURNO"] = Canonical.TurnOutcomeCode,
            ["DS_SIT_TOT_TURNO"] = Canonical.TurnOutcome
        };

    private static readonly IReadOnlyDictionary<string, string> Columns2010 =
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
            ["CD_CARGO"] = Canonical.OfficeCode,
            ["DS_CARGO"] = Canonical.OfficeName,
            ["SQ_CANDIDATO"] = Canonical.CandidateSequential,
            ["NR_CANDIDATO"] = Canonical.BallotNumber,
            ["NM_CANDIDATO"] = Canonical.CandidateName,
            ["NM_URNA_CANDIDATO"] = Canonical.UrnName,
            ["NM_SOCIAL_CANDIDATO"] = Canonical.SocialName,
            ["NR_CPF_CANDIDATO"] = Canonical.Cpf,
            ["NM_EMAIL"] = Canonical.Email,
            ["CD_SITUACAO_CANDIDATURA"] = Canonical.RegistrationStatusCode,
            ["DS_SITUACAO_CANDIDATURA"] = Canonical.RegistrationStatus,
            ["TP_AGREMIACAO"] = Canonical.AggregationType,
            ["NR_PARTIDO"] = Canonical.PartyNumber,
            ["SG_PARTIDO"] = Canonical.PartyAcronym,
            ["NM_PARTIDO"] = Canonical.PartyName,
            ["SQ_COLIGACAO"] = Canonical.CoalitionNumber,
            ["NM_COLIGACAO"] = Canonical.CoalitionName,
            ["DS_COMPOSICAO_COLIGACAO"] = Canonical.CoalitionComposition,
            ["DT_NASCIMENTO"] = Canonical.BirthDate,
            ["CD_GENERO"] = Canonical.GenderCode,
            ["DS_GENERO"] = Canonical.Gender,
            ["CD_GRAU_INSTRUCAO"] = Canonical.EducationCode,
            ["DS_GRAU_INSTRUCAO"] = Canonical.Education,
            ["CD_COR_RACA"] = Canonical.RaceCode,
            ["DS_COR_RACA"] = Canonical.Race,
            ["CD_OCUPACAO"] = Canonical.OccupationCode,
            ["DS_OCUPACAO"] = Canonical.Occupation,
            ["CD_SIT_TOT_TURNO"] = Canonical.TurnOutcomeCode,
            ["DS_SIT_TOT_TURNO"] = Canonical.TurnOutcome
        };

    private static readonly string[] RequiredFields =
    [
        Canonical.Year,
        Canonical.ElectionCode,
        Canonical.Round,
        Canonical.StateAcronym,
        Canonical.OfficeCode,
        Canonical.CandidateSequential,
        Canonical.PartyNumber
    ];

    public static TseLayout V2022() => TseLayout.Create(
        Version2022,
        delimiter: ';',
        quote: '"',
        encoding: Latin1,
        columns: Columns2022,
        requiredCanonicalFields: RequiredFields);

    public static TseLayout V2010() => TseLayout.Create(
        Version2010,
        delimiter: ';',
        quote: '"',
        encoding: Latin1,
        columns: Columns2010,
        requiredCanonicalFields: RequiredFields);

    public static TseLayout Get(string version) => version switch
    {
        Version2022 => V2022(),
        Version2010 => V2010(),
        _ => throw new ArgumentOutOfRangeException(
            nameof(version),
            version,
            "Unknown candidate layout version.")
    };
}

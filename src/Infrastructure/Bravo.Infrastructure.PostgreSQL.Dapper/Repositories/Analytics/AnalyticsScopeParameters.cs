using Bravo.Domain.Electoral.Analytics;
using Dapper;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Analytics;

/// <summary>
/// Parameters every analytical query shares.
/// </summary>
/// <remarks>
/// The optional identifiers are sent as strongly typed nullable values so a missing filter becomes
/// a <c>NULL</c> parameter instead of a missing predicate. The matching SQL always casts it, which
/// keeps Dapper from sending an untyped null that PostgreSQL would refuse to compare.
/// </remarks>
internal sealed record AnalyticsScopeParameters(
    Guid? ElectionId,
    int? Year,
    Guid? OfficeId,
    short? Round,
    Guid? StateId,
    Guid? MunicipalityId,
    Guid? ZoneId,
    Guid? CandidateId,
    Guid? CandidateIdentityId)
{
    public DynamicParameters ToDynamicParameters()
    {
        var parameters = new DynamicParameters();
        parameters.Add("ElectionId", ElectionId);
        parameters.Add("Year", Year);
        parameters.Add("OfficeId", OfficeId);
        parameters.Add("Round", Round);
        parameters.Add("StateId", StateId);
        parameters.Add("MunicipalityId", MunicipalityId);
        parameters.Add("ZoneId", ZoneId);
        parameters.Add("CandidateId", CandidateId);
        parameters.Add("CandidateIdentityId", CandidateIdentityId);

        return parameters;
    }
}

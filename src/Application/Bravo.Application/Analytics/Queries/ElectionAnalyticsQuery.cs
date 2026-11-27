using Bravo.Domain.Electoral.Analytics;

namespace Bravo.Application.Analytics.Queries;

/// <summary>
/// The electoral scope shared by every analytical query.
/// </summary>
/// <remarks>
/// The individual properties are flat on purpose: ASP.NET Core binds a query string to the members
/// of the bound object, so the HTTP contract reads naturally (<c>?officeId=…&amp;round=2</c>). A
/// nested object would only bind under a prefix such as <c>filter.officeId</c>.
///
/// <see cref="Filter"/> is a write-through façade for callers that already hold an
/// <see cref="ElectionAnalyticsFilter"/> (the MCP tools and the handlers). Reading it rebuilds a
/// filter from the current values, and assigning it copies each value onto the flat properties, so
/// there is exactly one representation underneath.
/// </remarks>
public abstract record ElectionAnalyticsQuery
{
    public int? Year { get; set; }
    public Guid? ElectionId { get; set; }
    public Guid? OfficeId { get; set; }
    public short? Round { get; set; }
    public Guid? CandidateId { get; set; }
    public Guid? CandidateIdentityId { get; set; }
    public Guid? StateId { get; set; }
    public Guid? MunicipalityId { get; set; }
    public Guid? ZoneId { get; set; }

    public ElectionAnalyticsFilter Filter
    {
        get => ToFilter();
        init
        {
            Year = value.Year;
            ElectionId = value.ElectionId;
            OfficeId = value.OfficeId;
            Round = value.Round;
            CandidateId = value.CandidateId;
            CandidateIdentityId = value.CandidateIdentityId;
            StateId = value.StateId;
            MunicipalityId = value.MunicipalityId;
            ZoneId = value.ZoneId;
        }
    }

    public ElectionAnalyticsFilter ToFilter()
        => new(
            Year,
            ElectionId,
            OfficeId,
            Round,
            CandidateId,
            CandidateIdentityId,
            StateId,
            MunicipalityId,
            ZoneId);
}

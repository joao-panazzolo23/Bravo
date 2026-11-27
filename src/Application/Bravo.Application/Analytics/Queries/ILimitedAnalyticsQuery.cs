namespace Bravo.Application.Analytics.Queries;

/// <summary>
/// Marks an analytical query that returns a bounded list.
/// </summary>
public interface ILimitedAnalyticsQuery
{
    int Limit { get; }
}

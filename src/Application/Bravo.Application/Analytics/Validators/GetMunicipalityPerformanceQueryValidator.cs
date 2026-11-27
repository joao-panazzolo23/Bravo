using Bravo.Application.Analytics.Queries;
using FluentValidation;

namespace Bravo.Application.Analytics.Validators;

public sealed class GetMunicipalityPerformanceQueryValidator
    : LimitedAnalyticsQueryValidator<GetMunicipalityPerformanceQuery>
{
}

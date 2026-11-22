using Bravo.Application.Imports.Common.Queries;
using FluentValidation;

namespace Bravo.Application.Imports.Common.Validators;

public sealed class ListImportFailedRecordsQueryValidator : AbstractValidator<ListImportFailedRecordsQuery>
{
    public ListImportFailedRecordsQueryValidator()
    {
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, 1_000);
    }
}

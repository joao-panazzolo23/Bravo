using Bravo.Application.Importing.Queries;
using FluentValidation;

namespace Bravo.Application.Importing.Validators;

public sealed class ListImportFailedRecordsQueryValidator : AbstractValidator<ListImportFailedRecordsQuery>
{
    public ListImportFailedRecordsQueryValidator()
    {
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, 1_000);
    }
}

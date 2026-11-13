using Bravo.Domain.Users.ViewModel;
using Bravo.Shared.Result;
using Mediator;

namespace Bravo.Application.Users.Queries;

public record ListUserQuery : IQuery<Result<IEnumerable<ListUserViewModel>>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
}

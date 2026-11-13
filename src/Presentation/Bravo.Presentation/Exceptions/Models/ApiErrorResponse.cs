using Bravo.Application.Exceptions;

namespace Bravo.Presentation.Exceptions.Models;

public record ApiErrorResponse
{
    public List<ApiError> Errors { get; set; } = new();
}
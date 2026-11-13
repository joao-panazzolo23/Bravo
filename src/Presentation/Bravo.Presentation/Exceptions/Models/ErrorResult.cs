using Bravo.Application.Exceptions;
using System.Net;

namespace Bravo.Presentation.Exceptions.Models;

public record ErrorResult
{
    public ErrorResult(
        HttpStatusCode statusCode,
        string type,
        string details = "One or more errors occurred.")
    {
        StatusCode = statusCode;
        Type = type;
        Details = details;
    }

    public ErrorResult(
        HttpStatusCode StatusCode,
        string Type,
        IEnumerable<ApiError>? Errors,
        string Details = "One or more errors occurred.")
    {
        this.StatusCode = StatusCode;
        this.Errors = Errors;
        this.Type = Type;
        this.Details = Details;
    }
    public HttpStatusCode StatusCode { get; }
    public string Type { get; }
    public string Details { get; set; }
    public IEnumerable<ApiError>? Errors { get; }

};

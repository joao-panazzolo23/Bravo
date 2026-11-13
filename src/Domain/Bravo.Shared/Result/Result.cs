using System.Net;
using System.Text.Json.Serialization;

namespace Bravo.Shared.Result;

/// <summary>
/// If you eventually don't want to use a Return, use MediatR's "Unit" record.
///
/// Todo: think about Results without Data. If I'm not supposed to return anything,
/// I would I need to declare return types? It gets messy and breaks DRY principle.
///
/// Creating another instance of this class means i'm returning a different class
/// Factories seems very welcome to solve this issue, but is indeed another class to implement.
///
/// And what about a list of exceptions and implementing Interfaces? Would that be over engineering?
/// Interfaces could be a way to mock data.
///
/// </summary>
/// <typeparam name="T"></typeparam>
public class Result<T>(HttpStatusCode statusCode, T? data = default, string? message = null)
{
    [JsonIgnore] private HttpStatusCode _statusCode { get; set; } = statusCode;
    [JsonIgnore] public int StatusCode => (int)_statusCode;
    public string? Message { get; set; } = message;
    public T? Data { get; set; } = data;

    public static Result<T> OK(T? data = default, string? message = null)
        => new(HttpStatusCode.OK, data, message);

    public static Result<T> NoContent(T? data = default, string? message = null)
        => new(HttpStatusCode.NoContent, data, message);

    public static Result<T> Unauthorized(T? data = default, string? message = null)
        => new(HttpStatusCode.Unauthorized, data, message);

    public static Result<T> NotFound(T? data = default, string? message = null)
        => new(HttpStatusCode.NotFound, data, message);

    public static Result<T> Created(T? data = default, string? message = null)
        => new(HttpStatusCode.Created, data, message);

    public static Result<T> Conflict(T? data = default, string? message = null)
        => new(HttpStatusCode.Conflict, data, message);

    public static Result<T> BadRequest(T? data = default, string? message = null)
        => new(HttpStatusCode.BadRequest, data, message);

    public static Result<T> InternalServerError(T? data = default, string? message = null)
        => new(HttpStatusCode.InternalServerError, data, message);
}

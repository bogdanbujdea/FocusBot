using System.Net;

namespace FocusBot.Core.Entities;

/// <summary>
/// Result of a FocusBot Web API call with optional user-facing error text.
/// </summary>
public sealed class ApiResult<T>
{
    private ApiResult(bool isSuccess, T? value, HttpStatusCode? statusCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public T? Value { get; }

    public HttpStatusCode? StatusCode { get; }

    public string? ErrorMessage { get; }

    public static ApiResult<T> Success(T value) =>
        new(true, value, null, null);

    public static ApiResult<T> Failure(HttpStatusCode statusCode) =>
        new(false, default, statusCode, GetUserFriendlyMessage(statusCode));

    public static ApiResult<T> NetworkError(string? detail = null)
    {
        var message = string.IsNullOrWhiteSpace(detail)
            ? "Could not connect to the server."
            : $"Could not connect to the server. {detail}";
        return new(false, default, null, message);
    }

    /// <summary>
    /// Used when the client cannot build a request (e.g. no access token).
    /// </summary>
    public static ApiResult<T> NotAuthenticated() =>
        new(false, default, HttpStatusCode.Unauthorized, GetUserFriendlyMessage(HttpStatusCode.Unauthorized));

    private static string GetUserFriendlyMessage(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code switch
        {
            401 or 403 => "Authentication error, please sign in again.",
            404 => "Session not found on server.",
            >= 500 and <= 599 => "API is not reachable, please try again later.",
            _ => "Something went wrong, please try again.",
        };
    }
}

namespace FocusBot.Core.Entities;

/// <summary>
/// Maps a failed session API result to another <see cref="ApiResult{T}"/> type.
/// </summary>
public static class ApiResultMappings
{
    public static ApiResult<T> FromFailedSessionCall<T>(ApiResult<ApiSessionResponse> r)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (r.IsSuccess)
            throw new ArgumentException("Expected a failed result.", nameof(r));
        if (r.StatusCode is null)
            return ApiResult<T>.NetworkError(r.ErrorMessage);
        return ApiResult<T>.Failure(r.StatusCode.Value);
    }
}

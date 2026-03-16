namespace FocusBot.Core.Interfaces;

public interface IAuthService
{
    Task<bool> SignInWithMagicLinkAsync(string email);
    Task<bool> HandleCallbackAsync(string uri);
    Task<string?> GetAccessTokenAsync();
    Task SignOutAsync();
    bool IsAuthenticated { get; }
    event Action? AuthStateChanged;
}

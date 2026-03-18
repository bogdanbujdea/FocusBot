using System.Net.Mail;

namespace FocusBot.WebAPI.Features.Waitlist;

public static class WaitlistEmailValidator
{
    public static bool TryNormalize(string? email, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmed = email.Trim();
        if (trimmed.Length is < 5 or > 254)
        {
            return false;
        }

        if (!trimmed.Contains('@', StringComparison.Ordinal))
        {
            return false;
        }

        if (!MailAddress.TryCreate(trimmed, out _))
        {
            return false;
        }

        normalized = trimmed.ToLowerInvariant();
        return true;
    }
}


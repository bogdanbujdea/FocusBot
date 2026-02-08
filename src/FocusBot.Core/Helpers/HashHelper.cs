using System.Security.Cryptography;
using System.Text;

namespace FocusBot.Core.Helpers;

/// <summary>
/// Computes content-based hashes for alignment cache keys.
/// </summary>
public static class HashHelper
{
    private const int MaxWindowTitleLength = 200;

    public static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    public static string NormalizeWindowTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;
        return title.Length <= MaxWindowTitleLength
            ? title
            : title[..MaxWindowTitleLength];
    }

    public static string ComputeWindowContextHash(string processName, string windowTitle)
    {
        var normalized = NormalizeWindowTitle(windowTitle);
        return ComputeHash($"{processName}|{normalized}");
    }

    public static string ComputeTaskContentHash(string description, string? context)
    {
        return ComputeHash($"{description}|{context ?? string.Empty}");
    }
}

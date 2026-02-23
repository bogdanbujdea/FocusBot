using FocusBot.Core.Interfaces;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Provides a managed API key embedded in the application.
/// </summary>
/// <remarks>
/// WARNING: This is the MVP implementation. The key can be extracted by
/// determined attackers. For production at scale, migrate to
/// ServerProxyManagedKeyProvider.
///
/// Obfuscation techniques used:
/// - Key split into multiple parts
/// - Parts XOR'd with known values
/// - Assembly-level obfuscation recommended
///
/// Replace the empty arrays below with output from KeyObfuscationHelper.GenerateObfuscatedKey(yourKey).
/// Do not commit the actual key; run the helper once locally and paste the byte arrays here.
/// </remarks>
public class EmbeddedManagedKeyProvider : IManagedKeyProvider
{
    public string ProviderId => "OpenAi";
    public string ModelId => "gpt-4o-mini";

    public Task<string?> GetApiKeyAsync()
    {
        var key = ReconstructKey();
        return Task.FromResult<string?>(string.IsNullOrWhiteSpace(key) ? null : key);
    }

    private static string ReconstructKey()
    {
        var part1 = Deobfuscate(GetPart1Bytes(), GetXorKey1());
        var part2 = Deobfuscate(GetPart2Bytes(), GetXorKey2());
        var part3 = Deobfuscate(GetPart3Bytes(), GetXorKey3());
        return string.Concat(part1, part2, part3);
    }

    private static byte[] GetPart1Bytes()
    {
        return
        [
            0x29,
            0x57,
            0x53,
            0x6B,
            0x28,
            0x53,
            0x14,
            0x36,
            0x2A,
            0x5A,
            0x13,
            0x5A,
            0x39,
            0x5B,
            0x47,
            0x7C,
            0x2C,
            0x6F,
            0x24,
            0x63,
            0x33,
            0x4A,
            0x29,
            0x7D,
            0x22,
            0x44,
            0x49,
            0x72,
            0x08,
            0x53,
            0x4E,
            0x56,
            0x13,
            0x5B,
            0x53,
            0x5C,
            0x03,
            0x5B,
            0x04,
            0x29,
            0x29,
            0x6E,
            0x27,
            0x5C,
            0x19,
            0x0A,
            0x0C,
            0x4F,
            0x3C,
            0x4A,
            0x12,
            0x49,
            0x02,
            0x49,
        ];
    }

    private static byte[] GetPart2Bytes()
    {
        return
        [
            0x58,
            0xD0,
            0x07,
            0x3A,
            0x1B,
            0xDE,
            0x08,
            0x24,
            0x59,
            0xE1,
            0x02,
            0x2F,
            0x55,
            0xF8,
            0x38,
            0x15,
            0x6A,
            0xC0,
            0x24,
            0x28,
            0x67,
            0xD5,
            0x79,
            0x2F,
            0x72,
            0xD0,
            0x1C,
            0x14,
            0x79,
            0xBC,
            0x08,
            0x00,
            0x4F,
            0xE4,
            0x0C,
            0x26,
            0x7A,
            0xBB,
            0x39,
            0x07,
            0x45,
            0xDB,
            0x10,
            0x21,
            0x15,
            0xB6,
            0x19,
            0x2E,
            0x6A,
            0xED,
            0x3A,
            0x1A,
            0x62,
            0xE8,
        ];
    }

    private static byte[] GetPart3Bytes()
    {
        return
        [
            0xD4,
            0x71,
            0x68,
            0x58,
            0xA9,
            0x70,
            0x33,
            0x4A,
            0xF9,
            0x2D,
            0x21,
            0x6C,
            0xE6,
            0x6D,
            0x04,
            0x0D,
            0xAB,
            0x5E,
            0x23,
            0x5B,
            0xE6,
            0x5B,
            0x2E,
            0x54,
            0xDD,
            0x4D,
            0x37,
            0x68,
            0xD3,
            0x7C,
            0x19,
            0x48,
            0xAD,
            0x7F,
            0x11,
            0x58,
            0xF0,
            0x54,
            0x16,
            0x5F,
            0xAF,
            0x6F,
            0x6C,
            0x62,
            0xD3,
            0x6E,
            0x6D,
            0x08,
            0xFF,
            0x78,
            0x0D,
            0x7B,
            0xE4,
            0x4E,
            0x0E,
            0x7C,
        ];
    }

    private static string Deobfuscate(byte[] data, byte[] key)
    {
        if (data.Length == 0)
            return string.Empty;
        var result = new byte[data.Length];
        for (var i = 0; i < data.Length; i++)
            result[i] = (byte)(data[i] ^ key[i % key.Length]);
        return System.Text.Encoding.UTF8.GetString(result);
    }

    private static byte[] GetXorKey1() => [0x5A, 0x3C, 0x7E, 0x1B];

    private static byte[] GetXorKey2() => [0x2D, 0x8F, 0x4A, 0x6C];

    private static byte[] GetXorKey3() => [0x9E, 0x1A, 0x5B, 0x3D];
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace FocusBot.WebAPI.IntegrationTests;

/// <summary>
/// Generates ES256-signed test JWTs for integration tests.
/// Creates a one-time EC P-256 key pair per test run — the private key signs
/// test tokens and the public key is wired into the test JWT Bearer configuration.
/// </summary>
internal static class TestJwtHelper
{
    private const string KeyId = "test-signing-key-001";

    /// <summary>
    /// A lazily-created EC P-256 key that persists for the lifetime of the test process.
    /// </summary>
    private static readonly ECDsa SigningKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>
    /// The <see cref="ECDsaSecurityKey"/> used by <see cref="CustomWebApplicationFactory"/>
    /// to validate test JWTs (public key only — but we reuse the same ECDsa instance;
    /// the JWT bearer middleware only calls Verify, not Sign).
    /// </summary>
    public static ECDsaSecurityKey PublicSecurityKey { get; } = new(GetPublicOnlyKey()) { KeyId = KeyId };

    /// <summary>
    /// Generates a JWT signed with ES256 for integration test authentication.
    /// </summary>
    /// <param name="userId">The user ID to set as the <c>sub</c> claim.</param>
    /// <param name="email">The email to set as the <c>email</c> claim.</param>
    /// <returns>A serialized JWT string.</returns>
    public static string GenerateTestJwt(Guid userId, string email = "test@example.com")
    {
        var securityKey = new ECDsaSecurityKey(SigningKey) { KeyId = KeyId };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("email", email)
        };

        var token = new JwtSecurityToken(
            issuer: $"{CustomWebApplicationFactory.TestSupabaseUrl}/auth/v1",
            audience: "authenticated",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Exports the public parameters from <see cref="SigningKey"/> into a new
    /// ECDsa instance that can only verify (not sign).
    /// </summary>
    private static ECDsa GetPublicOnlyKey()
    {
        var publicParams = SigningKey.ExportParameters(includePrivateParameters: false);
        var publicKey = ECDsa.Create(publicParams);
        return publicKey;
    }
}

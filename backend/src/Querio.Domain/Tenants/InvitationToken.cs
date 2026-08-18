using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Querio.Domain.Tenants;

/// <summary>
/// Invitation links are bearer-ish credentials, so the token is treated like a password:
/// generated with a cryptographic RNG, shown to the inviter exactly once, and stored only as
/// a hash. A leaked database dump yields no working invitations.
/// </summary>
public static class InvitationToken
{
    /// <summary>
    /// 256 bits. Guessing is not a threat model we want to reason about, and the token is
    /// only ever copied and pasted, so length costs nothing.
    /// </summary>
    private const int TokenBytes = 32;

    public static string Create() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

    public static byte[] Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }
}

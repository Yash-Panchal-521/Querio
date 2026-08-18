using System.ComponentModel.DataAnnotations;

namespace Querio.Api.Common.Authentication;

public sealed class FirebaseAuthenticationOptions
{
    public const string SectionName = "Authentication:Firebase";

    /// <summary>
    /// The Firebase project id. This is not cosmetic configuration — it is the entire
    /// security boundary. See <see cref="Issuer"/>.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Firebase signs every project's tokens with the same Google keys, published at a single
    /// shared JWKS endpoint. A token minted in anyone's free Firebase project therefore
    /// carries a perfectly valid signature against ours.
    ///
    /// The issuer and audience claims are the only thing separating our users from theirs, so
    /// validating them is not hardening — omitting it is a complete authentication bypass.
    /// </summary>
    public string Issuer => $"https://securetoken.google.com/{ProjectId}";

    /// <summary>Firebase sets <c>aud</c> to the bare project id.</summary>
    public string Audience => ProjectId;
}

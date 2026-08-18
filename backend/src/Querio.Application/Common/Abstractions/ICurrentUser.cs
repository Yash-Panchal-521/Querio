namespace Querio.Application.Common.Abstractions;

/// <summary>
/// The caller as their verified token describes them.
///
/// This is authentication only. It answers "who is holding the browser", never "what may
/// they do" — authorization is resolved against membership rows, because token claims cap at
/// roughly a kilobyte and go stale for up to an hour after a change.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Subject claim. The identity key — distinct per sign-in method, not per person.</summary>
    string? FirebaseUid { get; }

    string? Email { get; }

    bool EmailVerified { get; }

    string? DisplayName { get; }
}

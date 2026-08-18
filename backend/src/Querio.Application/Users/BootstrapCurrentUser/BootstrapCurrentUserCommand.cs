using Mediator;

namespace Querio.Application.Users.BootstrapCurrentUser;

/// <summary>
/// Creates or refreshes the caller's profile from their verified token.
///
/// Carries no payload deliberately: every field comes from the token, so a client cannot
/// claim an email address it has not proven it controls.
/// </summary>
public sealed record BootstrapCurrentUserCommand : ICommand<UserProfile>;

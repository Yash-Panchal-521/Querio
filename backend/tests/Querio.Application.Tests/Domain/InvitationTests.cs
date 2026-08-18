using Querio.Domain.Common.Errors;
using Querio.Domain.Tenants;

namespace Querio.Application.Tests.Domain;

public sealed class InvitationTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid InviterId = Guid.CreateVersion7();
    private static readonly Guid InviteeId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_raw_token_is_never_stored()
    {
        var (invitation, token) = Issue();

        // Only the hash is persisted, so a database dump yields no working invitations.
        invitation.TokenHash.ShouldBe(InvitationToken.Hash(token));
        invitation.TokenHash.ShouldNotBe(System.Text.Encoding.UTF8.GetBytes(token));
    }

    [Fact]
    public void Every_token_is_distinct()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => Issue().Token).ToArray();

        tokens.Distinct(StringComparer.Ordinal).Count().ShouldBe(tokens.Length);
    }

    [Fact]
    public void Invitations_last_seven_days()
    {
        var (invitation, _) = Issue();

        invitation.ExpiresAt.ShouldBe(Now.AddDays(7));
        invitation.IsPending(Now.AddDays(6)).ShouldBeTrue();
        invitation.IsPending(Now.AddDays(8)).ShouldBeFalse();
    }

    [Fact]
    public void The_invited_address_is_stored_normalised()
    {
        var (invitation, _) = Issue(email: "  Invitee@EXAMPLE.com ");

        invitation.Email.ShouldBe("invitee@example.com");
    }

    [Fact]
    public void Acceptance_ignores_casing_and_whitespace_in_the_address()
    {
        var (invitation, _) = Issue(email: "invitee@example.com");

        Should.NotThrow(() => invitation.Accept(InviteeId, " Invitee@Example.COM ", Now));

        invitation.AcceptedByUserId.ShouldBe(InviteeId);
    }

    [Fact]
    public void A_different_address_cannot_redeem_the_link()
    {
        var (invitation, _) = Issue(email: "invitee@example.com");

        // This is what makes a forwarded link useless to anyone else.
        Should.Throw<ForbiddenException>(() =>
            invitation.Accept(InviteeId, "someone.else@example.com", Now));

        invitation.AcceptedAt.ShouldBeNull();
    }

    [Fact]
    public void An_expired_link_reports_expiry_specifically()
    {
        var (invitation, _) = Issue();

        Should.Throw<ConflictException>(() =>
                invitation.Accept(InviteeId, "invitee@example.com", Now.AddDays(8)))
            .ErrorCode.ShouldBe("invitation.expired");
    }

    [Fact]
    public void A_link_cannot_be_used_twice()
    {
        var (invitation, _) = Issue();

        invitation.Accept(InviteeId, "invitee@example.com", Now);

        Should.Throw<ConflictException>(() =>
                invitation.Accept(Guid.CreateVersion7(), "invitee@example.com", Now))
            .ErrorCode.ShouldBe("invitation.already_accepted");
    }

    [Fact]
    public void A_revoked_link_stops_working_immediately()
    {
        var (invitation, _) = Issue();

        invitation.Revoke(Now);

        // Not at expiry — the point of revoking a link shared in error is that it stops now.
        Should.Throw<ConflictException>(() =>
                invitation.Accept(InviteeId, "invitee@example.com", Now))
            .ErrorCode.ShouldBe("invitation.revoked");
    }

    [Fact]
    public void Revoking_twice_is_not_an_error()
    {
        var (invitation, _) = Issue();

        invitation.Revoke(Now);

        Should.NotThrow(() => invitation.Revoke(Now.AddMinutes(5)));

        // The first revocation time is kept, since that is when access actually ended.
        invitation.RevokedAt.ShouldBe(Now);
    }

    [Fact]
    public void An_accepted_invitation_cannot_be_revoked()
    {
        var (invitation, _) = Issue();

        invitation.Accept(InviteeId, "invitee@example.com", Now);

        Should.Throw<ConflictException>(() => invitation.Revoke(Now))
            .ErrorCode.ShouldBe("invitation.already_accepted");
    }

    [Fact]
    public void Revocation_is_checked_before_expiry_so_the_message_is_the_useful_one()
    {
        var (invitation, _) = Issue();
        invitation.Revoke(Now);

        // Both are true at this point; "revoked" is what the person needs to hear.
        Should.Throw<ConflictException>(() =>
                invitation.Accept(InviteeId, "invitee@example.com", Now.AddDays(9)))
            .ErrorCode.ShouldBe("invitation.revoked");
    }

    private static (Invitation Invitation, string Token) Issue(string email = "invitee@example.com") =>
        Invitation.Issue(TenantId, email, TenantRole.Member, InviterId, Now);
}

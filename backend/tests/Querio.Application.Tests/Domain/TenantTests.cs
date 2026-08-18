using Querio.Domain.Common.Errors;
using Querio.Domain.Tenants;

namespace Querio.Application.Tests.Domain;

public sealed class TenantTests
{
    private static readonly Guid Founder = Guid.CreateVersion7();
    private static readonly Guid Colleague = Guid.CreateVersion7();

    [Fact]
    public void A_new_organization_always_has_an_owner()
    {
        var tenant = Tenant.Create("Ada Corp", "ada-corp", Founder);

        // An organization persisted without an owner would be unadministrable and
        // unrecoverable, so creation and first membership are one operation.
        tenant.OwnerCount.ShouldBe(1);
        tenant.MembershipFor(Founder)!.Role.ShouldBe(TenantRole.Owner);
    }

    [Fact]
    public void The_last_owner_cannot_be_demoted()
    {
        var tenant = Tenant.Create("Ada Corp", "ada-corp", Founder);

        var exception = Should.Throw<ConflictException>(() =>
            tenant.ChangeRole(Founder, TenantRole.Member));

        exception.ErrorCode.ShouldBe("tenant.last_owner");
        tenant.MembershipFor(Founder)!.Role.ShouldBe(TenantRole.Owner);
    }

    [Fact]
    public void The_last_owner_cannot_be_removed()
    {
        var tenant = Tenant.Create("Ada Corp", "ada-corp", Founder);

        Should.Throw<ConflictException>(() => tenant.RemoveMember(Founder))
            .ErrorCode.ShouldBe("tenant.last_owner");
    }

    [Fact]
    public void An_owner_may_step_down_once_another_owner_exists()
    {
        var tenant = Tenant.Create("Ada Corp", "ada-corp", Founder);
        tenant.AddMember(Colleague, TenantRole.Owner);

        Should.NotThrow(() => tenant.ChangeRole(Founder, TenantRole.Member));

        tenant.OwnerCount.ShouldBe(1);
        tenant.MembershipFor(Colleague)!.Role.ShouldBe(TenantRole.Owner);
    }

    [Fact]
    public void Removing_a_non_owner_never_trips_the_last_owner_guard()
    {
        var tenant = Tenant.Create("Ada Corp", "ada-corp", Founder);
        tenant.AddMember(Colleague, TenantRole.Member);

        Should.NotThrow(() => tenant.RemoveMember(Colleague));

        tenant.Memberships.Count.ShouldBe(1);
    }

    [Fact]
    public void The_same_person_cannot_hold_two_memberships()
    {
        var tenant = Tenant.Create("Ada Corp", "ada-corp", Founder);
        tenant.AddMember(Colleague, TenantRole.Member);

        Should.Throw<ConflictException>(() => tenant.AddMember(Colleague, TenantRole.Admin))
            .ErrorCode.ShouldBe("membership.already_exists");
    }

    [Fact]
    public void Renaming_leaves_the_slug_alone()
    {
        var tenant = Tenant.Create("Ada Corp", "ada-corp", Founder);

        tenant.Rename("Lovelace Industries");

        // The slug is in URLs people have already shared.
        tenant.Name.ShouldBe("Lovelace Industries");
        tenant.Slug.ShouldBe("ada-corp");
    }

    [Fact]
    public void Changing_a_role_to_the_one_already_held_is_a_no_op()
    {
        var tenant = Tenant.Create("Ada Corp", "ada-corp", Founder);

        // Would otherwise trip the last-owner guard for a change that alters nothing.
        Should.NotThrow(() => tenant.ChangeRole(Founder, TenantRole.Owner));
    }

    [Fact]
    public void Acting_on_someone_who_is_not_a_member_reports_not_found()
    {
        var tenant = Tenant.Create("Ada Corp", "ada-corp", Founder);

        Should.Throw<NotFoundException>(() => tenant.RemoveMember(Colleague));
        Should.Throw<NotFoundException>(() => tenant.ChangeRole(Colleague, TenantRole.Admin));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Querio.Api.Tests.Api;
using Querio.Domain.Users;
using Querio.Infrastructure.Persistence;

namespace Querio.Api.Tests.Persistence;

[Collection(nameof(QuerioApiCollection))]
public sealed class UserPersistenceTests(QuerioApiFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Provisioned_user_round_trips_with_a_normalised_email()
    {
        var user = User.Provision("firebase-uid-1", "  Ada.Lovelace@Example.COM ", emailVerified: true, "Ada Lovelace");

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

            var stored = await dbContext.Users.SingleAsync(
                candidate => candidate.FirebaseUid == "firebase-uid-1",
                TestContext.Current.CancellationToken);

            // Normalisation matters: lookups by email compare ordinally, with no
            // case-insensitive collation to fall back on.
            stored.Email.ShouldBe("ada.lovelace@example.com");
            stored.DisplayName.ShouldBe("Ada Lovelace");
            stored.EmailVerified.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Two_accounts_may_share_an_email_address()
    {
        // This is the account-linking limitation made executable: Firebase issues a distinct
        // uid per sign-in method, so the same person using Google and a password is two rows.
        // If someone "tidies up" by making email unique, this test fails and explains why.
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        dbContext.Users.Add(User.Provision("google-uid", "same@example.com", true, "Grace"));
        dbContext.Users.Add(User.Provision("password-uid", "same@example.com", false, "Grace"));

        await Should.NotThrowAsync(async () =>
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));

        var count = await dbContext.Users.CountAsync(
            user => user.Email == "same@example.com",
            TestContext.Current.CancellationToken);

        count.ShouldBe(2);
    }

    [Fact]
    public async Task The_same_firebase_uid_cannot_be_provisioned_twice()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        dbContext.Users.Add(User.Provision("duplicate-uid", "first@example.com", true, null));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        dbContext.Users.Add(User.Provision("duplicate-uid", "second@example.com", true, null));

        // The unique index is the real guard against a race between two concurrent bootstraps.
        await Should.ThrowAsync<DbUpdateException>(async () =>
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Audit_timestamps_are_stamped_on_insert_and_only_updated_on_change()
    {
        Guid userId;
        DateTimeOffset createdAt;
        DateTimeOffset firstUpdatedAt;

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();
            var user = User.Provision("audit-uid", "audit@example.com", false, null);

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            userId = user.Id;
            createdAt = user.CreatedAt;
            firstUpdatedAt = user.UpdatedAt;
        }

        // Nobody set these by hand; the interceptor did.
        createdAt.ShouldNotBe(default);
        firstUpdatedAt.ShouldBe(createdAt);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();
            var user = await dbContext.Users.SingleAsync(
                candidate => candidate.Id == userId,
                TestContext.Current.CancellationToken);

            user.RefreshProfile("audit@example.com", emailVerified: true, "Verified Now");
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            user.CreatedAt.ShouldBe(createdAt);
            user.UpdatedAt.ShouldBeGreaterThanOrEqualTo(firstUpdatedAt);
        }
    }

    [Fact]
    public async Task Identifiers_are_time_ordered_so_inserts_stay_at_the_index_edge()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        var first = User.Provision("ordered-1", "one@example.com", true, null);
        dbContext.Users.Add(first);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var second = User.Provision("ordered-2", "two@example.com", true, null);
        dbContext.Users.Add(second);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // UUID v7 embeds a timestamp in its leading bits, which is the whole reason for
        // choosing it over v4 — random ids would scatter across the primary-key B-tree.
        string.CompareOrdinal(second.Id.ToString(), first.Id.ToString()).ShouldBeGreaterThan(0);
    }
}

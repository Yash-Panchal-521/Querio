using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Querio.Domain.Common;

namespace Querio.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps created/updated timestamps at save time.
///
/// Doing it here rather than in handlers means no use case can forget, and every row written
/// in one SaveChanges shares an identical timestamp — which matters the first time you try to
/// reconstruct an ordering from the data.
/// </summary>
internal sealed class AuditableEntityInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = ToPostgresPrecision(timeProvider.GetUtcNow());

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            // A modified owned/child entity counts as modifying its parent.
            if (entry.State is EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State is EntityState.Modified || entry.HasChangedOwnedEntities())
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    /// <summary>
    /// Postgres <c>timestamptz</c> stores microseconds; .NET keeps 100-nanosecond ticks.
    /// Without truncating before the write, an entity held in memory and the same row read
    /// back compare unequal — which quietly breaks any "has this changed since" comparison
    /// and shows up in tests as flakiness rather than as the precision mismatch it is.
    /// </summary>
    private static DateTimeOffset ToPostgresPrecision(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerMicrosecond), value.Offset);
}

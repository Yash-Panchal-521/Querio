using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Querio.Infrastructure.Persistence.Interceptors;

internal static class EntityEntryExtensions
{
    /// <summary>
    /// An owned entity changing leaves its parent in <c>Unchanged</c>, so without this an
    /// edit confined to an owned value would never refresh the parent's UpdatedAt.
    /// </summary>
    public static bool HasChangedOwnedEntities(this EntityEntry entry) =>
        entry.References.Any(reference =>
            reference.TargetEntry is { } target
            && target.Metadata.IsOwned()
            && target.State is EntityState.Added or EntityState.Modified);
}

namespace Querio.Domain.Common;

/// <summary>
/// Base for every persisted aggregate.
///
/// Ids are UUID v7 rather than v4: they sort by creation time, so inserts land at the right
/// edge of the primary-key index instead of scattering across it. On a table that only ever
/// grows — documents, chunks, messages — that is the difference between a healthy B-tree and
/// a fragmented one.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    protected Entity()
        : this(Guid.CreateVersion7())
    {
    }

    protected Entity(Guid id) => Id = id;

    public Guid Id { get; private set; }

    public bool Equals(Entity? other) =>
        other is not null
        && other.GetType() == GetType()
        && other.Id != Guid.Empty
        && Id != Guid.Empty
        && other.Id == Id;

    public override bool Equals(object? obj) => obj is Entity entity && Equals(entity);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}

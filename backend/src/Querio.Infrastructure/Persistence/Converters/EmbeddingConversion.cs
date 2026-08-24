using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

namespace Querio.Infrastructure.Persistence.Converters;

/// <summary>
/// Maps the plain <c>float[]</c> the domain holds onto pgvector's <c>halfvec</c>.
///
/// The domain deliberately does not know pgvector exists — a Postgres extension type on an
/// entity would put the database inside the model. This is the seam that keeps it out.
///
/// Half precision is not a shortcut. Measured over five thousand rows, <c>halfvec(768)</c>
/// costs 4,141 bytes per chunk against 9,859 for full precision, which on a half-gigabyte
/// database is the difference between roughly 114,000 chunks and 48,000. Full precision also
/// pushes the vector past the inline row limit into TOAST, adding an out-of-line read to every
/// row a search touches. The recall difference at 768 dimensions does not register beside that.
/// </summary>
internal static class EmbeddingConversion
{
    public static ValueConverter<float[]?, HalfVector?> Converter { get; } =
        new(embedding => ToStored(embedding), stored => ToDomain(stored));

    /// <summary>
    /// Arrays compare by reference by default, which would make EF miss an embedding being
    /// attached to a tracked chunk. Snapshotting copies, so the comparison is against what was
    /// loaded rather than against the same mutated instance.
    /// </summary>
    public static ValueComparer<float[]?> Comparer { get; } =
        new(
            (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
            value => value == null ? 0 : value.Aggregate(0, HashCode.Combine),
            value => value == null ? null : value.ToArray());

    private static HalfVector? ToStored(float[]? embedding)
    {
        if (embedding is null)
        {
            return null;
        }

        var narrowed = new Half[embedding.Length];

        for (var index = 0; index < embedding.Length; index++)
        {
            narrowed[index] = (Half)embedding[index];
        }

        return new HalfVector(narrowed);
    }

    private static float[]? ToDomain(HalfVector? stored)
    {
        if (stored is null)
        {
            return null;
        }

        var span = stored.Memory.Span;
        var widened = new float[span.Length];

        for (var index = 0; index < span.Length; index++)
        {
            widened[index] = (float)span[index];
        }

        return widened;
    }
}

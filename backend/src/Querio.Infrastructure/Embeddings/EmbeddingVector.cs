using Querio.Domain.Documents;

namespace Querio.Infrastructure.Embeddings;

/// <summary>
/// The two things every provider's output must satisfy before it is allowed near the column.
///
/// Provider-independent on purpose. Both rules were learned from one provider and neither is
/// specific to it: a model capable of the right dimensionality is not the same as a model that
/// returned it, and a model that documents normalisation at one size may not normalise at
/// another. Trusting either would produce a database that stores perfectly and retrieves badly,
/// which no test downstream would report.
/// </summary>
internal static class EmbeddingVector
{
    /// <summary>
    /// Checks the dimensionality and scales the vector to unit length.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The provider returned the wrong number of values, or a vector with no direction.
    /// </exception>
    public static float[] Normalise(IReadOnlyList<float> values, string modelIdentity)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count != DocumentChunk.EmbeddingDimensions)
        {
            // Caught here rather than at the database, which would fail on a fixed-width column
            // with an error naming neither the provider nor the setting that caused it.
            throw new InvalidOperationException(
                $"{modelIdentity} returned {values.Count} dimensions; {DocumentChunk.EmbeddingDimensions} are required.");
        }

        double sumOfSquares = 0;

        for (var index = 0; index < values.Count; index++)
        {
            sumOfSquares += (double)values[index] * values[index];
        }

        var magnitude = Math.Sqrt(sumOfSquares);

        if (magnitude == 0)
        {
            throw new InvalidOperationException($"{modelIdentity} returned a zero vector, which cannot be normalised.");
        }

        var normalised = new float[values.Count];

        for (var index = 0; index < values.Count; index++)
        {
            normalised[index] = (float)(values[index] / magnitude);
        }

        return normalised;
    }
}

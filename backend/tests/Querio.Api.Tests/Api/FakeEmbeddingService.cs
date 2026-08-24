using System.Security.Cryptography;
using System.Text;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Documents;

namespace Querio.Api.Tests.Api;

/// <summary>
/// Deterministic vectors, no network, no allowance spent.
///
/// Derived from a hash of the text rather than random, so the same passage always embeds the
/// same way and a test can assert on retrieval order later without depending on a live model.
/// The real client is covered by its own tests, including live ones when a key is configured.
/// </summary>
public sealed class FakeEmbeddingService : IEmbeddingService
{
    /// <summary>Set by a test to simulate the provider refusing on an allowance.</summary>
    public EmbeddingQuotaException? NextFailure { get; set; }

    /// <summary>
    /// How many calls to serve before <see cref="NextFailure"/> is thrown. Zero throws on the
    /// next call; a higher number lets a document get part-way through first, which is the
    /// case that matters — a refusal on the very first batch leaves nothing to resume from.
    /// </summary>
    public int FailAfterCalls { get; set; }

    public int EmbedCallCount { get; private set; }

    public int MaxBatchSize => 8;

    /// <summary>Named so a test can tell these vectors from any real provider's.</summary>
    public string ModelIdentity => "fake-embedding@768";

    public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(
        IReadOnlyList<string> passages,
        CancellationToken cancellationToken)
    {
        EmbedCallCount++;

        if (NextFailure is { } failure)
        {
            if (FailAfterCalls > 0)
            {
                FailAfterCalls--;
            }
            else
            {
                NextFailure = null;

                throw failure;
            }
        }

        return Task.FromResult<IReadOnlyList<float[]>>([.. passages.Select(Vector)]);
    }

    public Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken) =>
        Task.FromResult(Vector(query));

    /// <summary>Unit length, like the real thing — the column and the index both assume it.</summary>
    private static float[] Vector(string text)
    {
        var seed = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var values = new float[DocumentChunk.EmbeddingDimensions];

        for (var index = 0; index < values.Length; index++)
        {
            values[index] = (seed[index % seed.Length] / 255f) - 0.5f;
        }

        var magnitude = (float)Math.Sqrt(values.Sum(value => (double)value * value));

        for (var index = 0; index < values.Length; index++)
        {
            values[index] /= magnitude;
        }

        return values;
    }
}

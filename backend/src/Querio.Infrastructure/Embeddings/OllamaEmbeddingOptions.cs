namespace Querio.Infrastructure.Embeddings;

/// <summary>
/// Where to find a locally-run Ollama, and which model to ask it for.
///
/// There is no allowance here, and that is the whole point of the provider: inference happens
/// on the developer's own machine, so there is nothing metered to pace, count or park. A
/// hundred-and-forty-page document can be ingested as often as a test needs it.
/// </summary>
public sealed class OllamaEmbeddingOptions
{
    public const string SectionName = "Embeddings:Ollama";

    public string BaseAddress { get; set; } = "http://localhost:11434/";

    /// <summary>
    /// Native 768 dimensions, so the column and its index are untouched, and an 8192-token
    /// context, so the chunk size chosen for a 2048-token ceiling has room to spare. Apache-2.0,
    /// and small enough (~274 MB) that CPU-only inference is unremarkable.
    /// </summary>
    public string Model { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Reported alongside every vector. Includes the dimensionality because the model can emit
    /// fewer — Matryoshka — and a vector's compatibility depends on both.
    /// </summary>
    public string ModelIdentity { get; set; } = "nomic-embed-text-v1.5@768";

    /// <summary>
    /// Larger than the hosted provider's, because the constraint is different: local inference
    /// is bounded by the machine rather than by an allowance, and a bigger batch simply keeps
    /// the model resident and the HTTP overhead down.
    /// </summary>
    public int BatchSize { get; set; } = 32;

    /// <summary>
    /// Prefix for text being stored. Not decoration: this model is trained with asymmetric
    /// instructions, and embedding a passage as though it were a question measurably costs
    /// recall — silently, since the vectors are still the right shape.
    /// </summary>
    public string DocumentPrefix { get; set; } = "search_document: ";

    /// <summary>Prefix for text doing the searching.</summary>
    public string QueryPrefix { get; set; } = "search_query: ";

    /// <summary>
    /// Generous, because a cold Ollama loads the model on the first request and a developer
    /// who has just started the stack should not see a timeout for it.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}

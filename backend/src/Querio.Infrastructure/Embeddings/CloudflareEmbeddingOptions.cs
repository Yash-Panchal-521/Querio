namespace Querio.Infrastructure.Embeddings;

/// <summary>
/// Workers AI, running an open-weights model on Cloudflare's compute.
///
/// The reason this exists rather than a self-hosted model: production runs on 0.1 CPU, which
/// cannot do a transformer forward pass at any useful speed, and there is no free second
/// container to move one into. This keeps the open-weights story without owning a server.
/// </summary>
public sealed class CloudflareEmbeddingOptions
{
    public const string SectionName = "Embeddings:Cloudflare";

    public string BaseAddress { get; set; } = "https://api.cloudflare.com/client/v4/";

    /// <summary>The account the Workers AI allowance belongs to. Not a secret.</summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>An API token with Workers AI read access. A secret.</summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Natively 768 dimensions, so the column and its index are untouched — the property that
    /// made this the candidate rather than a better-scoring model at 1024.
    /// </summary>
    public string Model { get; set; } = "@cf/baai/bge-base-en-v1.5";

    public string ModelIdentity { get; set; } = "bge-base-en-v1.5@768";

    /// <summary>
    /// How the token vectors become one sentence vector, and **not** a detail to leave at its
    /// default. The API defaults to mean pooling; this model family is trained with CLS. Mean
    /// pooling on a CLS-trained model returns vectors of exactly the right shape that simply
    /// retrieve worse — no error, no symptom, just quieter relevance.
    /// </summary>
    public string Pooling { get; set; } = "cls";

    /// <summary>
    /// Asymmetric, but only on one side: this family wants an instruction on the query and
    /// nothing on the passage. Documents are sent as they are.
    /// </summary>
    public string QueryPrefix { get; set; } = "Represent this sentence for searching relevant passages: ";

    /// <summary>
    /// The model's documented input ceiling. Enforced rather than trusted, because the provider
    /// truncates silently: a passage over the limit still returns a valid 768-dimensional vector,
    /// which then represents only the part of the text that fitted. That is a wrong answer no
    /// later check can see, so an oversized input is refused instead.
    /// </summary>
    public int MaxInputTokens { get; set; } = 512;

    public int BatchSize { get; set; } = 16;

    public int TimeoutSeconds { get; set; } = 60;
}

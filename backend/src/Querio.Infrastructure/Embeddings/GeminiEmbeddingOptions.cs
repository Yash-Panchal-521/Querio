namespace Querio.Infrastructure.Embeddings;

/// <summary>
/// How to reach Gemini, and how hard to lean on it.
/// </summary>
/// <summary>
/// Which embedding provider this process uses. Explicit, and never inferred from what happens
/// to be configured — a run that silently chose a different provider would write vectors into
/// an embedding space the rest of the column does not share.
/// </summary>
public enum EmbeddingProvider
{
    /// <summary>Hosted, metered, used in production.</summary>
    Gemini = 1,

    /// <summary>Local, unmetered, used in development.</summary>
    Ollama = 2,

    /// <summary>
    /// An open-weights model on Cloudflare's compute. Metered in tokens rather than per
    /// passage, which is what makes bulk ingestion affordable on a free allowance.
    /// </summary>
    Cloudflare = 3,
}

public sealed class GeminiEmbeddingOptions
{
    public const string SectionName = "Embeddings:Gemini";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseAddress { get; set; } = "https://generativelanguage.googleapis.com/";

    /// <summary>
    /// Generally available, unlike gemini-embedding-2 which is still preview. The trade is
    /// that this one requires callers to normalise vectors themselves below 3072 dimensions,
    /// where the newer model renormalises on its own.
    /// </summary>
    public string Model { get; set; } = "gemini-embedding-001";

    /// <summary>
    /// Inputs per request. The provider does not document a maximum, so this is deliberately
    /// modest — a value that is too high fails a whole batch, and the failure costs one of the
    /// day's requests to discover.
    ///
    /// Sixteen also keeps a single request comfortably inside a minute's token budget, so the
    /// limiter below can pace requests rather than having to refuse one outright.
    /// </summary>
    public int BatchSize { get; set; } = 16;

    /// <summary>
    /// Requests per minute, enforced on our side. The free tier allows around a hundred, and
    /// being refused costs the same allowance as succeeding — so it is cheaper to wait.
    /// </summary>
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// Tokens per minute, enforced on our side, and the limit that actually binds here.
    ///
    /// Requests per minute never does: a few hundred passages is a dozen or so requests, well
    /// under the allowance. Tokens are another matter — a batch of passages is thousands of
    /// them, so a handful of requests sent back to back exhausts a minute's tokens in seconds
    /// and everything after is refused. Counting requests cannot see that ceiling.
    ///
    /// The figure is not published for embedding models; it is measured. Refusal began just
    /// past thirty thousand tokens in a minute, so the default leaves headroom below that —
    /// our window and the provider's are not aligned, and a burst that straddles the boundary
    /// would otherwise still be refused.
    /// </summary>
    public int TokensPerMinute { get; set; } = 25_000;

    /// <summary>
    /// Passages per day, enforced on our side — and the ceiling that actually stopped this
    /// project. The provider meters `embed_content_free_tier_requests` at a thousand a day and
    /// counts each passage as one, so a hundred-and-forty-page document is a sixth of a day and
    /// a few uploads exhaust it.
    ///
    /// Enforced here so the queue parks itself before being refused. A refusal costs the same
    /// allowance as a success, so a worker that discovers the ceiling by asking spends the next
    /// day's allowance learning that yesterday's was gone.
    /// </summary>
    public int PassagesPerDay { get; set; } = 1_000;

    /// <summary>How long to keep trying a transient failure before giving the passage back.</summary>
    public int MaxAttempts { get; set; } = 4;
}

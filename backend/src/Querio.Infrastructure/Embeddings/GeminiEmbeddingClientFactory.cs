namespace Querio.Infrastructure.Embeddings;

/// <summary>
/// Configures the HTTP client for the embedding provider, in one place.
///
/// One place because the storage layer already taught this lesson: a test that configures its
/// own client is testing a configuration nothing ships, and the divergence is invisible until
/// the one code path that depends on it fails.
/// </summary>
public static class GeminiEmbeddingClientFactory
{
    public static void Configure(HttpClient client, GeminiEmbeddingOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        client.BaseAddress = new Uri(options.BaseAddress);

        // A header, never the query string. URLs reach access logs, traces and exception
        // messages; headers do not.
        client.DefaultRequestHeaders.Add("x-goog-api-key", options.ApiKey);

        // Embedding a full batch is slower than an ordinary request, but a hung connection
        // should not hold a worker for the default hundred seconds.
        client.Timeout = TimeSpan.FromSeconds(60);
    }
}

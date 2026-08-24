using System.Collections.Concurrent;
using Querio.Application.Common.Abstractions;

namespace Querio.Api.Tests.Api;

/// <summary>
/// Stands in for object storage in the endpoint tests.
///
/// A fake here rather than a container is deliberate, and only defensible because the real
/// implementation is covered against a live MinIO in <c>S3DocumentStorageTests</c>. These
/// tests are about what the endpoints do — status codes, isolation, permissions — and starting
/// a second container for each of them would buy nothing those tests do not already prove.
///
/// It keeps the bytes, so a test can still assert that deleting a document actually removed
/// its file rather than only its row.
/// </summary>
public sealed class InMemoryDocumentStorage : IDocumentStorage
{
    private readonly ConcurrentDictionary<string, byte[]> objects = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Keys => objects.Keys.ToArray();

    public bool Contains(string storageKey) => objects.ContainsKey(storageKey);

    public async Task<string> StoreAsync(
        Guid tenantId,
        string contentHash,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        // Same layout as the real implementation, so a test asserting on keys is asserting on
        // something true rather than on a shape only the fake has.
        var key = $"tenants/{tenantId:D}/documents/{contentHash}";

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        objects[key] = buffer.ToArray();

        return key;
    }

    public Task<Stream> OpenAsync(string storageKey, CancellationToken cancellationToken) =>
        objects.TryGetValue(storageKey, out var content)
            ? Task.FromResult<Stream>(new MemoryStream(content, writable: false))
            : throw new FileNotFoundException($"No object at '{storageKey}'.");

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        objects.TryRemove(storageKey, out _);

        return Task.CompletedTask;
    }

    public Task<Uri> CreateDownloadLinkAsync(
        string storageKey,
        string downloadFileName,
        TimeSpan lifetime,
        CancellationToken cancellationToken) =>
        Task.FromResult(new Uri($"https://storage.invalid/{storageKey}"));

    public void Clear() => objects.Clear();
}

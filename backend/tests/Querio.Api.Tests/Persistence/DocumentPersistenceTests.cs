using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Querio.Api.Tests.Api;
using Querio.Domain.Documents;
using Querio.Infrastructure.Persistence;

namespace Querio.Api.Tests.Persistence;

/// <summary>
/// The vector column is the one piece of this schema that an in-memory provider could never
/// check: half precision, a Postgres extension type, and a value converter between the domain
/// and the database. These run against real Postgres for exactly that reason.
/// </summary>
[Collection(nameof(QuerioApiCollection))]
public sealed class DocumentPersistenceTests(QuerioApiFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_embedding_round_trips_through_half_precision()
    {
        var tenantId = await OrganizationAsync("owner-embedding", "owner.embedding@example.com");

        var original = new float[DocumentChunk.EmbeddingDimensions];
        for (var index = 0; index < original.Length; index++)
        {
            // Values that exercise the narrowing rather than trivially survive it.
            original[index] = (float)Math.Sin(index) * 0.5f;
        }

        var documentId = await SeedDocumentAsync(tenantId, "handbook.pdf", "a".PadRight(64, 'a'));

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

            var chunk = DocumentChunk.Create(tenantId, documentId, 0, "Parental leave is 26 weeks.", "Handbook › Leave", 3, 0, 27, 7);
            chunk.AttachEmbedding(original, "test-embedding@768");

            dbContext.DocumentChunks.Add(chunk);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

            var stored = await dbContext.DocumentChunks
                .IgnoreQueryFilters()
                .SingleAsync(chunk => chunk.DocumentId == documentId, TestContext.Current.CancellationToken);

            stored.Embedding.ShouldNotBeNull();
            stored.Embedding.Length.ShouldBe(DocumentChunk.EmbeddingDimensions);
            stored.Breadcrumb.ShouldBe("Handbook › Leave");
            stored.PageNumber.ShouldBe(3);

            // Half precision carries about three decimal digits, so this asserts the values
            // survived the narrowing — not that they came back bit-identical, which they
            // cannot and need not.
            for (var index = 0; index < original.Length; index++)
            {
                stored.Embedding[index].ShouldBe(original[index], 0.001);
            }
        }
    }

    [Fact]
    public async Task Deleting_a_document_takes_its_chunks_with_it()
    {
        var tenantId = await OrganizationAsync("owner-cascade", "owner.cascade@example.com");
        var documentId = await SeedDocumentAsync(tenantId, "policies.md", "b".PadRight(64, 'b'));

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

            dbContext.DocumentChunks.Add(
                DocumentChunk.Create(tenantId, documentId, 0, "First passage.", null, null, 0, 14, 3));
            dbContext.DocumentChunks.Add(
                DocumentChunk.Create(tenantId, documentId, 1, "Second passage.", null, null, 14, 29, 3));

            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

            var document = await dbContext.Documents
                .IgnoreQueryFilters()
                .SingleAsync(candidate => candidate.Id == documentId, TestContext.Current.CancellationToken);

            dbContext.Documents.Remove(document);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

            // Orphaned chunks would keep consuming the storage budget the document was deleted
            // to reclaim, and would still be findable by search.
            var remaining = await dbContext.DocumentChunks
                .IgnoreQueryFilters()
                .CountAsync(chunk => chunk.DocumentId == documentId, TestContext.Current.CancellationToken);

            remaining.ShouldBe(0);
        }
    }

    [Fact]
    public async Task The_same_file_cannot_be_recorded_twice_in_one_organization()
    {
        var tenantId = await OrganizationAsync("owner-duplicate", "owner.duplicate@example.com");
        var hash = "c".PadRight(64, 'c');

        await SeedDocumentAsync(tenantId, "contract.pdf", hash);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        dbContext.Documents.Add(
            Document.Record(tenantId, Guid.CreateVersion7(), "contract-copy.pdf", FileFormat.Pdf, 2048, hash, $"tenants/{tenantId}/documents/{hash}"));

        // Checking in the handler is not enough on its own: two uploads landing together would
        // both pass the check and only the constraint can decide which one wins.
        await Should.ThrowAsync<DbUpdateException>(
            async () => await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private async Task<Guid> OrganizationAsync(string uid, string email)
    {
        var (_, tenantId) = await new TenantScenario(fixture).OrganizationAsync(uid, email, "Contoso");

        return tenantId;
    }

    private async Task<Guid> SeedDocumentAsync(Guid tenantId, string fileName, string contentHash)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        var document = Document.Record(
            tenantId,
            Guid.CreateVersion7(),
            fileName,
            FileFormat.Pdf,
            1024,
            contentHash,
            $"tenants/{tenantId}/documents/{contentHash}");

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return document.Id;
    }
}

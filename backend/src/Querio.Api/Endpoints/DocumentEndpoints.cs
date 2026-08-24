using Mediator;
using Microsoft.AspNetCore.Mvc;
using Querio.Api.Common.Authorization;
using Querio.Api.Common.Endpoints;
using Querio.Api.Common.RateLimiting;
using Querio.Application.Documents;
using Querio.Application.Documents.CreateDownloadLink;
using Querio.Application.Documents.DeleteDocument;
using Querio.Application.Documents.GetDocument;
using Querio.Application.Documents.ListDocumentChunks;
using Querio.Application.Documents.ListDocuments;
using Querio.Application.Documents.UploadDocument;
using Querio.Domain.Documents;

namespace Querio.Api.Endpoints;

internal sealed class DocumentEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup($"/api/v1/tenants/{{{TenantPolicies.TenantRouteKey}:guid}}/documents")
            .WithTags("Documents")
            .RequireAuthorization();

        group.MapPost("", async (
                Guid tenantId,
                IFormFile file,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                await using var content = file.OpenReadStream();

                var result = await mediator.Send(
                    new UploadDocumentCommand(
                        tenantId,
                        file.FileName,
                        // Advisory only. What the file actually is gets decided from its bytes.
                        file.ContentType,
                        content),
                    cancellationToken);

                // 200 rather than 201 when the organization already had this file: nothing was
                // created, and saying otherwise would have the interface announce a new upload
                // that is really the existing one.
                return result.AlreadyExisted
                    ? Results.Ok(result.Document)
                    : Results.Created(
                        $"/api/v1/tenants/{tenantId}/documents/{result.Document.Id}",
                        result.Document);
            })
            .WithName("UploadDocument")
            .WithSummary("Uploads a document and queues it for ingestion.")
            .RequireAuthorization(TenantPolicies.Member)
            .RequireRateLimiting(RateLimitPolicies.DocumentUpload)
            // Bearer tokens, not cookies, so there is no cross-site request to forge — and the
            // antiforgery filter would otherwise reject every multipart upload.
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(DocumentLimits.MaxFileBytes))
            .Produces<DocumentSummary>(StatusCodes.Status201Created)
            .Produces<DocumentSummary>(StatusCodes.Status200OK);

        group.MapGet("", async (
                Guid tenantId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new ListDocumentsQuery(tenantId), cancellationToken)))
            .WithName("ListDocuments")
            .WithSummary("Lists the organization's documents, newest first, with ingestion progress.")
            .RequireAuthorization(TenantPolicies.Member)
            .Produces<IReadOnlyList<DocumentSummary>>();

        group.MapGet("/{documentId:guid}", async (
                Guid tenantId,
                Guid documentId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new GetDocumentQuery(tenantId, documentId), cancellationToken)))
            .WithName("GetDocument")
            .WithSummary("Returns one document, including its ingestion progress.")
            .RequireAuthorization(TenantPolicies.Member)
            .Produces<DocumentSummary>();

        group.MapGet("/{documentId:guid}/chunks", async (
                Guid tenantId,
                Guid documentId,
                IMediator mediator,
                CancellationToken cancellationToken,
                int skip = 0,
                int take = 50) =>
                TypedResults.Ok(await mediator.Send(
                    new ListDocumentChunksQuery(tenantId, documentId, skip, take),
                    cancellationToken)))
            .WithName("ListDocumentChunks")
            .WithSummary("Returns the passages a document was split into, in order.")
            .RequireAuthorization(TenantPolicies.Member)
            .Produces<DocumentChunkPage>();

        group.MapPost("/{documentId:guid}/download-link", async (
                Guid tenantId,
                Guid documentId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(
                    new CreateDownloadLinkCommand(tenantId, documentId),
                    cancellationToken)))
            .WithName("CreateDocumentDownloadLink")
            .WithSummary("Returns a short-lived link to the original file. The bucket stays private.")
            .RequireAuthorization(TenantPolicies.Member)
            .Produces<DownloadLink>();

        group.MapDelete("/{documentId:guid}", async (
                Guid tenantId,
                Guid documentId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                await mediator.Send(new DeleteDocumentCommand(tenantId, documentId), cancellationToken);

                return TypedResults.NoContent();
            })
            .WithName("DeleteDocument")
            .WithSummary("Deletes a document, its chunks and its stored file. Uploader or administrator.")
            .RequireAuthorization(TenantPolicies.Member)
            .Produces(StatusCodes.Status204NoContent);
    }
}

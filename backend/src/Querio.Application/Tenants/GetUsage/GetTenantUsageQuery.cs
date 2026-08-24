using Mediator;

namespace Querio.Application.Tenants.GetUsage;

public sealed record GetTenantUsageQuery(Guid TenantId) : IQuery<TenantUsage>;

/// <summary>
/// What an organization has used and what it may use.
///
/// Both halves, deliberately. A limit somebody only meets by hitting it is indistinguishable
/// from a bug, and the free tiers this runs on are finite enough that people will reach them.
/// </summary>
public sealed record TenantUsage(
    int DocumentCount,
    int MaxDocuments,
    long StoredBytes,
    long MaxStoredBytes,
    int ChunkCount,
    int ReadyDocumentCount,
    int FailedDocumentCount);

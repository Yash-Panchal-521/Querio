using Querio.Domain.Tenants;

namespace Querio.Application.Tenants;

/// <summary>An organization as the caller sees it, including their own role in it.</summary>
public sealed record TenantSummary(
    Guid Id,
    string Name,
    string Slug,
    TenantRole Role,
    int MemberCount);

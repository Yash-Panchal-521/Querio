using Querio.Domain.Tenants;

namespace Querio.Application.Tenants.Members;

public sealed record MemberSummary(
    Guid UserId,
    string Email,
    string? DisplayName,
    TenantRole Role,
    DateTimeOffset JoinedAt);

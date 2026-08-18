using Querio.Application.Common.Abstractions;

namespace Querio.Infrastructure.Persistence;

/// <summary>
/// Used where there is no request and therefore no organization — design-time migrations and
/// the readiness probe. Reports no tenant, which with default-deny filters means tenant-owned
/// data is invisible rather than wide open.
/// </summary>
internal sealed class NoTenantContext : ITenantContext
{
    public static readonly NoTenantContext Instance = new();

    public Guid? TenantId => null;
}

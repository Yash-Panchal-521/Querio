using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Documents;

namespace Querio.Application.Tenants.GetUsage;

internal sealed class GetTenantUsageQueryHandler(IQuerioDbContext dbContext)
    : IQueryHandler<GetTenantUsageQuery, TenantUsage>
{
    public async ValueTask<TenantUsage> Handle(GetTenantUsageQuery query, CancellationToken cancellationToken)
    {
        // One round trip rather than four. The filters make this the organization's own rows,
        // so nothing here needs a WHERE on tenant.
        var totals = await dbContext.Documents
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Bytes = group.Sum(document => document.ByteSize),
                Ready = group.Count(document => document.Status == DocumentStatus.Ready),
                Failed = group.Count(document => document.Status == DocumentStatus.Failed),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var chunkCount = await dbContext.DocumentChunks
            .AsNoTracking()
            .CountAsync(cancellationToken);

        return new TenantUsage(
            totals?.Count ?? 0,
            DocumentLimits.MaxDocumentsPerTenant,
            totals?.Bytes ?? 0,
            DocumentLimits.MaxStoredBytesPerTenant,
            chunkCount,
            totals?.Ready ?? 0,
            totals?.Failed ?? 0);
    }
}

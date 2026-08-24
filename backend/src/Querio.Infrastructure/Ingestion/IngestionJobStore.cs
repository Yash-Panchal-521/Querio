using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Querio.Domain.Documents;
using Querio.Infrastructure.Persistence;

namespace Querio.Infrastructure.Ingestion;

/// <summary>
/// Hands one job to one worker.
///
/// The claim is a single statement rather than a read followed by a write, because anything
/// less is a race: two workers reading the same queued row would both believe they own it and
/// both spend the embedding allowance on the same document. <c>FOR UPDATE SKIP LOCKED</c> is
/// what makes concurrent claiming safe without a broker — each worker locks a different row
/// instead of queuing behind the same one.
/// </summary>
internal sealed class IngestionJobStore(QuerioDbContext dbContext)
{
    /// <summary>
    /// Claims the oldest job that is due, including one whose lease has expired.
    ///
    /// An expired lease is how a killed container's work returns to the queue. Nothing sweeps
    /// for it — the next worker to ask simply finds it eligible again.
    /// </summary>
    public async Task<IngestionJob?> ClaimAsync(
        string owner,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE ingestion_jobs
            SET state = {0},
                leased_by = {1},
                lease_expires_at = {2},
                attempt = attempt + 1,
                updated_at = {3}
            WHERE id = (
                SELECT id
                FROM ingestion_jobs
                WHERE (state = {4} AND available_at <= {3})
                   OR (state = {0} AND lease_expires_at < {3})
                ORDER BY available_at
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            RETURNING id
            """;

        var claimed = await dbContext.Database
            .SqlQueryRaw<Guid>(
                sql,
                (int)IngestionJobState.Leased,
                owner,
                now.Add(leaseDuration),
                now,
                (int)IngestionJobState.Queued)
            .ToListAsync(cancellationToken);

        if (claimed.Count == 0)
        {
            return null;
        }

        // Attempt was incremented by the statement above rather than by the domain method,
        // because the increment has to be part of the same atomic claim. Loading afterwards
        // gets the entity as it now stands.
        return await dbContext.IngestionJobs
            .FirstOrDefaultAsync(job => job.Id == claimed[0], cancellationToken);
    }

    /// <summary>Worker identity, for diagnosis rather than correctness.</summary>
    public static string OwnerName() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Environment.MachineName}/{Environment.ProcessId}");
}

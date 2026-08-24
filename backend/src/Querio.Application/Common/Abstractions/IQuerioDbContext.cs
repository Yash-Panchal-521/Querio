using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Querio.Domain.Documents;
using Querio.Domain.Tenants;
using Querio.Domain.Users;

namespace Querio.Application.Common.Abstractions;

/// <summary>
/// The database as handlers see it.
///
/// Deliberately exposes <see cref="DbSet{TEntity}"/> rather than hiding it behind
/// repositories: EF's DbContext is already a unit of work and DbSet already a repository, so
/// another layer on top would add indirection without adding isolation. What this interface
/// does buy is a seam for tests and a compile-time guarantee that handlers never reach the
/// concrete context — or the Npgsql provider behind it.
/// </summary>
public interface IQuerioDbContext
{
    DbSet<User> Users { get; }

    DbSet<Tenant> Tenants { get; }

    /// <summary>
    /// Not tenant-filtered, and must not be: this is the table that decides tenant access,
    /// and "which organizations do I belong to" is inherently a cross-tenant question.
    /// </summary>
    DbSet<Membership> Memberships { get; }

    /// <summary>
    /// Tenant-filtered by default. Redeeming an invitation happens before membership exists,
    /// so that path must opt out with IgnoreQueryFilters — deliberately conspicuous.
    /// </summary>
    DbSet<Invitation> Invitations { get; }

    /// <summary>Tenant-filtered. One uploaded file each.</summary>
    DbSet<Document> Documents { get; }

    /// <summary>
    /// Tenant-filtered. Passages of a document, with the vector that makes them findable.
    /// </summary>
    DbSet<DocumentChunk> DocumentChunks { get; }

    /// <summary>
    /// Not tenant-filtered, and must not be: the ingestion worker runs without a request and
    /// therefore without an organization, so a filtered queue would always be empty. The
    /// worker adopts the job's tenant before touching anything the filter covers.
    /// </summary>
    DbSet<IngestionJob> IngestionJobs { get; }

    /// <summary>
    /// Exposed so a handler can discard tracked state after a failed save and retry — the
    /// upsert races that unique indexes are there to catch.
    /// </summary>
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

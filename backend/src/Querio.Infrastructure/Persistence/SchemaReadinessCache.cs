namespace Querio.Infrastructure.Persistence;

/// <summary>
/// Remembers that this process has already proved its schema is current.
///
/// The readiness probe is polled continuously by the platform, and every poll used to open a
/// connection and read the migrations history. On a database that suspends when idle — Neon's
/// free tier does, after five minutes — that traffic alone is enough to keep it awake forever,
/// which spends a month of compute allowance in about seventeen days on a service nobody is
/// using.
///
/// Caching is deliberately one-directional. A success stays true because migrations are applied
/// as a deploy step before this process starts and cannot change underneath it; if they ever
/// did, the instance would be replaced rather than mutated. A failure is never cached, so a
/// database that was briefly unreachable at start-up can still recover without a restart.
/// </summary>
internal sealed class SchemaReadinessCache
{
    private volatile bool confirmed;

    public bool IsConfirmed => confirmed;

    public void Confirm() => confirmed = true;
}

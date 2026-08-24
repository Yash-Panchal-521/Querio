# 5. Run ingestion off a leased Postgres queue

**Status:** Accepted, 2026-08-19

## Context

Ingestion cannot happen during the upload request. Extracting, chunking and embedding a
document takes tens of seconds and depends on a third party, and a user watching a spinner for
that long will assume it has broken. So the work is queued.

The usual answer is a message broker. This project's constraint is that every service must have
a free tier, and a broker is one more thing to run, secure and keep alive.

## Decision

A table, claimed with `FOR UPDATE SKIP LOCKED`, held under a lease.

**The job is written in the same transaction as the document.** A document can therefore never
exist without work queued for it, and no second system has to be running for that to hold. A
broker cannot promise this without a distributed transaction or an outbox — which would be
another table, claimed the same way.

**Claiming is one statement, not a read then a write.** Anything less is a race: two workers
reading the same queued row would both believe they owned it and both spend the embedding
allowance producing identical vectors. `SKIP LOCKED` is what lets concurrent workers claim
*different* rows rather than queue behind the same one.

**A claim is a lease, not a lock.** It expires. A killed container's work returns to the queue
because the next worker to ask simply finds it eligible again — nothing sweeps, no operator
intervenes, and there is no lock to be stuck holding. Long documents renew mid-flight so slow
work is not mistaken for a dead worker.

**The queue carries cleanup too.** Deleting a document removes its row first and its stored
object second — deliberately, since the reverse would leave a document listed whose bytes are
gone. When the object delete fails, a `DeleteStoredObject` job is queued rather than a line
being written to a log. The alternative was a second queue with the same leasing, the same
backoff and the same crash-safety, built again.

## Failure is not one thing

| Outcome | Response | Why |
|---|---|---|
| Cannot read the file | Terminal. Document marked failed with a reason | It will not become readable on the fourth attempt, and each attempt costs allowance working documents need |
| Embedding allowance spent | **Paused**, attempt refunded, document shows `WaitingForQuota` | Nothing is wrong and nobody needs to act. Calling it failed invites a re-upload that spends the allowance again when it returns |
| Anything else | Retried with backoff, bounded, then terminal | Transient by assumption, but not forever |

Refunding the attempt on a quota pause matters more than it looks. An exhausted daily allowance
says nothing about whether a document can be ingested — spending retries on it would eventually
fail a perfectly good file for a reason that was never its fault.

## Tenancy

`IngestionJob` carries a `TenantId` but deliberately does not implement `IHasTenant` — the same
exception `Membership` makes. A worker has no request and therefore no organization, so a
filtered queue would always be empty and nothing would ever be ingested.

Isolation is not weakened. The worker claims a job, establishes that job's tenant through
`ITenantScope`, and only then touches documents or chunks — through exactly the same
default-deny filters a request goes through. `ITenantScope` is a separate interface from
`ITenantContext` so that reading the tenant stays universal while setting it has exactly two
callers: the authorization handler, after proving membership, and the worker, after claiming.

## Consequences

- **One job at a time.** The instance has half a gigabyte of memory and a quarter of a CPU, and
  the metered resource is counted in requests per day. Parallelism would spend the same
  allowance faster while making every document slower.
- **A sleeping instance runs no worker.** On a host that suspends when idle, ingestion resumes
  when the instance next wakes — in practice the moment someone opens the page that polls for
  status. Stated here rather than discovered later.
- **Tests drive the runner directly** rather than waiting on the background loop, which is why
  the per-job logic lives in `IngestionJobRunner` and not inside the `BackgroundService`. The
  properties worth proving — no double claim, an abandoned lease returning, a retry not
  duplicating — are exactly the ones a timing-dependent test reports unreliably.

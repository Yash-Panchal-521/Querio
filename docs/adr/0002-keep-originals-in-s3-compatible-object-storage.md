# 2. Keep uploaded originals in S3-compatible object storage

**Status:** Accepted, 2026-08-19

## Context

Uploaded files have to live somewhere. Postgres is the obvious place and the wrong one: Neon's
free plan caps the entire database at 0.5 GB, and that budget is already committed to chunk
text and embeddings — the derived data the product actually searches. A single 20 MB PDF would
cost four percent of it and buy nothing that ingestion has not already extracted.

Several providers offer around 10 GB free and speak the S3 API. Which one matters less than the
protocol, which is the point of this record.

## Decision

Originals go to an S3-compatible bucket through `AWSSDK.S3`. The implementation is named
`S3DocumentStorage` for the protocol, not for the vendor, because that is what it actually
depends on — and that naming has already earned itself.

**Backblaze B2 in production** (revised 2026-08-21). Cloudflare R2 was the original choice on
allowance and zero egress, and it remains a better product on both counts. It requires a payment
method with usage-based overage billing, and this project's constraint is that nothing can cost
anything — a card on file is an exposure a looping bug can reach, and this codebase produced
exactly such a loop the day before this was written. B2 gives 10 GB free permanently with no card
and the same S3 API, at the cost of egress being free only up to three times average monthly
storage. Downloads here are presigned GETs of documents somebody already uploaded, so that
ceiling is far away.

Switching cost one option. The SDK signs with a region and will not guess: R2 is not regional and
expects the literal `auto`, while B2 signs with its real region and refuses `auto`. That is now
`ObjectStorage:Region`, defaulting to `auto` so MinIO and R2 both keep working. Nothing else
changed, which is the whole return on having depended on the protocol rather than the vendor.

Keys are content-addressed and scoped to the organization:

```
tenants/{tenantId}/documents/{sha256}
```

Two properties fall out of that rather than needing code. Re-uploading identical bytes lands on
the same key instead of accumulating copies, so a retried upload is harmless. And everything
belonging to one organization sits under a single prefix, so per-tenant accounting and deletion
are a prefix operation.

Downloads are time-limited presigned GETs. The bucket is never public, and file bytes never
pass through the API.

The abstraction returns the key it chose rather than accepting one, so callers never compose
paths and the layout can change without touching a use case.

## Tests run against MinIO, not a mock

MinIO speaks the same API, so `Testcontainers.Minio` exercises the real client and the real
code path. Continuous integration therefore proves the storage logic without needing a hosted
credentials, and local development gets working uploads from `docker compose up -d` with no
storage account at all.

That choice paid for itself immediately. Two behaviours only a real server could have caught:

- The SDK builds presigned URLs as **HTTPS regardless** of the endpoint's scheme and regardless
  of `AmazonS3Config.UseHttp`. `GetPreSignedUrlRequest.Protocol` is what governs it. Storing and
  reading worked perfectly throughout — only a followed link failed.
- The first version of the test configured its own client instead of using the one production
  builds. That is how the above got in, so client construction now lives in a single factory
  that both use.

## Consequences

- Deleting a document has to delete from two systems, and they cannot be made atomic. The
  database row is the record of truth; a failed object delete is retried rather than ignored,
  and an orphaned object costs storage but never appears in the product.
- The bucket must exist before the first upload. Compose creates it locally; a hosted bucket
  needs it created once by hand.
- Five settings — service URL, region, access key, secret and bucket — are checked while the host
  is built, so a deployment missing them fails before the readiness probe passes rather than
  accepting a file and losing it.
- A wrong region fails at signature verification with an error about credentials rather than
  about a region, which is worth knowing before debugging it as a bad key.
- Presigned links are as strong as their lifetime. Ten minutes is long enough to click and short
  enough that a link pasted somewhere public has usually expired.

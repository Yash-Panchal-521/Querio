# 7. Run different embedding providers in development and production

**Status:** Accepted, 2026-08-21

## Context

Ingestion could not be tested. The hosted provider's free tier meters
`embed_content_free_tier_requests` at a thousand a day and counts **each passage** as one, so a
140-page document is a sixth of a day and a handful of uploads exhausts it. A manual test pass
that re-uploads the same files is exactly the workload that ceiling punishes, and it stopped one
mid-way through.

Paying is not available: every service in this project has to be free, permanently, and not on a
trial allocation.

Self-hosting in production is not available either. The API runs on a Render free web service —
**0.1 CPU and 512 MB**. Memory would almost accommodate a quantised model; a tenth of a core
would not. A transformer forward pass there is seconds per passage, competing with the request
that triggered it, and Render offers no free background worker or private service to move it
into.

## Decision

**Development embeds locally; production embeds through a hosted API.** The two environments run
different models on purpose.

Development uses `nomic-embed-text-v1.5` through Ollama, in Docker Compose beside Postgres and
MinIO. Nothing is metered, so a long document can be ingested as many times as a test needs.
It was chosen for the reason that costs nothing later: **768 dimensions natively**, so the
`halfvec(768)` column and its HNSW index are untouched — no migration, no re-index, no second
dimensionality to reason about. Apache-2.0, ~274 MB, and an 8192-token context that leaves the
existing chunk size room to spare.

Production stays on the hosted provider for now. The intended replacement is Cloudflare Workers
AI running `bge-base-en-v1.5` — also open weights, also natively 768 — but that is a separate
decision, because its 512-token input ceiling forces a smaller chunk target and therefore a
re-chunk and re-embed of everything.

## Provider identity is part of vector compatibility

The consequence that matters is not operational, it is a correctness one, and it is the reason
this is an ADR rather than a configuration note.

**Two models agreeing on dimensionality does not make their vectors comparable.** Cosine
distance between different embedding spaces is not a weaker signal, it is noise. Nothing errors.
The column accepts the values, the index builds, the query returns rows, and relevance is
quietly gone — a failure with no symptom except worse answers, which is indistinguishable from
the model simply being mediocre.

So `document_chunks.embedding_model` records which model produced each vector, as a stable
identifier including the dimensionality it was asked for — `nomic-embed-text-v1.5@768`. The
dimensionality belongs in the identifier because these models support Matryoshka truncation: the
name alone does not identify the space.

Retrieval filters on it:

```sql
WHERE embedding_model = @activeEmbeddingModel
ORDER BY embedding <=> @queryEmbedding
```

That turns silent semantic corruption into an explicit compatibility boundary — a document
embedded by another model becomes invisible to search rather than wrong in it.

## No automatic fallback

The provider is chosen by `Embeddings:Provider`, explicitly, and never inferred from which
credentials happen to be present. There is no failover between providers.

A fallback would be worse than an outage. Falling back mid-document writes half a document's
passages into one embedding space and half into another, under one document row, and the only
evidence would be `embedding_model` differing between chunks — which is precisely why it is
recorded per chunk rather than per document. When the configured provider cannot serve, the job
pauses and retries later, which is the behaviour the ingestion queue already has.

## Dimensionality is a storage invariant, not a provider promise

The dimension check and L2 normalisation moved out of the hosted provider's client into
`EmbeddingVector`, applied to every provider's output.

Both rules were learned from one provider and neither is specific to it. A model _capable_ of
768 dimensions is not a model that _returned_ 768. And a model documented as normalising at its
native size may not normalise at a reduced one — `gemini-embedding-001` only self-normalises at
3072, and an unnormalised vector stores perfectly well and retrieves badly. Enforcing both at
the boundary means a new provider cannot introduce either fault by being trusted.

## Consequences

A developer needs Docker and one extra container, and the first `docker compose up` pulls
274 MB. In exchange, ingestion in development has no ceiling at all.

Development and production databases hold vectors that cannot be compared with each other. This
is intended and now explicit rather than latent.

The hosted provider is no longer the primary path for bulk ingestion, but stays fully
implemented and is what production uses until the Cloudflare decision is made. Its daily
allowance is now metered on our side and the queue parks itself before being refused — see
[ADR 0006](0006-pace-embedding-by-tokens-and-resume-where-it-stopped.md).

On Cloudflare's allowance, one figure is worth labelling carefully in advance: **~3,300 passages
a day is a derived estimate, not a published quota.** It comes from arithmetic over two
published rates — a 10,000-neuron daily allowance, $0.011 per 1,000 neurons, and $0.067 per
million input tokens for that model. It sets the direction, roughly 3× the hosted provider's
practical ceiling and metered in tokens rather than per-passage requests, and it needs
confirming against real usage before anything depends on it.

# 1. Store embeddings as `halfvec(768)`

**Status:** Accepted, 2026-08-19

## Context

Chunk embeddings are the largest thing this system stores, and the database they live in has a
hard ceiling: Neon's free plan allows **0.5 GB per project**. That ceiling is not a detail to
optimise later — it decides how many documents the product can hold at all, and the project is
deliberately built on free tiers.

`gemini-embedding-001` emits 3072 dimensions natively and supports truncation to 768, which is
what we store. pgvector 0.8.6 — the version in our image — offers `vector` (4 bytes per
dimension) and `halfvec` (2 bytes).

## Decision

Store embeddings as `halfvec(768)`, indexed with HNSW using `halfvec_cosine_ops`,
`m = 16`, `ef_construction = 64`.

The domain entity holds a plain `float[]`. A value converter in Infrastructure narrows to
`halfvec` on write and widens on read, so `Querio.Domain` never references pgvector — a
Postgres extension type on a domain entity would put the database inside the model, which is
what `ArchitectureTests` exists to prevent.

## Why, measured

Five thousand rows of representative chunk text and random embeddings, on the real image:

| | bytes per chunk | HNSW index | chunks within 450 MB |
|---|---|---|---|
| `halfvec(768)` | **4,141** | 10 MB | **~114,000** |
| `vector(768)` | 9,859 | 20 MB | ~48,000 |

That is 2.4× the capacity — roughly 2,800 documents instead of 1,200 at forty chunks each.

The second effect was not anticipated and matters more over time. At full precision the 3 KB
embedding exceeds the inline row limit and Postgres moves it to TOAST — 20 MB of out-of-line
storage. Every row a search touches then costs an extra fetch. `halfvec` stays inline.

Half precision carries about three decimal digits. At 768 dimensions the recall difference does
not register beside doubling capacity and removing a per-row indirection from the hot path.

## Consequences

- Embeddings must be **L2-normalised before narrowing**, not after: `gemini-embedding-001`
  requires manual normalisation below 3072 dimensions, and normalising after the cast would
  compound the rounding.
- The dimension count is fixed at the column. Changing it is a migration plus a full re-embed,
  which is why 768 was chosen to match `bge-base` and `nomic-embed-text` — moving to a local
  model later stays a re-embed rather than a schema change.
- LINQ distance operators do not translate through a value converter. Retrieval is planned as
  hybrid vector plus full-text search fused with Reciprocal Rank Fusion, which is raw SQL
  regardless, so nothing is lost — but the next feature inherits this rather than discovering
  it.
- A row is validated at `DocumentChunk.AttachEmbedding` rather than at the database, so a
  provider returning the wrong dimensionality is named as such instead of surfacing as an
  opaque constraint violation.

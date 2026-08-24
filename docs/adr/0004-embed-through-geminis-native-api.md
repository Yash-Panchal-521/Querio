# 4. Embed through Gemini's native API, not the OpenAI-compatibility layer

**Status:** Accepted, 2026-08-19
**Supersedes:** the README's claim that changing embedding provider is a configuration change

## Context

The README originally specified reaching Gemini through its OpenAI-compatible endpoint, on the
reasoning that speaking a common dialect makes providers swappable by configuration. That is a
good instinct and, for this particular use, it does not survive contact with the documentation.

Two things this system depends on are undocumented for embeddings on that layer: **batching**,
and **output dimensionality**. And the compatibility layer states plainly that parameters it
does not support "will be silently ignored".

Silently ignored is the problem. Asking for 768 dimensions and receiving 3072 would not fail at
the request — it would fail later, at a `halfvec(768)` column, with an error about the database
rather than about the provider. Or worse, on a provider that truncated instead of erroring, it
would not fail at all and would simply retrieve badly.

## Decision

Call `models/gemini-embedding-001:batchEmbedContents` directly, with `outputDimensionality`
stated explicitly, behind our own `IEmbeddingService`.

Three consequences of the native API that the documentation is explicit about and we must
honour ourselves:

**Normalisation is ours.** `gemini-embedding-001` only normalises at its native 3072
dimensions. Below that, callers normalise. An unnormalised vector stores perfectly well and
retrieves badly — nothing downstream reports it, and the symptom looks like the model being
worse than it is.

**Retrieval is asymmetric.** Passages are embedded with `RETRIEVAL_DOCUMENT` and questions with
`RETRIEVAL_QUERY`. Using one for both is free to write, costs recall, and no test that checks
shapes would notice.

**Order is the mapping.** A batch returns vectors positionally. If the counts disagree the
mapping is unknowable, so a short response is refused rather than zipped — attaching vectors to
the wrong passages would produce rows that all look valid.

## Batching is not an optimisation

The free allowance is counted in **requests per day**, not tokens. One passage per request would
cap the product at roughly twenty documents a day. Batching is what makes the free tier usable,
which is why the batch size is configuration rather than a constant: the provider does not
document a maximum, and discovering it the hard way costs one of the day's requests.

Requests are also rate-limited on our side. Being refused costs the same allowance as
succeeding, so waiting is cheaper than asking and being told no.

## Failure handling

A `429` is **not** retried inside the client. A minute's throttling is worth waiting out and a
day's exhaustion is not, and the caller — which owns a queue and a status the user can see — is
the only thing positioned to tell the difference in a useful way. Transient failures (`5xx`,
timeouts) are retried with exponential backoff and jitter, bounded, so a runaway loop cannot
spend a day's allowance discovering that a service is down.

Distinguishing a daily limit from a per-minute one is a heuristic: the provider does not label
which allowance ran out in a machine-readable way, so the quota identifier it echoes back is
matched on. Getting it wrong is not dangerous — the worst outcome is pausing longer than needed.

## What is and is not proven

The stub-driven tests cover normalisation, count mismatch, quota classification, retry and batch
bounds — quickly, and without spending the allowance.

What only the live tests can prove is that the request shape is one the provider accepts, and
that the vectors mean something. Those skip themselves when no key is configured, so continuous
integration needs no credential, and they read the key from user secrets rather than a command
line where it would reach shell history.

The semantic test — a question about parental leave landing closer to the leave policy than to
the kitchen rota — is the one that catches the mistakes shapes cannot: a wrong task type,
normalisation on the wrong axis, a batch whose order does not match its inputs. Each of those
returns perfectly valid vectors that simply retrieve badly.

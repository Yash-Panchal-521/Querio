# 6. Pace embedding by tokens, and resume where it stopped

**Status:** Accepted, 2026-08-20

## Context

A 10 MB PDF of 219 passages was uploaded to a local stack with a real free-tier key. It never
finished. Watched over several minutes it embedded 64 passages, paused, resumed, embedded 32,
paused, resumed — the counter going down as often as up — while every cycle spent requests from
a daily allowance to make no progress at all.

Two separate mistakes, each of which alone would have been survivable.

## The limit we enforced was not the limit that binds

The client held itself to 60 requests a minute, on the reasoning that being refused costs the
same allowance as succeeding. That reasoning is right and the number is irrelevant: 219 passages
at 32 to a batch is seven requests, which no per-minute request allowance would ever notice.

Tokens are metered per minute too, and a batch of 32 passages is not one unit of anything — it
is roughly sixteen thousand tokens. Seven of those go out back to back in under a second,
because a request limiter sees seven requests and waves them through. Refusal began just past
thirty thousand tokens in a minute, which is exactly where the observed 64 came from: two
batches, then no.

**Decided:** a second limiter, over estimated tokens, in the same fixed window — and both
limiters held in a **singleton**, which the first attempt got wrong in a way worth recording. A
typed `HttpClient` is registered transient, so limiters constructed inside the embedding service
are rebuilt on every resolution, and the ingestion worker resolves one per job. Every document
therefore began with an untouched minute's allowance regardless of when the last one finished:
a rate limiter that limited nothing across the only boundary that mattered. The refusals
continued and the cause looked like a wrong figure rather than a wrong lifetime. Four characters
to the token — the approximation the chunker already displays with a "≈"; an exact count needs
the model's vocabulary and would change nothing, because the budget carries headroom anyway.

The default budget is 25,000 rather than the 30,000 where refusal was observed. Our window and
the provider's are not aligned, so a burst that straddles their boundary would still be refused
even while staying inside ours. Batch size drops from 32 to 16 in the same change, so one
request cannot be worth more than a fraction of a window.

None of these figures are published for embedding models. They are measured, and that is the
honest thing to say about them — which is why they are configuration rather than constants, and
why the refusal body is now logged.

## A pause has to be resumable, or it is a failure with better manners

The pipeline deleted a document's chunks before writing any, so that a retry replaced rather
than duplicated. Correct for a retry after a fault: whatever went wrong may have gone wrong
half-way, and a clean slate is the only trustworthy starting point.

A quota pause is not a fault. Nothing is wrong with the document, and something valuable
already exists — passages that cost allowance to produce. Deleting them and starting over means
re-spending that allowance on work already done, and for any document needing more than one
minute's worth of tokens the sum never converges. It is not slow; it does not terminate.

**Decided:** embedding starts from however far the last run got.

The condition for trusting the existing work is deliberately narrow. Chunks are written with
their vector already attached, so an existing row is always a finished one and the resume point
is simply how many rows there are. That is trusted only when this run chunked the document into
the same number of passages as the run that produced them, and only when their ordinals form a
contiguous run from zero. A different passage count means chunking changed and the text no
longer lines up with the vectors; a gap means something else did. Either way the answer is to
start again, because a passage embedded from text it no longer contains is a wrong answer no
later check would catch.

## The interface has to say which pause this is

Both pauses were one status and one hardcoded sentence about the daily allowance. So a document
throttled for two minutes announced a wait until midnight, and — because a pause was excluded
from polling on the grounds that it "could sit for hours" — sat at Paused until someone reloaded
the page, under a sentence promising it would resume on its own.

**Decided:** the document carries the reason and the resume time, not just the job. The row says
which allowance ran out because the server said so, and the screen keeps watching when the wait
is minutes and stops when it is hours. Polling through a day-long pause would keep an idle
instance and its database awake for nothing, which on a plan that suspends when idle is the
whole month's compute allowance — the original instinct was sound, only indiscriminate.

## The ceiling that actually stopped us was counted, not paced

Neither limiter addressed the refusal that ended a day's testing. The provider meters
`embed_content_free_tier_requests` at a thousand a day and counts **each passage** as one, so a
hundred-and-forty-page document is a sixth of a day and a handful of uploads exhausts it. No
per-minute pacing helps with a per-day total; it only decides how quickly you arrive.

Worse, the queue then behaved badly at the boundary. A refusal on a spent day arrives with a
short `retryDelay` — fifteen seconds, sometimes a minute — which is not a lie so much as
meaningless, and honouring it meant waking to be refused again. Eighty-four refusals in one
afternoon, each spending an allowance already gone.

**Decided:** count passages against a daily budget on our side and park the queue _before_
asking. A refusal costs the provider's allowance exactly what a success costs, so discovering
the ceiling by being told no guarantees the next day opens behind. Refusals are counted too,
because the provider's ceiling may sit below ours and the queue has to converge on parking
itself rather than probing.

Midnight UTC remains the assumed rollover, and remains an assumption — the boundary is not
documented for embedding models. It is now only reached when the provider has told us nothing,
and being early costs one refused request to find out.

## Consequences

A large document on a free-tier key now takes several minutes and several pauses to ingest, and
finishes. That is the trade: paced deliberately rather than sprinting into a wall.

The token estimate is an estimate. If it drifts badly low the provider will still refuse, and
the pause path handles that — the estimate is an optimisation over a mechanism that works
without it, not a replacement for it.

Resuming assumes chunking is deterministic. It is, and the guard above catches the case where a
release changes it, at the cost of re-embedding one document from scratch after such a change.

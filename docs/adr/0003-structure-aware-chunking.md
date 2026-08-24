# 3. Chunk on structure, and treat sections as hard boundaries

**Status:** Accepted, 2026-08-19

## Context

Chunking decides what retrieval can ever return. A boundary in the wrong place cannot be
recovered later by a better model or a cleverer query — the passage simply does not contain the
answer, and no amount of ranking will conjure it.

The embedding model accepts 2,048 tokens per input, so passages have an upper bound. Everything
else is a choice.

## Decision

**Break where the document breaks.** Prefer a block boundary, then a sentence end, then a word
break, and only cut mid-word when a single unbroken run exceeds a whole passage. The naive
alternative — cut every N characters — is why so many retrieval systems answer with half a
sentence from the middle of an unrelated section.

**A heading starts a new passage, always.** A passage never spans two sections. This was not the
original design; it came out of a test asserting that a paragraph under "Leave › Parental"
carries that path, which failed because the passage covered three sections at once and could
only be labelled with one of them.

**Read the breadcrumb at the end of a passage, not the start.** Documents routinely open with a
run of headings — a title, a section, a subsection — before any prose. The passage containing
that run belongs to the deepest of them. Labelling it with the first would misplace every
citation in the document's opening section.

**Consecutive headings do not each start a passage.** A break between them would emit passages
consisting of nothing but a heading, and a three-word passage embeds to a vector that matches
almost anything. A section starts at the first heading that *follows content*.

**Overlap stays inside a section.** 200 characters carry from one passage into the next so a
sentence straddling a boundary survives whole somewhere — but never across a section boundary.
This also came from a failing test: overlapping there opened each section's passage with the
previous section's tail, giving text one breadcrumb while it belonged under another, and
duplicating it into two passages that then compete in search results.

**Size in characters, not tokens.** 2,000 characters, roughly 500 tokens. An exact count would
mean shipping a vocabulary or spending an API call per chunk against a metered daily allowance.
Sizing well below the ceiling makes the approximation safe: even a pathological two characters
per token stays under half the limit. The interface shows the count as approximate, because it is.

## Structure comes from the format, and only where it is real

| Format | Headings | Pages |
|---|---|---|
| Word | **Stated.** A paragraph styled `Heading2` is a second-level heading because its author said so | — |
| Markdown | Stated, via ATX `#`. Markers are stripped: they are markup, not content | — |
| PDF | **None.** Structure is visual, not semantic | Recorded |
| Plain text | None | — |

PDFs deliberately get no headings. What looks like one is text that happens to be larger, and
inferring hierarchy from font size is guesswork that gets a document's structure confidently
wrong. Page numbers are recorded instead — and "page 12" is more use to a reader than a section
name we invented.

## Consequences

- A document with many short sections produces many short passages. That is the honest outcome:
  a 300-character section is its own idea, and merging it with its neighbour would give the
  result a breadcrumb true of only half its content.
- Breadcrumbs are only as good as the source's structure. Word is the most trustworthy, Markdown
  close behind, PDF has none at all.
- Chunking is pure logic with no external dependency, so it lives in Application rather than
  Infrastructure and is tested without a container.

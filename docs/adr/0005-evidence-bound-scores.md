# ADR 0005 — Evidence-bound criterion scores

## Status

Accepted

## Context

HireLens must not invent a numeric score. EU AI Act high-risk employment use requires that every number is traceable to a quote in the source document.

## Decision

`CriterionScore` is created only through `CriterionScore.Create` in the Evidence module. A non-null `Score` requires at least one `Evidence` (source, quote, offsets). Otherwise the row is persisted with `Score = null` and `EvidenceStatus = Insufficient`. Assigning a number without evidence throws `DomainException`.

HANA cannot express "at least one child row" as a CHECK. An integration test locks the invariant.

Scores store the prompt version that produced them and are never recomputed in place when a prompt changes.

## Consequences

- Matching proposes; Evidence admits. A later model cannot write a bare number through EF.
- Recruiter UI must render `null` as unknown, never as zero.

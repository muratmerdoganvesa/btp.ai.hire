# 0006 — Text-only interviews

## Status

Accepted

## Context

HireLens pre-interviews sit inside a high-risk employment assessment. Inferring emotion, personality, or honesty from face, voice, or biometrics would violate the product contract and EU AI Act limits.

## Decision

Interviews are text-only. Every question is bound to a criterion. Consent (`ConsentRecord`) cannot be skipped. Evaluation evidence uses transcript offsets, never biometric signals.

## Consequences

Video/audio capture is out of scope. Recruiter and candidate UIs disclose this limit. The interview score can blend into the overall score only through `IEvaluationBlendPort`.

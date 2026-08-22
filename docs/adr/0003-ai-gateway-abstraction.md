# ADR 0003 — AI Gateway as the only LLM door

## Status

Accepted

## Context

SAP ships official AI SDKs for JavaScript and Java only. HireLens is .NET. Employment scoring is high-risk under the EU AI Act: every model call must be masked, structured, retried, metered, and auditable.

## Decision

No application code calls an LLM over HTTP. All tasks go through `IAiGateway`. The gateway always runs `IPiiMasker` first, forces JSON Schema output, retries with Polly, records `AiInvocation`, and routes `AiTaskType` to a model profile from configuration (tenant `ModelPolicy` may override).

`SapOrchestrationProvider` is a thin `HttpClient` against AI Core Orchestration:

`POST {aiApiUrl}/v2/inference/deployments/{deploymentId}/v2/completion`

with a mandatory `AI-Resource-Group` header. Model settings live under `LLMModelDetails`. The request DTO is isolated so an upstream version change does not leak into modules.

## Consequences

- Model swaps and tenant policy are configuration, not code branches.
- Masking is enforced by unit test, not convention.
- Live verification of the Orchestration path requires `AICORE_SERVICE_KEY`; without it the stub provider is used and still must mask.

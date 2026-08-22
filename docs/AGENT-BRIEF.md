# HireLens — Agent brief (Phase 0 contract)

This file is the working contract for humans and later AI sessions. Product, stack, and non-negotiables do not change between phases.

## Product

HireLens is an AI-assisted recruiting platform on SAP BTP. One codebase serves many enterprise tenants.

It does: read CVs, match them to a job description with evidence-bound scores, run AI pre-interviews, and support recruiter decisions.

It does **not**: auto-reject or auto-accept; infer age, gender, nationality, marital status, photos, religion, or disability; infer emotion, personality, or honesty from face, voice, or biometrics; emit a numeric score without evidence (`unknown` / `Insufficient` instead).

Employment assessment is high-risk under the EU AI Act. These limits are architectural, not a later filter.

## Fixed stack

- Backend: .NET 10, C# 14, Minimal API, vertical slices, HANA EF + Dapper, FluentValidation, Serilog, OpenTelemetry, Polly, Hangfire.
- Frontend: React 19, TypeScript strict, Vite 7, Tailwind v4 CSS-first (`@theme`, no `tailwind.config.js`), shadcn/Radix, TanStack Query + Router, RHF + zod, Zustand, Recharts, i18next TR/EN.
- BTP: CF Docker deploy (`mcr.microsoft.com/dotnet/aspnet:10.0`), HANA Cloud, AI Core Orchestration, Object Store, XSUAA + IAS, Audit Log Service.

## Architecture rules

1. Modular monolith. Modules under `src/Modules/HireLens.Modules.{Name}/`. No Domain/Infrastructure references across modules. Contracts only. NetArchTest is mandatory.
2. Shared DB + `TenantId`. JWT-only tenant (`zid` / `app_tid`). No default tenant. Cross-tenant access is `404`.
3. `IAiGateway` is the only LLM door. Masking, schema, retry, telemetry, config routing.
4. Prompts live in `prompts/{taskType}/{version}.md`, never inline.
5. A numeric `Score` cannot exist without evidence. Domain factory + integration test; not a HANA CHECK.
6. Long work is `202 Accepted` + Hangfire. No synchronous wait.
7. Code, comments, commits, identifiers: English. User-visible copy: i18n (TR primary). `Result<T>` over control-flow exceptions. `record` preferred.

## Security (non-negotiable)

PII never enters an LLM prompt. Audit rows are append-only. Candidate AI disclosure + `ConsentRecord`. Human rationale required for decisions. KVKK/GDPR endpoints exist from Phase 1. Secrets only via VCAP / CI. CVs go to Object Store via presigned URL.

## Design system

No hard-coded color, type size, or spacing in apps — tokens only. OKLCH. Low scores are **neutral grey**, never red. No candidate photos. WCAG 2.2 AA.

## Phase 0 scope

Empty but working multi-tenant skeleton. No CV, scoring, or interview logic.

# ADR 0001 — Modular monolith

## Status

Accepted

## Context

HireLens is a multi-tenant SaaS on SAP BTP. A single Cloud Foundry application is cheaper to operate and easier to reason about than a fleet of services while the domain is still forming. Bounded contexts already exist (Tenancy, Identity, later Recruiting, Matching, Interview).

## Decision

Ship one deployable (`HireLens.Api` + `HireLens.Worker`) with modules under `src/Modules/HireLens.Modules.{Name}/`. Each module owns `Domain/`, `Application/`, `Infrastructure/`, `Endpoints/`, and `{Name}Module.cs`.

Cross-module communication uses only `HireLens.Contracts` DTOs and events. A module must not reference another module's Domain or Infrastructure. NetArchTest enforces this; a module is not added without the corresponding architecture test.

## Consequences

- One Docker image, one CF app (plus worker), one HANA schema.
- Module boundaries stay explicit; extracting a service later is a packaging change, not a rewrite.
- Shared persistence (`HireLensDbContext`) is a deliberate trade-off accepted in ADR 0002.

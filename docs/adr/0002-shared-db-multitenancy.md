# ADR 0002 — Shared database, TenantId isolation

## Status

Accepted

## Context

SAP HANA Cloud is provisioned once per landscape. Schema-per-tenant would multiply operational cost and block cross-tenant platform operations (metering, provisioning). Row-level isolation is sufficient if it cannot be bypassed.

## Decision

Use a shared schema. Every tenant-owned table has `TenantId UUID NOT NULL` and a `(TenantId, Id)` composite index. `HireLensDbContext` applies a global query filter to every `ITenantEntity` via reflection so a new entity cannot opt out by omission.

`TenantId` is read only from the JWT (`zid` for XSUAA, `app_tid` for IAS). Request body, query string, and headers are ignored. Missing claims yield `401` — there is no default tenant. Background jobs enter an explicit `SystemTenantScope`. Cross-tenant reads return `404` so existence is not leaked.

## Consequences

- A single connection string and a single migration stream.
- Isolation bugs are high severity; `TenantIsolationTests` are mandatory.
- Physical deletion of a tenant's rows is a compliance operation, not a day-to-day path.

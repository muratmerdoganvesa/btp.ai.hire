# ADR 0004 — Cloud Foundry deploy via Docker, not buildpack

## Status

Accepted

## Context

SAP BTP Cloud Foundry officially supports Java, Node.js, and Python buildpacks. `dotnet_core_buildpack` is a community system buildpack. SAP notes it may be removed without notice, and its default runtime is .NET 8. HireLens targets .NET 10 LTS.

## Decision

Deploy with `cf push --docker-image` (manifest `docker.image`, no `buildpacks` or `path`). Base image: `mcr.microsoft.com/dotnet/aspnet:10.0`. The process binds `PORT` as injected by CF and runs as a non-root user.

Base-image CVEs are our responsibility. CI rebuilds images on every main push and on a monthly schedule.

## Consequences

- We control the .NET patch level independently of SAP buildpack lag.
- The container registry must stay reachable from the CF org (GHCR).
- Image rebuilds are an operations duty, not an optional hygiene task.

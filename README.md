# HireLens


AI-assisted recruiting on SAP BTP. Phase 0 is a deployable multi-tenant skeleton: no CV parsing, scoring, or interviews yet.

Read [docs/AGENT-BRIEF.md](docs/AGENT-BRIEF.md) and [docs/adr](docs/adr) before changing architecture.

## Local backend

Prerequisites: .NET 10 SDK.

```bash
cd hirelens
cp .env.example .env
dotnet build
dotnet test --filter "Category!=Integration"
dotnet run --project src/HireLens.Api
```

API listens on `http://localhost:5080`.

- `GET /health/live` — process is up
- `GET /health/ready` — database reachable
- `POST /dev/token` — Development/Testing only. Issues a JWT with `zid` (or `app_tid` when `issuerKind=ias`)

Without `HANA_CONNECTION` the API uses EF Core InMemory. Without `AICORE_SERVICE_KEY` the AI Gateway uses a stub provider. Masking still runs.

Worker:

```bash
dotnet run --project src/HireLens.Worker
```

## Local frontend

Prerequisites: Node 22+, pnpm 9.

```bash
cd frontend
pnpm install
pnpm build
pnpm --filter @hirelens/recruiter-console dev
```

Recruiter console (`http://localhost:5173`) signs in through `/dev/token` and shows the current tenant on an empty dashboard.

## Environment

| Variable | Required | Purpose |
| --- | --- | --- |
| `DEV_JWT_SIGNING_KEY` | Local only | Symmetric key for `/dev/token` |
| `HANA_CONNECTION` | Production | SAP HANA ADO.NET connection string |
| `AICORE_SERVICE_KEY` | Optional | AI Core binding JSON |
| `AICORE_DEPLOYMENT_ID` | With AI Core | Orchestration deployment id |
| `AICORE_RESOURCE_GROUP` | With AI Core | Sent as `AI-Resource-Group` |
| `VCAP_SERVICES` | CF | HANA, XSUAA, Object Store, Audit Log bindings |
| `PORT` | CF | Injected by Cloud Foundry; the process binds it |

Secrets never enter the repository. Use user-provided services and CI secrets.

## BTP deploy

Images: `mcr.microsoft.com/dotnet/aspnet:10.0`. Push with Docker, not a buildpack. See ADR 0004.

```bash
docker build -f deploy/Dockerfile.api -t ghcr.io/<owner>/hirelens-api:local .
cf push hirelens-api --docker-image ghcr.io/<owner>/hirelens-api:local -f deploy/manifest.yml
```

Bind services named `hana_dev`, `hirelens-xsuaa`, `hirelens-objectstore`, `hirelens-auditlog` (create them in the cockpit first). XSUAA app security is in `deploy/xs-security.json`.

## Tests

Default `dotnet test` excludes `[Trait("Category","Integration")]`. Those tests call real HANA or AI Core and run only when `HANA_CONNECTION` and `AICORE_SERVICE_KEY` are set.

## Orchestration path

`POST {AI_API_URL}/v2/inference/deployments/{deploymentId}/v2/completion` with header `AI-Resource-Group`. Model settings are serialized as `LLMModelDetails` in `HireLens.AiGateway/Providers/OrchestrationCompletionRequest.cs`. Verify with curl against your landscape before treating the live provider as confirmed.

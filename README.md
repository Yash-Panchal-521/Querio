# Querio

**AI knowledge assistant (multi-tenant RAG SaaS).** A team uploads its documents and asks
questions in plain language. Answers are generated **grounded in their own content**, with
inline citations back to the source chunks and token-by-token streaming.

---

## Status

Authentication and multi-tenant access are complete end to end. Documents, retrieval and
answers are next.

| Area | State |
|---|---|
| Solution layout, CQRS pipeline, endpoint convention | Done |
| Structured logging, global error handling, rate limiting | Done |
| Persistence, EF Core, migrations, Testcontainers | Done |
| Accounts, sessions, organizations, members, invitations | Done |
| Tenant isolation, enforced at the data layer | Done |
| Web app: auth, organizations, members, design system | Done |
| Documents, ingestion, retrieval, answers | Not started |

## Architecture

Four projects, with the dependency direction enforced by the compiler **and** by tests that
fail the build if a layer reaches somewhere it should not:

```
Querio.Domain          entities, invariants, error model — no framework dependencies at all
   ▲
Querio.Application     use cases as vertical slices, CQRS pipeline behaviours
   ▲
Querio.Infrastructure  Postgres, object storage, model providers, ingestion worker
   ▲
Querio.Api             minimal-API endpoints, DI composition, HTTP concerns
```

**Features are vertical slices.** A use case lives in one folder —
`Documents/UploadDocument/{Command,Handler,Validator,Response}` — rather than being scattered
across top-level `Commands/`, `Queries/` and `Handlers/` directories.

Some deliberate choices, and why:

| Decision | Reasoning |
|---|---|
| CQRS via [`Mediator`](https://github.com/martinothamar/Mediator) | Source-generated, so no runtime reflection and AOT-safe. MediatR moved to a commercial licence at v13. |
| Minimal APIs, not controllers | SSE answer streaming via `IAsyncEnumerable` is markedly cleaner, and `TypedResults` yields real OpenAPI types. |
| **No repository pattern over EF Core** | `DbContext` is already a unit of work and `DbSet<T>` already a repository. Wrapping it adds indirection without isolation. |
| Exceptions, not `Result<T>` | One error idiom. Failures become RFC 9457 ProblemDetails with a `traceId` and a stable `errorCode`. |
| Errors carry a category, not an HTTP status | Keeps ASP.NET out of Domain; the API layer owns the category → status mapping. |
| Postgres job table for ingestion | `FOR UPDATE SKIP LOCKED` is transactional with the document row and needs no broker. |

## Tech stack

- **Frontend** — Next.js 16 (App Router), React 19, TypeScript (strict), Tailwind v4.
- **Backend** — ASP.NET Core on .NET 10, minimal APIs, `Mediator`, FluentValidation, Serilog.
- **Data** — PostgreSQL + `pgvector`, HNSW index. Hybrid retrieval: vector search fused with
  Postgres full-text search via Reciprocal Rank Fusion.
- **AI** — Gemini through `Microsoft.Extensions.AI`. `gemini-embedding-001` at **768
  dimensions** for ingestion, Gemini Flash for answers. Reached over Gemini's
  OpenAI-compatible endpoint, so swapping providers is a configuration change.
- **Async** — `BackgroundService` ingestion worker over a Postgres job table.
- **Storage** — Cloudflare R2 (S3-compatible) for raw uploads.
- **Auth** — Firebase Auth; tenant membership is held in Postgres, not only in token claims.

### Constraints worth knowing before contributing

- `gemini-embedding-001` accepts at most **2048 input tokens**, which caps chunk size.
- The `pgvector` column dimension is fixed at DDL. 768 is chosen partly because it matches
  `bge-base` and `nomic-embed-text`, so moving to a local model later is a re-embed rather
  than a migration.
- EF global query filters are bypassed by raw SQL and `IgnoreQueryFilters`. Postgres RLS is
  deferred, not rejected.

## Getting started

**Prerequisites** — .NET 10 SDK, Node 20+, pnpm, Docker.

No credentials are kept in the repository, including local ones. Pick a password for the
development database and put it in the two places that read one — an untracked `.env` for
Docker, and .NET user secrets for the API. It guards a container bound to localhost, so
choose anything:

```bash
cp .env.example .env
```

Fill in `POSTGRES_PASSWORD`, then give the API the matching connection string:

```bash
dotnet user-secrets set "ConnectionStrings:Querio" "Host=localhost;Port=5434;Database=querio;Username=querio;Password=THE_SAME_PASSWORD" --project backend/src/Querio.Api
```

Start Postgres (published on 5434 to avoid colliding with other local databases):

```bash
docker compose up -d
```

Apply migrations. This is a deliberate step and never runs automatically at start-up — with
more than one instance that races, and a half-migrated schema is worse than a stopped
rollout. The readiness probe reports the instance unready while its schema is behind:

The migration tooling runs against the Infrastructure project alone, so it never sees the
API's user secrets and takes `QUERIO_CONNECTION_STRING` instead:

```bash
QUERIO_CONNECTION_STRING="Host=localhost;Port=5434;Database=querio;Username=querio;Password=THE_SAME_PASSWORD" dotnet ef database update --project backend/src/Querio.Infrastructure --startup-project backend/src/Querio.Infrastructure
```

Run the API:

```bash
dotnet run --project backend/src/Querio.Api
```

Run the web app, in a second terminal:

```bash
pnpm --dir frontend dev
```

The API listens on `http://localhost:5063` and the web app on `http://localhost:3000`. Copy
`frontend/.env.example` to `frontend/.env.local` if you need to point at a different API.

With the API running, the interactive API reference is at `http://localhost:5063/scalar/v1`.

## Testing

```bash
cd backend && dotnet test --solution Querio.slnx
```

Run it from `backend/`, not from the repository root. .NET 10 removed the VSTest bridge, so
the suite runs on Microsoft.Testing.Platform (opted into via `backend/global.json`) with
xUnit v3 — and the SDK resolves that file by walking up from the working directory. From the
root it never sees it, falls back to VSTest, and rejects `--solution` with
`MSBUILD : error MSB1001: Unknown switch`.

Integration tests start their own throwaway Postgres via Testcontainers and apply the
migrations to it, so they need Docker running but never touch the compose database — the
suite is independent of local state.

Frontend checks:

```bash
pnpm --dir frontend typecheck && pnpm --dir frontend lint && pnpm --dir frontend build
```

## Deployment

| Piece | Runs on |
|---|---|
| Web app | Vercel, deployed from `main` by Vercel's own Git integration |
| API | Render, triggered by GitHub Actions after migrations |
| Database | Neon (Postgres + `pgvector`) |

`main` is deploy-only. Merging to it runs `.github/workflows/deploy.yml`, which applies
migrations first and only then tells Render to roll. That ordering matters: the readiness
probe reports an instance unready while its schema is behind, so traffic never reaches a
process whose schema does not match it.

The cost of that ordering is a window where the **old** code runs against the **new** schema,
so every migration must be backward-compatible with the release it replaces — add a column in
one release, write to it in the next, drop it a release later.

The web app is left to Vercel's own integration rather than driven from Actions. It has
nothing to sequence, and doing it that way keeps a deploy token out of GitHub.

### Render service

The API has no native runtime on Render, so it builds from `backend/Dockerfile` with the
service's Root Directory set to `backend`. Auto-Deploy is **off**: Render is driven by the
deploy hook from Actions instead, which is what keeps the build behind the migration step
rather than racing it.

Health Check Path is `/health/ready`. The sibling `/health/live` deliberately runs no checks
at all, so it cannot notice a database the instance can no longer reach.

| Variable | Notes |
|---|---|
| `ConnectionStrings__Querio` | Neon's **pooled** endpoint, in Npgsql keyword format — not a `postgresql://` URI |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Authentication__Firebase__ProjectId` | The Firebase project |
| `Cors__AllowedOrigins__0` | The Vercel origin, no trailing slash. `Cors:AllowedOrigins` is an array, hence the index |
| `OpenApi__ExposePublicly` | `true` publishes Scalar and Swagger on the public URL |

`ASPNETCORE_URLS` is baked into the image at port 10000 to match the `PORT` Render injects,
so it only needs setting if that ever differs.

### Secrets

Set on the `production` environment in GitHub:

| Name | What it is |
|---|---|
| `QUERIO_MIGRATION_CONNECTION_STRING` | Neon's **direct** endpoint, not the pooled one |
| `RENDER_DEPLOY_HOOK_URL` | Deploy hook from the Render service |

Migrations must use the direct endpoint: over Neon's PgBouncer pooler they fail with
`prepared statement "s0" already exists`, and a `SET search_path` does not survive past its
own transaction. The running application uses the pooled endpoint as normal.

Repository *variables* (not secrets — these are public identifiers) supply the frontend build:
`NEXT_PUBLIC_API_BASE_URL`, `NEXT_PUBLIC_FIREBASE_API_KEY`, `NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN`,
`NEXT_PUBLIC_FIREBASE_PROJECT_ID`.

### Known trade-off of the free tiers

Render and Neon both suspend when idle, so the first request after a quiet spell pays for a
container cold start and a database wake-up together. For a demo somebody clicks once, that
is the difference between feeling instant and feeling broken — worth a paid Render instance
before putting the link in front of anyone.

## Branching

`main` is reserved for deployment. Work happens on `dev` across multiple commits, and a pull
request to `main` is opened only once a feature is complete.

## Roadmap

- [x] Persistence: EF Core, tenant/user/membership schema, migrations
- [x] Firebase authentication, organizations, members and invitations
- [ ] `pgvector` column and document/chunk schema
- [ ] Upload to R2 with async ingestion (chunk → embed → store)
- [ ] Hybrid retrieval with Reciprocal Rank Fusion
- [ ] Streaming answers with inline citations
- [ ] Per-tenant usage limits and admin dashboard
- [ ] CI/CD, seeded demo tenant

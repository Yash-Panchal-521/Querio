"Querio": AI Knowledge Assistant (RAG SaaS)

### One-liner
A multi-tenant SaaS where a team uploads its documents and asks natural-language questions; answers are generated **grounded in their own content**, with inline citations and streaming responses.

### Tech stack
- **Frontend:** React + TypeScript. Polished streaming chat UI (token-by-token), source-citation chips, document library, upload with progress, usage dashboard.
- **Backend:** ASP.NET Core (C#) Web API.
- **Data:** PostgreSQL + **pgvector** extension (embeddings + metadata + tenant rows).
- **AI:** Azure OpenAI or OpenAI API — embeddings model for ingestion, chat model for answers.
- **Async:** background ingestion worker (ASP.NET hosted service or Azure Function) + a queue (Azure Storage Queue / in-Postgres job table) for chunk→embed→store.
- **Cache:** Redis (embedding cache, hot-answer cache, rate-limit counters).
- **Storage:** Azure Blob Storage for raw uploaded files.
- **Auth:** Firebase Auth

### Core features (MVP scope)
- Tenant/org signup + members.
- Document upload (PDF, Markdown, TXT) with async ingestion.
- Semantic + hybrid search over a tenant's docs.
- Streaming chat answers with inline citations back to source chunks.
- Conversation history per user.
- Per-tenant usage limits + admin dashboard (docs, tokens used, members).

### Deployment
- Frontend on Vercel; API + worker on Azure App Service or Render; Postgres (with pgvector) on Neon/Supabase/Azure; Redis on Upstash.
- GitHub Actions CI/CD: build, test (xUnit + Playwright), deploy.
- Live demo with a seeded sample tenant so recruiters can try it in 30 seconds.

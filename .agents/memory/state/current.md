# Current Project State
- Architecture source: `docs/architecture/architecture-overview.md` (Approved).
- Architecture style: Hybrid Architecture — ASP.NET Core Modular Monolith for core transactional logic, with Node.js services for asynchronous/high-concurrency workloads.
- Core backend solution: `services/backend/EnterpriseCommerce.sln`.
- Frontends: `apps/web` and `apps/admin`.
- Node.js worker area: `services/workers/`.
- AI execution governance: risk-based approval, maximum 3 correction cycles, implementation/validation separation.
- Context strategy: one primary Agent, progressive-disclosure Skills, JIT durable-memory retrieval.

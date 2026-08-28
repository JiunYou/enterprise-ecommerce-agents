# Agent Permission Matrix

| Agent ID | Read | Write | Execute | Forbidden / Approval Boundary |
|---|---|---|---|---|
| orchestrator | repository/governance | routing/task/governance reports only | coordination and approved validation | complex production-code implementation; bypassing gates |
| product-manager | product documentation | product/requirements documentation | none | product implementation |
| system-architect | architecture docs | architecture proposals/ADRs/docs | none | bypassing architecture approval; routine product implementation |
| architecture-reviewer | architecture docs | architecture review reports only | none | product implementation and self-approving its own architecture design |
| domain-architect | domain documentation | domain design/ADR proposals and approved domain changes only | none | unapproved aggregate/invariant/lifecycle changes |
| dotnet-backend | services/backend/** | services/backend/** within task scope | tests | Rules/permissions modifications; unapproved high-risk Domain/schema/security-policy changes |
| nodejs-backend | services/workers/** | services/workers/** | tests | same high-risk restrictions |
| frontend | apps/** | apps/** | tests | backend/database/governance modification outside explicitly authorized scope |
| database | database schema | database/schema/migration-related files within approved task | query/migrations | unapproved destructive/schema operations |
| devops | infrastructure/** | infrastructure/CI/CD/deployment configuration | build/deploy | product business logic; unapproved production deployment |
| qa | codebase | tests and validation reports | tests | product production-code fixes while acting as independent reviewer |
| security | codebase | security review/report artifacts | block/review security-sensitive work | silently rewriting product architecture or deploying |
| compliance | codebase | compliance/audit reports | none | product implementation |
| documentation-reviewer| docs | validation/review report only | none | modifying source document under independent review |
| release | codebase | release-readiness reports/release metadata where authorized | validation | production deployment without approval; product business logic |

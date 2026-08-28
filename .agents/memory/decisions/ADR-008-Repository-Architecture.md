---
Document ID: DOC-ADR-008
Version: 1.0
Owner Agent: Enterprise Software Delivery Architect
Created Date: 2026-08-24
Status: Proposed
Related ADR: ADR-001
---

# ADR-008: Repository Architecture Decision

## Status
Proposed

## Context
本平台採用 Hybrid Architecture，包含 ASP.NET Core 後端、Node.js 服務與 Next.js 前端。為了讓 AI Agent 與開發團隊能高效協作、共享型別 (Types) 與基礎設施設定，我們必須決定程式碼儲存庫 (Repository) 的架構。

## Decision
採用 **Polyglot Monorepo (多語系單一儲存庫)** 架構。
目錄結構如下：
- `/apps/` (前端與全端應用，如 Next.js)
- `/services/` (後端服務，包含 .NET Modular Monolith 與 Node.js Services)
- `/packages/` (跨專案共用的函式庫，如 Shared Types, UI Components)
- `/infrastructure/` (IaC, Docker, CI/CD 腳本)
- `/docs/` (架構與維運文件)
- `/tests/` (跨系統的 E2E 整合測試)
- `/.github/` (CI/CD Pipelines)

## Alternatives Considered
- **Polyrepo (Multi-repo):** 每個微服務與前端獨立一個 Repo。雖然隔離性高，但在跨邊界重構、API 協定同步以及 AI Agent 上下文切換時成本極高，不利於初期快速迭代。

## Consequences
- **Positive:** 程式碼跨專案搜尋容易；前端與 Node.js 可透過 Workspace 共享型別；單一 PR 即可涵蓋從後端 API 到前端 UI 的完整 Feature。
- **Negative:** 儲存庫體積隨時間增長；CI/CD Pipeline 需要設定精準的 Path filtering，避免無關的服務被觸發建置。

## Security Impact
所有專案共享同一個 Repo 權限，因此需依賴 `CODEOWNERS` 機制來限制特定目錄 (如 `/infrastructure/` 或 `/services/order/`) 的合併權限。

## Future Evolution
當特定子系統發展過大或交由完全獨立的外部團隊接手時，可考慮將其透過 Git Submodule 或移出獨立 Repo。

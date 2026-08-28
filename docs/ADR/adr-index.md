---
Document ID: DOC-ADR-INDEX
Version: 1.1
Owner Agent: Documentation Agent
Created Date: 2026-08-24
---

# ADR Registry

本清單索引了 Enterprise E-Commerce Platform 在架構設計與工程治理階段產出的所有 Architecture Decision Records (ADRs)。

| ADR ID | Title | Decision | Status | Related Phase |
| :--- | :--- | :--- | :--- | :--- |
| **ADR-Phase1-001** | Business Domain Classification Decision | 定義 Order 與 Inventory 為 Core Domain，其他為 Supporting / Generic | Approved | Phase 1 (Domain Analysis) |
| **ADR-Phase2-001** | Bounded Context & Event Strategy | 確立 Context 邊界，並決定使用 Domain Events 解耦 Order 與 Inventory | Approved | Phase 2 (DDD) |
| **ADR-001** | System Architecture Style Decision | 放棄純微服務與單體，採用 Hybrid Architecture (ASP.NET Core Monolith + Node.js Services) | Approved | Phase 3 (Arch Style) |
| **ADR-Phase4-001** | C4 Architecture Boundary & Component Decision | 決定了 Level 1~3 的具體劃分，並引入 RabbitMQ, Redis, Elasticsearch 等基礎設施 | Approved | Phase 4 (C4 Model) |
| **ADR-Technical-001** | ASP.NET Core Architecture Pattern Decision | ASP.NET Core Modular Monolith 內部全面採用 Clean Architecture 確保領域純粹性 | Approved | Phase 5 (Tech Arch) |
| **ADR-Security-001** | Authentication Architecture Decision | 採用雲端託管 Identity Provider 搭配 Stateless JWT，降低自建風險 | Approved | Phase 6 (Security) |
| **ADR-Security-002** | Secret Management Strategy Decision | 全面採用 Cloud Secret Manager 搭配 IAM Role，禁止機密寫入設定檔或 Docker | Approved | Phase 6 (Security) |
| **ADR-008** | Repository Architecture Decision | 採用 Polyglot Monorepo 統一管理前後端與基礎設施代碼 | Approved | Phase 8 (Engineering Blueprint) |
| **ADR-009** | Branch Strategy Decision | 採用 GitHub Flow / Trunk-based 搭配短期 Feature Branch | Approved | Phase 8 (Engineering Blueprint) |
| **ADR-010** | CI/CD Pipeline Strategy Decision | 使用 GitHub Actions 進行多階段建置、測試、安全檢查與容器映像建構 | Approved | Phase 8 (Engineering Blueprint) |
| **ADR-011** | Environment Strategy Decision | 劃分 Local, Dev, Staging, Prod 四套環境並嚴格管理環境隔離 | Approved | Phase 8 (Engineering Blueprint) |
| **ADR-012** | AI Agent Workflow Decision | 建立多 Agent 協作治理體系，定義各 Agent 之職責與審查邊界 | Approved | Phase 8 (Engineering Blueprint) |
| **ADR-013** | Platform Bootstrap Decision | 採用官方標準 CLI 工具初始化各技術棧骨架 | Approved | Phase 9 (Platform Foundation) |
| **ADR-014** | Logging Strategy Decision | 全面採用 Structured Logging (Serilog / Pino) 搭配關聯 ID 追蹤 | Approved | Phase 9 (Platform Foundation) |
| **ADR-015** | Local Development Environment Decision | 使用 Docker Compose 統一建立本地基礎設施與開發環境 | Approved | Phase 9 (Platform Foundation) |
| **ADR-016** | AI Agent Execution Control Strategy | 建立受控的 Agent 執行模型，定義任務邊界、強制終止條件與迭代上限 | Approved | Phase 9.6 (Governance Hardening) |
| **ADR-017** | AI Engineering Team Operating Model | 定義 AI 團隊的角色、職責與協作模型 | Approved | Phase 10.6 (AI Eng) |
| **ADR-018** | Application Layer Architecture | 定義 Application Layer 架構為 CQRS 模式 | Approved | Phase 11.0 (App Planning) |
| **ADR-019** | AI Workflow Execution Model | 確立 AI Workflow 的執行邊界與審查關卡 | Approved | Phase 11.2 (Workflow Setup) |
| **ADR-020** | Infrastructure Architecture | 確立基礎設施層 (EF Core, Outbox Pattern) | Approved | Phase 12.0 (Infra Planning) |
| **ADR-021** | API Layer Architecture | 確立 API 層 (ASP.NET Core Web API) 作為 Presentation 邊界，制定 RFC 7807 錯誤處理與資安規範 | Proposed | Phase 13.0 (API Planning) |

> 📁 **備註：** 詳細決策內容請參閱 `docs/ADR/` 與 `.agents/memory/decisions/` 目錄下的 Markdown 原始檔。

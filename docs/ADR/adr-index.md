---
Document ID: DOC-ADR-INDEX
Version: 1.0
Owner Agent: Documentation Agent
Created Date: 2026-08-24
---

# ADR Registry

本清單索引了 Enterprise E-Commerce Platform 在架構設計階段 (Phase 1 ~ Phase 6) 產出的所有 Architecture Decision Records (ADRs)。

| ADR ID | Title | Decision | Status | Related Phase |
| :--- | :--- | :--- | :--- | :--- |
| **ADR-Phase1-001** | Business Domain Classification Decision | 定義 Order 與 Inventory 為 Core Domain，其他為 Supporting / Generic | Approved | Phase 1 (Domain Analysis) |
| **ADR-Phase2-001** | Bounded Context & Event Strategy | 確立 Context 邊界，並決定使用 Domain Events 解耦 Order 與 Inventory | Approved | Phase 2 (DDD) |
| **ADR-001** | System Architecture Style Decision | 放棄純微服務與單體，採用 Hybrid Architecture (ASP.NET Core Monolith + Node.js Services) | Approved | Phase 3 (Arch Style) |
| **ADR-Phase4-001** | C4 Architecture Boundary & Component Decision | 決定了 Level 1~3 的具體劃分，並引入 RabbitMQ, Redis, Elasticsearch 等基礎設施 | Approved | Phase 4 (C4 Model) |
| **ADR-Technical-001** | ASP.NET Core Architecture Pattern Decision | ASP.NET Core Modular Monolith 內部全面採用 Clean Architecture 確保領域純粹性 | Approved | Phase 5 (Tech Arch) |
| **ADR-Security-001** | Authentication Architecture Decision | 採用雲端託管 Identity Provider 搭配 Stateless JWT，降低自建風險 | Approved | Phase 6 (Security) |
| **ADR-Security-002** | Secret Management Strategy Decision | 全面採用 Cloud Secret Manager 搭配 IAM Role，禁止機密寫入設定檔或 Docker | Approved | Phase 6 (Security) |

> 📁 **備註：** 詳細決策內容請參閱 `.agents/memory/decisions/` 目錄下的 Markdown 原始檔。

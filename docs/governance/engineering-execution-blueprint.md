---
Document ID: DOC-ENG-001
Version: 1.0
Owner Agent: Enterprise Software Delivery Architect
Created Date: 2026-08-24
Status: Draft
---

# Engineering Execution Blueprint

本文件將 Phase 1~7 的架構設計藍圖轉換為可執行的軟體工程基礎與交付流程。

## Current Architecture Understanding Summary
本平台採用 **Hybrid Architecture**。核心交易（如 Order, Inventory）依賴 ASP.NET Core Modular Monolith 處理，追求強一致性。外圍服務（搜尋、通知、AI 整合）採用 Node.js 獨立開發，以非同步與 Domain Events (RabbitMQ) 串接。前端以 Next.js (App Router) 為主體。基礎設施涵蓋 MySQL, Redis, Elasticsearch 與 Docker，並採用 OIDC、JWT 與 Cloud Secret Manager 構築防護網。

## Engineering Foundation Strategy
為支援多技術棧的協作與高度 AI 輔助開發，本策略圍繞「單一儲存庫、主幹開發、自動化護欄與階層式 Agent 治理」為核心。

## Repository Architecture Plan (ADR-008)
採用 **Polyglot Monorepo** 架構：
```text
/
├── apps/            # 前端與全端專案 (Next.js)
├── services/        # 後端服務 (.NET Monolith & Node.js Services)
├── packages/        # 共用模組 (Shared Types, UI Components)
├── infrastructure/  # 基礎設施即程式碼 (IaC), Docker Compose
├── docs/            # 架構設計文件 (現有的 architecture, domain, security 等)
├── tests/           # 跨系統端到端 (E2E) 整合測試
└── .github/         # CI/CD Pipelines
```

## Branch Strategy (ADR-009)
採用 **Trunk Based Development**：
- **短生命週期功能分支：** `feature/*`, `bugfix/*`，壽命不超過 2~3 天。
- **Release 策略：** 由 `main` 分支拉出 `release/*` 用於佈署至 Staging/Prod。
- **PR 要求：** 必須遵循 Conventional Commits，通過自動化 CI 檢查並經由 Agent + Human 雙重 Review。

## Development Workflow
1. **Developer Workflow:**
   `Issue -> Planning -> Branch -> Implementation -> Testing -> Security Scan -> Pull Request -> Review -> Merge -> Deployment`
2. **AI Agent Workflow:**
   `Task Assignment -> Architecture Context Loading -> Implementation -> Self Review -> Automated Validation (CI) -> Security Review -> Human Approval`

## CI/CD Strategy (ADR-010)
採用 **GitHub Actions** 劃分三層 Pipeline：
- **CI:** 針對異動路徑 (Path Filter) 觸發 Build, Unit Test, Lint, Formatting。
- **Security Pipeline:** SAST 掃描、相依性漏洞檢測、Secret Detection (TruffleHog) 與容器映像檔掃描。
- **CD:** 建置 Docker Image 推送至 Registry，自動佈署至 Dev 環境；Staging 與 Prod 需設置 Deployment Approval Gate。

## Environment Strategy (ADR-011)
- **Local:** Docker Compose 供開發者與 Agent 快速啟動。
- **Development (Dev):** 整合測試用，資料庫為暫時性。
- **QA:** 壓力測試與功能測試用，導入脫敏 (Anonymized) 資料。
- **Staging:** 架構 1:1 複製 Prod，用於業務端 UAT。
- **Production (Prod):** 營運環境，最高權限隔離。
- **Disaster Recovery (DR):** 異地備援。
*註：跨環境 Configuration 必須依賴環境變數；Secret 必須完全交由 Cloud Secret Manager 解析。*

## AI Agent Workflow (ADR-012)
採用 **Hierarchical Agent Model**：
- **Master Orchestrator Agent** 統籌分發。
- **Architecture Agent** 載入並解釋 Context。
- **Backend/Frontend/Node Agent** 等專家進行實作，彼此邊界嚴格隔離。
- **QA/Security Agent** 負責自動化審查與攔截。

## Sprint Implementation Roadmap
實作將採用漸進式交付，絕不直接切入功能：
- **Sprint 0 - Foundation Setup:** 建置 Monorepo、CI/CD Pipelines、Docker 環境與程式碼規範。
- **Sprint 1 - Identity Foundation:** 串接 OIDC Provider，打通前端與後端 JWT Auth。
- **Sprint 2 - Backend Core Skeleton:** 建置 ASP.NET Core Clean Architecture 骨架。
- **Sprint 3 - Database Foundation:** EF Core Migrations 與 MySQL 連線設定。
- **Sprint 4 - Order Domain:** 實作核心訂單建立與查詢邏輯。
- **Sprint 5 - Inventory Domain:** 實作庫存扣除與樂觀鎖控制。
- **Sprint 6 - Event Infrastructure:** 建立 RabbitMQ 整合，打通 Order 與 Inventory 的 Domain Event。
- **Sprint 7 - Frontend Foundation:** 建置 Next.js 架構、共用 UI 元件與 API Client。
- **Sprint 8 - Admin Portal:** 建置後台 RBAC 管理與審核介面。
- **Sprint 9 - Security Hardening:** 實作 Audit Logging、Secret Manager 替換與進階 ABAC。
- **Sprint 10 - Production Readiness:** 壓力測試、監控系統 (Observability) 導入與 DR 演練。

## ADR List
- **ADR-008:** Repository Architecture Decision
- **ADR-009:** Branch Strategy Decision
- **ADR-010:** CI/CD Pipeline Decision
- **ADR-011:** Environment Strategy Decision
- **ADR-012:** AI Agent Development Workflow Decision

## Risks and Mitigation
- **Risk:** AI Agent 產生幻覺或不顧架構直接修改。
  - **Mitigation:** ADR-012 透過 Hierarchical Agent 模式與自動化 CI/Security Review 強制攔截未授權的修改。
- **Risk:** Monorepo 的 CI 建置時間過長。
  - **Mitigation:** ADR-010 嚴格設置 Path Filtering，並利用 GitHub Actions 內部快取機制加速。

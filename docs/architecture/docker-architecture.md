---
Document ID: DOC-ARC-009
Version: 1.0
Owner Agent: DevOps Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-001
---

# Docker Architecture

為了實現環境一致性與未來可無縫遷移至 Kubernetes，平台全面採用容器化 (Containerization) 部署。

## 1. Container Boundary
每一個可獨立部署的應用皆為獨立 Container：
- `frontend-customer` (Next.js)
- `frontend-admin` (Next.js)
- `dotnet-monolith` (ASP.NET Core)
- `node-search-service`
- `node-notification-service`
- `node-ai-service`
*(註：Database 與 Message Broker 於正式環境採用 Cloud Managed Services，開發環境才使用 Docker Compose)*

## 2. Environment Strategy
- `Base Image`: 使用輕量化且安全的官方基底映像檔 (如 `alpine` 或 `distroless`)。
- `.dockerignore`: 必須排除 `.git`, `node_modules`, `bin`, `obj` 與機敏憑證檔案，縮小 Image 體積並確保安全。
- 嚴格實踐 **Immutable Image Pattern**：一個 Git Commit 只打包一個 Image 標籤 (Tag)，該 Image 會依序晉升 (Promote) 通過 Dev -> Staging -> Prod 環境，禁止依據環境重複 Build。

## 3. Configuration Management
- 容器內部不存放任何特定環境的設定檔 (Environment-Specific Config)。
- 遵守 Twelve-Factor App 原則，所有隨環境改變的變數 (如 DB 連線字串、API 網址) 必須於 Runtime 時透過 **Environment Variables (環境變數)** 注入。

## 4. Secret Injection
- 機敏資料 (API Keys, Database Passwords, JWT Secrets) 絕對禁止寫入 Dockerfile 或原始碼。
- 運行時 (Runtime)，Secrets 將由外部 Secret Manager (如 AWS Secrets Manager / Azure Key Vault / HashiCorp Vault) 透過 Orchestrator (Docker Swarm / K8s) 安全地掛載為環境變數或記憶體檔案 (tmpfs) 供容器讀取。

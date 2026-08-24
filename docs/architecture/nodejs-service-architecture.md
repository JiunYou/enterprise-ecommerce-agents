---
Document ID: DOC-ARC-007
Version: 1.0
Owner Agent: Node.js Backend Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-001
---

# Node.js Service Architecture

外圍的獨立服務 (Independent Services) 採用 Node.js (TypeScript) 實作，利用其非同步 I/O 與豐富生態系來處理高併發與整合任務。

## 1. Service Boundary & Responsibility

### Search Service
- **職責:** 接手高流量的商品列表與全文檢索。
- **架構:** 輕量化 API，無狀態 (Stateless)。接收 HTTP 請求，轉化為 Elasticsearch Query DSL，並將結果格式化後返回。

### Notification Service
- **職責:** 集中管理寄信 (Email) 與簡訊 (SMS)。
- **架構:** Worker Service 模式為主。不提供對外 API，純粹監聽 Message Broker 中的 Notification Command 或 Events。

### AI Integration Service
- **職責:** 處理推薦演算邏輯與客服大語言模型串接。
- **架構:** API Service，整合 LangChain.js 或官方 SDK，維護與 OpenAI/Anthropic API 的串接，並將對話狀態 (Chat Context) 快取於 Redis。

## 2. Communication Pattern
- **與 Frontend:** 提供 RESTful API 或 GraphQL。
- **與 ASP.NET Core Monolith:**
  - **讀取/同步:** 透過 gRPC 或 REST API 從 Monolith 獲取基礎資料。
  - **非同步事件:** 透過訂閱 RabbitMQ 接收來自 Monolith 的 Domain Events (例如：Search 監聽 `ProductCreated` 進行索引更新)。

## 3. Event Consumer Strategy
- 實作 Idempotency (冪等性)：Node.js Consumer 在處理 Queue 訊息時，必須根據 MessageID 或 Business Key 檢查 Redis/DB，確保重複消費不會導致錯誤副作用 (如發出兩次 Email)。
- 實作 Dead Letter Queue (DLQ) 機制：處理失敗次數超過閾值的事件，需移至 DLQ 供後續人工排查。

## 4. Deployment Boundary
- 每個 Node.js Service 擁有獨立的 `package.json`、`Dockerfile` 與 CI/CD Pipeline。
- 由於其 Stateless 特性，這些服務可隨意水平擴展 (Horizontal Pod Autoscaling, HPA)，與 ASP.NET Core Monolith 的生命週期完全脫鉤。

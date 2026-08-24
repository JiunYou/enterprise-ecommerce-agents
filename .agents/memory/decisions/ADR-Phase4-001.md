# ADR-Phase4-001: C4 Architecture Boundary Decision

## Metadata
- **ADR ID:** ADR-Phase4-001
- **Status:** Proposed
- **Owner Agent:** System Architect Agent
- **Created Date:** 2026-08-24
- **Related ADR:** ADR-Phase2-001, ADR-001

## Context
在確立了 Hybrid Architecture (ADR-001) 之後，我們需要透過 C4 Model 定義系統的實體與邏輯邊界。我們必須決定外部相依系統的範圍 (System Context)、各種技術堆疊（如 .NET, Node.js, MySQL, Redis, Elasticsearch 等）在 Container 層級的職責，以及在 ASP.NET Core Monolith 內部的 Component 劃分。這確保團隊與 AI Agent 在後續實作時，有明確的邊界遵循，避免架構退化 (Architecture Degradation)。

## Decision
1. **Level 1 (System Context):** 確立系統對外邊界，將金流 (Payment)、物流 (Shipping)、通訊 (Email/SMS) 視為 External Systems，不將其業務邏輯實作於本地。
2. **Level 2 (Container):** 確立採用 Redis (因應購物車與 Session 的極高吞吐)、RabbitMQ (因應核心 Domain Events 解耦)、Elasticsearch (因應商品檢索效能) 與 Object Storage 作為輔助基礎設施。
3. **Level 3 (Component):** 
   - 將 **ASP.NET Core Monolith** 內部劃分為：Identity, Customer, Catalog, Cart, Order, Inventory, Marketing, Administration 8 大元件，元件間禁止直接跨邊界寫入資料庫，只能透過內部 In-Process API 或 Event 溝通。
   - 將 **Node.js Services** 劃分為 Search, Notification, AI Integration 等邊界，專注於高效能讀取或非同步整合任務。

## Reason
- 引入 Message Broker (RabbitMQ) 是為了完美落實 Phase 2 的 `OrderPlaced`, `InventoryReserved` 等 Domain Events，確保 Order 與 Inventory 之間的非同步解耦與容錯能力。
- 引入 Search Engine (Elasticsearch) 是為了解決關聯式資料庫在複雜商品過濾 (Faceted Search) 時的效能瓶頸。
- Node.js 非常適合 I/O 密集型工作，將 Search (讀取 Elasticsearch) 與 Notification 部署於 Node.js 服務，可充分發揮其技術優勢，保護 .NET 核心不受外圍突發流量衝擊。

## Impact
- **技術複雜度增加：** 團隊必須維護 Elasticsearch 與 RabbitMQ，這增加了 DevOps (Phase 6) 的基礎設施即程式碼 (IaC) 的負擔。
- **開發紀律要求：** ASP.NET Core 內部的 Component 劃分完全依賴開發紀律 (例如避免跨 Namespace 直接 Query DB)。後續必須透過 Roslyn Analyzers 或 ArchUnitNET 進行架構依賴檢查。

## Future Evolution
若 Node.js 的 Search Service 負載極大，它可以輕易地在 Kubernetes 中進行 Horizontal Pod Autoscaling (HPA)，而不用跟著龐大的 ASP.NET Core 單體一起擴充資源。若未來行銷活動極度複雜，ASP.NET Core 內的 Marketing Component 也可以抽出成為獨立的微服務。

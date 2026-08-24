---
Document ID: DOC-ARC-001
Version: 1.0
Owner Agent: System Architect Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-001
---

# Architecture Style Analysis

## 1. Modular Monolith Analysis
- **優勢：**
  - 開發初期速度極快，單一程式碼庫 (Monorepo/Single Solution) 容易管理。
  - 沒有分散式系統的網路延遲與分散式交易 (Distributed Transaction) 複雜度問題。
  - 重構容易，修改介面與型別安全可由編譯器保證。
  - 基礎設施要求低，部署與監控單純。
- **限制：**
  - 當系統規模擴大、開發團隊增多時，可能面臨 Merge Conflict 與佈署塞車。
  - 只能統一擴展 (Scale as a whole)，無法針對高負載模組（如購物車、商品搜尋）獨立擴充資源。
  - 技術堆疊被綁死單一語言 (如全 .NET)。
- **適用情境：** 中小型團隊、新創初期、業務邏輯高度耦合的情境。
- **與目前 DDD Boundary 相容性：** 可以在單一應用程式內透過 Folder / Project 來劃分 Bounded Context，符合邏輯隔離原則，但缺乏實體隔離與獨立部署能力。
- **未來演進方式：** 當模組內部凝聚力高且邊界清晰時，未來可輕易將特定模組抽取為獨立的 Microservice。

## 2. Microservices Analysis
- **優勢：**
  - 強大的獨立部署與獨立擴容能力，極致的資源隔離。
  - 技術異質性：各服務可選擇最適合的語言與資料庫。
  - 團隊解耦：多團隊可平行開發，不受其他團隊發布週期的影響。
- **限制：**
  - 分散式系統複雜度極高：需處理網路錯誤、重試機制、Eventual Consistency。
  - Infrastructure Requirement 高：需要 Kubernetes、Service Mesh、API Gateway 等。
  - Operational Impact 大：日誌追蹤 (Distributed Tracing)、監控難度倍增，維運門檻極高。
  - 初期開發速度慢，需建立大量自動化 CI/CD 與基礎腳手架。
- **適用情境：** 大型企業、百人以上開發團隊、極高併發且資源消耗極端不均的系統。
- **DDD Compatibility：** 完美對應 Bounded Context，每個 Microservice 封裝一個 Bounded Context，達到實體層級的強隔離。

## 3. Hybrid Architecture Analysis
- **可能架構：**
  - **Core Business (核心業務):** 使用 ASP.NET Core 實作 Modular Monolith，負責處理包含 Order, Inventory, Identity 等強一致性要求的 Bounded Context。
  - **Independent Services (獨立服務):** 使用 Node.js 實作輕量、高 IO 或特化服務，例如 Catalog Search, Cart, Notification, AI Integration, Reporting 等。
- **優勢：**
  - 兼顧開發速度與特定場景的效能擴展性。
  - 核心業務保留了事務強一致性與低重構成本；高變動或需特殊技術棧的服務則享受微服務的靈活性。
  - **AI Agent 開發模型友好：** AI Agent 可以輕易接手單一明確的 Node.js 獨立服務（如建立一個 Notification Service），而不必在一開始就牽涉龐大的核心單體系統。
- **限制：**
  - 仍需引入一定程度的分散式治理與基礎設施 (如 Event Broker)。
  - 系統邊界需非常明確，需避免跨單體與微服務的同步 API 呼叫煉獄。
- **適用情境：** 具備明確核心交易流程，但周邊服務擴充性與創新需求極高的現代企業平台。

## 4. Architecture Recommendation

**Decision: Hybrid Architecture**

**Decision Reason:**
- **Business Reason:** 企業平台需快速驗證核心交易模型（適合單體），同時又必須具備未來無縫擴展進階功能與快速創新（如 AI 推薦、搜尋）的業務彈性。
- **Technical Reason:** Phase 1 專案背景已明確定義了多種技術堆疊 (ASP.NET Core 與 Node.js)。將複雜且需嚴格 ACID 事務的 Order, Inventory 放入 ASP.NET Core Modular Monolith 確保安全與可靠性；將 Cart, Search, API Gateway 實作為 Node.js，發揮其高併發非同步 I/O 與輕量化優勢。
- **Operational Reason:** 降低了純微服務架構初期的維運地獄，同時避免了純單體架構未來無法針對特定模組（如雙 11 大促時的購物車與搜尋）獨立擴容的效能瓶頸。

**Rejected Alternatives:**
- **Option A (Modular Monolith Only):** 無法滿足專案中已明訂的 Node.js 技術堆疊需求，且單體架構難以因應電商平台在 Catalog / Cart 上局部且劇烈的超高負載，也限制了單獨交由 AI Agent 開發輕量服務的可能。
- **Option B (Microservices Only):** 初期過度設計 (Over-engineering)。分散式交易（如訂單與庫存的交互）會嚴重拖垮初期開發速度，提升專案失敗風險，且初期業務規模並不需要如此細粒度的實體隔離。

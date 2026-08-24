---
Document ID: DOC-DOM-003
Version: 1.0
Owner Agent: Domain Architect Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-Phase1-001
---

# Core Domain Analysis

## Domain Classification Matrix

| Domain | Type | Reason (Business Differentiation, Complexity, Revenue Impact) |
|---|---|---|
| **Order Domain** | **Core** | 處理交易轉換與履約，邏輯高度複雜，直接影響企業營收。客製化的訂單流程（如預購、分期）是電商競爭力核心。 |
| **Marketing Domain** | **Core** | 促銷與優惠規則引擎高度客製化，是吸引客戶、提升客單價與品牌差異化的關鍵競爭優勢。 |
| **Catalog Domain** | **Supporting** | 商品展示為電商必備功能，但商品資料結構通常具有共通性，非主要差異化來源。支援 Order 與 Marketing 的運作。 |
| **Inventory Domain** | **Supporting** | 控制存貨數量，確保履約順利。邏輯雖然重要且對一致性要求高，但在商業上屬於後勤支援性質。 |
| **Identity & Access Domain** | **Generic** | 使用者認證與授權為所有現代系統的基礎需求，可直接採用標準化方案（如 OAuth2, OpenID Connect），不構成商業護城河。 |
| **Analytics Domain** | **Generic / Supporting** | 報表與數據分析雖然對決策有幫助，但在初期通常可透過現成 BI 工具或標準化彙整完成。 |

---
**Assumption (未確認假設):**
- 將 Marketing 歸類為 Core 是基於現代電商在優惠券、促銷策略上經常需要高度客製化的假設。
- **Business Question:** 我們的平台是否計畫在行銷玩法上有獨特的創新？若僅是基本發放折價券，則應降級為 Supporting Domain。

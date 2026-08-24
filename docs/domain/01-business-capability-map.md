---
Document ID: DOC-DOM-001
Version: 1.0
Owner Agent: Product Manager Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-Phase1-001
---

# Business Capability Map

## 1. Enterprise Commerce Platform Capability Tree

```text
Enterprise Commerce Platform
├── Customer Experience
│   ├── Product Discovery (商品瀏覽、搜尋)
│   ├── Shopping Cart Management (購物車)
│   └── Customer Interaction (評論、收藏)
├── Product Commerce
│   ├── Catalog Management (商品管理)
│   └── Inventory Control (庫存管理)
├── Order Fulfillment
│   ├── Order Processing (訂單建立與管理)
│   └── Fulfillment Management (履約追蹤)
├── Customer Management
│   ├── Identity Management (會員註冊、登入)
│   ├── Profile Management (會員資料維護)
│   └── Access Control (使用者與權限管理)
├── Marketing
│   └── Promotion Management (優惠券發放與核銷)
└── Administration
    ├── Platform Analytics (報表生成)
    └── Security & Audit (稽核日誌紀錄)
```

## 2. Capability Details

### Core Capability (核心能力 - 直接創造商業價值)

| Capability | Description | Business Value | Complexity | Priority |
| :--- | :--- | :--- | :--- | :--- |
| **Order Processing** | 處理客戶下單、付款狀態追蹤與訂單生命週期管理。 | 平台營收直接來源，攸關轉換率。 | High | Critical |
| **Promotion Management** | 管理行銷活動、優惠券的發放與折扣計算。 | 刺激銷售，提升客單價與回購率。 | High | High |
| **Product Discovery** | 提供快速且精準的商品搜尋與瀏覽體驗。 | 直接影響客戶購買決策與停留時間。 | Medium | High |

### Supporting Capability (支援能力 - 支援核心商業流程)

| Capability | Description | Business Value | Complexity | Priority |
| :--- | :--- | :--- | :--- | :--- |
| **Catalog Management** | 維護商品基本資料、規格、圖片與上下架狀態。 | 提供核心商品數據，確保資訊正確性。 | Medium | High |
| **Inventory Control** | 監控商品可用庫存，防止超賣。 | 確保履約能力，維持客戶信任度。 | High | Critical |
| **Customer Interaction** | 收集並展示用戶評論、管理收藏清單。 | 提升用戶參與度與商品可信度。 | Low | Medium |

### Generic Capability (通用能力 - 非電商專屬之基礎設施)

| Capability | Description | Business Value | Complexity | Priority |
| :--- | :--- | :--- | :--- | :--- |
| **Identity Management** | 處理使用者認證與授權。 | 確保系統安全性與個人化基礎。 | Medium | High |
| **Access Control** | 管理後台操作人員的權限配置 (RBAC)。 | 符合資安與內部控管規範。 | Medium | Medium |
| **Platform Analytics** | 彙整交易與營運數據產出報表。 | 支援商業決策。 | Medium | Medium |
| **Security & Audit** | 記錄系統操作與交易日誌。 | 滿足合規性與事後追溯需求。 | Low | Low |

---
**Assumption (未確認假設):**
- 目前未包含金流 (Payment) 與物流 (Shipping) 外部整合的詳細 Capability，假設初期採第三方 API 串接。
- **Business Question:** 是否需要發展自有金流錢包或複雜的逆物流 (退換貨) 管理？

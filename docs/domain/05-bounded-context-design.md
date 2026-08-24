---
Document ID: DOC-DOM-005
Version: 1.0
Owner Agent: Domain Architect Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-Phase2-001
---

# Bounded Context Design

基於 Phase 1 的業務領域分析與 DDD 原則，我們重新審視了潛在的 Context，並將職責邊界進行了整併與拆分：

## 1. Catalog Context
- **Context Name:** Catalog Context
- **Purpose:** 提供商品目錄、分類與搜尋功能。
- **Business Capability:** Product Discovery, Catalog Management。
- **Core Responsibility:** 維護商品基本資料、價格快照、規格，確保前端高併發瀏覽體驗。
- **Owned Model:** Product, Category, Price, Attribute。
- **Excluded Responsibility:** 實體庫存數量（屬於 Inventory Context）。
- **Dependencies:** None (上游)。

## 2. Order Context
- **Context Name:** Order Context
- **Purpose:** 管理客戶的購買承諾與履約狀態。
- **Business Capability:** Order Processing, Fulfillment Tracking。
- **Core Responsibility:** 訂單建立、狀態流轉、退換貨處理。
- **Owned Model:** Order, OrderItem, ShippingAddress。
- **Excluded Responsibility:** 購物車管理（拆分至 Cart Context）、金流串接（拆分至 Payment Context）。
- **Dependencies:** Cart Context (轉化), Catalog Context (商品快照), Inventory Context (扣減庫存), Payment Context (付款狀態)。

## 3. Cart Context (拆分設計)
- **Context Name:** Cart Context
- **Purpose:** 暫存與管理消費者的購買意圖。
- **Business Capability:** Shopping Cart Management。
- **Core Responsibility:** 購物車項目增刪、即時促銷折扣試算。
- **Owned Model:** Cart, CartItem。
- **Excluded Responsibility:** 最終交易狀態機。
- **Dependencies:** Catalog Context (即時資訊), Marketing Context (優惠套用)。
- *(註：將 Cart 從 Order 獨立，因為 Cart 的生命週期極短、寫入極端頻繁且丟失容忍度高，與 Order 強一致性要求不同)*

## 4. Inventory Context
- **Context Name:** Inventory Context
- **Purpose:** 防超賣並精準管理可用庫存。
- **Business Capability:** Inventory Control。
- **Core Responsibility:** 可用庫存計算、預留庫存 (Reservation)、實際扣減與回補。
- **Owned Model:** Stock, Reservation, Warehouse。
- **Excluded Responsibility:** 商品描述與定價。
- **Dependencies:** 無直接同步依賴，主要接收來自 Order Context 的非同步事件。

## 5. Payment Context (新增)
- **Context Name:** Payment Context
- **Purpose:** 處理外部金流串接與對帳。
- **Business Capability:** Payment Processing。
- **Core Responsibility:** 建立付款連結、接收 Webhook、管理付款狀態 (Authorized, Captured, Refunded)。
- **Owned Model:** PaymentTransaction, Refund。
- **Excluded Responsibility:** 訂單自身的狀態機推演。
- **Dependencies:** Order Context (提供交易基本資訊)。

## 6. Marketing Context (合併設計)
- **Context Name:** Marketing Context
- **Purpose:** 提供促銷玩法與用戶互動以刺激銷售。
- **Business Capability:** Promotion Management, Customer Interaction。
- **Core Responsibility:** 優惠券派發與核銷邏輯、滿額贈、商品評價 (Review)。
- **Owned Model:** Coupon, PromotionRule, Review。
- **Excluded Responsibility:** 訂單金額的最終扣款記錄。
- **Dependencies:** Catalog Context (指定商品優惠), Identity Context (特定用戶優惠)。
- *(註：將 Review 合併入 Marketing 中，作為 Engagement 用戶參與度機制的一部分)*

## 7. Identity Context (通用整合)
- **Context Name:** Identity Context
- **Purpose:** 提供全平台統一的身份認證與基礎授權。
- **Business Capability:** Identity & Access Management。
- **Core Responsibility:** 用戶註冊、登入 Token 核發、權限控制 (RBAC)、基礎 Profile。
- **Owned Model:** User, Role, Session。
- **Excluded Responsibility:** 針對電商特化的數據（如收件地址直接交由 Order 管理，確保歷史快照一致性）。
- **Dependencies:** None。

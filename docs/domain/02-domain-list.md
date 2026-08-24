---
Document ID: DOC-DOM-002
Version: 1.0
Owner Agent: Domain Architect Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-Phase1-001
---

# Domain List

## 1. Order Domain
- **Domain Name:** Order Domain
- **Purpose:** 管理客戶的購買意圖與履約過程，確保營收順利轉換。
- **Business Responsibility:** 購物車管理、訂單成立、狀態機推進（付款、出貨、完成）。
- **Primary Actor:** 消費者 (Customer)、系統管理員 (Admin)。
- **Main Business Rules:**
  - 訂單成立前必須鎖定或確認庫存。
  - 購物車內的商品價格以結帳當下為準。
- **Potential Bounded Context:** Order Context

## 2. Catalog Domain
- **Domain Name:** Catalog Domain
- **Purpose:** 組織與展示商品資訊，最大化商品曝光與客戶搜尋體驗。
- **Business Responsibility:** 商品 CRUD、分類管理、搜尋與過濾。
- **Primary Actor:** 商店管理者 (Merchant/Admin)、消費者 (Customer)。
- **Main Business Rules:**
  - 下架商品不可被搜尋或加入購物車。
- **Potential Bounded Context:** Catalog Context

## 3. Inventory Domain
- **Domain Name:** Inventory Domain
- **Purpose:** 精確控制實體或虛擬資產數量，避免超賣並提升資金週轉率。
- **Business Responsibility:** 庫存扣減、回補、保留與低水位警告。
- **Primary Actor:** 倉管人員 (Warehouse Staff)、系統管理員 (Admin)。
- **Main Business Rules:**
  - 庫存數量不可為負數。
  - 訂單成立即保留庫存，超時未付款則釋放。
- **Potential Bounded Context:** Inventory Context

## 4. Marketing Domain
- **Domain Name:** Marketing Domain
- **Purpose:** 透過促銷手段與互動機制提升客單價與回購率。
- **Business Responsibility:** 優惠券規則引擎、收藏清單管理、評論收集。
- **Primary Actor:** 行銷人員 (Marketer)、消費者 (Customer)。
- **Main Business Rules:**
  - 優惠券不能疊加使用（除非特定規則允許）。
  - 僅有實際購買過該商品的會員可以留下評論。
- **Potential Bounded Context:** Marketing Context

## 5. Identity & Access Domain
- **Domain Name:** Identity & Access Domain
- **Purpose:** 管理系統參與者的身份與授權，確保資訊安全。
- **Business Responsibility:** 註冊、登入 (SSO/JWT)、RBAC 權限管理、基本資料維護。
- **Primary Actor:** 所有使用者 (All Users)。
- **Main Business Rules:**
  - 敏感操作需具備對應角色的權限。
- **Potential Bounded Context:** Identity Context

## 6. Analytics Domain
- **Domain Name:** Analytics Domain
- **Purpose:** 提供商業決策所需的數據支撐。
- **Business Responsibility:** 營業額報表、訂單分析。
- **Primary Actor:** 決策層 (Executive)、營運人員 (Operator)。
- **Main Business Rules:**
  - 報表數據需符合特定的時間維度與權限隔離。
- **Potential Bounded Context:** Analytics Context

---
**Assumption (未確認假設):**
- 評價與收藏暫時歸類在 Marketing Domain 中，因為兩者皆用於提升轉換率。
- **Business Question:** 「評論」是否有可能未來擴充成一個龐大的社群互動功能？如果是，可能需要獨立成 Engagement Domain。

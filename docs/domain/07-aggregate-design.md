---
Document ID: DOC-DOM-007
Version: 1.0
Owner Agent: Domain Architect Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-Phase2-001
---

# Aggregate Design

針對核心與關鍵支援領域，我們規劃以下 Aggregate 邊界，以確保資料的一致性與交易隔離。

## 1. Order Context: Order Aggregate

- **Aggregate Name:** Order
- **Purpose:** 管理單次交易的完整生命週期（從建立、付款、出貨到完成）。
- **Aggregate Root:** `Order`
- **Entities:** `OrderItem`, `PaymentRecord`
- **Value Objects:** `Money` (總金額), `Address` (收件地址快照), `OrderStatus` (狀態機)
- **Invariant Rules:**
  - `Order` 的總金額必須等於所有 `OrderItem` 的單價乘數量，減去折扣金額。
  - `Order` 在狀態變更為 `Shipped` 之前，必須確保已經獲得 `Paid` 狀態。
  - 訂單一經建立不可刪除，只能進行取消 (`Cancelled`) 或退款狀態流轉。
- **Consistency Boundary:** 訂單及其底下的所有項目與收件資訊同屬一個事務邊界，任何狀態的變更或項目異動都必須經由 Aggregate Root (`Order`) 進行驗證。

## 2. Catalog Context: Product Aggregate

- **Aggregate Name:** Product
- **Purpose:** 管理單一商品的基本展示資訊與銷售規格生命週期。
- **Aggregate Root:** `Product`
- **Entities:** `ProductVariant` (商品規格，如顏色/尺寸)
- **Value Objects:** `Money` (售價), `Dimensions` (長寬高), `ProductStatus` (上/下架)
- **Invariant Rules:**
  - 每一個 `ProductVariant` 必須擁有獨一無二的 SKU 識別碼。
  - `Product` 狀態若設為下架，所有的 `ProductVariant` 必須自動失去可銷售資格。
- **Consistency Boundary:** 變更商品價格或規格，需針對整個 `Product` 進行讀取與修改，確保前端獲取資訊的完整性。

## 3. Inventory Context: Stock Aggregate

- **Aggregate Name:** Stock
- **Purpose:** 高效控制單一 SKU 的庫存數量，絕對防止超賣。
- **Aggregate Root:** `Stock` (以單一 SKU 為單位)
- **Entities:** `Reservation` (庫存預留紀錄)
- **Value Objects:** `Quantity` (現有量/預留量/可用量)
- **Invariant Rules:**
  - 絕對不變條件：`可用量 (Available) = 現有量 (On-Hand) - 預留量 (Reserved)`。
  - `Available Quantity` 絕對不允許小於 0。
  - 每個 `Reservation` 具有過期時間（如 15 分鐘未付款），超過必須釋放。
- **Consistency Boundary:** 一個 SKU 的庫存扣減、回補必須針對單一 `Stock` Aggregate 進行原子操作 (Atomic Operation) 或樂觀鎖防護。

## 4. Identity Context: User Aggregate

- **Aggregate Name:** User
- **Purpose:** 管理用戶驗證資訊與系統權限狀態。
- **Aggregate Root:** `User`
- **Entities:** `UserRole`
- **Value Objects:** `EmailAddress`, `PasswordHash`, `UserStatus` (活躍/鎖定)
- **Invariant Rules:**
  - `EmailAddress` 在系統中必須保證唯一。
  - 若 `User` 被標記為鎖定 (Locked)，必須在領域層面拒絕任何驗證請求。
- **Consistency Boundary:** 權限指派與密碼修改應對 `User` Root 進行保護與狀態管理。

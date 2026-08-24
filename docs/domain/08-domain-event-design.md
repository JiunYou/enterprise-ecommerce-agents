---
Document ID: DOC-DOM-008
Version: 1.0
Owner Agent: Domain Architect Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-Phase2-001
---

# Domain Event Design

為了實踐 Bounded Context 之間的解耦，並支持 Event-Driven Architecture，我們定義了以下關鍵的 Domain Events。

## 1. 訂單相關事件 (Order Events)

### `OrderPlaced`
- **Producer Context:** Order Context
- **Consumer Context:** Inventory Context, Payment Context
- **Business Meaning:** 消費者確認送出訂單，系統完成初步訂單建立。
- **Payload/Content:** OrderID, CustomerID, LineItems (SKU, Quantity), TotalAmount。

### `OrderPaid`
- **Producer Context:** Order Context
- **Consumer Context:** Inventory Context (確認實際扣減庫存), Marketing Context (核銷優惠券)
- **Business Meaning:** 訂單已成功完成付款流程。
- **Payload/Content:** OrderID, PaymentID, AmountPaid, PaidAt。

### `OrderCancelled`
- **Producer Context:** Order Context
- **Consumer Context:** Inventory Context (釋放預留庫存)
- **Business Meaning:** 訂單因超時未付款，或由客戶主動取消而終止。
- **Payload/Content:** OrderID, ReasonCode。

## 2. 庫存相關事件 (Inventory Events)

### `InventoryReserved`
- **Producer Context:** Inventory Context
- **Consumer Context:** Order Context
- **Business Meaning:** 庫存已成功為指定的訂單保留。
- **Payload/Content:** OrderID, ReservedSKUs, ReservationExpiry。

### `InventoryReservationFailed`
- **Producer Context:** Inventory Context
- **Consumer Context:** Order Context
- **Business Meaning:** 庫存不足，保留失敗（Order Context 收到此事件後，應觸發訂單自動取消補償機制）。
- **Payload/Content:** OrderID, FailedSKUs。

## 3. 金流相關事件 (Payment Events)

### `PaymentAuthorized`
- **Producer Context:** Payment Context
- **Consumer Context:** Order Context
- **Business Meaning:** 外部金流網關回報付款授權成功。
- **Payload/Content:** PaymentID, OrderID, ProviderReference, Amount。

## 4. 行銷相關事件 (Marketing Events)

### `CouponRedeemed`
- **Producer Context:** Marketing Context
- **Consumer Context:** Analytics Context
- **Business Meaning:** 某張行銷優惠券已被正式核銷使用。
- **Payload/Content:** CouponCode, OrderID, CustomerID, DiscountAmount。

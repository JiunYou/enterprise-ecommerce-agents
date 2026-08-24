---
Document ID: DOC-DOM-006
Version: 1.0
Owner Agent: Domain Architect Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-Phase2-001
---

# Context Map

## 1. Context Relationship Description

1. **Order Context ↔ Catalog Context**
   - **Type:** Conformist (遵從者)
   - **Description:** Order Context 需要 Catalog 的商品資訊（如名稱、價格），但 Order 不會強制要求 Catalog 改變其模型。Order Context 查詢 Catalog 的 API 來記錄商品快照。

2. **Order Context ↔ Inventory Context**
   - **Type:** Customer/Supplier (Order 為 Customer)
   - **Description:** 訂單成立時必須向 Inventory Context 請求庫存預留。為確保非同步解耦且能因應高併發，兩者之間透過 Domain Events (Event-Driven) 進行溝通。

3. **Cart Context ↔ Marketing Context**
   - **Type:** Customer/Supplier (Cart 為 Customer)
   - **Description:** 購物車結算金額需要呼叫 Marketing Context 進行優惠計算，Marketing 提供 API 供 Cart 即時呼叫。

4. **Payment Context ↔ Order Context**
   - **Type:** Anti-Corruption Layer (ACL)
   - **Description:** Payment Context 封裝了外部第三方支付網關的複雜性，扮演 ACL 的角色。Order Context 不需要知道外部金流的實作細節，只需監聽 Payment Context 發出的 `PaymentAuthorized` 事件即可進行狀態轉換。

5. **Identity Context ↔ All Other Contexts**
   - **Type:** Published Language
   - **Description:** Identity 產生的 JWT Token 包含用戶身份資訊。其他所有 Context 皆作為下游，遵循此 Published Language 來識別用戶身份，不需直接耦合 Identity 的 Database。

## 2. Context Map Diagram (C4 Level)

```mermaid
graph TD
    %% Generic Domains
    Identity[Identity Context<br/>Generic] 
    Payment[Payment Context<br/>Generic / ACL]
    
    %% Supporting Domains
    Catalog[Catalog Context<br/>Supporting]
    Cart[Cart Context<br/>Supporting]
    
    %% Core Domains
    Order[Order Context<br/>Core]
    Marketing[Marketing Context<br/>Core]
    Inventory[Inventory Context<br/>Supporting/Core]

    %% Relationships
    Identity -->|Published Language| Catalog
    Identity -->|Published Language| Order
    Identity -->|Published Language| Cart
    Identity -->|Published Language| Marketing
    
    Catalog -->|Conformist| Order
    Catalog -->|Conformist| Cart
    
    Marketing -->|API (Supplier)| Cart
    
    Order -->|Domain Events| Inventory
    Order -.->|Event/Command| Payment
    Payment -.->|Domain Events| Order
```

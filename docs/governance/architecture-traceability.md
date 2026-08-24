---
Document ID: DOC-GOV-001
Version: 1.0
Owner Agent: Compliance Agent
Created Date: 2026-08-24
---

# Architecture Traceability Matrix

本矩陣確保系統所有技術決策與元件設計，皆能追溯至最初的業務需求，並受到妥善的安全與架構管控。

## Traceability Chain
`Requirement -> Business Capability -> Domain -> Bounded Context -> Component -> Security Control -> ADR`

| Requirement | Business Capability | Domain | Bounded Context | Component | Security Control | Related ADR |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **安全會員註冊與登入** | Account Management | Identity (Supporting) | Identity Context | ASP.NET Identity Module | OIDC, JWT in HttpOnly Cookie, Rate Limiting | ADR-001, ADR-Security-001 |
| **商品瀏覽與高併發搜尋** | Product Discovery | Catalog (Supporting) | Catalog Context | Node.js Search Service (Elasticsearch) | Read-Only API, API Gateway Rate Limit | ADR-Phase2-001, ADR-001 |
| **購物車與結帳處理** | Checkout Process | Order (Core) | Order Context | ASP.NET Order Module | HTTPS/TLS 1.2+, WAF, ABAC | ADR-Phase1-001, ADR-Technical-001 |
| **確保扣庫存不超賣** | Inventory Tracking | Inventory (Core) | Inventory Context | ASP.NET Inventory Module | Optimistic Concurrency (RowVersion) | ADR-Phase2-001 |
| **第三方金流串接** | Payment Processing | Payment (Generic) | Payment Context | Node.js / External Provider | Webhook HMAC Signature Validation, Secrets in Secret Manager | ADR-Phase4-001, ADR-Security-002 |
| **信件與簡訊通知** | Customer Communication | Notification (Generic) | Notification Context | Node.js Notification Service | Queue Consumer Idempotency, DLQ | ADR-001 |
| **後台權限管理與操作紀錄** | Platform Administration | Administration (Supporting) | Admin Context | ASP.NET Admin Module | MFA, RBAC, Append-Only Audit Log | ADR-Security-001 |

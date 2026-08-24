---
Document ID: DOC-ARC-010
Version: 1.0
Owner Agent: System Architect Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-001
---

# REST API Architecture

為確保全平台各項微服務與模組間的通訊一致性，所有公開或內部 API 必須遵循以下 RESTful API 設計與治理規範。

## 1. Versioning
- 必須在 URL 路徑中明確標示版本號（例如 `/api/v1/orders`）。
- 任何破壞性變更 (Breaking Changes) 必須升級主版本號 (如 `v2`)，並保證前一版本具備一定的向後相容過渡期 (Deprecation Period)。

## 2. Resource Naming
- 採用標準的集合與單數名詞結構表示 Resource (如 `GET /users`, `GET /users/{id}`)。
- 嚴格禁止在 URI 中加入動詞（避免 `/api/v1/getOrders`），行為應由 HTTP Method 決定。
- 資源巢狀結構至多兩層 (如 `/users/{id}/orders`)，以維持 API 簡潔與可擴展性。

## 3. HTTP Convention
- **GET:** 讀取資源，必須確保冪等性 (Idempotent) 與安全性。
- **POST:** 建立新資源，或執行不具備冪等性的複雜領域操作 (Command)。
- **PUT:** 完整替換資源 (Idempotent)。
- **PATCH:** 局部更新資源 (通常搭配 JSON Patch 格式)。
- **DELETE:** 標記刪除或實體刪除資源 (Idempotent)。

## 4. Error Model
- 統一採用 RFC 7807 (Problem Details for HTTP APIs) 標準回傳錯誤資訊。
- 必須包含欄位：`type` (錯誤文檔連結), `title` (簡短說明), `status` (HTTP Status Code), `detail` (詳細原因描述), 允許擴充欄位 (如 Validation Errors 清單)。

## 5. Pagination
- 對於會回傳集合的端點，必須強制實作分頁 (Pagination)。
- 採用 Cursor-based Pagination (游標分頁) 優先於 Offset-based Pagination，以確保在大數據量查詢時的高效能與資料一致性 (不會因中途刪增資料而跳頁)。

## 6. Authentication & Authorization
- **Authentication:** 統一透過 `Authorization: Bearer {JWT}` Header 傳遞身份。
- **Authorization:** 遵循 API Gateway 或 Middleware 進行角色與權限邊界攔截。對於敏感操作 (如修改他人訂單)，需在 API 邏輯層進階驗證資源擁有者 (Resource Owner Validation)。

## 7. Rate Limiting
- 在 API Gateway 層級強制套用速率限制 (Rate Limiting)，以防止 DDoS 攻擊與暴力破解。
- 當超出限制時，必須回傳 HTTP `429 Too Many Requests`，並在 Response Header 中加入 `Retry-After` 指示。

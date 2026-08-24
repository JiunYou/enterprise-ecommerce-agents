---
Document ID: DOC-SEC-006
Version: 1.0
Owner Agent: Security Review Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: None
---

# Audit Log Architecture

為了滿足法遵 (Compliance) 與異常追查需求，所有關鍵業務操作必須記錄至不可篡改的 Audit Log。

## 1. Audit Log Strategy
日誌內容必須確保 5W (Who, When, What, Where, Result) 完整：
- **Who:** 觸發動作的 UserID、Role 與 SessionID。
- **When:** 動作發生的精確時間戳記 (UTC)。
- **What:** 執行的具體操作 (Action Name) 與前後資料差異 (Data Payload / Delta)。
- **Where:** 來源 IP Address、User Agent 與目標資源 ID。
- **Result:** 操作成功或失敗的結果代碼。

## 2. Key Audit Events Analysis
- **Admin Action:** 記錄 Admin 對系統配置的所有修改，防範內部威脅 (Insider Threat)。
- **Order Modification:** 記錄退款 (Refund) 或訂單取消操作，包含執行人員與金額。
- **Permission Change:** 記錄對 `UserRole` 的指派與移除。
- **Security Event:** 記錄連續登入失敗、密碼重置與 403 Forbidden 嘗試。

## 3. Storage & Integrity
Audit Log 不應混在一般 Application Log 之中，必須寫入專屬的 Append-Only Database (或直接送入 SIEM)，並設定較長的資料保留期限 (Retention Policy)。

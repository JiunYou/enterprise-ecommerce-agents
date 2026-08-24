---
Document ID: DOC-SEC-001
Version: 1.0
Owner Agent: Security Review Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-Security-001, ADR-Security-002
---

# STRIDE Threat Model

本文件針對 Enterprise E-Commerce Platform (Hybrid Architecture) 進行 STRIDE 威脅建模分析，以定義各元件的 Trust Boundary 與攻擊面。

## 1. Trust Boundaries & Data Flow
- **Public Zone (Untrusted):** 網際網路、Customer / Admin Web Browser。
- **DMZ / API Gateway (Semi-Trusted):** 負責 HTTPS 終止、WAF、Rate Limiting、Token Validation。
- **Application Zone (Trusted):** ASP.NET Core Monolith, Node.js Services, Message Broker (RabbitMQ)。
- **Data Zone (Highly Trusted):** MySQL Database, Redis Cache。
- **External Dependencies:** Payment Provider, Shipping, Email/SMS API。

## 2. STRIDE Analysis

| 威脅面向 | 潛在攻擊情境 (Attack Surface) | 緩解措施 (Mitigation) |
| :--- | :--- | :--- |
| **S**poofing (欺騙) | 攻擊者偽造 Customer 或 Admin 身份存取 Web App 或直接呼叫 API Gateway。 | 強制雙因素認證 (MFA)，API 端點全面檢驗 JWT 簽章，並設定 Token 較短時效 (Short-lived Token)。 |
| **T**ampering (竄改) | 中間人攻擊竄改購物車金額；竄改傳遞給 Payment Provider 的回呼 (Webhook) Payload。 | 全程強制 HTTPS (HSTS)，金流 Webhook 必須校驗 Payload Signature (HMAC)。 |
| **R**epudiation (否認) | Admin 否認刪除訂單；Customer 否認建立高額訂單。 | 導入具備不可否認性 (Non-Repudiation) 的 Audit Log 系統，記錄詳細的 Who/When/What。 |
| **I**nformation Disclosure (資訊洩漏) | 資料庫備份外流；日誌 (Logs) 中不小心記錄了明碼信用卡號或 PII 資料。 | MySQL 儲存級加密 (TDE)，密碼使用 Argon2id 雜湊，實作 Logging Data Redaction 遮蔽 PII 與 Secret。 |
| **D**enial of Service (阻斷服務) | 惡意機器人大量呼叫 Node.js Search Service 導致資料庫 CPU 耗盡。 | API Gateway 實作 Rate Limiting 與 IP Blacklisting；Search 依賴 Elasticsearch 而非直接存取 DB。 |
| **E**levation of Privilege (權限提升) | 攻擊者利用越權漏洞，將 Customer JWT 提升為 Admin 權限操作 ASP.NET Core。 | 實作嚴謹的 Role-Based Access Control (RBAC) 與 Resource-level 驗證 (ABAC)，禁止直接以 Client 參數做權限判斷。 |

---
Document ID: DOC-SEC-005
Version: 1.0
Owner Agent: Security Review Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: None
---

# Application Security (OWASP Top 10 Mapping)

針對 OWASP Top 10，我們在架構層面實作以下防護策略：

## 1. Injection (注入攻擊)
- **防護措施:** 全面採用 Entity Framework Core (ASP.NET Core) 與 Prisma/TypeORM (Node.js)，杜絕字串拼接的 SQL 操作。API Gateway 透過 WAF 攔截已知 SQLi/XSS Payload。

## 2. Broken Authentication (身分驗證失效)
- **防護措施:** 遵循 Authentication Architecture 規範。強制密碼強度校驗、使用 Argon2id 雜湊、阻擋暴力破解 (Rate Limit + Account Lockout)，並以 HttpOnly Cookie 保護 Token。

## 3. Sensitive Data Exposure (敏感資料外洩)
- **防護措施:** 資料庫啟動 TDE (Transparent Data Encryption)，傳輸層全面強制 TLS 1.2+ (HSTS)。對於 PII 資料庫欄位 (如信用卡後四碼)，設定更嚴格的存取稽核。

## 4. Broken Access Control (權限控制失效)
- **防護措施:** API Controller 必須掛載 `[Authorize(Roles="...")]`。更重要的是，在 Domain Service 層實作 Resource Owner Validation (ABAC)，防止 Insecure Direct Object Reference (IDOR) 攻擊。

## 5. Security Misconfiguration (安全設定不當)
- **防護措施:** Docker Image 基底採用 `distroless` 移除 Shell 環境。所有 Container 設定為 Read-Only Root Filesystem 與 Non-Root User 執行，防止提權攻擊。

## 6. Security Logging and Monitoring Failures (日誌與監控失效)
- **防護措施:** 導入集中式 Log 系統 (ELK / Serilog)，實作 Audit Architecture 記錄關鍵事件，並針對異常登入或越權嘗試配置即時告警 (Alerting)。

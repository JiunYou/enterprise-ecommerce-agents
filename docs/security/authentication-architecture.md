---
Document ID: DOC-SEC-002
Version: 1.0
Owner Agent: Security Review Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-Security-001
---

# Authentication Architecture

## 1. Identity Architecture
本系統採用 **OIDC (OpenID Connect) 與 OAuth 2.0** 標準。Identity Component (或外部 IdP) 將做為全平台的 Token 發行中心。

## 2. Token Strategy & Session Management
- **Access Token:** 採用短效期 (Short-lived，如 15 分鐘) 的 JWT (JSON Web Token)，用於無狀態 (Stateless) API 授權。
- **儲存方式:** Frontend 取得 Access Token 後，強制儲存於 **HttpOnly, Secure, SameSite=Strict 的 Cookie** 中，防禦 XSS 與 CSRF 攻擊。嚴禁儲存於 `localStorage`。

## 3. Refresh Token Strategy
- Refresh Token 設定長效期 (如 7 天) 並實作 **Refresh Token Rotation (輪換機制)**。
- 每次使用 Refresh Token 換取新的 Access Token 時，會核發新的 Refresh Token 並作廢舊的。若偵測到已作廢的 Refresh Token 再次被使用（可能是被盜用），將立即強制撤銷 (Revoke) 該使用者所有關聯的 Tokens。

## 4. Multi-Factor Authentication (MFA)
- Admin Portal 強制啟用 MFA (Authenticator App / TOTP 或 WebAuthn)。
- Customer Web 在高風險操作 (如：變更收件地址、高額信用卡刷卡) 時，觸發 Step-up Authentication (如簡訊 OTP 驗證)。

## 5. Account Recovery
- 提供信箱重置連結 (帶有一次性且短效期的 Secure Token)。
- 若連續登入失敗超過 5 次，將帳號進入鎖定狀態 (Account Lockout) 30 分鐘，並發送警告信件，防止暴力破解。

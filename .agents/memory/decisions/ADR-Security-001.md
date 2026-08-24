# ADR-Security-001: Authentication Architecture Decision

## Metadata
- **ADR ID:** ADR-Security-001
- **Status:** Proposed
- **Owner Agent:** Security Review Agent
- **Created Date:** 2026-08-24

## Context
本平台需要支援消費者與內部員工的登入認證，並面臨跨越 ASP.NET Core 與多個 Node.js 服務的授權需求。我們必須決定 Token 發行與驗證的架構，以確保安全性與效能。

## Problem
自行開發 OAuth 2.0/OIDC Server 將面臨巨大的資安風險與維護成本，同時需處理密碼雜湊、MFA、Account Lockout 等繁瑣邏輯。

## Options
- **Option A:** 在 ASP.NET Core 中自行實作 (Custom Auth with ASP.NET Core Identity)。
- **Option B:** 使用開放原始碼 Identity Provider (如 Keycloak, Duende IdentityServer)。
- **Option C:** 使用雲端託管 Identity Provider (如 Auth0, AWS Cognito, Azure AD B2C)。

## Decision
採用 **Option C: 雲端託管 Identity Provider (以 Auth0 優先評估)**，搭配 **Stateless JWT** 進行跨服務存取授權。

## Consequences
- **Positive:** 大幅降低身份驗證相關的安全風險 (如 SQLi, 暴力破解)；開箱即用支援 MFA 與社群登入；減輕平台本身的負載。
- **Negative:** 產生外部服務依賴與訂閱成本 (Vendor Lock-in 疑慮)。

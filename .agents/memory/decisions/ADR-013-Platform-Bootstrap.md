---
Document ID: ADR-013
Version: 1.0
Owner Agent: Enterprise Platform Engineer Agent
Created Date: 2024-05-20
Status: Approved
Related ADR: ADR-008
---

# ADR-013: Platform Bootstrap Decision

## Status
Approved

## Context
在 Phase 9 中，我們需要將設計轉化為真實的程式碼庫 (Codebase)。選擇哪些 CLI 工具來自動產生框架結構，會深遠地影響初始配置的品質與未來的升級相容性。

## Decision
我們決定完全採用官方 CLI 工具進行各技術棧的初始化：
- Frontend: 採用 `npx create-next-app` 搭配 `--ts`, `--tailwind`, `--eslint`, `--app` 等參數。
- Backend: 採用 `dotnet new sln`, `classlib`, `webapi` 指令生成專案，手動建立 Clean Architecture 參照。
- Node.js Services: 採用原生 `npm init` 搭配手動注入 `typescript`, `tsx`, `pino` 依賴。

## Alternatives Considered
- 使用企業自定義樣板 (Custom Boilerplates)：雖然可以高度客製，但在後續框架(如 .NET 9 或 Next.js 15) 升級時容易產生無法相容的負擔。

## Consequences
- **Positive:** 完全貼合各框架最新標準，未來升級無痛。
- **Negative:** 初期需要手動在不同專案間設定 CORS、共用 Types 關聯，花費較多設定時間。

## Security Impact
採用最新原廠 CLI 生成之樣板，可避免引入帶有已知漏洞的老舊起手式套件。

## Future Evolution
當這套設定穩定後，可將此架構打包為內部腳本樣板，供後續新建其他 Node 服務時快速套用。

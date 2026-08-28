---
Document ID: DOC-ADR-012
Version: 1.0
Owner Agent: Enterprise Software Delivery Architect
Created Date: 2026-08-24
Status: Proposed
Related ADR: None
---

# ADR-012: AI Agent Development Workflow Decision

## Status
Proposed

## Context
本專案將重度依賴 AI Agents 進行程式碼編寫與架構落實。為了確保程式碼品質與架構不劣化，我們必須確立一套嚴謹的 Agent 協作、授權與審查工作流 (Agent Coding Workflow)。

## Decision
建立 **Hierarchical Agent Model (階層式 Agent 模型)** 與嚴格的 **Guardrails (護欄)**。

### Agent 模型與職責
- **Master Orchestrator Agent:** 分派任務，協調其他 Agent 合作，總管全域狀態。
- **Architecture Agent:** 負責解釋 Architecture Package (ADRs, C4 Model)，為下層 Agent 提供上下文。
- **Specialist Agents (Backend, Frontend, Node, DB, DevOps):** 負責各專屬領域的具體 Code 產出。不允許跨領域修改 (例：Frontend Agent 不能去動 EF Core Migration)。
- **QA & Security Agents:** 負責代碼產出後的自動化 Review、掃描與攻擊面測試。

### Allowed / Forbidden Actions
- **Allowed:** 實作 Unit Test, 建立 API Controller, 實作 Repository, 重構內部邏輯。
- **Forbidden:** 未經人類批准前，絕對禁止在 Production 環境執行腳本、禁止修改 IAM 權限配置、禁止發送含有 Sensitive Data 的日誌。

### Workflow
`Task Assignment -> Architecture Context Loading -> Implementation -> Self Review -> Automated Validation (CI) -> Security Review -> Human Approval -> Merge`

## Alternatives Considered
- **Flat Agent Model:** 所有 Agent 權限相同並自由互動。容易導致權限過載、設計衝突與難以追蹤責任。

## Consequences
- **Positive:** 架構一致性受到高度保障；降低了 Agent 產生幻覺 (Hallucination) 破壞系統的風險。
- **Negative:** 每一個 Feature 的開發週期需經過層層關卡，Agent 之間溝通與驗證的 Token 成本極高。

## Security Impact
Security Agent 扮演了自動化守門員，確保所有由 Specialist Agent 產生的程式碼符合 OWASP Top 10 與 Secret 處理規範。

## Future Evolution
開發並匯入自訂的 LSP (Language Server Protocol) 工具，讓 Agent 能更精準地掌握 Monorepo 跨檔案語意，進而提升 Review 與 Implementation 的準確度。

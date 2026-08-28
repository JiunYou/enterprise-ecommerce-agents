---
Document ID: DOC-ADR-009
Version: 1.0
Owner Agent: Enterprise Software Delivery Architect
Created Date: 2026-08-24
Status: Proposed
Related ADR: None
---

# ADR-009: Branch Strategy Decision

## Status
Proposed

## Context
為支援 Enterprise Team 與 AI Assisted Development 頻繁的協作，我們需要一個能快速整合程式碼、減少 Merge Conflict，並支援 Continuous Delivery (CD) 的分支策略。

## Decision
採用 **Trunk Based Development (主幹開發)**，搭配 **Short-lived Feature Branches (短生命週期功能分支)**。
- **Branch Naming:** `feature/{issue-id}-{desc}`, `bugfix/{issue-id}-{desc}`, `hotfix/{issue-id}-{desc}`
- **Commit Convention:** 遵循 Conventional Commits (如 `feat:`, `fix:`, `chore:`)，以便自動化產出 Changelog。
- **Pull Request Policy:** 功能分支壽命不應超過 2-3 天。必須通過 CI 與 Security Scan。
- **Review Requirement:** 至少需要 1 位 Human Approval 與 Security Agent Validation 方可 Merge。
- **Release & Hotfix:** 採用 Release Branch 從 `main` 分支出來用於 Staging/Prod 佈署；Hotfix 必須從對應 Release Branch 修復後再 Cherry-pick 回 `main`。

## Alternatives Considered
- **Git Flow:** 包含 `develop`, `release`, `hotfix`, `feature`。過於繁瑣，不適合高度自動化 CI/CD 與 AI Agent 快速迭代。
- **GitHub Flow:** 非常簡潔，但對於企業級的多環境 (Staging/Prod) 放行管控稍顯不足。

## Consequences
- **Positive:** 極大化 CI 的效益，避免長期分支導致的嚴重衝突；強迫開發者與 Agent 拆解細小且可獨立驗證的 Task。
- **Negative:** 需高度依賴 Feature Flags 機制，以隱藏尚未完成但已合併至主幹的功能。

## Security Impact
主幹 (`main`) 與發布分支 (`release/*`) 必須設定 Branch Protection Rules，禁止 Force Push 與直接 Commit。

## Future Evolution
團隊成熟後可過渡至無 Release Branch，完全由 `main` 搭配進階的 Feature Flags 與 Canary Release 實現持續部署。

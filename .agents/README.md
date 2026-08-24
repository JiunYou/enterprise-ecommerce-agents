# Enterprise E-Commerce Platform - AI Agent Team Architecture

本專案採用 AI Agent 團隊架構進行開發，以 `master-orchestrator-agent` 為首，協同各個專業 Agent 進行自動化與半自動化的軟體工程。

## Agent Team 架構
- **Master Orchestrator Agent**: AI Team Leader，負責任務拆解、Agent 路由分派與成果驗證。
- **Product Manager Agent**: 負責需求分析與 PRD。
- **System Architect Agent**: 負責系統與架構設計。
- **Backend & Frontend Agents**: 負責各端的程式開發。
- **Database Agent**: 負責資料庫 Schema 與效能。
- **Security & Compliance Agents**: 負責資安把關與合規審查。
- **QA & DevOps Agents**: 負責測試與 CI/CD 部署。

## Agent 使用方式
1. 當有新任務時，將需求交由 `master-orchestrator-agent` 進行分析。
2. Orchestrator 會依據領域將工作分配給對應的 Agent。
3. 每個 Agent 在執行時，應載入其專屬職責描述與相關 Skills。
4. 最終產出需經過 Orchestrator 驗證才算完成。

## Skill 使用方式
Skills 位於 `.agents/skills/` 目錄。
- 每個 Agent 執行特定任務前，需讀取對應的 `SKILL.md` (例如寫 C# 需讀取 `enterprise-dotnet`)。
- 開發與審查時，強制掛載 `clean-code` 與 `secure-development` 作為判斷基準。

## Development Workflow
本專案定義了標準工作流，位於 `.agents/workflows/`：
- `feature-development.md`: 一般功能開發流程。
- `security-review.md`: 安全掃描與檢查流程。
- `architecture-design.md`: 架構設計與決策流程 (包含 ADR)。
- `code-review.md`: 程式碼審查標準流程。
- `release.md`: 上線發布流程。

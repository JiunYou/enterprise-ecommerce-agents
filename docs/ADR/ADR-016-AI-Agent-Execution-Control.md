---
Document ID: ADR-016
Version: 1.0
Owner Agent: AI Governance Architect Agent
Created Date: 2026-08-24
Status: Proposed
Related ADR: ADR-012
---

# ADR-016: AI Agent 執行控制與防護策略 (AI Agent Execution Control Strategy)

## 狀態 (Status)
Proposed

## 背景與問題脈絡 (Context)
在導入 AI Agent 進行企業級電商系統的架構設計與基礎建設時，觀察到若缺乏嚴格的執行邊界與終止控制，自主性 Agent 可能會引發以下重大風險：
1. **無窮生成與自我修正迴圈 (Infinite Generation Loop)：** Agent 在面對未收斂的問題或過度追求完美時，可能陷入反覆修改、自發重構的無限循環，造成資源浪費與中斷。
2. **範疇蔓延 (Scope Creep)：** Agent 自行推演並實作未經授權的功能或跨模組重構，破壞了原本的階段性規劃。
3. **非預期檔案修改 (Unnecessary File Modifications)：** 觸及非指派目錄或修改已確立的架構決策與設定檔。

## 決策 (Decision)
我們決定採用「受控的 Agent 執行模型」(Controlled Agent Execution Model)，並建立強制性治理機制：
1. **明確的任務邊界 (Explicit Task Boundary)：** 每個任務必須明確定義「允許修改範圍」與「禁止更動範圍」。Routine 工作可自主執行，但高風險變更（架構、安全性、破壞性變更）必須強制設定 Approval Gate。
2. **明確定義的完成條件 (Defined Completion Criteria)：** 具備可驗證的交付標準。一旦達成驗收條件，Agent 必須產生最終報告並「立即停止」(STOP IMMEDIATELY)。
3. **強制迭代上限 (Maximum Iteration Limit)：** 單一問題修復或自動除錯上限為 3 次，若超過則強制中止並交由人工介入分析 (BLOCKER)。
4. **實作與驗證角色分離 (Separation of Concerns)：** 實作型 Agent (Implementation Agent) 僅負責代碼產出與基礎驗證；驗證型 Agent (Validation Agent) 負責合規性與獨立驗收，避免「球員兼裁判」導致盲目修改。

## 評估之替代方案 (Alternatives Considered)
1. **完全自主式 Agent (Fully Autonomous Agent)：**
   - *否決理由：* 缺乏邊界與檢查點，在大型複雜企業專案中不可控，極易產生非預期的破壞性修改與死循環。
2. **純人工傳統開發 (Human-only Development)：**
   - *否決理由：* 無法發揮大型語言模型與自動化 Agent 在規格解析、程式碼生成及規範檢核的高生產力優勢。

## 影響與後果 (Consequences)
- **正面效益 (Positive)：**
  - 提升執行的可預測性與穩定性。
  - 減少對非相關模組的誤改與意外破壞。
  - 審計與變更歷史更清晰，利於追蹤責任與修訂過程。
- **潛在限制 (Negative)：**
  - 增加了人工確認與各階段檢查點（Gate/Checkpoint）的頻率。

## 未來演進 (Future Evolution)
- 整合自動化審批工作流 (Automated Approval Workflow)。
- 建立更細緻的 Agent 角色權限系統 (Role-based Access Control for Agents)。
- 開發自動化變更影響分析工具 (Change Impact Analysis Tooling)。

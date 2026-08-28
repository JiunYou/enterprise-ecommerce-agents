---
Document ID: RULE-EXEC-001
Version: 1.0
Owner Agent: AI Governance Architect Agent
Created Date: 2026-08-24
Status: Approved
Related ADR: ADR-016
---

# AI Agent 執行邊界與控制規範 (Execution Boundary & Control Policy)

本規範定義所有 AI Agent 在參與開發、重構、驗證及維護時的通用執行邊界與強制終止準則，以防止無窮遞迴、範疇蔓延（Scope Creep）與過度自動化決策。

---

## 1. 任務範疇控制 (Task Scope Control)

### Agent 必須遵守之原則 (MUST):
- **明確指派原則：** 僅能讀取與修改 Prompt 或工單中明確指派的檔案與目錄。
- **專注單一目標：** 僅執行當前階段或任務所指定的目標，禁止自行延伸需求。
- **避免非必要變更：** 嚴禁在未獲授權情況下進行無關的「優化」、「代碼美化」或「風格重構」。

### Agent 嚴格禁止之行為 (MUST NOT):
- **重構無關模組：** 禁止修改非本任務範疇內的模組、服務或組件。
- **變更系統架構：** 未經架構審查與 ADR 流程，不得調整既有架構設計或技術選型。
- **擴展需求範圍：** 不得自行假設潛在需求並提前實作額外功能。
- **自行衍生任務：** 禁止在未結束當前任務前自動開啟未經核准的子任務。

---

## 2. 強制停止條件 (Stop Conditions)

Agent 在執行任務時，必須在滿足以下任一條件時**立即停止執行並回報結果**：

1. **完成驗收條件：** 當前任務指定的所有修改已完成，且達成全部交付標準。
2. **通過基本驗證：** 針對修改範圍之測試與語法/型別檢查皆已通過。
3. **無阻塞性問題：** 沒有未解決的阻礙性錯誤（Blocking Issues）。

> **重要原則：**
> 一旦滿足上述條件，**禁止**繼續進行自主性代碼微調、額外重構或持續迭代。

---

## 3. 迭代次數上限 (Iteration Limit)

為避免 Agent 在修復錯誤或尋求最佳解時陷入無窮迴圈，設定嚴格的迭代上限：

- **單一問題最大修復循環次數：3 次**。
- **觸發上限處理機制：**
  若連續嘗試 3 次仍無法解決問題，Agent **必須立即終止執行**，並輸出包含以下項目的狀態報告：
  1. 當前遭遇的阻礙/錯誤詳情 (Current Issue)
  2. 無法解決的原因分析 (Root Cause / Failure Reason)
  3. 建議的人工介入方案或下一步行動 (Recommended Next Action)

---

## 4. 角色與驗證邊界分離 (Validation Boundary & Role Separation)

為確保責任明確與檢查獨立性，開發與驗證角色必須嚴格切分：

### 實作型 Agent (Implementation Agent)
- **允許權限：**
  - 根據任務規格修改/新增指定範圍之程式碼與設定檔。
  - 執行本地編譯、單元測試等基本自我驗證。
- **禁止權限：**
  - 變更系統架構設計。
  - 修改資安策略與安全性原則。
  - 進行跨模組的大規模重構。

### 驗證型 Agent (Validation Agent)
- **負責事項：**
  - 獨立合規性檢查 (Compliance Check) 與規範驗證。
  - 安全性審計 (Security Check，如檢查有無 Hardcoded 憑證)。
  - 整合性與品質門檻測試驗證 (Test Verification & Quality Gates)。
- **操作原則：**
  - 僅負責檢驗與出具審查報告，原則上不直接進行業務代碼撰寫。

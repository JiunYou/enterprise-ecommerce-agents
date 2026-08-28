---
Document ID: TEMPLATE-TASK-EXEC-001
Version: 1.0
Owner Agent: AI Governance Architect Agent
Created Date: 2026-08-24
Status: Approved
Related ADR: ADR-016
---

# Task Execution Template

使用本範本以約束與指導各階段 AI Agent 執行任務，防止超出範圍與無窮迴圈。

---

## 1. 任務基本資訊 (Task Overview)
- **Task 名稱：** [填寫具體任務名稱，例如：Order Domain Entity 實作]
- **Task 類型 (Task Type)：** `[Implementation | Validation | Review]`
- **負責 Agent (Assigned Agent)：** [填寫負責的 Agent 角色]
- **目標 (Objective)：** [簡述任務預期達成的目標]

---

## 2. 邊界與限制 (Execution Boundary)
- **允許操作檔案/路徑 (Allowed Files)：**
  - `[指定路徑1]`
  - `[指定路徑2]`
- **禁止修改檔案/路徑 (Forbidden Files)：**
  - `[指定路徑1]`
  - `[指定路徑2]`
- **禁止行為 (Forbidden Actions)：**
  - 禁止未經授權的重構或優化。
  - 禁止跨 Domain 修改代碼。
  - 禁止變更架構決策與資安策略。

---

## 3. 完成與驗收條件 (Completion Criteria)
- [ ] 條件 1: [具體功能或產出]
- [ ] 條件 2: [具體功能或產出]
- [ ] 條件 3: [具體功能或產出]

---

## 4. 驗證要求 (Validation Requirement)
- [ ] 靜態分析/Lint 檢查通過
- [ ] 單元測試/建置通過
- [ ] 安全與合規檢查通過

---

## 5. 強制停止條件 (STOP CONDITION)
> [!IMPORTANT]
> **當所有「完成與驗收條件」皆已滿足，且通過「驗證要求」時：**
> **請立即停止執行 (STOP IMMEDIATELY)。**
> **嚴禁繼續進行額外的代碼優化、風格重構或新增未列入需求的項目。**
> 
> 若連續修復錯誤超過 **3 次** 仍未通過，亦必須立即停止並回報錯誤原因，等待人工指示。

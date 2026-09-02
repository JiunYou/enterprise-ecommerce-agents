---
Document ID: RULE-EXEC-001
Version: 1.1
Owner Agent: AI Governance Architect Agent
Created Date: 2026-08-24
Last Modified: 2026-09-03
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

---

## 5. 程式化驗證優先與證據政策 (Programmatic Verification First & Evidence Policy)

### 5.1 證據階層 (Evidence Hierarchy)
驗證行為必須依序採用最高可用階層之證據，優先採用較強的機器可驗證證據 (machine-verifiable evidence)：
1. **可執行自動化測試 (Executable automated test)**
2. **確定性驗證腳本 / 命令 (Deterministic verification script / command)**
3. **編譯器 / 靜態分析器 / Linter / 型別檢查器 (Compiler / static analyzer / linter / type checker)**
4. **具機械可觀察結果之運行時探測 (Runtime probe with mechanically observable result)**
5. **直接源碼 / 設定檔檢視 (Direct source/config inspection)**
6. **AI 推理與分析 (AI reasoning)**

### 5.2 機器可驗證優先原則 (Machine-Verifiable First)
若驗收條件或需求可合理透過可執行測試、確定性命令、腳本、編譯器、分析器、Linter、型別檢查器、HTTP 探測、資料庫斷言、檔案系統斷言或 Git 命令等機制檢驗，**必須**使用該機械化機制驗證。嚴禁以 AI 檢視或散文推理替代機器可驗證之驗證手段。

### 5.3 AI 主張非實質證據 (AI Claims Are Not Evidence)
未取得外部機械證據前，AI 推理僅屬假設。以下宣告均**不得**單獨作為 PASS 驗收之充分證據：
- "I inspected the code and it is correct."
- "This should work."
- "The tests should pass."
- "The endpoint is protected."
- "No secrets are exposed."
- "The feature is complete."

### 5.4 PASS 判定標準 (PASS Requirement)
機器可驗證之項目僅在具備實際執行證據時，方可標記為 **PASS**。報告中應包含適用之執行證據：
- 確切執行之 command / test
- 結束與結果狀態 (exit / result status)
- 通過 / 失敗 / 略過具體計數 (passed / failed / skipped counts)
- 機械可觀察之狀態與輸出 (mechanically observed state)

### 5.5 未執行或失敗驗證狀態 (Failed or Missing Execution)
若驗證未執行、無法執行、中斷、逾時無可靠結果、依賴不可用之基礎設施或產生模糊輸出，**嚴禁**標記為 PASS。必須使用準確之非 PASS 狀態：
- **FAIL**
- **NOT VERIFIED**
- **PARTIAL**
- **ENVIRONMENT BLOCKED**
- **DEFERRED**

### 5.6 禁止推構結果 (No Result Reconstruction)
嚴禁從原始碼、AI 記憶、舊有摘要、私有 Agent 日誌、測試名稱、預期行為或過往類似執行推論或重建缺失之測試結果。缺乏執行證據一律標記為：**NOT VERIFIED**。

### 5.7 測試優於代碼檢視 (Test Over Inspection)
當系統行為可合理表示為可維護之自動化回歸測試時，優先建立或重用該測試。
- 範例：真實授權測試驗證顧客請求獲得 403，其證據力遠高於僅透過代碼檢視發現 `[Authorize]` 標記。源碼檢視僅可用於解釋行為成因，不能取代運行時斷言。

### 5.8 機器導出狀態優先 (Machine-Derived State)
驗證時優先採用確定性機器狀態：
- **API 行為**：採用 HTTP 狀態碼與回應主體斷言
- **資料庫行為**：採用 SQL / Schema / 資料條件約束斷言
- **檔案系統行為**：採用檔案存在性、權限與雜湊斷言
- **儲存庫拓撲**：採用 Git 命令 (`rev-parse`, `status`, `diff`, `merge-base`)
- **前端行為**：採用 Lint、型別檢查、建置與自動化測試
- **安全性**：採用授權與未授權請求之實體測試
- **並發性**：採用受控並發測試與資料庫狀態斷言
- **機密安全**：採用確定性儲存庫掃描與設定檢查，且絕不印出真實機密值

### 5.9 AI 審查定位與標記 (AI Review Role)
AI 推理僅適用於：需求解讀、架構一致性審查、語意代碼審查、潛在缺陷識別、驗證策略制定與機器失敗原因詮釋。
若 AI 審查為唯一或最高可用證據，結果必須明確分類為：
- **AI_REVIEW_ONLY** 或 **NOT_MACHINE_VERIFIED**
嚴禁將其視為等同於可執行測試之證據。

### 5.10 完成與交付規則 (Completion Rule)
一項功能或特性僅在所有機器可驗證之驗收條件皆取得相應機器證據後，方可宣告為 **VERIFIED**。
若有任何必要之運行時條件尚未驗證，狀態必須保持為 **NOT VERIFIED**、**PARTIAL** 或 **DEFERRED**。AI 判斷不得將其升級為 VERIFIED。

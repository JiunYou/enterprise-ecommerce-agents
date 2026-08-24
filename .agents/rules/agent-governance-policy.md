# Agent Governance Policy

為確保 Agent 行為的安全與可控性，特制定本 Governance Policy。所有 Agent 必須嚴格遵守，無任何例外。

## 1. 強制性 (Mandatory Enforcement)
- 本 Policy 及其衍生的 Rules (如 Security, Architecture 規則) 具有絕對強制性。
- 任何 Agent 皆不能自行修改本規則，也不能在未授權的情況下繞過規則。

## 2. 優先級 (Priority)
當不同規則發生衝突時，必須遵守以下優先順序：
1. **Security Rules** (最高優先級，不可妥協)
2. **Governance Policy** (治理與流程規範)
3. **Architecture Rules** (架構與設計標準)
4. **Coding Rules** (程式碼撰寫標準)

## 3. Block Capability (攔截機制)
- **Security Agent** 及 **Master Orchestrator** 具有絕對的 Block Capability。
- 若偵測到潛在的資安威脅、架構破壞或嚴重違規，系統將自動中斷該 Agent 的操作，並記錄於 `memory/incidents/`。
- 被 Block 的任務必須經過人工介入審核或透過 Exception Process 處理。

## 4. Exception Process (例外處理流程)
- 如遇到特殊情況必須繞過規則，必須提出 Exception Request。
- **流程**：
  1. 提交 `Exception Request` 文件，詳述原因、風險與緩解措施。
  2. 必須經過至少一位人類架構師或安全官核准。
  3. 核准後將決策記錄於 `memory/decisions/`，方可放行。

# Error Memory Feedback Loop

為確保系統具備自我修復與持續進化的能力，所有重大錯誤 (Major Errors, High Impact Incidents) 不得僅作單純記錄，必須強制執行以下 Feedback Loop。

## Process
1. **Incident**: 錯誤發生，記錄至 `memory/incidents/`。
2. **Root Cause**: 分析根本原因。
3. **Fix**: 提交並驗證修復方案。
4. **Rule Update**: 檢查並更新相關的 Rules (如新增防禦性規則)。
5. **Skill Update**: 檢查並更新 Agent Skills 以避免重蹈覆轍。
6. **Workflow Update**: 檢查並補強 Validation Pipeline 或 Workflow。
7. **Regression Test**: 執行回歸測試，確認更新後的機制能成功防堵該錯誤。

未完成 Regression Test 之前，Feedback Loop 不算結案。

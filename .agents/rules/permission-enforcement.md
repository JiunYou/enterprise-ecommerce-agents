# Permission Enforcement Rule

## Mandatory Bootstrapping
orchestrator 必須在任何任務開始時載入以下核心文件：
- `mandatory-rules.md`
- `agent-governance-policy.md`
- `permission-matrix.md`

## Enforcement Policy
任何 Agent 在執行過程中，若發出違反 Permission Matrix 定義（例如：越權寫入、執行被禁止的操作）的行為，必須觸發以下機制：
- **Task Status**: 立即標記為 `BLOCKED`
- **Execution**: 強制中止該 Agent 的操作
- **Logging**: 記錄至 `.agents/memory/incidents/` 並通報 Security Agent

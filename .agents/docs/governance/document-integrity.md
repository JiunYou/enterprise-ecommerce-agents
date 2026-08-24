# Document Integrity System

所有狀態為 `Approved` 的核心治理文件，必須在其 YAML Frontmatter 或文件底部具備 Integrity Metadata，以確保文件防篡改與可追溯。

## Integrity Metadata Requirement
- `Version Control`: Git commit hash 或版控識別碼
- `SHA256 Hash`: 文件內容的 SHA256 摘要
- `Validator Identity`: 執行最終驗證的 Agent 或人工簽核者 ID
- `Validation Timestamp`: 通過驗證的精確時間 (ISO 8601 格式)
- `Change History`: 變更紀錄與關聯 Issue/PR 連結

## Enforcement
若 `Approved` 文件缺少上述任一欄位，Validation Pipeline 將退回狀態至 `Reviewing` 或 `Draft`。

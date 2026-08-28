# Document Metadata Standard

為確保文件的可追溯性與一致性，所有重要文件（包含但不限於 Architecture, ADR, API, Database, Security, Deployment）都必須在文件頂部使用 YAML Frontmatter 格式包含以下標準 Metadata。

## 必填 Metadata 欄位

- `Document ID`: 唯一識別碼 (例如：`ADR-001`, `ARCH-002`)
- `Version`: 版本號 (例如：`1.0.0`)
- `Owner`: 文件負責人或主要維護的 Agent (例如：`Architecture Agent`)
- `Reviewer`: 負責審查的 Agent 或人員 (例如：`Security Agent`, `Orchestrator`)
- `Validation Status`: 目前狀態 (Draft | Reviewing | Validated | Approved | Deprecated)
- `Related Decision`: 關聯的決策或文件 ID (若無則填寫 `N/A`)

## 範例

```yaml
---
Document ID: DOC-001
Version: 1.0.0
Owner: Architecture Agent
Reviewer: Security Agent
Validation Status: Draft
Related Decision: ADR-001
---
```

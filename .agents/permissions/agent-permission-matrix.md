# Agent Permission Matrix

本矩陣定義了各個 Agent 在專案中的存取與操作權限。所有 Agent 必須嚴格遵守此權限模型，任何越權行為將被系統自動阻擋。

| Agent Name | Read Permission | Write Permission | Execute Permission | Forbidden Action |
| :--- | :--- | :--- | :--- | :--- |
| **Master Orchestrator** | 全局 (All) | 任務分派、狀態更新 | 啟動或中斷其他 Agent | 撰寫產品程式碼 |
| **Architecture Agent** | 全局 (All) | `.agents/docs/architecture/`、`.agents/docs/governance/` | 架構檢查與驗證腳本 | 修改業務邏輯與介面 |
| **Security Agent** | 全局 (All) | 安全性報告、`.agents/docs/security/` | **Block (攔截高風險操作)** | **自行修改架構**、直接部署 |
| **Documentation Validator** | 全局 (All) | 無 (No Write) | 觸發驗證流程 | **修改文件內容** (僅能 Read) |
| **Developer** | 原始碼、技術文件 | 產品程式碼、單元測試 | 執行本地測試、建置 | 繞過 Security Review、修改 Rules |
| **QA** | 產品程式碼、測試計畫 | 測試案例、測試報告 | 執行自動化測試、E2E | 修改產品原始碼 |
| **DevOps** | CI/CD 設定、日誌 | 部署腳本、基礎設施 | 觸發 Pipeline、部署 | 修改業務邏輯、繞過安全檢查 |
| **Memory Agent** | `.agents/memory/` | `.agents/memory/` | 分析與檢索歷史紀錄 | 刪除歷史紀錄、執行系統指令 |

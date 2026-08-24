---
Document ID: DOC-SEC-004
Version: 1.0
Owner Agent: Security Review Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-Security-002
---

# Secret Management Architecture

## 1. Secret Lifecycle
本系統嚴禁將 Secrets (Database Connection String, Payment API Keys, JWT Signing Keys) 寫入原始碼 (Hardcode) 或未加密的設定檔 (`.env`, `appsettings.json`) 中。

1. **Creation:** Secrets 由基礎架構團隊在 Secret Vault 中產生。
2. **Storage:** 儲存於高安全性的集中式 Secrets Manager。
3. **Access Control:** 依據 Container 的 IAM Role (Role-Based) 賦予最小讀取權限。
4. **Rotation:** 實施定期輪換 (如 90 天自動 Rotate DB 密碼)，應用程式需支援連線字串熱重載 (Hot Reload) 或容忍重啟。
5. **Audit:** 紀錄所有存取 Secret 的時間與主體。

## 2. Solution Comparison (見 ADR-Security-002)
- **HashiCorp Vault:** 功能最強但維運成本極高。
- **Cloud Secret Manager (AWS/Azure/GCP):** 與雲端 IAM 深度整合，隨需付費，維運成本低。
- **Container Secret (K8s Secrets):** 易於實作但預設僅為 Base64 需額外設定 KMS Encryption，且不方便跨環境共享。

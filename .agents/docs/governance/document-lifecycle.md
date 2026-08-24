# Document Lifecycle

所有核心文件（如 Architecture, ADR, API, Database, Security, Deployment）均須遵守以下生命週期狀態。

## 狀態定義

1. **Draft (草稿)**
   - 文件正在撰寫或初步構思中。
   - 尚未準備好進行審查。

2. **Reviewing (審查中)**
   - 內容已完成初步撰寫。
   - 正由相關 Domain Agent 或 Validator 進行技術、架構或安全性審查。

3. **Validated (已驗證)**
   - 通過所有必要的 Validation Pipeline 檢查。
   - 包含 Schema Validation、架構與安全性等自動化檢核。

4. **Approved (已核准)**
   - 由負責人 (Owner) 或相關審查者 (Reviewer) 正式批准。
   - 成為正式參考標準。若有任何驗證失敗，**不得**進入此狀態。

5. **Deprecated (已廢棄)**
   - 內容已過時或被新的決策 (如新的 ADR) 取代。
   - 應標示取代此文件的連結。

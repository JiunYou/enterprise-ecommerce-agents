---
Document ID: DOC-GOV-002
Version: 1.0
Owner Agent: System Architect Agent
Created Date: 2026-08-24
---

# AI Engineering Governance Documentation (Agent Development Policy)

本文件定義 AI Agents 參與 Enterprise E-Commerce Platform 開發與設計時，必須遵守的協作邊界、權限與審核流程。

## 1. Agent Responsibility
- **Product Manager Agent:** 負責需求釐清與 Business Domain 邊界定義。
- **Domain Architect Agent:** 負責 DDD Strategic Design，產出 Bounded Context 與 Context Map。
- **System Architect Agent:** 總體架構負責人，決定架構風格 (Hybrid) 並產出 C4 Model 與技術選型。
- **Security Review Agent:** 把關 OWASP、STRIDE 威脅建模與存取控制架構。
- **Technology-Specific Agents (.NET, Node.js, Frontend, DB):** 負責各技術堆疊內的細部架構設計與（未來）程式碼產出。
- **Documentation/Compliance Validator Agents:** 負責確保所有產出符合 YAML Metadata 規範、追溯性與法遵。

## 2. Permission Boundary
- **Phase 1~7 (Design Phase):** 禁止所有 Agent 產生 Application Code (Frontend/Backend)、Database Schema 或執行 Infrastructure Scripts。僅允許產出 `.md` 規格文件與決策紀錄。
- **Implementation Phase (未來):** Agent 在修改程式碼前，必須閱讀相關的架構藍圖 (docs/architecture) 與安全政策 (docs/security)。

## 3. Document Ownership & Metadata Requirement
任何文件產出必須包含 YAML Metadata 檔頭，格式如下：
```yaml
---
Document ID: {ID}
Version: {Version}
Owner Agent: {Agent Name}
Created Date: {Date}
Status: {Draft|Proposed|Approved}
Related ADR: {ADR ID if applicable}
---
```
非該 Document Owner 的 Agent，不得隨意竄改該文件之核心定義。

## 4. ADR Requirement
凡涉及以下範疇之重大決策，**必須**強制建立 ADR：
- 架構風格轉換 (如 Monolith 轉 Microservices)
- 引入新的關鍵性技術或第三方服務 (如 Database, Message Broker, Auth Provider)
- 會影響跨 Bounded Context 通訊機制的變更
- 改變資安防護策略與資料隱私的處理方式

## 5. Security Review Requirement
所有技術架構文件與 ADR 在轉為 Approved 狀態前，必須由 **Security Review Agent** 進行檢視，確保未違反 `threat-model.md` 與 `application-security.md` 中的 OWASP 緩解措施。

## 6. Validation Workflow
每一次 Phase 完成前，必須召喚 **Documentation Validator Agent** 執行檢查：
1. 確保所有輸出檔案存在且 Metadata 正確。
2. 確保沒有產生預期外的程式碼檔案。
3. 確認 Architecture Traceability 未斷裂。

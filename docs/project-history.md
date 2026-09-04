# 專案歷史紀錄 (Project History)

本文件為專案擁有者導向的歷史紀錄，記錄合併至 `main` 分支的真實合併點（Merge Points）。
更新規範：
- 僅在實際整合至 `main` 的 Merge Point 時更新；個別本機實驗或日常任務不單獨記錄。
- 摘要記錄經程式化驗證所交付的業務價值與關鍵架構決策。
- 嚴禁包含密鑰、暫時憑證、個人隱私資料 (PII)、原始權杖或暫時性通道 URL。

## 合併點 (Merge Points)

### 2026-08-28 — PR #1 — Refactor/agent context routing v1
- 交付價值：建立 Agent 上下文路由架構與基礎規格目錄。
- 關鍵決策：確定以 progressive disclosure 模式管理 agent 與 skill 路由。

### 2026-08-29 — PR #2 — fix: finalize governance execution and git state gates
- 交付價值：落實治理執行邊界與 Git 狀態檢查閘門。
- 關鍵決策：以嚴格的狀態檢查防範分支漂移與未驗證變更。

### 2026-08-29 — PR #3 — feat: add customer product catalog
- 交付價值：交付顧客端產品目錄瀏覽功能與後端分頁查詢 API。
- 關鍵決策：前後端依照垂直切片架構實現產品列表。

### 2026-08-29 — PR #4 — feat: add customer product detail
- 交付價值：交付顧客端產品詳情頁面與對應查詢端點。
- 關鍵決策：維持展示層與領域實體之隔離。

### 2026-08-29 — PR #5 — feat: add customer catalog sorting
- 交付價值：支援產品目錄多維度排序功能。
- 關鍵決策：於查詢處理器實現白名單排序欄位驗證。

### 2026-08-29 — PR #6 — feat: Implement Customer Identity & Order Ownership Security Boundary
- 交付價值：建立顧客身分識別與訂單所有權安全防護邊界。
- 關鍵決策：以 IDOR 防護為核心，於領域服務強制校驗顧客所有權。

### 2026-08-29 — PR #7 — fix: externalize jwt authentication configuration
- 交付價值：將 JWT 驗證組態外部化，消除硬編碼設定。
- 關鍵決策：區隔開發環境與生產環境認證參數。

### 2026-09-02 — PR #8 — fix: close inventory authorization attack surface
- 交付價值：關閉庫存調整相關端點的未授權攻擊面。
- 關鍵決策：強制要求特定權限範圍以防止未授權調用。

### 2026-09-02 — PR #9 — governance: require programmatic verification evidence
- 交付價值：將程式化驗證第一 (Programmatic Verification First) 正式納入治理規則。
- 關鍵決策：禁止 AI 自我宣稱作為通過證據，所有 Merge Point 必須具備可執行的機器驗證結果。

### 2026-09-04 — PR #10 — feat: complete customer auth v1
- **垂直切片**：Customer Auth v1
- **交付價值**：
  - Auth0 顧客端 Web 登入與伺服器端 Session 管理
  - 外部 Auth0 身分 (Issuer + Subject) 映射至內部系統 CustomerId
  - 於 JWT 注入自訂 `urn:enterprisecommerce:customer_id` claim
  - 已認證 Customer Web 至 WebApi 的安全呼叫鏈條
  - 內部身分解析端點 (Identity Resolver) 僅限專用 M2M 憑證存取
  - MySQL 身分關聯持久化與唯一性約束
  - 完整防護顧客所有權與認證授權安全邊界 (IDOR 防護)
- **驗證成果**：
  - 後端建置通過 (0 warnings, 0 errors)
  - 296 項後端測試全數通過 (Domain: 63, Application: 103, Infrastructure: 23, WebApi.IntegrationTests: 107)
  - Auth0 Post-Login Action 單元測試通過 (12 passed, 0 failed)
  - 前端程式碼檢查 (lint)、TypeScript 型別檢查與 production build 通過
  - 真實 Auth0 Live E2E 完整流程驗證 (真實顧客登入、token 交換、身分解析、Session 建立、自訂 claim 注入、WebApi 認證呼叫)
  - 授權邊界安全驗證通過 (顧客權杖存取 M2M 端點回傳 403 Forbidden、匿名請求保護端點回傳 401 Unauthorized)
- **關鍵決策**：
  - Customer Web 不具備 `identity:resolve` 授權範圍，嚴格限縮攻擊面。
  - 內部 Identity Resolver 僅接受專用 M2M Client 認證。
  - 外部 IdP 實體標識與內部領域 CustomerId 保持解耦，由資料庫持久化對應關係。
  - 暫時性通道 (Quick Tunnel) 僅為驗證環境工具，不屬於正式架構。

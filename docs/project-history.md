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
- **交付價值**：交付 Customer Web 唯讀產品目錄瀏覽功能（含搜尋、分頁、空狀態與錯誤狀態處理），消費既有之 GET /api/v1/products 後端端點。
- **關鍵決策**：重用既有公開產品 API，而不額外新增目錄後端合約。

### 2026-08-29 — PR #4 — feat: add customer product detail
- **交付價值**：交付 Customer Web 產品詳情路由與目錄導航，使用既有之 GET /api/v1/products/{id} 後端端點。
- **關鍵決策**：重用既有產品詳情 API，此 PR 變更維持在 apps/web 範圍內。

### 2026-08-29 — PR #5 — feat: add customer catalog sorting
- **交付價值**：交付 Customer Web 排序 UI 與 URL 查詢狀態處理（名稱／價格升降冪）。
- **關鍵決策**：在透過既有目錄 API 輔助函式轉發 sortBy/sortOrder 之前，於 apps/web 透過白名單映射驗證公開排序選項。

### 2026-08-29 — PR #6 — feat: Implement Customer Identity & Order Ownership Security Boundary
- 交付價值：建立顧客身分識別與訂單所有權安全防護邊界。
- 關鍵決策：以 IDOR 防護為核心，於領域服務強制校驗顧客所有權。

### 2026-08-29 — PR #7 — fix: externalize jwt authentication configuration
- 交付價值：將 JWT 驗證組態外部化，消除硬編碼設定。
- 關鍵決策：區隔開發環境與生產環境認證參數。

### 2026-09-02 — PR #8 — fix: close inventory authorization attack surface
- **交付價值**：移除未使用且對外暴露的庫存預留端點，同時於權威性訂單送出流程中保留庫存預留邏輯。
- **關鍵決策**：移除未使用的攻擊面，而非引入推測性的外部/M2M 授權合約。

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

### 2026-09-04 — PR #11 — feat: add authenticated shopping cart v1
- **垂直切片**：Shopping Cart v1
- **交付價值**：
  - 已認證顧客持久化購物車
  - 重用 Pending Order 作為購物車載體
  - 惰性首次加購購物車建立（Lazy First-Add Creation）
  - 商品加入、數量更新、品項移除功能
  - 同一商品數量合併
  - 行總計與購物車總金額計算
  - Customer Web 加入購物車表單與 `/cart` 頁面 UI
  - 嚴格的顧客身分隔離（Customer Isolation）
- **驗證成果**：
  - 後端建置通過 (0 warnings, 0 errors)
  - 330 項後端自動化測試全數通過 (Domain: 71, Application: 111, Infrastructure: 26, WebApi.IntegrationTests: 122)
  - 前端程式碼檢查 (lint)、TypeScript 型別檢查與 production build 通過
  - 匿名購物車操作回傳 401 Unauthorized
  - 缺少 CustomerId claim 回傳 403 Forbidden
  - 顧客隔離與所有權保護測試全數通過
  - 治理審計與 git diff --check 檢查通過
- **關鍵決策**：
  - 無獨立 ShoppingCart aggregate、無專屬資料庫表、無額外資料庫遷移 (Migration)
  - CustomerId 嚴格僅自認證 Claims 提取，不接受來自瀏覽器任意指定
  - Access Token 維持在伺服器端，不暴露至瀏覽器
  - 首次加購併發競爭情況記錄為已接受之 v1 限制
  - 連接埠 3000/new-api 未被干擾；未宣稱 Live Browser Cart E2E 驗證

### 2026-09-04 — PR #12 — feat: add checkout order submission v1
- **垂直切片**：Checkout / Order Submission v1
- **交付價值**：
  - 已認證顧客付款前結帳審查（`/checkout`）
  - 購物車至結帳頁面導航（Cart &rarr; Checkout Navigation）
  - 伺服器端訂單送出（Server Action 與 server-only 訂單輔助模組）
  - 重用既有 Pending Order 進行提交
  - 權威性庫存保留與 MySQL 交易保護（InnoDB 列級悲觀鎖定與防死鎖排序）
  - 成功送出後訂單狀態轉換（Pending &rarr; Submitted）
  - 訂單送出後自動自作用中購物車清除
  - 已送出訂單確認頁面（`/orders/[id]`）與明細載入
  - 安全且明確的業務錯誤處理（庫存不足、狀態無效、空購物車等）
- **驗證成果**：
  - 後端建置通過 (0 warnings, 0 errors)
  - 343 項後端自動化測試全數通過 (Domain: 71, Application: 115, Infrastructure: 26, WebApi.IntegrationTests: 131)
  - 真實 MySQL OrderSubmission 驗收測試通過 (3 passed, 0 failed)
  - 真實 MySQL 庫存保留持久化驗證通過 (Available 50 &rarr; 48, Reserved 0 &rarr; 2)
  - 真實 MySQL 多品項庫存不足交易全額回滾驗證通過 (MYSQL_PARTIAL_RESERVATION_ROLLBACK=PASS)
  - 匿名訂單送出操作回傳 401 Unauthorized
  - 缺少 CustomerId claim 回傳 403 Forbidden
  - 跨顧客訂單查詢與送出阻擋（404 NotFound / Fail-Closed）
  - 前端程式碼檢查 (lint)、TypeScript 型別檢查與 production build 通過
  - 專案治理審計與 git diff --check 檢查通過
- **關鍵決策**：
  - 重用既有 `PUT /api/v1/orders/{id}/submit` 與 `SubmitOrderCommand` 正式路徑，正式後端零變更 (BACKEND_PRODUCTION_CHANGE_REQUIRED=NO)
  - 無新建 Checkout aggregate、無 Checkout 資料表、無資料庫遷移 (Migration)
  - 結帳流程維持為付款前階段（Pre-Payment），最終成功狀態為 `Submitted`，非 `Paid`
  - 付款整合 (Payment) 與出貨物流 (Shipping) 明確延後至後續垂直切片
  - 存取權杖嚴格維持於伺服器端，不暴露給瀏覽器
  - 連接埠 3000 / new-api 未受干擾；未宣稱 Live Browser Checkout E2E 驗證

### 2026-09-04 — PR #13 — feat: add payment integration v1
- **垂直切片**：Payment Integration v1
- **交付價值**：
  - 已認證顧客付款發起（POST /api/v1/payments/initiate）與所有權校驗
  - 綠界科技（ECPay AIO V5）託管信用卡支付整合
  - 提供者中立之 PaymentAttempt 狀態機與領域生命週期管理
  - 同訂單相同 IdempotencyKey 冪等重複使用作用中 Pending Attempt
  - 同訂單不同 IdempotencyKey 重試建立全新 PaymentAttempt 與獨立 MerchantTradeNo
  - 權威性綠界 ReturnURL 背景回調處理（POST /api/v1/payments/webhooks/ecpay）
  - 回調 CheckMacValue 雜湊簽章與特店編號安全驗證
  - 重複通知冪等性保障（PaymentWebhookReceipts 唯一約束）
  - SimulatePaid 模擬付款防護（不誤標記訂單為 Paid）
  - 延遲成功付款或已取消訂單之退款標記防護（RefundRequired）
  - Customer Web 安全託管 POST 表單跳轉與嚴格 ActionUrl 白名單
  - 移除未啟用之 Stripe 適配器原始碼、相依套件與測試，消除無效代碼
- **驗證成果**：
  - 後端建置通過 (0 warnings, 0 errors)
  - 431 項後端自動化測試全數通過 (Domain: 71, Application: 117, Infrastructure: 88, WebApi.IntegrationTests: 155)
  - 前端程式碼檢查 (lint)、TypeScript 型別檢查與 production build 通過
  - 綠界 Stage 測試環境真實端到端付款驗證通過 (ECPAY_GENUINE_STAGE_HAPPY_PATH=PASS)
  - 綠界真實背景回調通知接收與驗簽通過 (ECPAY_GENUINE_RETURNURL=PASS)
  - 失敗/未付款後重試生命週期供應商相容性驗證通過 (ECPAY_PROVIDER_RETRY_COMPATIBILITY=PASS)
  - 供應商端成功收費筆數確切為 1 筆，無重複扣款 (SUCCESSFUL_PROVIDER_CHARGE_COUNT=1, DUPLICATE_SUCCESSFUL_PROVIDER_CHARGE=NO)
  - 專案治理審計與 git diff --check 檢查通過
- **關鍵決策**：
  - 綠界科技 ECPay 為 Payment v1 唯一作用中提供者，不引入多供應商選擇器或執行期動態切換
  - 相同 OrderId + IdempotencyKey 重用現有 Pending Attempt；不同 IdempotencyKey 建立新 Attempt
  - 延遲成功或訂單狀態非 Submitted 時 Attempt 轉為 RefundRequired，訂單狀態不重複轉換
  - 現行資料庫結構原生支援一對多 PaymentAttempt 關聯，無需額外資料庫遷移 (ECPAY_SCHEMA_CHANGE_REQUIRED_FINAL=NO)
  - Stripe 因境外測試商戶註冊限制暫緩真實 E2E 驗證，並已自執行期與相依套件中完整移除

### 2026-09-05 — PR #14 — feat: add order shipping address v1
- **垂直切片**：Order Shipping Address v1（結帳配送地址快照）
- **交付價值**：
  - 顧客於結帳流程輸入收件人姓名、電話、國家、郵遞區號、城市與街道地址
  - 收件地址由訂單聚合根擁有，作為不可變更之歷史快照（ShippingAddress）
  - 擴充訂單送出流程：於交易與鎖定前執行地址驗證，並原子化持久化地址快照、保留庫存與變更狀態為 Submitted
  - 擁有者顧客可於訂單詳情頁檢視完整配送收件快照
  - 完整支援未含地址之歷史舊訂單查詢與渲染相容性（可安全處理 null）
- **驗證成果**：
  - 後端建置通過 (0 warnings, 0 errors)
  - 474 項後端自動化測試全數通過 (Domain: 105, Application: 119, Infrastructure: 89, WebApi.IntegrationTests: 161)
  - 前端程式碼檢查 (lint)、TypeScript 型別檢查與 production build 通過
  - 真實 MySQL 自前一版遷移（AddCustomerIdentities）之真實升級路徑驗證通過 (PREVIOUS_SCHEMA_TO_SHIPPING_MIGRATION_UPGRADE=PASS)
  - 空白資料庫全新遷移通過 (FRESH_DATABASE_MIGRATION=PASS)
  - 資料庫 Down 遷移回滾驗證通過 (MIGRATION_DOWN_VALIDATION=PASS)
  - 專案治理審計與 git diff --check 檢查通過
  - PII 安全防護邊界驗證通過（無日誌洩漏、無儲存持久化、無 URL 傳遞、權杖伺服端保留）
- **關鍵決策**：
  - ShippingAddress 歸屬於訂單聚合（Order Context），不建立 Customer 常用地址簿抽象
  - 送出後地址快照不可變更，不提供任意修改端點
  - 僅新增單一最小化 EF Core 資料庫遷移（Orders 表 7 個可為空欄位），不建立額外關聯表
  - 物流運費計算、貨運商 API 與物流追蹤明確延後至後續垂直切片
  - 生產環境付款 (Payment) 行為零變更，測試調整僅適配訂單送出之簽名規範

### 2026-09-05 — PR #15 — feat: add admin fulfillment v1
- **垂直切片**：Admin Fulfillment v1
- **交付價值**：
  - 建立 Admin-only Paid Order fulfillment queue
  - 建立 Auth0-backed Admin Web 履約儀表板
  - 管理員可檢視已付款訂單、品項及 ShippingAddress
  - 重用既有 ShipOrder 流程完成 Paid → Shipped
  - 已出貨訂單自 Paid fulfillment queue 移除
- **驗證成果**：
  - 後端建置通過，505 項後端自動化測試全數通過 (Domain 105 / Application 128 / Infrastructure 89 / WebApi Integration 183)
  - MySQL fulfillment acceptance test 通過
  - Admin lint、TypeScript typecheck、production build 通過
  - Genuine Auth0 non-Admin E2E 回傳 403 且無訂單/ShippingAddress PII 洩漏
  - Genuine Auth0 Admin login 與 role authorization 通過
  - Genuine browser Ship E2E 通過
  - DB 狀態 Paid → Shipped
  - fulfillment queue 1 → 0
  - git diff --check 與 governance audit 通過
- **關鍵決策**：
  - ASP.NET RoleClaimType 採 provider-neutral 可配置設計
  - genuine Auth0 Admin role claim 使用 `urn:enterprisecommerce:roles`
  - Admin Web 使用獨立 Auth0 Regular Web Application，但重用既有 EnterpriseCommerce API audience
  - Access Token 僅留在 server-side，並限制只能轉送至 configured API origin
  - 重用既有 ShipOrder / Order.Ship()，不建立第二套 Shipment state machine
  - 無資料庫 migration、無 Payment production 變更、無 Customer Web 變更
  - 完整 Admin Order Management 明確延後至下一個獨立 Vertical Slice

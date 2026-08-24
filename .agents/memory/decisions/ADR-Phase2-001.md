# ADR-Phase2-001: Bounded Context Boundary Decision

## Context
延續 Phase 1 的 Domain 分析，在進行 Domain Driven Design (DDD) 的 Strategic Design 時，我們需要為系統定義明確的 Bounded Context 邊界，並確保各 Context 職責專一、具備獨立演進的能力。尤其針對「購物車 (Cart)」、「付款 (Payment)」與「評論 (Review)」等功能，是否應獨立為 Context 是本次設計的核心決策。

## Decision
我們決定確立以下 7 個 Bounded Context 劃分：
1. **Catalog Context**: 負責商品展示與檢索。
2. **Order Context**: 負責交易生命週期。
3. **Cart Context (從 Order 獨立)**: 專責管理購買意圖與極高頻率的暫存寫入。
4. **Inventory Context**: 專責防超賣強一致性邏輯。
5. **Payment Context (新增)**: 作為隔離外部第三方金流網關的 Anti-Corruption Layer (ACL)。
6. **Marketing Context (包含 Review)**: 負責所有促銷規則與用戶商品互動。
7. **Identity Context**: 處理認證與權限。

## Reason
- **Cart 獨立：** 購物車的生命週期短暫且異動極為頻繁，讀寫比極高；相較之下，Order 需要嚴謹的狀態機防護與 ACID 事務。將兩者剝離可允許 Cart 使用更輕量、高併發的儲存方案（如 Redis Cache），而不影響 Order 的關聯式結構設計。
- **Payment 獨立：** 金流商 API 變動頻繁且狀態機複雜。透過 Payment Context 作為 ACL，Order Context 可專注於核心業務邏輯，僅需透過 `PaymentAuthorized` 等 Domain Event 即可得知金流結果，達到高度解耦。
- **Review 併入 Marketing：** 評價系統目前作為增加轉換率的輔助工具，歸類於 Engagement 範疇，在業務初期暫不需要獨立為龐大的 Context。

## Alternative Considered
- **Alternative 1: 讓 Order Context 直接管理購物車。** 
  - *Why rejected:* 雖然邏輯相近，但在大型電商情境下（如雙 11 等大促活動），購物車的寫入量極大，會直接壓垮 Order 的資料庫，違反效能與資源隔離原則。
- **Alternative 2: 將 Customer Profile 獨立為 Customer Context。**
  - *Why rejected:* 目前會員系統除帳號密碼外，多為收件地址或優惠券紀錄。收件地址可直接由 Order 擷取為快照 (Snapshot) 管理（以確保歷史訂單地址不變），優惠券屬 Marketing。無足夠複雜的「客戶資產」以支撐獨立的 Customer Context。因此將認證收斂至 Identity Context 即可。

## Impact
- **通訊方式：** Cart 與 Marketing 將使用 API 即時呼叫（同步依賴）；而 Order, Inventory, Payment 之間將大量採用 Domain Event (非同步) 以確保系統強健性 (Resilience) 與最終一致性 (Eventual Consistency)。
- **持久化考量：** 每個 Bounded Context 被賦予獨立的 Database 架構選擇權。例如 Cart 可選 Redis，Order 與 Inventory 可選 MySQL/PostgreSQL，Identity 甚至可外包給第三方 IdP。

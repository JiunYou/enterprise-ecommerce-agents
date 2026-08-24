---
Document ID: DOC-DOM-004
Version: 1.0
Owner Agent: Domain Architect Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-Phase1-001
---

# Domain Boundary Candidate

基於 Phase 1 的業務領域分析，我們初步識別出以下 Future Bounded Context Candidates。這些 Context 將在 Phase 2 (DDD Design) 中進行更細緻的驗證與設計。

## 1. Catalog Context
- **為何需要分離：** 商品資料（如名稱、描述、圖片）具有高讀取頻率，且變化較慢。與交易和庫存的生命週期完全不同，適合獨立部署以應付高併發搜尋與瀏覽。
- **主要責任：** 商品資料維護、分類樹狀結構、搜尋索引。
- **與其他 Context 關係：**
  - 提供商品快照給 Order Context 建立訂單。

## 2. Order Context
- **為何需要分離：** 訂單是業務核心，狀態機複雜且涉及多個系統（庫存、支付）。必須與瀏覽行為解耦，確保交易的高可用性與一致性。
- **主要責任：** 購物車管理、訂單創建、訂單狀態機流轉、價格最終計算。
- **與其他 Context 關係：**
  - 需要鎖定 Inventory Context 的庫存。
  - 需要套用 Marketing Context 的優惠規則。

## 3. Inventory Context
- **為何需要分離：** 庫存的扣減需要極高的一致性處理（防超賣），技術挑戰大（如使用 Redis 或分散式鎖）。與商品展示分離可確保商品模組不被庫存高併發操作拖垮。
- **主要責任：** 可用庫存計算、庫存預留 (Reservation)、實際扣減與回補。
- **與其他 Context 關係：**
  - 接收來自 Order Context 的預留與扣減請求。

## 4. Identity Context
- **為何需要分離：** 認證與授權屬於通用基礎設施，且安全要求極高。獨立出 Context 有利於未來實作 SSO 或替換為第三方 Identity Provider (如 Auth0)。
- **主要責任：** 會員註冊、JWT 發放、RBAC 角色管理。
- **與其他 Context 關係：**
  - 所有其他 Context 皆依賴其驗證 JWT 以識別使用者身份。

## 5. Marketing Context
- **為何需要分離：** 促銷規則引擎變動頻繁，且在大型活動（如雙 11）時會有極大負載。獨立出來有助於敏捷開發與獨立擴容。
- **主要責任：** 優惠券發放、折扣計算邏輯、收藏與評論管理。
- **與其他 Context 關係：**
  - 提供折扣資訊給 Order Context (購物車)。

---
**Assumption (未確認假設):**
- 購物車 (Shopping Cart) 目前劃分在 Order Context 內。
- **Business Question:** 購物車若未來被當作一種「行銷與促銷提醒」的工具，是否應該從 Order Context 中剝離，形成獨立的 Cart Context？

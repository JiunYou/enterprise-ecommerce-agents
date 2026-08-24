# ADR-Phase1-001: Business Domain Classification Decision

## Context
本專案為全新開發之企業級電商平台 (Enterprise E-Commerce Platform)。在 Phase 1 (Business Domain Analysis) 階段，我們必須根據 Domain Driven Design (DDD) 的原則，對系統進行高階的領域劃分，識別出核心競爭優勢 (Core Domain)、支援業務 (Supporting Domain) 以及通用基礎建設 (Generic Domain)。此決策將直接影響後續微服務或模組邊界的劃分與資源投入的優先順序。

## Decision
我們決定將業務領域劃分並分類如下：
- **Core Domain:** Order Domain, Marketing Domain
- **Supporting Domain:** Catalog Domain, Inventory Domain
- **Generic Domain:** Identity & Access Domain, Analytics Domain

具體考量為：將資源集中於「訂單轉換 (Order)」與「促銷引擎 (Marketing)」以建立商業護城河；將「使用者認證 (Identity)」視為可標準化或替換的通用元件；將「商品 (Catalog)」與「庫存 (Inventory)」視為關鍵的業務支援系統，但非獨特創新所在。

## Rationale
1. **資源最大化：** 企業資源有限，必須將最優秀的架構設計與開發精力投入在 Core Domain (Order & Marketing)，以帶來最大的營收影響 (Revenue Impact)。
2. **變更隔離：** 行銷活動與優惠規則 (Marketing) 變動頻繁，獨立並視為核心領域有助於敏捷迭代。
3. **系統穩定性：** 將高併發的商品展示 (Catalog) 與強一致性要求的庫存扣減 (Inventory) 從訂單核心分離，可避免彼此的非功能性需求互相干擾。

## Impact
- **技術選型影響：** Core Domain 需採用高度可測試、可擴展的架構（如 Clean Architecture, Domain Events）；Generic Domain 可優先考慮採用現成開源方案或第三方服務（如 JWT/Identity Provider）。
- **團隊組織：** 未來團隊的分工將以這些 Domain Boundary 為基礎（Conway's Law）。

## Alternative Considered
**Alternative 1:** 將 Inventory 視為 Core Domain。
- **Why rejected:** 雖然庫存技術挑戰極大（防超賣），但它屬於防禦性需求（確保不出錯），而非進攻性需求（創造新營收）。因此將其定調為 Supporting Domain。

**Alternative 2:** 將 Shopping Cart 獨立為單獨的 Core Domain。
- **Why rejected:** 初期購物車邏輯與訂單高度綁定，為避免過度設計，目前先將其收斂於 Order Domain。若未來購物車具備高度複雜的放棄挽回 (Cart Abandonment) 行銷邏輯，再考慮分離。

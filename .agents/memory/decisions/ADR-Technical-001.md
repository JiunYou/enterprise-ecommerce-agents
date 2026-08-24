# ADR-Technical-001: ASP.NET Core Architecture Pattern Decision

## Metadata
- **ADR ID:** ADR-Technical-001
- **Status:** Proposed
- **Owner Agent:** .NET Backend Agent
- **Created Date:** 2026-08-24
- **Related ADR:** ADR-001

## Context
作為 Enterprise E-Commerce Platform 核心模組的 ASP.NET Core Modular Monolith 負責處理高度複雜的交易狀態與強一致性資料 (Order, Inventory, Identity 等)。為了確保這套核心系統能長期維護、防禦技術債的累積，並順利實踐 Domain Driven Design (DDD) 中「領域模型為核心」的理念，我們必須決定其內部的架構分層模式 (Architecture Pattern)。

## Problem
傳統的三層架構 (N-Tier Layered Architecture) 容易造成領域邏輯 (Business Logic) 與資料庫存取層 (Data Access) 或基礎設施高度耦合，導致替換框架、撰寫單元測試極其困難，且無法貫徹 DDD 中 Aggregate 內部的不變性。

## Options
- **Option A:** Traditional Layered Architecture (Presentation -> Business -> Data Access)
- **Option B:** Clean Architecture (Domain 為核心，依賴反轉)
- **Option C:** Hexagonal Architecture (Ports and Adapters)

## Decision
我們選擇 **Option B: Clean Architecture**（部分概念融合 Hexagonal Architecture）。

具體規範如下：
1. **Domain Layer 為絕對核心**：無任何外部相依性，包含 Entity, Aggregate Root, Value Object。
2. **Application Layer 負責 Use Case (CQRS)**：定義 Repository Interface 與外圍依賴介面，處理流程協調，但不涉及具體實作。
3. **Infrastructure Layer 負責實作細節**：透過 Dependency Injection 注入 EF Core, 第三方 API 實作。
4. **依賴方向 (Dependency Rule)**：嚴格只能由外向內，外層依賴內層。

## Consequences

### Positive Consequences
- **核心高度純粹：** 業務邏輯與資料庫、UI 框架完全解耦。未來即使更換 ORM 或升級 .NET 框架，核心 Domain 程式碼幾乎不需修改。
- **高度可測試：** Application Layer 與 Domain Layer 可以不依賴資料庫、輕鬆透過 Mock 介面撰寫快速且穩定的 Unit Tests。
- **強制 DDD 實踐：** 由於 Infrastructure 被推到最外層，開發者被迫先思考領域模型 (Domain Model)，而非先設計 Database Tables。

### Negative Consequences
- **初期學習曲線高：** 開發團隊與 AI Agent 需要適應 CQRS, MediatR, Repository Pattern 與依賴反轉 (IoC) 等大量介面轉換。
- **程式碼樣板增加 (Boilerplate)：** 一個簡單的 CRUD 操作可能需要建立 DTO, Command, Handler, Interface 與 Controller 等多個檔案，初期開發速度較傳統架構略慢。

## Future Considerations
為了減緩 Boilerplate 過多的問題，團隊需導入程式碼生成器 (Scaffolding) 或 AI 開發流程來加速標準 CRUD Command 的建立，將精力集中在複雜的領域邏輯上。

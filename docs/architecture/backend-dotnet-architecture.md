---
Document ID: DOC-ARC-006
Version: 1.0
Owner Agent: .NET Backend Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-Technical-001
---

# ASP.NET Core Architecture Design

本文件定義 Enterprise E-Commerce Platform 後端核心 (Modular Monolith) 的架構規範，採用 Clean Architecture。

## 1. Architecture Pattern: Clean Architecture
依據 ADR-Technical-001 決策，ASP.NET Core 單體將採用 Clean Architecture，確保業務邏輯 (Domain) 不受框架與基礎設施污染。

## 2. Layer Responsibility & Dependency Direction
依賴方向 (Dependency Direction) 必須嚴格由外向內，內層不可參照外層。
- **Domain Layer (最內層):** 包含 Entity, Value Object, Aggregate Root, Domain Event, Domain Exception。絕對不依賴任何外部套件（除 .NET 基礎型別外）。
- **Application Layer:** 包含 Use Cases (CQRS Commands/Queries), DTOs, Repository Interfaces, Validation Rules (FluentValidation)。負責協調 Domain Object 完成任務。
- **Infrastructure Layer:** 包含 Repository 實作 (EF Core), 第三方 API Client (如呼叫外部系統), Message Broker 實作 (RabbitMQ)。
- **Presentation/API Layer (最外層):** ASP.NET Core Web API 專案。負責 Controller/Minimal API 定義、Middleware、DI 容器註冊、Swagger。

## 3. Module Isolation (Modular Monolith 邊界)
- **目錄結構:** 頂層以 Context (如 `Order`, `Inventory`, `Identity`) 劃分 Module (Project)。每個 Module 內部再實作自己的 Clean Architecture 4 層。
- **Isolation Rule:** `Order` Module 的 Application Layer 絕對禁止參照 `Inventory` Module 的 Infrastructure Layer 或 DB Context。

## 4. Domain Interaction
Module 之間的通訊嚴格受控：
1. **In-Process API (Synchronous):** 僅限唯讀查詢，透過定義在 Shared Kernel 的 Interfaces 呼叫。
2. **Domain Events (Asynchronous):** 寫入或狀態變更的跨模組溝通，必須透過發佈 Domain Event，交由 Message Broker (或 MediatR In-Memory Bus) 觸發下游模組處理。

## 5. Transaction Boundary
- 一個 Command 只能修改一個 Aggregate Root。
- 採用 Entity Framework Core 實作 Repository Pattern。
- **Unit of Work:** 交易邊界僅限於單一 Module 的單次 HTTP Request 內。若跨 Module (如 Order 扣 Inventory)，必須依賴最終一致性 (Eventual Consistency)，不使用分散式事務 (2PC/DTC)。

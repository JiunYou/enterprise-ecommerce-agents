---
Document ID: DOC-ARC-FINAL-001
Version: 1.0
Owner Agent: System Architect Agent
Created Date: 2026-08-24
Status: Approved
---

# Architecture Overview

本文件為 Enterprise E-Commerce Platform 專案之最終架構概覽 (Architecture Package)，彙整了從業務領域到具體技術與安全之全景藍圖，供開發人員、AI Agent、安全團隊與未來維護者作為單一事實來源 (Single Source of Truth)。

## 1. System Vision
打造一個具備高擴展性、強資料一致性與高安全性的企業級電子商務平台，支援 B2C (Customer Web) 與 B2B (Admin Portal) 情境。

## 2. Architecture Style
依據 `ADR-001`，系統採用 **Hybrid Architecture (混合架構)**：
- 核心交易與強一致性邏輯保留於單體架構 (ASP.NET Core Modular Monolith)。
- 高併發與非同步整合任務拆分為獨立微服務 (Node.js Services)。

## 3. DDD Summary
透過 Strategic Design (`ADR-Phase1-001`, `ADR-Phase2-001`) 劃分出：
- **Core Domain:** Order, Inventory
- **Supporting Domain:** Catalog, Identity
- **Generic Domain:** Notification, Payment
- **Bounded Context:** 各領域界線清晰，跨邊界採用 Domain Events (RabbitMQ) 實現最終一致性。

## 4. C4 Summary
透過 C4 Model (`ADR-Phase4-001`) 定義了系統宏觀至微觀的層次：
- **Level 1 Context:** 釐清與 Customer, Admin 及外部 Provider (Payment/Email) 的邊界。
- **Level 2 Container:** 確定了 Next.js, ASP.NET Core, Node.js, MySQL, Redis, RabbitMQ 的部署拓撲。
- **Level 3 Component:** 定義了 ASP.NET Core Modular Monolith 內部的依賴關係。

## 5. Technology Stack
- **Frontend:** Next.js (App Router), React, TypeScript, Zustand, next-intl
- **Backend (Core):** ASP.NET Core 8+, Clean Architecture, Entity Framework Core
- **Backend (Services):** Node.js, TypeScript
- **Database / Cache:** MySQL, Redis
- **Message Broker:** RabbitMQ
- **Search:** Elasticsearch
- **Infrastructure:** Docker, Cloud Secret Manager

## 6. Security Summary
基於 OWASP 與 STRIDE (`ADR-Security-001`, `ADR-Security-002`)，系統具備：
- **Identity:** 雲端託管 OIDC Provider，JWT (HttpOnly Secure Cookie)。
- **Authorization:** 結合 RBAC 與 ABAC (Resource Owner Validation)。
- **Secret Management:** 嚴禁 Hardcode，全面依賴 Cloud Secret Manager 與 IAM Role 掛載。
- **Audit Logging:** 嚴格紀錄關鍵寫入操作之 Who/When/What/Where/Result。

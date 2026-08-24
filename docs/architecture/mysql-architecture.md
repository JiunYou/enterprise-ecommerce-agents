---
Document ID: DOC-ARC-008
Version: 1.0
Owner Agent: Database Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-001
---

# MySQL Architecture

MySQL 作為平台核心關聯式資料庫，其架構規劃必須符合 DDD 的隔離原則與高效能維運需求。

## 1. Schema Strategy & Data Ownership
- 單一 Database Cluster，但針對不同的 Modular Monolith Context (如 Order, Inventory, Identity) 建立 **獨立的 Logical Database (Schema)**。
- **資料擁有權 (Data Ownership):** 
  - `Identity` 模組只能讀寫 `identity_db`；`Order` 模組只能讀寫 `order_db`。
  - 絕對禁止跨 Schema 直接進行 JOIN 操作（防範架構退化）。需要跨模組資料時，只能在 Application 記憶體層組裝，或依賴資料冗餘/快照 (Snapshot)。

## 2. Transaction Strategy
- 支援 ACID 事務，但範圍限制在單一 Database 內部。
- 採用 Optimistic Concurrency Control (樂觀鎖) 防止併發更新衝突（特別是在 `inventory_db` 的庫存扣減）。
- 在 Entity Framework Core 中設定 RowVersion 欄位實作併發控制。

## 3. Migration Strategy
- 採用 Code-First Migration (EF Core Migrations)。
- 嚴格遵守「向前相容 (Forward-Compatible)」的 Schema 更新原則：
  - 不可直接刪除欄位 (Drop Column) 或修改型別。
  - 若需破壞性變更，需採用多階段發布 (Add New Column -> Dual Write -> Read from New -> Drop Old)。

## 4. Backup Strategy
- 每日完整備份 (Full Backup) 並上傳至 Object Storage (AWS S3) 以防範災難。
- 啟用二進位日誌 (Binlog) 以支援 Point-in-Time Recovery (PITR)。

## 5. Replication Strategy
- 採用 Primary-Replica (Master-Slave) 架構。
- 寫入請求與強一致性讀取指向 Primary 節點。
- 針對繁重的後台報表 (Administration) 查詢，配置 Read-Only Replica 進行流量卸載 (Read-Write Splitting)，保護核心寫入效能。

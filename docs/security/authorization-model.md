---
Document ID: DOC-SEC-003
Version: 1.0
Owner Agent: Security Review Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: None
---

# Authorization Model

## 1. Authorization Strategy
本系統融合 **RBAC (Role-Based Access Control)** 與 **ABAC (Attribute-Based Access Control)**。
- **RBAC:** 用於功能模組的存取控制 (例如：是否允許進入商品管理頁面)。
- **ABAC:** 用於資料層級的存取控制 (例如：Customer 只能查看 `ownerId == currentUserId` 的訂單)。

## 2. Role Matrix

| Role | 權限範圍 (Permissions) | 備註 |
| :--- | :--- | :--- |
| **Customer** | 僅能讀寫自己擁有的資料 (訂單、購物車、Profile)。對 Catalog 為唯讀。 | - |
| **Admin** | 具備基礎後台檢視與操作權限，受限於特定指派模組。 | - |
| **Product Manager** | 可讀寫 Catalog, 調整售價與分類，無權干涉庫存與訂單。 | - |
| **Inventory Manager** | 僅可管理 Warehouse 與 Stock 的增減。 | - |
| **Order Manager** | 可檢視訂單、處理退換貨 (Refund/Cancel)，無權變更商品定價。 | - |
| **Auditor** | 僅具備系統所有資料與 Audit Log 的 **唯讀 (Read-Only)** 權限。 | 不可進行寫入操作 |
| **Super Admin** | 最高權限，可管理系統組態與員工權限指派。 | 需經過最嚴格的存取控制 (如 IP 白名單 + 硬體 MFA) |

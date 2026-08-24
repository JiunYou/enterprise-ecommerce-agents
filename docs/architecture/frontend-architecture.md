---
Document ID: DOC-ARC-005
Version: 1.0
Owner Agent: Frontend Agent
Created Date: 2026-08-24
Status: Draft
Related ADR: ADR-001
---

# Frontend Architecture Design

本文件定義 Enterprise E-Commerce Platform 的前端 (Customer Web Application & Admin Portal) 架構規範。

## 1. Technology Stack
- **Framework:** Next.js (App Router)
- **Library:** React
- **Language:** TypeScript (Strict Mode)

## 2. Application Structure (FSD Pattern)
我們採用 Feature-Sliced Design (FSD) 作為專案結構基礎，以確保高內聚與低耦合：
- `app/`: Next.js App Router 的進入點與頁面佈局 (Routing)。
- `features/`: 封裝特定的業務功能 (如 `Cart`, `Checkout`, `ProductDetail`)。
- `entities/`: 核心領域資料的 UI 呈現邏輯與共享狀態 (如 `User`, `Product`)。
- `shared/`: 跨全專案共用的基礎 UI 元件 (Button, Input)、API Client、Hooks 與 Utils。

## 3. Routing Strategy
- **Customer Web (B2C):** 運用 Next.js App Router。商品展示首頁與分類頁採用 Static Site Generation (SSG) 或 Incremental Static Regeneration (ISR) 確保最佳 SEO 與 LCP 效能；購物車與結帳頁面則採用 Server-Side Rendering (SSR) 或 Client-Side Rendering (CSR) 以處理動態資料。
- **Admin Portal (B2B):** 以 Client-Side Rendering (CSR) 為主，並在 Root Layout 實作 Route Guard (Auth Middleware)，攔截未授權的存取。

## 4. Component Architecture
- **Server Components (RSC):** 優先使用 Server Components 進行靜態資料獲取與渲染，降低 Client-Side Bundle Size。
- **Client Components:** 僅在需要互動 (useState, onClick)、生命週期 (useEffect) 或存取 Browser API 時，標記 `"use client"`。

## 5. State Management
- **Server State:** 使用 React Server Components 或 SWR / React Query 進行快取與資料同步。
- **Global UI State:** 使用 Zustand 處理跨層級的輕量級全域狀態 (如：購物車側邊欄開關、Theme 狀態)。
- **Local State:** 使用 React 原生的 `useState` 與 `useReducer`。

## 6. Authentication Flow
- 使用 NextAuth.js 整合 Identity Context，獲取 JWT Token。
- 將 Access Token 儲存於 HttpOnly Secure Cookie 中防範 XSS。
- 在 Middleware 中驗證 Token 有效性並決定路由導向 (如未登入導向 `/login`)。

## 7. API Client Pattern
- 封裝基於 `fetch` 或 `Axios` 的全域 API Client。
- **Request Interceptor:** 自動夾帶 JWT Token (Authorization Header)。
- **Response Interceptor:** 統一攔截 401 Unauthorized，並無縫觸發 Refresh Token 流程，若失敗則清空狀態導向登入頁。

## 8. Error Handling
- **Boundary:** 在 App Router 的每一層定義 `error.tsx` 進行 Error Boundary 捕捉。
- **API Error:** 統一擷取後端回傳的 ProblemDetails (RFC 7807) 格式，轉譯為 User-Friendly 提示訊息 (Toast)。

## 9. Internationalization Strategy (i18n)
- 採用 `next-intl` 處理多語系。
- 語系切換 (Locale) 綁定在 URL 路徑中 (如 `/en-US/products`, `/zh-TW/products`)，確保多語系頁面能被搜尋引擎正確索引 (SEO Friendly)。

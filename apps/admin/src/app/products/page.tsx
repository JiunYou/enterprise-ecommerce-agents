import Link from "next/link";
import { auth0 } from "@/lib/auth0";
import { getAdminProducts, AdminProductSummary } from "@/lib/products";
import { AdminHeader } from "@/components/AdminHeader";

export const dynamic = "force-dynamic";

interface ProductsPageProps {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}

export default async function AdminProductsPage({ searchParams }: ProductsPageProps) {
  const session = await auth0.getSession();

  // 1. 未登入狀態處理
  if (!session || !session.user) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-6 sm:p-8 shadow-xs dark:border-zinc-800 dark:bg-zinc-900">
          <div className="flex flex-col items-center text-center">
            <div className="flex h-12 w-12 items-center justify-center rounded-full bg-indigo-50 text-indigo-600 dark:bg-indigo-950 dark:text-indigo-400">
              <svg
                className="h-6 w-6"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                aria-hidden="true"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth="2"
                  d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"
                />
              </svg>
            </div>
            <h1 className="mt-4 text-xl font-bold tracking-tight text-zinc-900 dark:text-zinc-50">
              Enterprise Commerce 管理後台
            </h1>
            <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
              請使用具備管理員權限的帳號登入，以存取商品目錄管理系統。
            </p>
            <a
              href="/auth/login"
              className="mt-6 inline-flex min-h-[44px] w-full touch-manipulation items-center justify-center rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:bg-indigo-500 dark:hover:bg-indigo-400"
            >
              管理員登入
            </a>
          </div>
        </div>
      </div>
    );
  }

  // 2. 解析 Next.js 16 Promise-based searchParams
  const resolvedParams = await searchParams;

  const rawPage = Array.isArray(resolvedParams.page)
    ? resolvedParams.page[0]
    : resolvedParams.page;
  const page = rawPage && parseInt(rawPage, 10) > 0 ? parseInt(rawPage, 10) : 1;

  const rawSearch = Array.isArray(resolvedParams.searchTerm)
    ? resolvedParams.searchTerm[0]
    : resolvedParams.searchTerm;
  const currentSearch = rawSearch && rawSearch.trim() !== "" ? rawSearch.trim() : undefined;

  const rawOnlyActive = Array.isArray(resolvedParams.onlyActive)
    ? resolvedParams.onlyActive[0]
    : resolvedParams.onlyActive;
  // onlyActive: "true" => true, "false" => false, 預設 false (管理員預設檢視全量目錄)
  const currentOnlyActive =
    rawOnlyActive === "true" ? true : rawOnlyActive === "false" ? false : false;

  const rawSortBy = Array.isArray(resolvedParams.sortBy)
    ? resolvedParams.sortBy[0]
    : resolvedParams.sortBy;
  const currentSortBy =
    rawSortBy && ["name", "price"].includes(rawSortBy.trim().toLowerCase())
      ? rawSortBy.trim().toLowerCase()
      : undefined;

  const rawSortOrder = Array.isArray(resolvedParams.sortOrder)
    ? resolvedParams.sortOrder[0]
    : resolvedParams.sortOrder;
  const currentSortOrder =
    rawSortOrder && ["asc", "desc"].includes(rawSortOrder.trim().toLowerCase())
      ? rawSortOrder.trim().toLowerCase()
      : undefined;

  const pageSize = 20;

  // 3. 呼叫管理員商品列表 API
  const result = await getAdminProducts({
    page,
    pageSize,
    onlyActive: currentOnlyActive,
    searchTerm: currentSearch,
    sortBy: currentSortBy,
    sortOrder: currentSortOrder,
  });

  // 4. 處理未授權（401）狀態
  if (result.status === "unauthenticated") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-6 sm:p-8 text-center shadow-xs dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            登入階段已過期
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            您的身分驗證權杖已逾期或無效，請重新進行登入。
          </p>
          <div className="mt-6 flex flex-wrap justify-center gap-3">
            <a
              href="/auth/login"
              className="inline-flex min-h-[44px] touch-manipulation items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:bg-indigo-500 dark:hover:bg-indigo-400"
            >
              重新登入
            </a>
            <a
              href="/auth/logout"
              className="inline-flex min-h-[44px] touch-manipulation items-center justify-center rounded-lg border border-zinc-300 px-4 py-2 text-sm font-semibold text-zinc-700 shadow-xs transition-colors hover:bg-zinc-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-800"
            >
              登出
            </a>
          </div>
        </div>
      </div>
    );
  }

  // 5. 處理權限不足（403 Forbidden）狀態
  if (result.status === "forbidden") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-lg rounded-xl border border-rose-200 bg-rose-50/50 p-6 sm:p-8 text-center shadow-xs dark:border-rose-900/50 dark:bg-rose-950/20">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-rose-100 text-rose-600 dark:bg-rose-900/50 dark:text-rose-400">
            <svg
              className="h-6 w-6"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636"
              />
            </svg>
          </div>
          <h2 className="mt-4 text-xl font-bold text-zinc-900 dark:text-zinc-50">
            存取被拒 (403 Forbidden)
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            當前登入之帳號未具備系統管理員 (Admin) 權限，無法存取商品目錄管理。
          </p>
          <div className="mt-6 flex justify-center">
            <a
              href="/auth/logout"
              className="inline-flex min-h-[44px] touch-manipulation items-center justify-center rounded-lg border border-zinc-300 bg-white px-4 py-2 text-sm font-semibold text-zinc-700 shadow-xs transition-colors hover:bg-zinc-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
            >
              切換帳號 / 登出
            </a>
          </div>
        </div>
      </div>
    );
  }

  // 6. 處理 400 Bad Request
  if (result.status === "badRequest") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-amber-200 bg-white p-6 sm:p-8 text-center shadow-xs dark:border-amber-900/50 dark:bg-zinc-900">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-amber-100 text-amber-600 dark:bg-amber-950 dark:text-amber-400">
            <svg
              className="h-6 w-6"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
          </div>
          <h2 className="mt-4 text-lg font-bold text-zinc-900 dark:text-zinc-50">
            查詢參數無效
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400 break-words">
            {result.message}
          </p>
          <div className="mt-6">
            <Link
              href="/products"
              className="inline-flex min-h-[44px] touch-manipulation items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:bg-indigo-500 dark:hover:bg-indigo-400"
            >
              重設篩選條件
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // 7. 處理伺服器連線/未知錯誤
  if (result.status === "error") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-6 sm:p-8 text-center shadow-xs dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            系統連線異常
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400 break-words">
            {result.message}
          </p>
          <div className="mt-6">
            <Link
              href="/products"
              className="inline-flex min-h-[44px] touch-manipulation items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:bg-indigo-500 dark:hover:bg-indigo-400"
            >
              重新整理
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const { items, totalCount } = result.data;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  // 建構分頁輔助連結（保持 query 參數完整）
  const createPaginationUrl = (targetPage: number) => {
    const params = new URLSearchParams();
    params.set("page", targetPage.toString());
    params.set("onlyActive", currentOnlyActive.toString());
    if (currentSearch) {
      params.set("searchTerm", currentSearch);
    }
    if (currentSortBy) {
      params.set("sortBy", currentSortBy);
    }
    if (currentSortOrder) {
      params.set("sortOrder", currentSortOrder);
    }
    return `/products?${params.toString()}`;
  };

  const isFiltered =
    Boolean(currentSearch) ||
    currentOnlyActive !== false ||
    Boolean(currentSortBy) ||
    Boolean(currentSortOrder);

  return (
    <div className="min-h-screen bg-zinc-50 dark:bg-zinc-950">
      {/* 導航標頭 */}
      <AdminHeader
        activeSection="products"
        title="商品目錄管理 (Admin Catalog)"
        subtitle="商品狀態監控、價格調整與下架停用"
        userLabel={session.user.name || session.user.email || "管理員"}
        action={
          <Link
            href="/products/new"
            className="inline-flex min-h-[36px] touch-manipulation items-center justify-center rounded-lg bg-indigo-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:bg-indigo-500 dark:hover:bg-indigo-400"
          >
            建立商品
          </Link>
        }
      />

      {/* 主要內容區 */}
      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 sm:py-8 lg:px-8">
        {/* 篩選與搜尋表單 (原生 GET Form，維持 URL 驅動架構) */}
        <section
          aria-labelledby="products-filter-heading"
          className="mb-6 rounded-xl border border-zinc-200 bg-white p-4 sm:p-5 shadow-xs dark:border-zinc-800 dark:bg-zinc-900"
        >
          <h2 id="products-filter-heading" className="sr-only">
            商品查詢與篩選工具
          </h2>
          <form
            method="GET"
            action="/products"
            className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:flex lg:flex-wrap lg:items-end lg:gap-4"
          >
            {/* 搜尋關鍵字或 SKU */}
            <div className="w-full sm:col-span-2 lg:col-span-1 lg:min-w-[240px] lg:flex-1">
              <label
                htmlFor="searchTerm"
                className="mb-1 block text-xs font-semibold text-zinc-700 dark:text-zinc-300"
              >
                搜尋商品名稱或 SKU
              </label>
              <input
                type="text"
                id="searchTerm"
                name="searchTerm"
                defaultValue={currentSearch || ""}
                placeholder="輸入商品關鍵字或 SKU..."
                className="block h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-xs text-zinc-900 placeholder-zinc-400 shadow-xs focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 sm:h-9 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder-zinc-500"
              />
            </div>

            {/* 狀態過濾 */}
            <div className="w-full sm:w-auto lg:w-48">
              <label
                htmlFor="onlyActive"
                className="mb-1 block text-xs font-semibold text-zinc-700 dark:text-zinc-300"
              >
                狀態過濾
              </label>
              <select
                id="onlyActive"
                name="onlyActive"
                defaultValue={currentOnlyActive ? "true" : "false"}
                className="block h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-xs text-zinc-900 shadow-xs focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 sm:h-9 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
              >
                <option value="false">全量目錄 (含已停用)</option>
                <option value="true">僅上架中 (Active)</option>
              </select>
            </div>

            {/* 排序欄位 */}
            <div className="w-full sm:w-auto lg:w-36">
              <label
                htmlFor="sortBy"
                className="mb-1 block text-xs font-semibold text-zinc-700 dark:text-zinc-300"
              >
                排序欄位
              </label>
              <select
                id="sortBy"
                name="sortBy"
                defaultValue={currentSortBy || "name"}
                className="block h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-xs text-zinc-900 shadow-xs focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 sm:h-9 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
              >
                <option value="name">商品名稱</option>
                <option value="price">商品售價</option>
              </select>
            </div>

            {/* 排序方向 */}
            <div className="w-full sm:w-auto lg:w-32">
              <label
                htmlFor="sortOrder"
                className="mb-1 block text-xs font-semibold text-zinc-700 dark:text-zinc-300"
              >
                排序方向
              </label>
              <select
                id="sortOrder"
                name="sortOrder"
                defaultValue={currentSortOrder || "asc"}
                className="block h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-xs text-zinc-900 shadow-xs focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 sm:h-9 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
              >
                <option value="asc">升冪 (遞增)</option>
                <option value="desc">降冪 (遞減)</option>
              </select>
            </div>

            {/* 篩選與重設動作按鈕組 */}
            <div className="flex flex-wrap items-center gap-2 pt-1 sm:col-span-2 sm:pt-0 lg:col-span-1">
              <button
                type="submit"
                className="inline-flex min-h-[44px] flex-1 touch-manipulation items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 sm:min-h-[36px] sm:flex-none dark:bg-indigo-500 dark:hover:bg-indigo-400"
              >
                套用篩選
              </button>
              {isFiltered && (
                <Link
                  href="/products"
                  className="inline-flex min-h-[44px] touch-manipulation items-center justify-center rounded-lg border border-zinc-300 bg-white px-3 py-2 text-xs font-medium text-zinc-700 shadow-xs transition-colors hover:bg-zinc-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 sm:min-h-[36px] dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-700"
                >
                  清除篩選
                </Link>
              )}
            </div>
          </form>
        </section>

        {/* 列表資訊摘要列 */}
        <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
          <div className="min-w-0">
            <h2 className="text-base font-semibold text-zinc-900 dark:text-zinc-100">
              商品目錄清單
            </h2>
            <p className="text-xs text-zinc-500 dark:text-zinc-400">
              支援依名稱或價格排序，並呈現即時上下架狀態。
            </p>
          </div>
          <span className="inline-flex shrink-0 items-center rounded-md bg-zinc-100 px-2.5 py-1 text-xs font-medium text-zinc-800 dark:bg-zinc-800 dark:text-zinc-200">
            總計：{totalCount} 件商品
          </span>
        </div>

        {/* 商品列表內容 */}
        {items.length === 0 ? (
          <div className="flex min-h-[300px] flex-col items-center justify-center rounded-xl border border-dashed border-zinc-300 bg-white p-6 sm:p-8 text-center shadow-xs dark:border-zinc-800 dark:bg-zinc-900">
            <div className="flex h-12 w-12 items-center justify-center rounded-full bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">
              <svg
                className="h-6 w-6"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                aria-hidden="true"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth="2"
                  d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4"
                />
              </svg>
            </div>
            <h3 className="mt-3 text-sm font-semibold text-zinc-900 dark:text-zinc-100">
              查無符合條件之商品
            </h3>
            <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
              目前條件下沒有任何商品記錄，請調整關鍵字或重設篩選條件。
            </p>
            {isFiltered && (
              <div className="mt-4">
                <Link
                  href="/products"
                  className="inline-flex min-h-[36px] items-center justify-center rounded-md border border-zinc-300 bg-white px-3 py-1.5 text-xs font-medium text-zinc-700 shadow-xs transition-colors hover:bg-zinc-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
                >
                  清除目前篩選條件
                </Link>
              </div>
            )}
          </div>
        ) : (
          <div className="space-y-4">
            {/* 行動端與平板專屬卡片清單 (< 1024px, 涵蓋 Mobile 375px 與 Tablet 768px) */}
            <div className="space-y-3 lg:hidden">
              {items.map((product: AdminProductSummary) => (
                <div
                  key={product.id}
                  className="rounded-xl border border-zinc-200 bg-white p-4 shadow-xs transition-colors dark:border-zinc-800 dark:bg-zinc-900"
                >
                  {/* 卡片頂部：商品名稱與上架狀態徽章 */}
                  <div className="flex items-start justify-between gap-3 border-b border-zinc-100 pb-3 dark:border-zinc-800/80">
                    <div className="min-w-0 flex-1">
                      <h3 className="break-words text-sm font-bold text-zinc-900 dark:text-zinc-100">
                        {product.name}
                      </h3>
                      <div className="mt-1 flex items-center gap-1.5 font-mono text-[11px] text-zinc-500 dark:text-zinc-400">
                        <span className="shrink-0 text-[10px] uppercase text-zinc-400 dark:text-zinc-500">ID:</span>
                        <span className="break-all">{product.id}</span>
                      </div>
                    </div>
                    <span
                      className={`inline-flex shrink-0 items-center rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${
                        product.isActive
                          ? "border border-emerald-200/60 bg-emerald-50 text-emerald-700 dark:border-emerald-800/60 dark:bg-emerald-950/60 dark:text-emerald-300"
                          : "border border-zinc-200/60 bg-zinc-100 text-zinc-600 dark:border-zinc-700/60 dark:bg-zinc-800 dark:text-zinc-400"
                      }`}
                    >
                      {product.isActive ? "上架中 (Active)" : "已下架 (Inactive)"}
                    </span>
                  </div>

                  {/* 卡片中間資訊：手機單欄堆疊，平板雙欄排版 */}
                  <div className="grid grid-cols-1 gap-2.5 py-3 sm:grid-cols-2 sm:gap-4 text-xs">
                    <div>
                      <span className="block text-[10px] font-medium text-zinc-400 dark:text-zinc-500">
                        商品 SKU
                      </span>
                      <span className="mt-0.5 block break-all font-mono font-medium text-zinc-700 dark:text-zinc-300">
                        {product.sku}
                      </span>
                    </div>

                    <div className="sm:text-right">
                      <span className="block text-[10px] font-medium text-zinc-400 dark:text-zinc-500">
                        商品售價
                      </span>
                      <span className="mt-0.5 block text-sm font-bold text-zinc-900 sm:text-base dark:text-zinc-100">
                        {product.currency} {product.price.toLocaleString("zh-TW", { minimumFractionDigits: 2 })}
                      </span>
                    </div>
                  </div>

                  {/* 卡片底部操作：觸控友善詳情按鈕 */}
                  <div className="border-t border-zinc-100 pt-3 dark:border-zinc-800/80">
                    <Link
                      href={`/products/${product.id}`}
                      className="inline-flex min-h-[44px] w-full touch-manipulation items-center justify-center rounded-lg bg-zinc-100 px-4 py-2.5 text-xs font-semibold text-zinc-800 shadow-xs transition-colors hover:bg-zinc-200 active:bg-zinc-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
                    >
                      檢視詳情 / 管理 &rarr;
                    </Link>
                  </div>
                </div>
              ))}
            </div>

            {/* 桌面專屬高密度營運表格 (>= 1024px，涵蓋 Desktop 1280px) */}
            <div className="hidden overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-xs lg:block dark:border-zinc-800 dark:bg-zinc-900">
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-zinc-200 text-left text-xs dark:divide-zinc-800">
                  <thead className="bg-zinc-50 text-zinc-600 dark:bg-zinc-800/50 dark:text-zinc-400">
                    <tr>
                      <th scope="col" className="px-5 py-3.5 font-semibold">
                        商品名稱 (Product Name)
                      </th>
                      <th scope="col" className="px-5 py-3.5 font-semibold">
                        SKU
                      </th>
                      <th scope="col" className="px-5 py-3.5 font-semibold">
                        商品識別碼 (Product ID)
                      </th>
                      <th scope="col" className="px-4 py-3.5 font-semibold">
                        售價
                      </th>
                      <th scope="col" className="px-4 py-3.5 font-semibold">
                        狀態
                      </th>
                      <th scope="col" className="px-5 py-3.5 text-right font-semibold">
                        操作
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800/60">
                    {items.map((product: AdminProductSummary) => (
                      <tr
                        key={product.id}
                        className="transition-colors hover:bg-zinc-50/80 dark:hover:bg-zinc-800/40"
                      >
                        <td className="px-5 py-3.5 max-w-[260px]">
                          <span className="break-words font-medium text-zinc-900 dark:text-zinc-100">
                            {product.name}
                          </span>
                        </td>
                        <td className="px-5 py-3.5">
                          <span className="break-all font-mono text-zinc-600 dark:text-zinc-300">
                            {product.sku}
                          </span>
                        </td>
                        <td className="px-5 py-3.5">
                          <span className="break-all font-mono text-[11px] text-zinc-500 dark:text-zinc-400">
                            {product.id}
                          </span>
                        </td>
                        <td className="whitespace-nowrap px-4 py-3.5 font-semibold text-zinc-900 dark:text-zinc-100">
                          {product.currency} {product.price.toLocaleString("zh-TW", { minimumFractionDigits: 2 })}
                        </td>
                        <td className="px-4 py-3.5">
                          <span
                            className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${
                              product.isActive
                                ? "border border-emerald-200/60 bg-emerald-50 text-emerald-700 dark:border-emerald-800/60 dark:bg-emerald-950/60 dark:text-emerald-300"
                                : "border border-zinc-200/60 bg-zinc-100 text-zinc-600 dark:border-zinc-700/60 dark:bg-zinc-800 dark:text-zinc-400"
                            }`}
                          >
                            {product.isActive ? "上架中 (Active)" : "已下架 (Inactive)"}
                          </span>
                        </td>
                        <td className="whitespace-nowrap px-5 py-3.5 text-right">
                          <Link
                            href={`/products/${product.id}`}
                            className="inline-flex min-h-[32px] items-center rounded-md bg-zinc-100 px-3 py-1.5 text-xs font-medium text-zinc-800 transition-colors hover:bg-zinc-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
                          >
                            檢視詳情 / 管理 &rarr;
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>

            {/* 分頁導覽列 (支援行動端堆疊與平板/桌面水平佈局) */}
            <nav
              aria-label="商品分頁導覽"
              className="flex flex-col items-center justify-between gap-3 rounded-xl border border-zinc-200 bg-white px-4 py-3.5 shadow-xs sm:flex-row sm:px-6 dark:border-zinc-800 dark:bg-zinc-900"
            >
              <div className="text-xs text-zinc-500 dark:text-zinc-400">
                第 <span className="font-semibold text-zinc-800 dark:text-zinc-200">{page}</span> 頁 / 共{" "}
                <span className="font-semibold text-zinc-800 dark:text-zinc-200">{totalPages}</span> 頁 (總計 {totalCount} 件商品)
              </div>
              <div className="flex w-full items-center justify-center gap-3 sm:w-auto">
                {page > 1 ? (
                  <Link
                    href={createPaginationUrl(page - 1)}
                    className="inline-flex min-h-[44px] flex-1 touch-manipulation items-center justify-center rounded-lg border border-zinc-300 bg-white px-4 py-2 text-xs font-semibold text-zinc-700 shadow-xs transition-colors hover:bg-zinc-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 sm:min-h-[36px] sm:flex-none sm:rounded-md sm:px-3 sm:py-1.5 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
                  >
                    上一頁
                  </Link>
                ) : (
                  <span
                    aria-disabled="true"
                    className="inline-flex min-h-[44px] flex-1 cursor-not-allowed items-center justify-center rounded-lg border border-zinc-200 bg-zinc-50 px-4 py-2 text-xs font-semibold text-zinc-400 sm:min-h-[36px] sm:flex-none sm:rounded-md sm:px-3 sm:py-1.5 dark:border-zinc-800 dark:bg-zinc-900/50 dark:text-zinc-600"
                  >
                    上一頁
                  </span>
                )}

                {page < totalPages ? (
                  <Link
                    href={createPaginationUrl(page + 1)}
                    className="inline-flex min-h-[44px] flex-1 touch-manipulation items-center justify-center rounded-lg border border-zinc-300 bg-white px-4 py-2 text-xs font-semibold text-zinc-700 shadow-xs transition-colors hover:bg-zinc-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 sm:min-h-[36px] sm:flex-none sm:rounded-md sm:px-3 sm:py-1.5 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
                  >
                    下一頁
                  </Link>
                ) : (
                  <span
                    aria-disabled="true"
                    className="inline-flex min-h-[44px] flex-1 cursor-not-allowed items-center justify-center rounded-lg border border-zinc-200 bg-zinc-50 px-4 py-2 text-xs font-semibold text-zinc-400 sm:min-h-[36px] sm:flex-none sm:rounded-md sm:px-3 sm:py-1.5 dark:border-zinc-800 dark:bg-zinc-900/50 dark:text-zinc-600"
                  >
                    下一頁
                  </span>
                )}
              </div>
            </nav>
          </div>
        )}
      </main>
    </div>
  );
}

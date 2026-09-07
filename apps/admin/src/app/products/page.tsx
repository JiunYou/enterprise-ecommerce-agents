import Link from "next/link";
import { auth0 } from "@/lib/auth0";
import { getAdminProducts } from "@/lib/products";

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
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-8 shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          <div className="flex flex-col items-center text-center">
            <div className="flex h-12 w-12 items-center justify-center rounded-full bg-indigo-50 text-indigo-600 dark:bg-indigo-950 dark:text-indigo-400">
              <svg
                className="h-6 w-6"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
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
              className="mt-6 inline-flex w-full items-center justify-center rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-indigo-500 active:bg-indigo-700 dark:bg-indigo-500 dark:hover:bg-indigo-400"
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
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-8 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            登入階段已過期
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            您的身分驗證權杖已逾期或無效，請重新進行登入。
          </p>
          <div className="mt-6 flex justify-center gap-4">
            <a
              href="/auth/login"
              className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
            >
              重新登入
            </a>
            <a
              href="/auth/logout"
              className="rounded-lg border border-zinc-300 px-4 py-2 text-sm font-semibold text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-200"
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
        <div className="w-full max-w-lg rounded-xl border border-rose-200 bg-rose-50/50 p-8 text-center shadow-sm dark:border-rose-900/50 dark:bg-rose-950/20">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-rose-100 text-rose-600 dark:bg-rose-900/50 dark:text-rose-400">
            <svg
              className="h-6 w-6"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
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
              className="rounded-lg border border-zinc-300 bg-white px-4 py-2 text-sm font-semibold text-zinc-700 shadow-sm transition-colors hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
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
        <div className="w-full max-w-md rounded-xl border border-amber-200 bg-white p-8 text-center shadow-sm dark:border-amber-900/50 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            查詢參數無效
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            {result.message}
          </p>
          <div className="mt-6">
            <Link
              href="/products"
              className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
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
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-8 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            系統連線異常
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            {result.message}
          </p>
          <div className="mt-6">
            <Link
              href="/products"
              className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
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

  // 建構分頁輔助連結
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

  return (
    <div className="min-h-screen bg-zinc-50 dark:bg-zinc-950">
      {/* 導航標頭 */}
      <header className="border-b border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-4 sm:px-6 lg:px-8">
          <div className="flex items-center gap-3">
            <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-indigo-600 text-white font-bold text-base">
              EC
            </span>
            <div>
              <h1 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
                商品目錄管理 (Admin Catalog)
              </h1>
              <p className="text-xs text-zinc-500 dark:text-zinc-400">
                商品狀態監控、價格調整與下架停用
              </p>
            </div>
            <nav className="ml-6 hidden items-center gap-2 sm:flex">
              <Link
                href="/"
                className="rounded-md px-3 py-1.5 text-xs font-medium text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"
              >
                訂單履約
              </Link>
              <Link
                href="/orders"
                className="rounded-md px-3 py-1.5 text-xs font-medium text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"
              >
                訂單管理
              </Link>
              <Link
                href="/products"
                className="rounded-md bg-zinc-100 px-3 py-1.5 text-xs font-semibold text-zinc-900 dark:bg-zinc-800 dark:text-zinc-100"
              >
                商品管理
              </Link>
            </nav>
          </div>
          <div className="flex items-center gap-4">
            <div className="hidden text-right sm:block">
              <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">
                {session.user.name || session.user.email || "管理員"}
              </p>
              <span className="inline-flex items-center rounded-full bg-indigo-50 px-2 py-0.5 text-[10px] font-medium text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300">
                Admin Role
              </span>
            </div>
            <a
              href="/auth/logout"
              className="rounded-md border border-zinc-300 bg-white px-3 py-1.5 text-xs font-semibold text-zinc-700 shadow-sm transition-colors hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
            >
              登出
            </a>
          </div>
        </div>
      </header>

      {/* 主要內容區 */}
      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        {/* 篩選與搜尋表單 */}
        <section className="mb-6 rounded-xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          <form method="GET" action="/products" className="flex flex-wrap items-end gap-4">
            <div className="min-w-[220px] flex-1">
              <label
                htmlFor="searchTerm"
                className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
              >
                搜尋商品名稱或 SKU
              </label>
              <input
                type="text"
                id="searchTerm"
                name="searchTerm"
                defaultValue={currentSearch || ""}
                placeholder="輸入關鍵字或 SKU..."
                className="mt-1 block w-full rounded-lg border border-zinc-300 px-3 py-1.5 text-xs text-zinc-900 placeholder-zinc-400 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder-zinc-500"
              />
            </div>

            <div className="w-36">
              <label
                htmlFor="onlyActive"
                className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
              >
                狀態過濾
              </label>
              <select
                id="onlyActive"
                name="onlyActive"
                defaultValue={currentOnlyActive ? "true" : "false"}
                className="mt-1 block w-full rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-xs text-zinc-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
              >
                <option value="false">全量目錄 (含已停用)</option>
                <option value="true">僅上架中 (Active)</option>
              </select>
            </div>

            <div className="w-32">
              <label
                htmlFor="sortBy"
                className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
              >
                排序欄位
              </label>
              <select
                id="sortBy"
                name="sortBy"
                defaultValue={currentSortBy || "name"}
                className="mt-1 block w-full rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-xs text-zinc-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
              >
                <option value="name">商品名稱</option>
                <option value="price">商品售價</option>
              </select>
            </div>

            <div className="w-28">
              <label
                htmlFor="sortOrder"
                className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
              >
                排序方向
              </label>
              <select
                id="sortOrder"
                name="sortOrder"
                defaultValue={currentSortOrder || "asc"}
                className="mt-1 block w-full rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-xs text-zinc-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
              >
                <option value="asc">升冪 (遞增)</option>
                <option value="desc">降冪 (遞減)</option>
              </select>
            </div>

            <div className="flex gap-2">
              <button
                type="submit"
                className="rounded-lg bg-indigo-600 px-4 py-1.5 text-xs font-semibold text-white shadow-sm transition-colors hover:bg-indigo-500 active:bg-indigo-700 dark:bg-indigo-500 dark:hover:bg-indigo-400"
              >
                套用篩選
              </button>
              <Link
                href="/products"
                className="rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-xs font-semibold text-zinc-700 shadow-sm transition-colors hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
              >
                重設
              </Link>
            </div>
          </form>
        </section>

        {/* 商品列表表格 */}
        <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          <div className="border-b border-zinc-200 px-6 py-4 dark:border-zinc-800">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-sm font-semibold text-zinc-900 dark:text-zinc-50">
                  商品項目 ({totalCount})
                </h2>
                <p className="mt-0.5 text-xs text-zinc-500 dark:text-zinc-400">
                  第 {page} 頁，共 {totalPages} 頁
                </p>
              </div>
            </div>
          </div>

          {items.length === 0 ? (
            <div className="px-6 py-12 text-center text-xs text-zinc-500 dark:text-zinc-400">
              查無符合條件的商品。
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-zinc-200 bg-zinc-50 font-medium text-zinc-600 dark:border-zinc-800 dark:bg-zinc-800/50 dark:text-zinc-400">
                  <tr>
                    <th scope="col" className="px-6 py-3">商品名稱</th>
                    <th scope="col" className="px-6 py-3">SKU</th>
                    <th scope="col" className="px-6 py-3">售價</th>
                    <th scope="col" className="px-6 py-3">狀態</th>
                    <th scope="col" className="px-6 py-3 text-right">操作</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-zinc-200 dark:divide-zinc-800">
                  {items.map((product) => (
                    <tr
                      key={product.id}
                      className="transition-colors hover:bg-zinc-50/80 dark:hover:bg-zinc-800/40"
                    >
                      <td className="px-6 py-3.5 font-medium text-zinc-900 dark:text-zinc-50">
                        {product.name}
                      </td>
                      <td className="px-6 py-3.5 font-mono text-zinc-600 dark:text-zinc-300">
                        {product.sku}
                      </td>
                      <td className="px-6 py-3.5 font-semibold text-zinc-900 dark:text-zinc-50">
                        {product.currency} {product.price.toLocaleString("zh-TW", { minimumFractionDigits: 2 })}
                      </td>
                      <td className="px-6 py-3.5">
                        {product.isActive ? (
                          <span className="inline-flex items-center rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-medium text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300">
                            上架中 (Active)
                          </span>
                        ) : (
                          <span className="inline-flex items-center rounded-full bg-zinc-100 px-2 py-0.5 text-[11px] font-medium text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400">
                            已下架 (Inactive)
                          </span>
                        )}
                      </td>
                      <td className="px-6 py-3.5 text-right">
                        <Link
                          href={`/products/${product.id}`}
                          className="font-medium text-indigo-600 hover:text-indigo-500 dark:text-indigo-400 dark:hover:text-indigo-300"
                        >
                          檢視詳情 / 管理 &rarr;
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* 分頁導覽 */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between border-t border-zinc-200 px-6 py-3 dark:border-zinc-800">
              <div className="flex gap-2">
                {page > 1 ? (
                  <Link
                    href={createPaginationUrl(page - 1)}
                    className="rounded-md border border-zinc-300 bg-white px-3 py-1 text-xs font-medium text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200"
                  >
                    &larr; 上一頁
                  </Link>
                ) : (
                  <span className="cursor-not-allowed rounded-md border border-zinc-200 px-3 py-1 text-xs font-medium text-zinc-400 dark:border-zinc-800 dark:text-zinc-600">
                    &larr; 上一頁
                  </span>
                )}

                {page < totalPages ? (
                  <Link
                    href={createPaginationUrl(page + 1)}
                    className="rounded-md border border-zinc-300 bg-white px-3 py-1 text-xs font-medium text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200"
                  >
                    下一頁 &rarr;
                  </Link>
                ) : (
                  <span className="cursor-not-allowed rounded-md border border-zinc-200 px-3 py-1 text-xs font-medium text-zinc-400 dark:border-zinc-800 dark:text-zinc-600">
                    下一頁 &rarr;
                  </span>
                )}
              </div>
              <span className="text-xs text-zinc-500 dark:text-zinc-400">
                第 {page} / {totalPages} 頁
              </span>
            </div>
          )}
        </div>
      </main>
    </div>
  );
}

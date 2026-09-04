import Link from "next/link";
import { getProducts } from "@/lib/catalog";
import { formatPrice } from "@/lib/format";
import { AuthControls } from "@/components/AuthControls";

interface PageProps {
  searchParams: Promise<{
    page?: string | string[];
    q?: string | string[];
    searchTerm?: string | string[];
    sort?: string | string[];
  }>;
}

type SortOption = "name-asc" | "name-desc" | "price-asc" | "price-desc";

export default async function CatalogPage({ searchParams }: PageProps) {
  const resolvedParams = await searchParams;

  const rawSearch =
    typeof resolvedParams.q === "string"
      ? resolvedParams.q
      : typeof resolvedParams.searchTerm === "string"
      ? resolvedParams.searchTerm
      : "";
  const searchTerm = rawSearch.trim();

  const rawSort =
    typeof resolvedParams.sort === "string" ? resolvedParams.sort.trim() : "";

  let sortBy: "name" | "price" | undefined = undefined;
  let sortOrder: "asc" | "desc" | undefined = undefined;
  let validSort: SortOption | "" = "";

  if (rawSort === "name-asc") {
    sortBy = "name";
    sortOrder = "asc";
    validSort = "name-asc";
  } else if (rawSort === "name-desc") {
    sortBy = "name";
    sortOrder = "desc";
    validSort = "name-desc";
  } else if (rawSort === "price-asc") {
    sortBy = "price";
    sortOrder = "asc";
    validSort = "price-asc";
  } else if (rawSort === "price-desc") {
    sortBy = "price";
    sortOrder = "desc";
    validSort = "price-desc";
  }

  const rawPage =
    typeof resolvedParams.page === "string"
      ? parseInt(resolvedParams.page, 10)
      : 1;
  const page = Number.isInteger(rawPage) && rawPage > 0 ? rawPage : 1;
  const pageSize = 12;

  const result = await getProducts({
    page,
    pageSize,
    searchTerm: searchTerm || undefined,
    sortBy,
    sortOrder,
  });

  const createPageHref = (targetPage: number) => {
    const params = new URLSearchParams();
    if (searchTerm) {
      params.set("q", searchTerm);
    }
    if (validSort) {
      params.set("sort", validSort);
    }
    if (targetPage > 1) {
      params.set("page", targetPage.toString());
    }
    const queryString = params.toString();
    return queryString ? `/?${queryString}` : "/";
  };

  const clearSearchHref = validSort ? `/?sort=${encodeURIComponent(validSort)}` : "/";

  return (
    <div className="min-h-screen bg-zinc-50 text-zinc-900 dark:bg-zinc-950 dark:text-zinc-100">
      {/* 頂部導航列 / 商店識別 */}
      <header className="border-b border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:px-8">
          <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <Link href="/" className="inline-block">
                <h1 className="text-2xl font-bold tracking-tight text-zinc-900 dark:text-white">
                  Enterprise Commerce
                </h1>
              </Link>
              <p className="text-sm text-zinc-500 dark:text-zinc-400">
                商品型錄與線上商務展示
              </p>
            </div>
            <div className="flex items-center gap-4">
              <Link
                href="/cart"
                className="inline-flex items-center text-sm font-medium text-zinc-700 hover:text-zinc-900 dark:text-zinc-300 dark:hover:text-zinc-100"
              >
                購物車
              </Link>
              <AuthControls />
            </div>
          </div>
        </div>
      </header>

      {/* 主要內容區 */}
      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
        {/* 搜尋與排序列 */}
        <section aria-label="商品搜尋與排序" className="mb-8">
          <form
            method="GET"
            action="/"
            role="search"
            className="flex flex-col gap-3 sm:flex-row sm:items-center"
          >
            <div className="relative flex-1">
              <label htmlFor="search-input" className="sr-only">
                搜尋商品名稱或 SKU
              </label>
              <input
                id="search-input"
                type="search"
                name="q"
                defaultValue={searchTerm}
                placeholder="搜尋商品名稱或 SKU..."
                className="w-full rounded-lg border border-zinc-300 bg-white px-4 py-2.5 text-sm text-zinc-900 shadow-sm placeholder:text-zinc-400 focus:border-zinc-500 focus:outline-none focus:ring-1 focus:ring-zinc-500 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-100 dark:placeholder:text-zinc-500"
              />
            </div>
            <div className="flex flex-wrap gap-2 sm:flex-nowrap">
              <div className="relative">
                <label htmlFor="sort-select" className="sr-only">
                  商品排序
                </label>
                <select
                  id="sort-select"
                  name="sort"
                  defaultValue={validSort}
                  className="rounded-lg border border-zinc-300 bg-white px-3 py-2.5 text-sm text-zinc-900 shadow-sm focus:border-zinc-500 focus:outline-none focus:ring-1 focus:ring-zinc-500 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-100"
                >
                  <option value="">預設排序</option>
                  <option value="name-asc">名稱：A → Z</option>
                  <option value="name-desc">名稱：Z → A</option>
                  <option value="price-asc">價格：低 → 高</option>
                  <option value="price-desc">價格：高 → 低</option>
                </select>
              </div>
              <button
                type="submit"
                className="rounded-lg bg-zinc-900 px-5 py-2.5 text-sm font-medium text-white shadow-sm transition hover:bg-zinc-800 focus:outline-none focus:ring-2 focus:ring-zinc-500 focus:ring-offset-2 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
              >
                搜尋
              </button>
              {searchTerm && (
                <Link
                  href={clearSearchHref}
                  className="rounded-lg border border-zinc-300 bg-white px-4 py-2.5 text-sm font-medium text-zinc-700 shadow-sm transition hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-300 dark:hover:bg-zinc-800"
                >
                  清除
                </Link>
              )}
            </div>
          </form>

          {searchTerm && (
            <p className="mt-3 text-sm text-zinc-600 dark:text-zinc-400">
              搜尋關鍵字：<span className="font-semibold">「{searchTerm}」</span>
            </p>
          )}
        </section>

        {/* 狀態渲染：錯誤 / 空結果 / 商品列表 */}
        {!result.success ? (
          <section
            aria-label="系統訊息"
            className="rounded-xl border border-red-200 bg-red-50 p-6 text-red-800 dark:border-red-900/50 dark:bg-red-950/40 dark:text-red-300"
          >
            <h2 className="text-base font-semibold">無法載入商品目錄</h2>
            <p className="mt-1 text-sm">{result.error}</p>
            <div className="mt-4">
              <Link
                href={createPageHref(page)}
                className="inline-flex items-center rounded-md bg-red-100 px-3 py-1.5 text-sm font-medium text-red-900 transition hover:bg-red-200 dark:bg-red-900/60 dark:text-red-200 dark:hover:bg-red-900"
              >
                重新整理
              </Link>
            </div>
          </section>
        ) : result.data.items.length === 0 ? (
          <section
            aria-label="無商品結果"
            className="rounded-xl border border-zinc-200 bg-white p-12 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
          >
            <h2 className="text-lg font-medium text-zinc-900 dark:text-zinc-100">
              查無符合條件的商品
            </h2>
            <p className="mt-2 text-sm text-zinc-500 dark:text-zinc-400">
              {searchTerm
                ? "請嘗試更換搜尋關鍵字或清除篩選條件。"
                : "目前目錄中尚無上架商品。"}
            </p>
            {searchTerm && (
              <div className="mt-6">
                <Link
                  href={clearSearchHref}
                  className="inline-flex items-center rounded-lg bg-zinc-900 px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-zinc-800 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
                >
                  清除搜尋條件
                </Link>
              </div>
            )}
          </section>
        ) : (
          <section aria-label="商品列表">
            <div className="mb-4 flex items-center justify-between text-sm text-zinc-500 dark:text-zinc-400">
              <span>
                第 {result.data.page} 頁，共 {result.data.totalPages} 頁 (共{" "}
                {result.data.totalCount} 筆商品)
              </span>
            </div>

            {/* 商品網格 */}
            <ul className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              {result.data.items.map((product) => (
                <li
                  key={product.id}
                  className="flex flex-col justify-between rounded-xl border border-zinc-200 bg-white p-6 shadow-sm transition hover:shadow-md dark:border-zinc-800 dark:bg-zinc-900"
                >
                  <Link
                    href={`/products/${product.id}`}
                    className="group flex h-full flex-col justify-between"
                  >
                    <div>
                      <h3 className="text-base font-semibold text-zinc-900 transition group-hover:text-blue-600 dark:text-zinc-100 dark:group-hover:text-blue-400">
                        {product.name}
                      </h3>
                      <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
                        SKU: <span className="font-mono">{product.sku}</span>
                      </p>
                    </div>
                    <div className="mt-6 border-t border-zinc-100 pt-4 dark:border-zinc-800">
                      <span className="text-lg font-bold text-zinc-900 dark:text-zinc-100">
                        {formatPrice(product.price, product.currency)}
                      </span>
                    </div>
                  </Link>
                </li>
              ))}
            </ul>

            {/* 分頁導航 */}
            {result.data.totalPages > 1 && (
              <nav
                aria-label="分頁導航"
                className="mt-10 flex items-center justify-between border-t border-zinc-200 pt-6 dark:border-zinc-800"
              >
                <div>
                  {result.data.hasPreviousPage ? (
                    <Link
                      href={createPageHref(result.data.page - 1)}
                      className="inline-flex items-center rounded-lg border border-zinc-300 bg-white px-4 py-2 text-sm font-medium text-zinc-700 shadow-sm transition hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-300 dark:hover:bg-zinc-800"
                    >
                      &larr; 上一頁
                    </Link>
                  ) : (
                    <span
                      aria-disabled="true"
                      className="inline-flex cursor-not-allowed items-center rounded-lg border border-zinc-200 bg-zinc-100 px-4 py-2 text-sm font-medium text-zinc-400 dark:border-zinc-800 dark:bg-zinc-800/40 dark:text-zinc-600"
                    >
                      &larr; 上一頁
                    </span>
                  )}
                </div>

                <span className="text-sm text-zinc-600 dark:text-zinc-400">
                  {result.data.page} / {result.data.totalPages}
                </span>

                <div>
                  {result.data.hasNextPage ? (
                    <Link
                      href={createPageHref(result.data.page + 1)}
                      className="inline-flex items-center rounded-lg border border-zinc-300 bg-white px-4 py-2 text-sm font-medium text-zinc-700 shadow-sm transition hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-300 dark:hover:bg-zinc-800"
                    >
                      下一頁 &rarr;
                    </Link>
                  ) : (
                    <span
                      aria-disabled="true"
                      className="inline-flex cursor-not-allowed items-center rounded-lg border border-zinc-200 bg-zinc-100 px-4 py-2 text-sm font-medium text-zinc-400 dark:border-zinc-800 dark:bg-zinc-800/40 dark:text-zinc-600"
                    >
                      下一頁 &rarr;
                    </span>
                  )}
                </div>
              </nav>
            )}
          </section>
        )}
      </main>
    </div>
  );
}

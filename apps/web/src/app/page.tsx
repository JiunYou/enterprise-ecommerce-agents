import Link from "next/link";
import { getProducts } from "@/lib/catalog";
import { formatPrice } from "@/lib/format";
import { CustomerHeader } from "@/components/CustomerHeader";

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
    <div className="min-h-screen bg-stone-50 text-stone-900 dark:bg-stone-950 dark:text-stone-100">
      <CustomerHeader />

      {/* 主要內容區 */}
      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
        {/* 目錄標題與導言 */}
        <div className="mb-8">
          <h1 className="text-2xl font-bold tracking-tight text-stone-900 dark:text-stone-50 sm:text-3xl">
            商品目錄
          </h1>
          <p className="mt-1.5 text-sm text-stone-600 dark:text-stone-400">
            瀏覽全系列在售商品，提供最新庫存與透明定價。
          </p>
        </div>

        {/* 搜尋與排序列 */}
        <section aria-label="商品搜尋與排序" className="mb-8">
          <div className="rounded-2xl border border-stone-200/80 bg-white/80 p-4 shadow-xs backdrop-blur-xs dark:border-stone-800/80 dark:bg-stone-900/60 sm:p-5">
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
                  className="h-10 w-full rounded-lg border border-stone-300 bg-stone-50/60 px-3.5 text-sm text-stone-900 transition placeholder:text-stone-400 focus:border-stone-500 focus:bg-white focus:outline-none focus:ring-2 focus:ring-stone-400/30 dark:border-stone-700 dark:bg-stone-950/50 dark:text-stone-100 dark:placeholder:text-stone-500 dark:focus:border-stone-400 dark:focus:bg-stone-900 dark:focus:ring-stone-600/30"
                />
              </div>
              <div className="flex flex-wrap items-center gap-2 sm:flex-nowrap">
                <div className="relative min-w-[130px] flex-1 sm:flex-none">
                  <label htmlFor="sort-select" className="sr-only">
                    商品排序
                  </label>
                  <select
                    id="sort-select"
                    name="sort"
                    defaultValue={validSort}
                    className="h-10 w-full rounded-lg border border-stone-300 bg-stone-50/60 px-3 text-sm text-stone-900 transition focus:border-stone-500 focus:bg-white focus:outline-none focus:ring-2 focus:ring-stone-400/30 dark:border-stone-700 dark:bg-stone-950/50 dark:text-stone-100 dark:focus:border-stone-400 dark:focus:bg-stone-900 dark:focus:ring-stone-600/30"
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
                  className="h-10 inline-flex items-center justify-center rounded-lg bg-stone-900 px-5 text-sm font-medium text-white shadow-xs transition hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-500 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
                >
                  搜尋
                </button>
                {searchTerm && (
                  <Link
                    href={clearSearchHref}
                    className="h-10 inline-flex items-center justify-center rounded-lg border border-stone-300 bg-white px-4 text-sm font-medium text-stone-700 shadow-xs transition hover:bg-stone-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:border-stone-700 dark:bg-stone-900 dark:text-stone-300 dark:hover:bg-stone-800"
                  >
                    清除
                  </Link>
                )}
              </div>
            </form>

            {searchTerm && (
              <div className="mt-3.5 border-t border-stone-200/60 pt-3 text-xs text-stone-500 dark:border-stone-800/60 dark:text-stone-400">
                目前搜尋關鍵字：
                <span className="font-semibold text-stone-800 dark:text-stone-200">
                  「{searchTerm}」
                </span>
              </div>
            )}
          </div>
        </section>

        {/* 狀態渲染：錯誤 / 空結果 / 商品列表 */}
        {!result.success ? (
          <section
            aria-label="系統訊息"
            className="rounded-2xl border border-red-200 bg-red-50/70 p-6 text-red-900 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-200 sm:p-8"
          >
            <h2 className="text-base font-semibold">無法載入商品目錄</h2>
            <p className="mt-1.5 text-sm text-red-700 dark:text-red-300">{result.error}</p>
            <div className="mt-5">
              <Link
                href={createPageHref(page)}
                className="inline-flex items-center justify-center rounded-lg border border-red-300 bg-white px-4 py-2 text-sm font-medium text-red-800 shadow-xs transition hover:bg-red-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:ring-offset-2 dark:border-red-800 dark:bg-stone-900 dark:text-red-200 dark:hover:bg-stone-800"
              >
                重新整理
              </Link>
            </div>
          </section>
        ) : result.data.items.length === 0 ? (
          <section
            aria-label="無商品結果"
            className="rounded-2xl border border-stone-200 bg-white p-12 text-center shadow-xs dark:border-stone-800 dark:bg-stone-900 sm:py-16"
          >
            <div
              className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-stone-100 text-stone-400 dark:bg-stone-800 dark:text-stone-500"
              aria-hidden="true"
            >
              <span className="text-xl leading-none">⊘</span>
            </div>
            <h2 className="mt-4 text-base font-semibold text-stone-900 dark:text-stone-100">
              查無符合條件的商品
            </h2>
            <p className="mx-auto mt-1.5 max-w-sm text-sm text-stone-500 dark:text-stone-400">
              {searchTerm
                ? "請嘗試更換搜尋關鍵字或清除篩選條件。"
                : "目前目錄中尚無上架商品。"}
            </p>
            {searchTerm && (
              <div className="mt-6">
                <Link
                  href={clearSearchHref}
                  className="inline-flex items-center justify-center rounded-lg bg-stone-900 px-4 py-2 text-sm font-medium text-white shadow-xs transition hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
                >
                  清除搜尋條件
                </Link>
              </div>
            )}
          </section>
        ) : (
          <section aria-label="商品列表">
            <div className="mb-5 flex flex-col gap-1 text-xs text-stone-500 dark:text-stone-400 sm:flex-row sm:items-center sm:justify-between sm:text-sm">
              <p>
                第 <span className="font-medium text-stone-800 dark:text-stone-200">{result.data.page}</span> 頁，共{" "}
                <span className="font-medium text-stone-800 dark:text-stone-200">{result.data.totalPages}</span> 頁
                <span className="mx-1.5 text-stone-300 dark:text-stone-700">·</span>
                共 <span className="font-medium text-stone-800 dark:text-stone-200">{result.data.totalCount}</span> 筆商品
              </p>
            </div>

            {/* 商品網格 */}
            <ul className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              {result.data.items.map((product) => {
                const initial = product.name.trim().charAt(0).toUpperCase() || "P";
                return (
                  <li
                    key={product.id}
                    className="flex flex-col rounded-xl border border-stone-200/90 bg-white p-4 shadow-2xs transition-all hover:border-stone-400/80 hover:shadow-xs dark:border-stone-800 dark:bg-stone-900 dark:hover:border-stone-700"
                  >
                    <Link
                      href={`/products/${product.id}`}
                      className="group flex h-full flex-col focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-lg"
                    >
                      {/* 展示性視覺區塊 (Product Visual Tile) */}
                      {product.imageUrl && product.imageUrl.trim().length > 0 ? (
                        <div className="relative aspect-4/3 w-full overflow-hidden rounded-lg border border-stone-200/70 bg-stone-100 transition-colors group-hover:border-stone-300 dark:border-stone-800 dark:bg-stone-850 dark:group-hover:border-stone-700">
                          {/* eslint-disable-next-line @next/next/no-img-element -- Bounded external product image metadata rendering without server-side proxy */}
                          <img
                            src={product.imageUrl.trim()}
                            alt={product.name}
                            loading="lazy"
                            decoding="async"
                            referrerPolicy="no-referrer"
                            className="h-full w-full object-cover"
                          />
                        </div>
                      ) : (
                        <div
                          aria-hidden="true"
                          className="relative flex aspect-4/3 w-full items-center justify-center rounded-lg border border-stone-200/70 bg-radial from-stone-50 to-stone-100 text-stone-400 transition-colors group-hover:border-stone-300 dark:border-stone-800 dark:from-stone-900 dark:to-stone-850 dark:text-stone-600 dark:group-hover:border-stone-700"
                        >
                          <span className="text-2xl font-bold tracking-wider text-stone-400/80 select-none dark:text-stone-500/80">
                            {initial}
                          </span>
                        </div>
                      )}

                      {/* 商品資訊與層次 */}
                      <div className="mt-3.5 flex flex-1 flex-col justify-between">
                        <div>
                          <h2 className="text-base font-semibold text-stone-900 transition-colors group-hover:text-stone-600 dark:text-stone-100 dark:group-hover:text-stone-300 line-clamp-2">
                            {product.name}
                          </h2>
                          <p className="mt-1 text-xs text-stone-400 dark:text-stone-500">
                            SKU: <span className="font-mono">{product.sku}</span>
                          </p>
                        </div>
                        <div className="mt-4 border-t border-stone-100 pt-3 dark:border-stone-800/80">
                          <span className="text-lg font-bold tracking-tight text-stone-900 dark:text-stone-100">
                            {formatPrice(product.price, product.currency)}
                          </span>
                        </div>
                      </div>
                    </Link>
                  </li>
                );
              })}
            </ul>

            {/* 分頁導航 */}
            {result.data.totalPages > 1 && (
              <nav
                aria-label="分頁導航"
                className="mt-12 flex items-center justify-between border-t border-stone-200 pt-6 dark:border-stone-800"
              >
                <div>
                  {result.data.hasPreviousPage ? (
                    <Link
                      href={createPageHref(result.data.page - 1)}
                      className="inline-flex items-center rounded-lg border border-stone-300 bg-white px-4 py-2 text-sm font-medium text-stone-700 shadow-xs transition hover:bg-stone-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:border-stone-700 dark:bg-stone-900 dark:text-stone-300 dark:hover:bg-stone-800"
                    >
                      &larr; 上一頁
                    </Link>
                  ) : (
                    <span
                      aria-disabled="true"
                      className="inline-flex cursor-not-allowed items-center rounded-lg border border-stone-200 bg-stone-100/60 px-4 py-2 text-sm font-medium text-stone-400 dark:border-stone-800 dark:bg-stone-900/40 dark:text-stone-600"
                    >
                      &larr; 上一頁
                    </span>
                  )}
                </div>

                <span className="text-sm font-medium text-stone-600 dark:text-stone-400">
                  {result.data.page} / {result.data.totalPages}
                </span>

                <div>
                  {result.data.hasNextPage ? (
                    <Link
                      href={createPageHref(result.data.page + 1)}
                      className="inline-flex items-center rounded-lg border border-stone-300 bg-white px-4 py-2 text-sm font-medium text-stone-700 shadow-xs transition hover:bg-stone-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:border-stone-700 dark:bg-stone-900 dark:text-stone-300 dark:hover:bg-stone-800"
                    >
                      下一頁 &rarr;
                    </Link>
                  ) : (
                    <span
                      aria-disabled="true"
                      className="inline-flex cursor-not-allowed items-center rounded-lg border border-stone-200 bg-stone-100/60 px-4 py-2 text-sm font-medium text-stone-400 dark:border-stone-800 dark:bg-stone-900/40 dark:text-stone-600"
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

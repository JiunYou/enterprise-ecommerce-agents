import Link from "next/link";
import Image from "next/image";
import { getWishlist } from "@/lib/wishlist";
import { getProductById, ProductDetail } from "@/lib/catalog";
import { formatPrice } from "@/lib/format";
import { CustomerHeader } from "@/components/CustomerHeader";
import { RemoveWishlistItemButton } from "@/components/RemoveWishlistItemButton";

interface WishlistPageProps {
  searchParams: Promise<{
    page?: string;
  }>;
}

export default async function WishlistPage({
  searchParams,
}: WishlistPageProps) {
  const resolvedParams = await searchParams;
  const parsedPage = parseInt(resolvedParams.page || "1", 10);
  const currentPage = isNaN(parsedPage) || parsedPage < 1 ? 1 : parsedPage;
  const pageSize = 25;

  const result = await getWishlist(currentPage, pageSize);

  // 若取得收藏成功，逐項獲取目前公開商品資訊以豐富呈現 (Section 29: N+1 Enrichment)
  let enrichedItems: Array<{
    productId: string;
    addedAt: string;
    product: ProductDetail | null;
  }> = [];

  if (result.success && result.data.items.length > 0) {
    enrichedItems = await Promise.all(
      result.data.items.map(async (item) => {
        try {
          const productRes = await getProductById(item.productId);
          if (productRes.success && productRes.data.isActive) {
            return {
              productId: item.productId,
              addedAt: item.addedAt,
              product: productRes.data,
            };
          }
        } catch {
          // 商品無法取得或發生異常，維持 null
        }
        return {
          productId: item.productId,
          addedAt: item.addedAt,
          product: null,
        };
      })
    );
  }

  const totalPages = result.success
    ? Math.ceil(result.data.totalCount / pageSize) || 1
    : 1;

  return (
    <div className="min-h-screen bg-stone-50 text-stone-900 dark:bg-stone-950 dark:text-stone-100">
      <CustomerHeader subtitle="您的個人專屬收藏清單" />

      {/* 主要內容區 */}
      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
        {/* 頂部導覽與頁面標題 */}
        <div className="mb-8">
          <Link
            href="/"
            className="inline-flex items-center gap-1.5 text-sm font-medium text-stone-600 transition-colors hover:text-stone-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-sm dark:text-stone-400 dark:hover:text-stone-200"
          >
            <span aria-hidden="true">&larr;</span> 繼續選購商品
          </Link>
          <div className="mt-4 flex flex-col gap-1 sm:flex-row sm:items-baseline sm:justify-between">
            <h1 className="text-2xl font-bold tracking-tight text-stone-950 dark:text-stone-50 sm:text-3xl">
              我的收藏清單
            </h1>
            {result.success && result.data.totalCount > 0 && (
              <p className="text-sm text-stone-500 dark:text-stone-400">
                共 {result.data.totalCount} 項收藏商品
              </p>
            )}
          </div>
        </div>

        {/* 狀態渲染：未登入 / 系統錯誤 / 空清單 / 收藏列表 */}
        {!result.success ? (
          result.unauthorized ? (
            <section
              aria-label="需要登入"
              className="rounded-2xl border border-stone-200 bg-white p-8 text-center shadow-xs dark:border-stone-800 dark:bg-stone-900 sm:p-12"
            >
              <div
                aria-hidden="true"
                className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-stone-100 text-stone-400 dark:bg-stone-800 dark:text-stone-500"
              >
                <svg
                  className="h-6 w-6"
                  fill="none"
                  viewBox="0 0 24 24"
                  strokeWidth="1.5"
                  stroke="currentColor"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.501 20.118a7.5 7.5 0 0 1 14.998 0A17.933 17.933 0 0 1 12 21.75c-2.676 0-5.216-.584-7.499-1.632Z"
                  />
                </svg>
              </div>
              <h2 className="mt-4 text-lg font-semibold text-stone-950 dark:text-stone-50">
                尚未登入會員
              </h2>
              <p className="mx-auto mt-2 max-w-sm text-sm text-stone-500 dark:text-stone-400">
                請先登入以檢視並管理您的專屬收藏商品。
              </p>
              <div className="mt-6">
                <a
                  href="/auth/login?returnTo=/wishlist"
                  className="inline-flex min-h-[44px] items-center justify-center rounded-lg bg-stone-900 px-6 py-2.5 text-sm font-medium text-white shadow-xs transition-colors hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
                >
                  登入會員
                </a>
              </div>
            </section>
          ) : (
            <section
              aria-label="載入失敗"
              className="rounded-2xl border border-red-200 bg-red-50/70 p-6 text-red-900 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-200 sm:p-8"
            >
              <h2 className="text-lg font-semibold sm:text-xl">
                無法載入收藏清單
              </h2>
              <p className="mt-2 text-sm text-red-700 dark:text-red-300">
                {result.error}
              </p>
              <div className="mt-6">
                <Link
                  href="/wishlist"
                  className="inline-flex items-center justify-center rounded-lg border border-red-300 bg-white px-4 py-2 text-sm font-medium text-red-800 shadow-xs transition hover:bg-red-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:ring-offset-2 dark:border-red-800 dark:bg-stone-900 dark:text-red-200 dark:hover:bg-stone-800"
                >
                  重新嘗試
                </Link>
              </div>
            </section>
          )
        ) : result.data.totalCount === 0 || enrichedItems.length === 0 ? (
          <section
            aria-label="收藏清單為空"
            className="rounded-2xl border border-stone-200 bg-white p-8 text-center shadow-xs dark:border-stone-800 dark:bg-stone-900 sm:p-12"
          >
            <div
              aria-hidden="true"
              className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-stone-100 text-stone-400 dark:bg-stone-800 dark:text-stone-500"
            >
              <svg
                className="h-6 w-6"
                fill="none"
                viewBox="0 0 24 24"
                strokeWidth="1.5"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z"
                />
              </svg>
            </div>
            <h2 className="mt-4 text-lg font-semibold text-stone-950 dark:text-stone-50">
              目前沒有任何收藏商品
            </h2>
            <p className="mx-auto mt-2 max-w-sm text-sm text-stone-500 dark:text-stone-400">
              您可以在瀏覽商品時點擊「加入收藏」，方便日後快速查看。
            </p>
            <div className="mt-6">
              <Link
                href="/"
                className="inline-flex min-h-[44px] items-center justify-center rounded-lg bg-stone-900 px-6 py-2.5 text-sm font-medium text-white shadow-xs transition-colors hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
              >
                瀏覽精選商品
              </Link>
            </div>
          </section>
        ) : (
          <div className="space-y-6">
            <ul className="divide-y divide-stone-200 rounded-2xl border border-stone-200 bg-white shadow-xs dark:divide-stone-800 dark:border-stone-800 dark:bg-stone-900">
              {enrichedItems.map((item) => {
                if (!item.product) {
                  // Section 30: 下架或不存在商品的優雅中性展示
                  return (
                    <li
                      key={item.productId}
                      className="flex flex-col gap-4 p-4 sm:flex-row sm:items-center sm:justify-between sm:p-6"
                    >
                      <div className="flex items-center gap-4">
                        <div
                          aria-hidden="true"
                          className="flex h-16 w-16 shrink-0 items-center justify-center rounded-xl bg-stone-100 text-stone-400 dark:bg-stone-800 dark:text-stone-500"
                        >
                          <span className="text-xl">⊘</span>
                        </div>
                        <div>
                          <p className="text-base font-semibold text-stone-700 dark:text-stone-300">
                            商品目前無法瀏覽
                          </p>
                          <p className="mt-1 text-xs text-stone-400 dark:text-stone-500">
                            此商品可能已下架或暫時無法公開查看。
                          </p>
                        </div>
                      </div>
                      <div className="flex items-center justify-end">
                        <RemoveWishlistItemButton productId={item.productId} />
                      </div>
                    </li>
                  );
                }

                // Section 31: 正常上架商品展示
                const p = item.product;
                return (
                  <li
                    key={item.productId}
                    className="flex flex-col gap-4 p-4 sm:flex-row sm:items-center sm:justify-between sm:p-6"
                  >
                    <div className="flex items-center gap-4 min-w-0">
                      <div className="relative h-16 w-16 shrink-0 overflow-hidden rounded-xl bg-stone-100 dark:bg-stone-800">
                        {p.imageUrl ? (
                          <Image
                            src={p.imageUrl}
                            alt={p.name}
                            fill
                            className="object-cover"
                            sizes="64px"
                          />
                        ) : (
                          <div className="flex h-full w-full items-center justify-center text-xs font-semibold text-stone-400">
                            無圖片
                          </div>
                        )}
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <Link
                            href={`/products/${p.id}`}
                            className="text-base font-semibold text-stone-950 transition-colors hover:text-stone-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 rounded-sm dark:text-stone-50 dark:hover:text-stone-200 truncate"
                          >
                            {p.name}
                          </Link>
                          {p.category && p.category.trim().length > 0 && (
                            <span className="inline-flex items-center rounded-md bg-stone-100 px-2 py-0.5 text-xs font-medium text-stone-600 dark:bg-stone-800 dark:text-stone-300">
                              {p.category.trim()}
                            </span>
                          )}
                        </div>
                        <p className="mt-1 text-sm font-bold text-stone-900 dark:text-stone-100">
                          {formatPrice(p.price, p.currency)}
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center justify-end gap-3 shrink-0">
                      <Link
                        href={`/products/${p.id}`}
                        className="inline-flex items-center justify-center rounded-lg bg-stone-900 px-3.5 py-1.5 text-xs font-medium text-white shadow-2xs transition hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
                      >
                        查看商品
                      </Link>
                      <RemoveWishlistItemButton productId={item.productId} />
                    </div>
                  </li>
                );
              })}
            </ul>

            {/* 分頁導覽控制項 */}
            {totalPages > 1 && (
              <nav
                aria-label="收藏清單分頁導覽"
                className="flex items-center justify-between border-t border-stone-200 px-2 py-4 dark:border-stone-800"
              >
                <div>
                  {currentPage > 1 ? (
                    <Link
                      href={`/wishlist?page=${currentPage - 1}`}
                      className="inline-flex items-center gap-1 rounded-lg border border-stone-300 bg-white px-3.5 py-2 text-sm font-medium text-stone-700 shadow-2xs hover:bg-stone-50 dark:border-stone-700 dark:bg-stone-900 dark:text-stone-300 dark:hover:bg-stone-800"
                    >
                      &larr; 上一頁
                    </Link>
                  ) : (
                    <span className="inline-flex items-center gap-1 rounded-lg border border-stone-200 bg-stone-100 px-3.5 py-2 text-sm font-medium text-stone-400 cursor-not-allowed dark:border-stone-800 dark:bg-stone-850 dark:text-stone-600">
                      &larr; 上一頁
                    </span>
                  )}
                </div>

                <p className="text-xs text-stone-500 dark:text-stone-400 sm:text-sm">
                  第 {currentPage} 頁 / 共 {totalPages} 頁
                </p>

                <div>
                  {currentPage < totalPages ? (
                    <Link
                      href={`/wishlist?page=${currentPage + 1}`}
                      className="inline-flex items-center gap-1 rounded-lg border border-stone-300 bg-white px-3.5 py-2 text-sm font-medium text-stone-700 shadow-2xs hover:bg-stone-50 dark:border-stone-700 dark:bg-stone-900 dark:text-stone-300 dark:hover:bg-stone-800"
                    >
                      下一頁 &rarr;
                    </Link>
                  ) : (
                    <span className="inline-flex items-center gap-1 rounded-lg border border-stone-200 bg-stone-100 px-3.5 py-2 text-sm font-medium text-stone-400 cursor-not-allowed dark:border-stone-800 dark:bg-stone-850 dark:text-stone-600">
                      下一頁 &rarr;
                    </span>
                  )}
                </div>
              </nav>
            )}
          </div>
        )}
      </main>
    </div>
  );
}

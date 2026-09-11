import Link from "next/link";
import { getCustomerOrders } from "@/lib/orders";
import { formatPrice } from "@/lib/format";
import { CustomerHeader } from "@/components/CustomerHeader";

interface OrdersPageProps {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}

export default async function CustomerOrderHistoryPage({ searchParams }: OrdersPageProps) {
  const resolvedParams = await searchParams;
  const rawPage = Array.isArray(resolvedParams?.page)
    ? resolvedParams.page[0]
    : resolvedParams?.page;
  const parsedPage = rawPage ? parseInt(rawPage, 10) : 1;
  const requestedPage = !isNaN(parsedPage) && parsedPage > 0 ? Math.floor(parsedPage) : 1;

  const result = await getCustomerOrders(requestedPage, 25);

  const totalCount = result.success ? result.data.totalCount : 0;
  const currentPage = result.success ? result.data.page : requestedPage;
  const pageSize = result.success ? result.data.pageSize : 25;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const hasPrevious = currentPage > 1;
  const hasNext = currentPage < totalPages;

  return (
    <div className="min-h-screen bg-stone-50 text-stone-900 dark:bg-stone-950 dark:text-stone-100">
      <CustomerHeader subtitle="歷史訂單紀錄" />

      {/* 主要內容區 */}
      <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6 lg:px-8">
        {/* 頂部導覽與穩定頁面標題 H1 */}
        <div className="mb-8">
          <Link
            href="/"
            className="inline-flex items-center gap-1.5 text-sm font-medium text-stone-600 transition-colors hover:text-stone-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-sm dark:text-stone-400 dark:hover:text-stone-200"
          >
            <span aria-hidden="true">&larr;</span> 返回商品型錄
          </Link>
          <div className="mt-4 flex flex-col gap-1 sm:flex-row sm:items-baseline sm:justify-between">
            <h1 className="text-2xl font-bold tracking-tight text-stone-950 dark:text-stone-50 sm:text-3xl">
              我的訂單
            </h1>
            {result.success && totalCount > 0 && (
              <p className="text-sm text-stone-500 dark:text-stone-400">
                共 {totalCount} 筆訂單
              </p>
            )}
          </div>
          <p className="mt-1 text-sm text-stone-500 dark:text-stone-400">
            檢視您過去送出的所有訂單狀態與詳細內容。
          </p>
        </div>

        {/* 狀態渲染：未登入 / 系統錯誤 / 空狀態 / 越界狀態 / 訂單清單 */}
        {!result.success ? (
          result.unauthorized ? (
            /* 狀態 A: 未登入 */
            <section
              aria-labelledby="unauthorized-heading"
              className="rounded-2xl border border-stone-200 bg-white p-8 text-center shadow-sm dark:border-stone-800 dark:bg-stone-900 sm:p-12"
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
              <h2
                id="unauthorized-heading"
                className="mt-4 text-lg font-semibold text-stone-950 dark:text-stone-50"
              >
                需要登入會員
              </h2>
              <p className="mx-auto mt-2 max-w-sm text-sm text-stone-500 dark:text-stone-400">
                請登入以檢視您的歷史訂單紀錄。
              </p>
              <div className="mt-6">
                <a
                  href="/auth/login?returnTo=/orders"
                  className="inline-flex min-h-[44px] items-center justify-center rounded-lg bg-stone-900 px-6 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
                >
                  登入會員
                </a>
              </div>
            </section>
          ) : (
            /* 狀態 D: 載入失敗 */
            <section
              aria-labelledby="error-heading"
              className="rounded-2xl border border-red-200 bg-red-50/60 p-6 text-red-900 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-200 sm:p-8"
            >
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
                <div
                  aria-hidden="true"
                  className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-red-100 text-red-600 dark:bg-red-900/50 dark:text-red-400"
                >
                  <svg
                    className="h-5 w-5"
                    fill="none"
                    viewBox="0 0 24 24"
                    strokeWidth="1.5"
                    stroke="currentColor"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      d="M12 9v3.75m9-.75a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 3.75h.008v.008H12v-.008Z"
                    />
                  </svg>
                </div>
                <div className="flex-1 min-w-0">
                  <h2
                    id="error-heading"
                    className="text-base font-semibold text-red-950 dark:text-red-100"
                  >
                    無法載入訂單紀錄
                  </h2>
                  <p className="mt-1 break-words text-sm text-red-700 dark:text-red-300">
                    {result.error}
                  </p>
                  <div className="mt-5 flex flex-wrap gap-3">
                    <Link
                      href="/"
                      className="inline-flex min-h-[44px] items-center justify-center rounded-lg border border-red-300 bg-white px-4 py-2 text-sm font-medium text-red-900 transition-colors hover:bg-red-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:ring-offset-2 dark:border-red-900 dark:bg-stone-900 dark:text-red-200 dark:hover:bg-stone-800"
                    >
                      返回商品型錄
                    </Link>
                  </div>
                </div>
              </div>
            </section>
          )
        ) : totalCount === 0 ? (
          /* 狀態 C: 真空狀態 (True Empty History) */
          <section
            aria-labelledby="empty-heading"
            className="rounded-2xl border border-stone-200 bg-white p-8 text-center shadow-sm dark:border-stone-800 dark:bg-stone-900 sm:p-12"
          >
            <div
              aria-hidden="true"
              className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-stone-100 text-stone-400 dark:bg-stone-800 dark:text-stone-500"
            >
              <svg
                className="h-6 w-6"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={1.5}
                  d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"
                />
              </svg>
            </div>
            <h2
              id="empty-heading"
              className="mt-4 text-lg font-semibold text-stone-950 dark:text-stone-50"
            >
              尚無訂單紀錄
            </h2>
            <p className="mx-auto mt-2 max-w-sm text-sm text-stone-500 dark:text-stone-400">
              您目前尚未建立任何已送出的歷史訂單。
            </p>
            <div className="mt-6">
              <Link
                href="/"
                className="inline-flex min-h-[44px] items-center justify-center rounded-lg bg-stone-900 px-6 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
              >
                前往選購商品
              </Link>
            </div>
          </section>
        ) : result.data.items.length === 0 ? (
          /* 狀態 E: 越界頁碼狀態 (Out-of-range Page Recovery) */
          <section
            aria-labelledby="out-of-range-heading"
            className="rounded-2xl border border-stone-200 bg-white p-8 text-center shadow-sm dark:border-stone-800 dark:bg-stone-900 sm:p-12"
          >
            <div
              aria-hidden="true"
              className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-amber-50 text-amber-600 dark:bg-amber-950/50 dark:text-amber-400"
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
                  d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z"
                />
              </svg>
            </div>
            <h2
              id="out-of-range-heading"
              className="mt-4 text-lg font-semibold text-stone-950 dark:text-stone-50"
            >
              該頁面無訂單紀錄
            </h2>
            <p className="mx-auto mt-2 max-w-sm text-sm text-stone-500 dark:text-stone-400">
              您請求的第 {currentPage} 頁超出有效範圍（歷史訂單共 {totalCount} 筆，上限為第 {totalPages} 頁）。
            </p>
            <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
              <Link
                href="/orders?page=1"
                className="inline-flex min-h-[44px] items-center justify-center rounded-lg bg-stone-900 px-6 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
              >
                返回第 1 頁
              </Link>
              <Link
                href="/"
                className="inline-flex min-h-[44px] items-center justify-center rounded-lg border border-stone-300 bg-white px-5 py-2.5 text-sm font-medium text-stone-800 shadow-sm transition-colors hover:bg-stone-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-200 dark:hover:bg-stone-700"
              >
                返回商品型錄
              </Link>
            </div>
          </section>
        ) : (
          /* 狀態 B: 訂單列表與分頁導航 */
          <div className="space-y-6">
            <ul role="list" className="space-y-4">
              {result.data.items.map((order) => {
                const formattedDate = new Date(order.submittedAt).toLocaleString("zh-TW", {
                  year: "numeric",
                  month: "2-digit",
                  day: "2-digit",
                  hour: "2-digit",
                  minute: "2-digit",
                });

                return (
                  <li
                    key={order.id}
                    className="overflow-hidden rounded-2xl border border-stone-200 bg-white p-5 shadow-sm transition-all duration-200 hover:border-stone-300 hover:shadow-md dark:border-stone-800 dark:bg-stone-900 dark:hover:border-stone-700 sm:p-6"
                  >
                    <div className="flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
                      {/* 訂單識別、狀態與下單時間 */}
                      <div className="min-w-0 flex-1 space-y-2">
                        <div className="flex flex-wrap items-center gap-2.5 sm:gap-3">
                          <span className="text-xs font-semibold uppercase tracking-wider text-stone-400 dark:text-stone-500">
                            訂單編號
                          </span>
                          <span className="font-mono text-sm font-semibold tracking-tight text-stone-900 dark:text-stone-100 break-all">
                            #{order.id}
                          </span>
                          <div className="shrink-0">
                            {order.status === "Paid" ? (
                              <span className="inline-flex items-center rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-semibold text-emerald-800 ring-1 ring-inset ring-emerald-600/20 dark:bg-emerald-950/50 dark:text-emerald-300 dark:ring-emerald-500/30">
                                {order.status} (已付款)
                              </span>
                            ) : order.status === "Submitted" ? (
                              <span className="inline-flex items-center rounded-full bg-amber-50 px-2.5 py-0.5 text-xs font-semibold text-amber-800 ring-1 ring-inset ring-amber-600/20 dark:bg-amber-950/50 dark:text-amber-300 dark:ring-amber-500/30">
                                {order.status} (待付款)
                              </span>
                            ) : order.status === "Shipped" ? (
                              <span className="inline-flex items-center rounded-full bg-sky-50 px-2.5 py-0.5 text-xs font-semibold text-sky-800 ring-1 ring-inset ring-sky-600/20 dark:bg-sky-950/50 dark:text-sky-300 dark:ring-sky-500/30">
                                {order.status} (已出貨)
                              </span>
                            ) : order.status === "Cancelled" ? (
                              <span className="inline-flex items-center rounded-full bg-stone-100 px-2.5 py-0.5 text-xs font-semibold text-stone-700 ring-1 ring-inset ring-stone-500/20 dark:bg-stone-800 dark:text-stone-300 dark:ring-stone-600/30">
                                {order.status} (已取消)
                              </span>
                            ) : (
                              <span className="inline-flex items-center rounded-full bg-stone-100 px-2.5 py-0.5 text-xs font-semibold text-stone-700 ring-1 ring-inset ring-stone-500/20 dark:bg-stone-800 dark:text-stone-300 dark:ring-stone-600/30">
                                {order.status}
                              </span>
                            )}
                          </div>
                        </div>
                        <p className="text-xs text-stone-500 dark:text-stone-400">
                          下單時間：{formattedDate}
                        </p>
                      </div>

                      {/* 總計金額與查看詳情操作 */}
                      <div className="flex flex-col gap-3 pt-3 border-t border-stone-100 dark:border-stone-800/80 sm:flex-row sm:items-center sm:gap-6 sm:border-0 sm:pt-0 sm:justify-end">
                        <div className="flex items-baseline justify-between gap-4 sm:flex-col sm:items-end sm:gap-0">
                          <span className="text-xs text-stone-500 dark:text-stone-400">
                            總計金額
                          </span>
                          <p className="font-mono text-base font-bold text-stone-950 dark:text-stone-50 sm:text-lg tabular-nums">
                            {formatPrice(order.totalAmount, order.currency)}
                          </p>
                        </div>

                        <Link
                          href={`/orders/${encodeURIComponent(order.id)}`}
                          className="inline-flex min-h-[44px] items-center justify-center rounded-lg border border-stone-300 bg-white px-4 py-2 text-xs font-semibold text-stone-800 shadow-sm transition-colors hover:bg-stone-50 hover:text-stone-950 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-200 dark:hover:bg-stone-700 dark:hover:text-stone-50 sm:min-h-0 sm:py-2"
                        >
                          查看詳情 <span aria-hidden="true" className="ml-1">&rarr;</span>
                        </Link>
                      </div>
                    </div>
                  </li>
                );
              })}
            </ul>

            {/* 分頁導覽區塊 */}
            {totalPages > 1 && (
              <nav
                aria-label="訂單分頁導覽"
                className="flex flex-col items-center justify-between gap-4 rounded-2xl border border-stone-200 bg-white p-4 shadow-sm dark:border-stone-800 dark:bg-stone-900 sm:flex-row sm:px-6"
              >
                <div className="text-sm text-stone-500 dark:text-stone-400">
                  第 <span className="font-semibold text-stone-900 dark:text-stone-100">{currentPage}</span> 頁，共{" "}
                  <span className="font-semibold text-stone-900 dark:text-stone-100">{totalPages}</span> 頁
                </div>
                <div className="flex items-center gap-3">
                  {hasPrevious ? (
                    <Link
                      href={currentPage === 2 ? "/orders" : `/orders?page=${currentPage - 1}`}
                      className="inline-flex min-h-[44px] items-center justify-center rounded-lg border border-stone-300 bg-white px-4 py-2 text-sm font-medium text-stone-800 shadow-sm transition-colors hover:bg-stone-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-200 dark:hover:bg-stone-700"
                    >
                      &larr; 上一頁
                    </Link>
                  ) : (
                    <span
                      aria-disabled="true"
                      className="inline-flex min-h-[44px] cursor-not-allowed items-center justify-center rounded-lg border border-stone-200 bg-stone-100 px-4 py-2 text-sm font-medium text-stone-400 dark:border-stone-800 dark:bg-stone-900/50 dark:text-stone-600"
                    >
                      &larr; 上一頁
                    </span>
                  )}

                  {hasNext ? (
                    <Link
                      href={`/orders?page=${currentPage + 1}`}
                      className="inline-flex min-h-[44px] items-center justify-center rounded-lg border border-stone-300 bg-white px-4 py-2 text-sm font-medium text-stone-800 shadow-sm transition-colors hover:bg-stone-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-200 dark:hover:bg-stone-700"
                    >
                      下一頁 &rarr;
                    </Link>
                  ) : (
                    <span
                      aria-disabled="true"
                      className="inline-flex min-h-[44px] cursor-not-allowed items-center justify-center rounded-lg border border-stone-200 bg-stone-100 px-4 py-2 text-sm font-medium text-stone-400 dark:border-stone-800 dark:bg-stone-900/50 dark:text-stone-600"
                    >
                      下一頁 &rarr;
                    </span>
                  )}
                </div>
              </nav>
            )}

            <div className="pt-2">
              <Link
                href="/"
                className="inline-flex items-center text-sm font-medium text-stone-600 transition-colors hover:text-stone-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-sm dark:text-stone-400 dark:hover:text-stone-200"
              >
                &larr; 返回商品型錄
              </Link>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}

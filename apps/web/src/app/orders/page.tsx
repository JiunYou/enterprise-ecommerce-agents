import Link from "next/link";
import { getCustomerOrders } from "@/lib/orders";
import { formatPrice } from "@/lib/format";
import { AuthControls } from "@/components/AuthControls";

export default async function CustomerOrderHistoryPage() {
  const result = await getCustomerOrders();

  return (
    <div className="min-h-screen bg-zinc-50 text-zinc-900 dark:bg-zinc-950 dark:text-zinc-100">
      {/* 頂部導航列 */}
      <header className="border-b border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:px-8">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <Link href="/" className="inline-block">
                <h1 className="text-2xl font-bold tracking-tight text-zinc-900 dark:text-white">
                  Enterprise Commerce
                </h1>
              </Link>
              <p className="text-sm text-zinc-500 dark:text-zinc-400">
                歷史訂單紀錄
              </p>
            </div>
            <div className="flex items-center gap-4">
              <Link
                href="/cart"
                className="inline-flex items-center text-sm font-semibold text-zinc-600 transition hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-zinc-100"
              >
                購物車
              </Link>
              <AuthControls />
            </div>
          </div>
        </div>
      </header>

      {/* 主要內容區 */}
      <main className="mx-auto max-w-4xl px-4 py-8 sm:px-6 lg:px-8">
        {!result.success ? (
          result.unauthorized ? (
            /* 狀態 A: 未登入 */
            <section
              aria-label="需要登入"
              className="rounded-xl border border-zinc-200 bg-white p-12 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
            >
              <h2 className="text-lg font-medium text-zinc-900 dark:text-zinc-100">
                需要登入會員
              </h2>
              <p className="mt-2 text-sm text-zinc-500 dark:text-zinc-400">
                請登入以檢視您的歷史訂單紀錄。
              </p>
              <div className="mt-6">
                <a
                  href="/auth/login?returnTo=/orders"
                  className="inline-flex items-center rounded-lg bg-zinc-900 px-5 py-2.5 text-sm font-medium text-white shadow-sm transition hover:bg-zinc-800 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
                >
                  登入會員
                </a>
              </div>
            </section>
          ) : (
            /* 狀態 D: 載入失敗 */
            <section
              aria-label="系統錯誤訊息"
              className="rounded-xl border border-red-200 bg-red-50 p-6 text-red-800 dark:border-red-900/50 dark:bg-red-950/40 dark:text-red-300"
            >
              <h2 className="text-base font-semibold">無法載入訂單紀錄</h2>
              <p className="mt-1 text-sm">{result.error}</p>
              <div className="mt-4 flex gap-3">
                <Link
                  href="/"
                  className="inline-flex items-center rounded-md bg-red-100 px-3 py-1.5 text-sm font-medium text-red-900 transition hover:bg-red-200 dark:bg-red-900/60 dark:text-red-200 dark:hover:bg-red-900"
                >
                  返回商品型錄
                </Link>
              </div>
            </section>
          )
        ) : result.data.length === 0 ? (
          /* 狀態 C: 空狀態 */
          <section
            aria-label="尚無訂單紀錄"
            className="rounded-xl border border-zinc-200 bg-white p-12 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
          >
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-zinc-100 dark:bg-zinc-800">
              <svg
                className="h-6 w-6 text-zinc-500 dark:text-zinc-400"
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
            <h2 className="mt-4 text-lg font-medium text-zinc-900 dark:text-zinc-100">
              尚無訂單紀錄
            </h2>
            <p className="mt-2 text-sm text-zinc-500 dark:text-zinc-400">
              您目前尚未建立任何已送出的歷史訂單。
            </p>
            <div className="mt-6">
              <Link
                href="/"
                className="inline-flex items-center rounded-lg bg-zinc-900 px-5 py-2.5 text-sm font-medium text-white shadow-sm transition hover:bg-zinc-800 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
              >
                前往選購商品
              </Link>
            </div>
          </section>
        ) : (
          /* 狀態 B: 訂單列表 */
          <div className="space-y-6">
            <div>
              <h2 className="text-xl font-bold tracking-tight text-zinc-900 dark:text-white">
                您的訂單歷史 (共 {result.data.length} 筆)
              </h2>
              <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">
                檢視您過去送出的所有訂單狀態與詳細內容。
              </p>
            </div>

            <ul role="list" className="space-y-4">
              {result.data.map((order) => {
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
                    className="overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-sm transition hover:border-zinc-300 dark:border-zinc-800 dark:bg-zinc-900 dark:hover:border-zinc-700"
                  >
                    <div className="p-6">
                      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                        <div className="space-y-1">
                          <div className="flex items-center gap-3">
                            <span className="font-mono text-sm font-bold text-zinc-900 dark:text-zinc-100">
                              #{order.id}
                            </span>
                            {order.status === "Paid" ? (
                              <span className="inline-flex items-center rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-semibold text-emerald-800 dark:bg-emerald-900/60 dark:text-emerald-200">
                                {order.status} (已付款)
                              </span>
                            ) : order.status === "Submitted" ? (
                              <span className="inline-flex items-center rounded-full bg-blue-100 px-2.5 py-0.5 text-xs font-semibold text-blue-800 dark:bg-blue-900/60 dark:text-blue-200">
                                {order.status} (待付款)
                              </span>
                            ) : order.status === "Shipped" ? (
                              <span className="inline-flex items-center rounded-full bg-purple-100 px-2.5 py-0.5 text-xs font-semibold text-purple-800 dark:bg-purple-900/60 dark:text-purple-200">
                                {order.status} (已出貨)
                              </span>
                            ) : order.status === "Cancelled" ? (
                              <span className="inline-flex items-center rounded-full bg-zinc-200 px-2.5 py-0.5 text-xs font-semibold text-zinc-800 dark:bg-zinc-800 dark:text-zinc-300">
                                {order.status} (已取消)
                              </span>
                            ) : (
                              <span className="inline-flex items-center rounded-full bg-zinc-100 px-2.5 py-0.5 text-xs font-semibold text-zinc-800 dark:bg-zinc-800 dark:text-zinc-300">
                                {order.status}
                              </span>
                            )}
                          </div>
                          <p className="text-xs text-zinc-500 dark:text-zinc-400">
                            下單時間：{formattedDate}
                          </p>
                        </div>

                        <div className="flex items-center justify-between gap-6 sm:justify-end">
                          <div className="text-left sm:text-right">
                            <span className="text-xs text-zinc-500 dark:text-zinc-400">
                              總計金額
                            </span>
                            <p className="font-mono text-base font-bold text-zinc-900 dark:text-zinc-100">
                              {formatPrice(order.totalAmount, order.currency)}
                            </p>
                          </div>

                          <Link
                            href={`/orders/${encodeURIComponent(order.id)}`}
                            className="inline-flex items-center rounded-lg border border-zinc-300 bg-white px-3.5 py-2 text-xs font-semibold text-zinc-700 shadow-sm transition hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
                          >
                            查看詳情 &rarr;
                          </Link>
                        </div>
                      </div>
                    </div>
                  </li>
                );
              })}
            </ul>

            <div className="pt-2">
              <Link
                href="/"
                className="inline-flex items-center text-sm font-medium text-zinc-600 transition hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-zinc-100"
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

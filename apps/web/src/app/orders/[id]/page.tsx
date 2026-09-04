import Link from "next/link";
import { getOrderById } from "@/lib/orders";
import { formatPrice } from "@/lib/format";
import { AuthControls } from "@/components/AuthControls";

interface OrderConfirmationPageProps {
  params: Promise<{
    id: string;
  }>;
}

export default async function OrderConfirmationPage({
  params,
}: OrderConfirmationPageProps) {
  const { id } = await params;
  const result = await getOrderById(id);

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
                訂單送出確認 (Pre-Payment)
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
            <section
              aria-label="需要登入"
              className="rounded-xl border border-zinc-200 bg-white p-12 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
            >
              <h3 className="text-lg font-medium text-zinc-900 dark:text-zinc-100">
                需要登入會員
              </h3>
              <p className="mt-2 text-sm text-zinc-500 dark:text-zinc-400">
                請登入以檢視您的訂單確認資訊。
              </p>
              <div className="mt-6">
                <a
                  href={`/auth/login?returnTo=/orders/${encodeURIComponent(id)}`}
                  className="inline-flex items-center rounded-lg bg-zinc-900 px-5 py-2.5 text-sm font-medium text-white shadow-sm transition hover:bg-zinc-800 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
                >
                  登入會員
                </a>
              </div>
            </section>
          ) : (
            <section
              aria-label="系統錯誤訊息"
              className="rounded-xl border border-red-200 bg-red-50 p-6 text-red-800 dark:border-red-900/50 dark:bg-red-950/40 dark:text-red-300"
            >
              <h3 className="text-base font-semibold">無法載入訂單確認資訊</h3>
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
        ) : (
          <div className="space-y-6">
            {/* 成功確認橫幅 */}
            <section
              aria-label="訂單送出成功"
              className="rounded-xl border border-emerald-200 bg-emerald-50 p-6 dark:border-emerald-900/50 dark:bg-emerald-950/40 dark:text-emerald-300"
            >
              <div className="flex items-start gap-4">
                <div className="rounded-full bg-emerald-100 p-2 text-emerald-600 dark:bg-emerald-900/80 dark:text-emerald-300">
                  <svg
                    className="h-6 w-6"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M5 13l4 4L19 7"
                    />
                  </svg>
                </div>
                <div>
                  <h2 className="text-lg font-bold text-emerald-900 dark:text-emerald-100">
                    訂單已成功送出！
                  </h2>
                  <p className="mt-1 text-sm text-emerald-800 dark:text-emerald-200">
                    感謝您的購買！您的訂單已正式送出並完成庫存保留。本階段為付款前結帳流程（Pre-Payment），後續付款與出貨處理將於下一階段為您提供。
                  </p>
                </div>
              </div>
            </section>

            {/* 訂單基本資訊卡片 */}
            <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
              <div className="border-b border-zinc-200 bg-zinc-50/60 px-6 py-4 dark:border-zinc-800 dark:bg-zinc-900/50">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <span className="text-xs font-semibold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
                      訂單編號
                    </span>
                    <p className="font-mono text-base font-bold text-zinc-900 dark:text-zinc-100">
                      {result.data.id}
                    </p>
                  </div>
                  <div className="sm:text-right">
                    <span className="text-xs font-semibold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
                      訂單狀態
                    </span>
                    <div className="mt-0.5">
                      <span className="inline-flex items-center rounded-full bg-blue-100 px-2.5 py-0.5 text-xs font-semibold text-blue-800 dark:bg-blue-900/60 dark:text-blue-200">
                        {result.data.status} (已送出)
                      </span>
                    </div>
                  </div>
                </div>
              </div>

              {/* 訂單商品清單 */}
              <div className="px-6 py-4">
                <h3 className="text-sm font-semibold text-zinc-700 dark:text-zinc-300">
                  訂單商品品項 (共 {result.data.items.length} 項)
                </h3>
              </div>
              <ul role="list" className="divide-y divide-zinc-200 border-t border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
                {result.data.items.map((item) => (
                  <li
                    key={item.productId}
                    className="flex flex-col gap-2 p-6 sm:flex-row sm:items-center sm:justify-between"
                  >
                    <div className="flex-1">
                      <h4 className="text-base font-semibold text-zinc-900 dark:text-zinc-100">
                        {item.productName || "商品 (" + item.productId.slice(0, 8) + "...)"}
                      </h4>
                      <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
                        商品 ID: {item.productId}
                      </p>
                      <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">
                        單價：{formatPrice(item.unitPrice, item.currency)}
                      </p>
                    </div>

                    <div className="flex items-center gap-8">
                      <div className="text-sm text-zinc-600 dark:text-zinc-400">
                        <span>數量：</span>
                        <span className="font-mono font-semibold text-zinc-900 dark:text-zinc-100">
                          {item.quantity}
                        </span>
                      </div>
                      <div className="w-28 text-right font-mono text-base font-semibold text-zinc-900 dark:text-zinc-100">
                        {formatPrice(item.totalPrice, item.currency)}
                      </div>
                    </div>
                  </li>
                ))}
              </ul>

              {/* 總計摘要列 */}
              <div className="border-t border-zinc-200 bg-zinc-50 px-6 py-5 dark:border-zinc-800 dark:bg-zinc-900/60">
                <div className="flex items-center justify-between">
                  <span className="text-base font-semibold text-zinc-700 dark:text-zinc-300">
                    訂單總計金額 ({result.data.currency})
                  </span>
                  <span className="font-mono text-2xl font-extrabold text-zinc-900 dark:text-zinc-100">
                    {formatPrice(result.data.totalAmount, result.data.currency)}
                  </span>
                </div>
              </div>
            </div>

            {/* 返回按鈕列 */}
            <div className="flex justify-between items-center pt-2">
              <Link
                href="/cart"
                className="inline-flex items-center text-sm font-medium text-zinc-600 transition hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-zinc-100"
              >
                &larr; 前往購物車
              </Link>
              <Link
                href="/"
                className="inline-flex items-center rounded-lg bg-zinc-900 px-6 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-zinc-800 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
              >
                繼續選購商品
              </Link>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}

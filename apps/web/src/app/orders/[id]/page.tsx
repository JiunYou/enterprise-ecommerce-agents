import Link from "next/link";
import { getOrderById } from "@/lib/orders";
import { formatPrice } from "@/lib/format";
import { AuthControls } from "@/components/AuthControls";
import { PaymentButton } from "@/components/PaymentButton";

interface OrderConfirmationPageProps {
  params: Promise<{
    id: string;
  }>;
  searchParams?: Promise<{
    payment?: string;
  }>;
}

export default async function OrderConfirmationPage({
  params,
  searchParams,
}: OrderConfirmationPageProps) {
  const { id } = await params;
  const query = await searchParams;
  const paymentParam = query?.payment;

  const result = await getOrderById(id);

  const isSubmitted = result.success && result.data.status === "Submitted";
  const isPaid = result.success && result.data.status === "Paid";
  const isCancelled = result.success && result.data.status === "Cancelled";

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
                訂單詳情與結帳付款
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
              <h3 className="text-base font-semibold">無法載入訂單資訊</h3>
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
            {/* 支付跳轉回傳狀態提示橫幅 */}
            {(paymentParam === "success" || paymentParam === "returned") && (
              isPaid ? (
                <section
                  aria-label="付款成功"
                  className="rounded-xl border border-emerald-200 bg-emerald-50 p-6 dark:border-emerald-900/50 dark:bg-emerald-950/40 dark:text-emerald-300"
                >
                  <div className="flex items-start gap-4">
                    <div className="rounded-full bg-emerald-100 p-2 text-emerald-600 dark:bg-emerald-900/80 dark:text-emerald-300">
                      <svg className="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                      </svg>
                    </div>
                    <div>
                      <h2 className="text-lg font-bold text-emerald-900 dark:text-emerald-100">
                        付款已成功完成！
                      </h2>
                      <p className="mt-1 text-sm text-emerald-800 dark:text-emerald-200">
                        系統已透過安全 Webhook 驗證您的付款，訂單正式轉為「已付款」狀態，我們將儘速為您安排出貨。
                      </p>
                    </div>
                  </div>
                </section>
              ) : (
                <section
                  aria-label="付款處理中"
                  className="rounded-xl border border-amber-200 bg-amber-50 p-6 dark:border-amber-900/50 dark:bg-amber-950/40 dark:text-amber-300"
                >
                  <div className="flex items-start gap-4">
                    <div className="rounded-full bg-amber-100 p-2 text-amber-600 dark:bg-amber-900/80 dark:text-amber-300">
                      <svg className="h-6 w-6 animate-pulse" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                      </svg>
                    </div>
                    <div>
                      <h2 className="text-lg font-bold text-amber-900 dark:text-amber-100">
                        付款資訊處理中
                      </h2>
                      <p className="mt-1 text-sm text-amber-800 dark:text-amber-200">
                        我們已接收到您的結帳回傳，伺服器正等候安全 Webhook 完成確認。若狀態尚未更新，請稍候片刻並重新整理頁面。
                      </p>
                    </div>
                  </div>
                </section>
              )
            )}

            {paymentParam === "cancelled" && (
              <section
                aria-label="付款已取消"
                className="rounded-xl border border-zinc-200 bg-zinc-100 p-6 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300"
              >
                <div className="flex items-start gap-4">
                  <div className="rounded-full bg-zinc-200 p-2 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
                    <svg className="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                    </svg>
                  </div>
                  <div>
                    <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-100">
                      付款流程已取消
                    </h2>
                    <p className="mt-1 text-sm text-zinc-700 dark:text-zinc-300">
                      您已中途取消結帳。您的訂單仍安全保留在庫存中，若您準備好完成購買，可點擊下方按鈕重新進行安全付款。
                    </p>
                  </div>
                </div>
              </section>
            )}

            {/* 標準狀態橫幅 (無 query parameter 時) */}
            {!paymentParam && (
              isPaid ? (
                <section
                  aria-label="訂單已付款"
                  className="rounded-xl border border-emerald-200 bg-emerald-50 p-6 dark:border-emerald-900/50 dark:bg-emerald-950/40 dark:text-emerald-300"
                >
                  <div className="flex items-start gap-4">
                    <div className="rounded-full bg-emerald-100 p-2 text-emerald-600 dark:bg-emerald-900/80 dark:text-emerald-300">
                      <svg className="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                      </svg>
                    </div>
                    <div>
                      <h2 className="text-lg font-bold text-emerald-900 dark:text-emerald-100">
                        訂單已完成付款
                      </h2>
                      <p className="mt-1 text-sm text-emerald-800 dark:text-emerald-200">
                        此訂單已確認付款，目前正由倉儲系統處理中。
                      </p>
                    </div>
                  </div>
                </section>
              ) : isSubmitted ? (
                <section
                  aria-label="訂單已送出待付款"
                  className="rounded-xl border border-blue-200 bg-blue-50 p-6 dark:border-blue-900/50 dark:bg-blue-950/40 dark:text-blue-300"
                >
                  <div className="flex items-start gap-4">
                    <div className="rounded-full bg-blue-100 p-2 text-blue-600 dark:bg-blue-900/80 dark:text-blue-300">
                      <svg className="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                      </svg>
                    </div>
                    <div>
                      <h2 className="text-lg font-bold text-blue-900 dark:text-blue-100">
                        訂單已送出，等待付款
                      </h2>
                      <p className="mt-1 text-sm text-blue-800 dark:text-blue-200">
                        商品庫存已為您保留。請點擊「前往安全付款」完成安全託管結帳。
                      </p>
                    </div>
                  </div>
                </section>
              ) : isCancelled ? (
                <section
                  aria-label="訂單已取消"
                  className="rounded-xl border border-zinc-200 bg-zinc-100 p-6 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300"
                >
                  <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-100">
                    此訂單已取消
                  </h2>
                  <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">
                    該訂單已被取消或逾期失效，無法再進行付款。
                  </p>
                </section>
              ) : null
            )}

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
                      {isPaid ? (
                        <span className="inline-flex items-center rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-semibold text-emerald-800 dark:bg-emerald-900/60 dark:text-emerald-200">
                          {result.data.status} (已付款)
                        </span>
                      ) : isSubmitted ? (
                        <span className="inline-flex items-center rounded-full bg-blue-100 px-2.5 py-0.5 text-xs font-semibold text-blue-800 dark:bg-blue-900/60 dark:text-blue-200">
                          {result.data.status} (待付款)
                        </span>
                      ) : isCancelled ? (
                        <span className="inline-flex items-center rounded-full bg-zinc-200 px-2.5 py-0.5 text-xs font-semibold text-zinc-800 dark:bg-zinc-800 dark:text-zinc-300">
                          {result.data.status} (已取消)
                        </span>
                      ) : (
                        <span className="inline-flex items-center rounded-full bg-zinc-100 px-2.5 py-0.5 text-xs font-semibold text-zinc-800 dark:bg-zinc-800 dark:text-zinc-300">
                          {result.data.status}
                        </span>
                      )}
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

              {/* 配送收件資訊快照 */}
              <div className="border-t border-zinc-200 bg-white px-6 py-5 dark:border-zinc-800 dark:bg-zinc-900">
                <h3 className="text-sm font-semibold text-zinc-700 dark:text-zinc-300">
                  配送收件資訊
                </h3>
                {result.data.shippingAddress ? (
                  <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 text-sm">
                    <div>
                      <span className="text-xs text-zinc-500 dark:text-zinc-400">收件人</span>
                      <p className="font-medium text-zinc-900 dark:text-zinc-100">
                        {result.data.shippingAddress.recipientName}
                      </p>
                    </div>
                    <div>
                      <span className="text-xs text-zinc-500 dark:text-zinc-400">聯絡電話</span>
                      <p className="font-medium text-zinc-900 dark:text-zinc-100">
                        {result.data.shippingAddress.phone}
                      </p>
                    </div>
                    <div className="sm:col-span-2">
                      <span className="text-xs text-zinc-500 dark:text-zinc-400">送貨地址</span>
                      <p className="font-medium text-zinc-900 dark:text-zinc-100">
                        [{result.data.shippingAddress.countryCode}] {result.data.shippingAddress.postalCode} {result.data.shippingAddress.city} {result.data.shippingAddress.addressLine1}
                        {result.data.shippingAddress.addressLine2 ? ` ${result.data.shippingAddress.addressLine2}` : ""}
                      </p>
                    </div>
                  </div>
                ) : (
                  <div className="mt-2 rounded-lg bg-zinc-50 p-3 text-xs text-zinc-500 dark:bg-zinc-800/60 dark:text-zinc-400">
                    此歷史訂單無收件資訊快照 (Shipping information unavailable for this historical order)
                  </div>
                )}
              </div>

              {/* 總計摘要與付款按鈕區 */}
              <div className="border-t border-zinc-200 bg-zinc-50 px-6 py-5 dark:border-zinc-800 dark:bg-zinc-900/60">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <span className="text-base font-semibold text-zinc-700 dark:text-zinc-300">
                      訂單總計金額 ({result.data.currency})
                    </span>
                    <p className="font-mono text-2xl font-extrabold text-zinc-900 dark:text-zinc-100">
                      {formatPrice(result.data.totalAmount, result.data.currency)}
                    </p>
                  </div>

                  {/* 僅在 Submitted 狀態下顯示安全付款動作 */}
                  {isSubmitted && (
                    <div className="sm:text-right">
                      <PaymentButton orderId={result.data.id} />
                    </div>
                  )}
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

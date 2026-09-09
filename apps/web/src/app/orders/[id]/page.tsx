import Link from "next/link";
import { getOrderById } from "@/lib/orders";
import { formatPrice } from "@/lib/format";
import { CustomerHeader } from "@/components/CustomerHeader";
import { PaymentButton } from "@/components/PaymentButton";
import { CancelOrderButton } from "@/components/CancelOrderButton";

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
    <div className="min-h-screen bg-stone-50 text-stone-900 dark:bg-stone-950 dark:text-stone-100">
      <CustomerHeader subtitle="訂單詳情與結帳付款" />

      {/* 主要內容區 */}
      <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6 lg:px-8">
        {/* 頂部導覽與穩定頁面標題 H1 */}
        <div className="mb-8">
          <Link
            href="/orders"
            className="inline-flex items-center gap-1.5 text-sm font-medium text-stone-600 transition-colors hover:text-stone-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-sm dark:text-stone-400 dark:hover:text-stone-200"
          >
            <span aria-hidden="true">&larr;</span> 返回訂單紀錄
          </Link>
          <div className="mt-4 flex flex-col gap-1 sm:flex-row sm:items-baseline sm:justify-between">
            <h1 className="text-2xl font-bold tracking-tight text-stone-950 dark:text-stone-50 sm:text-3xl">
              訂單詳情
            </h1>
            {result.success && (
              <p className="font-mono text-xs text-stone-500 dark:text-stone-400 break-all">
                #{result.data.id}
              </p>
            )}
          </div>
          <p className="mt-1 text-sm text-stone-500 dark:text-stone-400">
            檢視此筆訂單之完整購買品項、配送收件資訊與付款狀態。
          </p>
        </div>

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
                請登入以檢視您的訂單確認資訊。
              </p>
              <div className="mt-6">
                <a
                  href={`/auth/login?returnTo=/orders/${encodeURIComponent(id)}`}
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
                    無法載入訂單資訊
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
        ) : (
          <div className="space-y-6">
            {/* 支付跳轉回傳狀態提示橫幅 */}
            {(paymentParam === "success" || paymentParam === "returned") && (
              isPaid ? (
                <section
                  aria-labelledby="payment-success-heading"
                  className="rounded-2xl border border-emerald-200/80 bg-emerald-50/70 p-5 dark:border-emerald-900/40 dark:bg-emerald-950/25 dark:text-emerald-300 sm:p-6"
                >
                  <div className="flex items-start gap-3.5 sm:gap-4">
                    <div
                      aria-hidden="true"
                      className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-emerald-700 dark:bg-emerald-900/60 dark:text-emerald-300"
                    >
                      <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                      </svg>
                    </div>
                    <div className="min-w-0 flex-1">
                      <h2
                        id="payment-success-heading"
                        className="text-base font-bold text-emerald-950 dark:text-emerald-100 sm:text-lg"
                      >
                        付款已成功完成！
                      </h2>
                      <p className="mt-1 break-words text-xs text-emerald-900/90 dark:text-emerald-200/90 sm:text-sm">
                        系統已透過安全 Webhook 驗證您的付款，訂單正式轉為「已付款」狀態，我們將儘速為您安排出貨。
                      </p>
                    </div>
                  </div>
                </section>
              ) : (
                <section
                  aria-labelledby="payment-processing-heading"
                  className="rounded-2xl border border-amber-200/80 bg-amber-50/70 p-5 dark:border-amber-900/40 dark:bg-amber-950/25 dark:text-amber-300 sm:p-6"
                >
                  <div className="flex items-start gap-3.5 sm:gap-4">
                    <div
                      aria-hidden="true"
                      className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-amber-100 text-amber-700 dark:bg-amber-900/60 dark:text-amber-300"
                    >
                      <svg className="h-5 w-5 animate-pulse" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                      </svg>
                    </div>
                    <div className="min-w-0 flex-1">
                      <h2
                        id="payment-processing-heading"
                        className="text-base font-bold text-amber-950 dark:text-amber-100 sm:text-lg"
                      >
                        付款資訊處理中
                      </h2>
                      <p className="mt-1 break-words text-xs text-amber-900/90 dark:text-amber-200/90 sm:text-sm">
                        我們已接收到您的結帳回傳，伺服器正等候安全 Webhook 完成確認。若狀態尚未更新，請稍候片刻並重新整理頁面。
                      </p>
                    </div>
                  </div>
                </section>
              )
            )}

            {paymentParam === "cancelled" && (
              <section
                aria-labelledby="payment-cancelled-heading"
                className="rounded-2xl border border-stone-200 bg-stone-100/80 p-5 dark:border-stone-800 dark:bg-stone-900 dark:text-stone-300 sm:p-6"
              >
                <div className="flex items-start gap-3.5 sm:gap-4">
                  <div
                    aria-hidden="true"
                    className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-stone-200 text-stone-700 dark:bg-stone-800 dark:text-stone-300"
                  >
                    <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                    </svg>
                  </div>
                  <div className="min-w-0 flex-1">
                    <h2
                      id="payment-cancelled-heading"
                      className="text-base font-bold text-stone-950 dark:text-stone-100 sm:text-lg"
                    >
                      付款流程已取消
                    </h2>
                    <p className="mt-1 break-words text-xs text-stone-700 dark:text-stone-300 sm:text-sm">
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
                  aria-labelledby="order-paid-heading"
                  className="rounded-2xl border border-emerald-200/80 bg-emerald-50/70 p-5 dark:border-emerald-900/40 dark:bg-emerald-950/25 dark:text-emerald-300 sm:p-6"
                >
                  <div className="flex items-start gap-3.5 sm:gap-4">
                    <div
                      aria-hidden="true"
                      className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-emerald-700 dark:bg-emerald-900/60 dark:text-emerald-300"
                    >
                      <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                      </svg>
                    </div>
                    <div className="min-w-0 flex-1">
                      <h2
                        id="order-paid-heading"
                        className="text-base font-bold text-emerald-950 dark:text-emerald-100 sm:text-lg"
                      >
                        訂單已完成付款
                      </h2>
                      <p className="mt-1 break-words text-xs text-emerald-900/90 dark:text-emerald-200/90 sm:text-sm">
                        此訂單已確認付款，目前正由倉儲系統處理中。
                      </p>
                    </div>
                  </div>
                </section>
              ) : isSubmitted ? (
                <section
                  aria-labelledby="order-submitted-heading"
                  className="rounded-2xl border border-sky-200/80 bg-sky-50/70 p-5 dark:border-sky-900/40 dark:bg-sky-950/25 dark:text-sky-300 sm:p-6"
                >
                  <div className="flex items-start gap-3.5 sm:gap-4">
                    <div
                      aria-hidden="true"
                      className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-sky-100 text-sky-700 dark:bg-sky-900/60 dark:text-sky-300"
                    >
                      <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                      </svg>
                    </div>
                    <div className="min-w-0 flex-1">
                      <h2
                        id="order-submitted-heading"
                        className="text-base font-bold text-sky-950 dark:text-sky-100 sm:text-lg"
                      >
                        訂單已送出，等待付款
                      </h2>
                      <p className="mt-1 break-words text-xs text-sky-900/90 dark:text-sky-200/90 sm:text-sm">
                        商品庫存已為您保留。請點擊「前往安全付款」完成安全託管結帳。
                      </p>
                    </div>
                  </div>
                </section>
              ) : isCancelled ? (
                <section
                  aria-labelledby="order-cancelled-heading"
                  className="rounded-2xl border border-stone-200 bg-stone-100/80 p-5 dark:border-stone-800 dark:bg-stone-900 dark:text-stone-300 sm:p-6"
                >
                  <div className="flex items-start gap-3.5 sm:gap-4">
                    <div
                      aria-hidden="true"
                      className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-stone-200 text-stone-700 dark:bg-stone-800 dark:text-stone-300"
                    >
                      <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                      </svg>
                    </div>
                    <div className="min-w-0 flex-1">
                      <h2
                        id="order-cancelled-heading"
                        className="text-base font-bold text-stone-950 dark:text-stone-100 sm:text-lg"
                      >
                        此訂單已取消
                      </h2>
                      <p className="mt-1 break-words text-xs text-stone-600 dark:text-stone-400 sm:text-sm">
                        該訂單已被取消或逾期失效，無法再進行付款。
                      </p>
                    </div>
                  </div>
                </section>
              ) : null
            )}

            {/* 訂單基本資訊卡片 */}
            <div className="overflow-hidden rounded-2xl border border-stone-200 bg-white shadow-sm dark:border-stone-800 dark:bg-stone-900">
              <div className="border-b border-stone-100 bg-stone-50/70 p-5 dark:border-stone-800 dark:bg-stone-900/60 sm:p-6">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="min-w-0">
                    <span className="text-xs font-semibold uppercase tracking-wider text-stone-400 dark:text-stone-500">
                      訂單編號
                    </span>
                    <p className="font-mono text-sm sm:text-base font-bold text-stone-950 dark:text-stone-50 break-all">
                      {result.data.id}
                    </p>
                  </div>
                  <div className="shrink-0 sm:text-right">
                    <span className="text-xs font-semibold uppercase tracking-wider text-stone-400 dark:text-stone-500">
                      訂單狀態
                    </span>
                    <div className="mt-1">
                      {isPaid ? (
                        <span className="inline-flex items-center rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-semibold text-emerald-800 ring-1 ring-inset ring-emerald-600/20 dark:bg-emerald-950/50 dark:text-emerald-300 dark:ring-emerald-500/30">
                          {result.data.status} (已付款)
                        </span>
                      ) : isSubmitted ? (
                        <span className="inline-flex items-center rounded-full bg-amber-50 px-2.5 py-0.5 text-xs font-semibold text-amber-800 ring-1 ring-inset ring-amber-600/20 dark:bg-amber-950/50 dark:text-amber-300 dark:ring-amber-500/30">
                          {result.data.status} (待付款)
                        </span>
                      ) : isCancelled ? (
                        <span className="inline-flex items-center rounded-full bg-stone-100 px-2.5 py-0.5 text-xs font-semibold text-stone-700 ring-1 ring-inset ring-stone-500/20 dark:bg-stone-800 dark:text-stone-300 dark:ring-stone-600/30">
                          {result.data.status} (已取消)
                        </span>
                      ) : (
                        <span className="inline-flex items-center rounded-full bg-stone-100 px-2.5 py-0.5 text-xs font-semibold text-stone-700 ring-1 ring-inset ring-stone-500/20 dark:bg-stone-800 dark:text-stone-300 dark:ring-stone-600/30">
                          {result.data.status}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              </div>

              {/* 訂單商品清單 */}
              <div className="p-5 sm:p-6">
                <h3 className="text-sm font-semibold text-stone-900 dark:text-stone-100">
                  訂單商品品項 (共 {result.data.items.length} 項)
                </h3>
              </div>

              <ul role="list" className="divide-y divide-stone-100 border-t border-stone-100 dark:divide-stone-800 dark:border-stone-800">
                {result.data.items.map((item) => {
                  const initialChar = (item.productName?.trim() || item.productId).charAt(0).toUpperCase();

                  return (
                    <li
                      key={item.productId}
                      className="flex flex-col gap-4 p-5 transition-colors sm:flex-row sm:items-center sm:justify-between sm:p-6"
                    >
                      <div className="flex items-start gap-4 min-w-0 flex-1">
                        {/* 純展示性裝飾字首磚 */}
                        <div
                          aria-hidden="true"
                          className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-stone-100 font-mono text-base font-bold text-stone-600 dark:bg-stone-800 dark:text-stone-300"
                        >
                          {initialChar}
                        </div>
                        <div className="min-w-0 flex-1 space-y-1">
                          <h4 className="text-sm sm:text-base font-semibold text-stone-950 dark:text-stone-50 break-words">
                            {item.productName || "商品 (" + item.productId.slice(0, 8) + "...)"}
                          </h4>
                          <p className="font-mono text-xs text-stone-400 dark:text-stone-500 break-all">
                            商品 ID: {item.productId}
                          </p>
                          <p className="text-xs text-stone-600 dark:text-stone-400">
                            單價：{formatPrice(item.unitPrice, item.currency)}
                          </p>
                        </div>
                      </div>

                      <div className="flex items-center justify-between gap-6 border-t border-stone-50 pt-3 dark:border-stone-800/60 sm:border-0 sm:pt-0 sm:justify-end">
                        <div className="text-xs sm:text-sm text-stone-600 dark:text-stone-400">
                          <span>數量：</span>
                          <span className="font-mono font-semibold text-stone-950 dark:text-stone-50 tabular-nums">
                            {item.quantity}
                          </span>
                        </div>
                        <div className="text-right font-mono text-sm sm:text-base font-bold text-stone-950 dark:text-stone-50 tabular-nums">
                          {formatPrice(item.totalPrice, item.currency)}
                        </div>
                      </div>
                    </li>
                  );
                })}
              </ul>

              {/* 配送收件資訊快照 */}
              <div className="border-t border-stone-100 bg-white p-5 dark:border-stone-800 dark:bg-stone-900 sm:p-6">
                <h3 className="text-sm font-semibold text-stone-900 dark:text-stone-100">
                  配送收件資訊
                </h3>
                {result.data.shippingAddress ? (
                  <div className="mt-4 grid grid-cols-1 gap-4 text-sm sm:grid-cols-2">
                    <div className="rounded-xl border border-stone-100 bg-stone-50/50 p-3.5 dark:border-stone-800/80 dark:bg-stone-800/40">
                      <span className="text-xs font-medium text-stone-400 dark:text-stone-500">收件人</span>
                      <p className="mt-0.5 font-medium text-stone-900 dark:text-stone-100 break-words">
                        {result.data.shippingAddress.recipientName}
                      </p>
                    </div>
                    <div className="rounded-xl border border-stone-100 bg-stone-50/50 p-3.5 dark:border-stone-800/80 dark:bg-stone-800/40">
                      <span className="text-xs font-medium text-stone-400 dark:text-stone-500">聯絡電話</span>
                      <p className="mt-0.5 font-medium text-stone-900 dark:text-stone-100 break-words">
                        {result.data.shippingAddress.phone}
                      </p>
                    </div>
                    <div className="sm:col-span-2 rounded-xl border border-stone-100 bg-stone-50/50 p-3.5 dark:border-stone-800/80 dark:bg-stone-800/40">
                      <span className="text-xs font-medium text-stone-400 dark:text-stone-500">送貨地址</span>
                      <p className="mt-0.5 font-medium text-stone-900 dark:text-stone-100 break-words">
                        [{result.data.shippingAddress.countryCode}] {result.data.shippingAddress.postalCode} {result.data.shippingAddress.city} {result.data.shippingAddress.addressLine1}
                        {result.data.shippingAddress.addressLine2 ? ` ${result.data.shippingAddress.addressLine2}` : ""}
                      </p>
                    </div>
                  </div>
                ) : (
                  <div className="mt-3 rounded-xl border border-stone-200 bg-stone-50/60 p-4 text-xs text-stone-500 dark:border-stone-800 dark:bg-stone-800/40 dark:text-stone-400 break-words">
                    此歷史訂單無收件資訊快照 (Shipping information unavailable for this historical order)
                  </div>
                )}
              </div>

              {/* 總計摘要與付款按鈕區 */}
              <div className="border-t border-stone-100 bg-stone-50/80 p-5 dark:border-stone-800 dark:bg-stone-900/60 sm:p-6">
                <div className="flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <span className="text-xs font-semibold uppercase tracking-wider text-stone-400 dark:text-stone-500">
                      訂單總計金額 ({result.data.currency})
                    </span>
                    <p className="font-mono text-2xl font-extrabold text-stone-950 dark:text-stone-50 sm:text-3xl tabular-nums">
                      {formatPrice(result.data.totalAmount, result.data.currency)}
                    </p>
                  </div>

                  {/* 僅在 Submitted 狀態下顯示安全付款與取消動作 */}
                  {isSubmitted && (
                    <div className="flex w-full flex-col items-stretch gap-3 sm:w-auto sm:items-end sm:text-right">
                      <PaymentButton orderId={result.data.id} />
                      <CancelOrderButton orderId={result.data.id} />
                    </div>
                  )}
                </div>
              </div>
            </div>

            {/* 返回按鈕列 */}
            <div className="flex flex-wrap items-center justify-between gap-4 pt-2">
              <Link
                href="/cart"
                className="inline-flex min-h-[44px] items-center text-sm font-medium text-stone-600 transition-colors hover:text-stone-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-sm dark:text-stone-400 dark:hover:text-stone-200"
              >
                &larr; 前往購物車
              </Link>
              <Link
                href="/"
                className="inline-flex min-h-[44px] items-center justify-center rounded-xl bg-stone-900 px-6 py-2.5 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
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

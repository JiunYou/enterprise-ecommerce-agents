import Link from "next/link";
import { auth0 } from "@/lib/auth0";
import { getFulfillmentQueue, FulfillmentOrder } from "@/lib/fulfillment";
import { ShipOrderButton } from "@/components/ShipOrderButton";

export const dynamic = "force-dynamic";

export default async function AdminFulfillmentPage() {
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
              請使用具備管理員權限的帳號登入，以存取訂單履約與出貨作業系統。
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

  // 2. 已登入，向後端取得履約佇列
  const fulfillmentResult = await getFulfillmentQueue();

  // 3. 處理未授權（401）狀態
  if (fulfillmentResult.status === "unauthenticated") {
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

  // 4. 處理權限不足（403 Forbidden）狀態：嚴格不洩漏任何 PII
  if (fulfillmentResult.status === "forbidden") {
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
            當前登入之帳號未具備系統管理員 (Admin) 權限，無法存取或查看訂單履約與收件人資訊。
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

  // 5. 處理一般後端連線/伺服器錯誤
  if (fulfillmentResult.status === "error") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-8 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            系統連線異常
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            {fulfillmentResult.message}
          </p>
          <div className="mt-6">
            <Link
              href="/"
              className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
            >
              重新整理
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // 6. 成功載入履約佇列
  const orders = fulfillmentResult.orders;

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
                訂單履約中心 (Admin Fulfillment)
              </h1>
              <p className="text-xs text-zinc-500 dark:text-zinc-400">
                管理已付款待出貨訂單
              </p>
            </div>
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
        <div className="mb-6 flex items-center justify-between">
          <div>
            <h2 className="text-base font-semibold text-zinc-900 dark:text-zinc-100">
              待出貨訂單佇列
            </h2>
            <p className="text-xs text-zinc-500 dark:text-zinc-400">
              僅顯示狀態為已付款 (Paid) 之有效訂單，依提交時間由早至晚排序。
            </p>
          </div>
          <span className="inline-flex items-center rounded-md bg-zinc-100 px-2.5 py-1 text-xs font-medium text-zinc-800 dark:bg-zinc-800 dark:text-zinc-200">
            待處理筆數：{orders.length}
          </span>
        </div>

        {/* 空佇列狀態 */}
        {orders.length === 0 ? (
          <div className="flex min-h-[300px] flex-col items-center justify-center rounded-xl border border-dashed border-zinc-300 bg-white p-8 text-center dark:border-zinc-800 dark:bg-zinc-900">
            <div className="flex h-12 w-12 items-center justify-center rounded-full bg-emerald-50 text-emerald-600 dark:bg-emerald-950 dark:text-emerald-400">
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
                  d="M5 13l4 4L19 7"
                />
              </svg>
            </div>
            <h3 className="mt-3 text-sm font-semibold text-zinc-900 dark:text-zinc-100">
              目前無待履約的訂單
            </h3>
            <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
              所有已付款訂單均已完成出貨或目前暫無新的已付款訂單。
            </p>
          </div>
        ) : (
          <div className="space-y-6">
            {orders.map((order: FulfillmentOrder) => {
              const hasAddress = order.shippingAddress !== null;

              return (
                <div
                  key={order.id}
                  className="rounded-xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
                >
                  {/* 訂單頂部資訊 */}
                  <div className="flex flex-wrap items-center justify-between gap-4 border-b border-zinc-100 pb-4 dark:border-zinc-800">
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="text-xs font-mono font-bold text-zinc-900 dark:text-zinc-50">
                          {order.id}
                        </span>
                        <span className="inline-flex items-center rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300">
                          {order.status}
                        </span>
                      </div>
                      <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
                        顧客識別號：
                        <span className="font-mono">{order.customerId}</span>
                      </p>
                    </div>
                    <div className="flex items-center gap-6">
                      <div className="text-right">
                        <span className="text-xs text-zinc-500 dark:text-zinc-400">
                          訂單金額
                        </span>
                        <p className="text-sm font-bold text-zinc-900 dark:text-zinc-50">
                          {order.currency} {order.totalAmount.toLocaleString()}
                        </p>
                      </div>
                      <ShipOrderButton
                        orderId={order.id}
                        hasShippingAddress={hasAddress}
                      />
                    </div>
                  </div>

                  {/* 訂單內容物與地址欄位網格 */}
                  <div className="mt-4 grid grid-cols-1 gap-6 md:grid-cols-2">
                    {/* 左側：購買項目清單 */}
                    <div>
                      <h4 className="text-xs font-semibold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
                        訂購項目 ({order.items.length})
                      </h4>
                      <div className="mt-2 divide-y divide-zinc-100 rounded-lg border border-zinc-100 dark:divide-zinc-800 dark:border-zinc-800">
                        {order.items.map((item, index) => (
                          <div
                            key={index}
                            className="flex items-center justify-between p-3 text-xs"
                          >
                            <div>
                              <span className="font-mono font-medium text-zinc-800 dark:text-zinc-200">
                                {item.productId}
                              </span>
                              <p className="text-zinc-400">
                                數量：{item.quantity} × {item.currency}{" "}
                                {item.unitPrice.toLocaleString()}
                              </p>
                            </div>
                            <span className="font-semibold text-zinc-900 dark:text-zinc-100">
                              {item.currency} {item.totalPrice.toLocaleString()}
                            </span>
                          </div>
                        ))}
                      </div>
                    </div>

                    {/* 右側：收件資訊 */}
                    <div>
                      <h4 className="text-xs font-semibold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
                        配送收件資訊 (Shipping Address)
                      </h4>
                      {hasAddress ? (
                        <div className="mt-2 rounded-lg border border-zinc-100 bg-zinc-50/50 p-4 text-xs text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900/50 dark:text-zinc-300">
                          <p className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">
                            {order.shippingAddress!.recipientName}
                          </p>
                          <p className="mt-1 text-zinc-600 dark:text-zinc-400">
                            聯絡電話：{order.shippingAddress!.phone}
                          </p>
                          <p className="mt-2 text-zinc-600 dark:text-zinc-400">
                            [{order.shippingAddress!.postalCode}]{" "}
                            {order.shippingAddress!.city} (
                            {order.shippingAddress!.countryCode})
                          </p>
                          <p className="text-zinc-800 dark:text-zinc-200">
                            {order.shippingAddress!.addressLine1}
                          </p>
                          {order.shippingAddress!.addressLine2 && (
                            <p className="text-zinc-800 dark:text-zinc-200">
                              {order.shippingAddress!.addressLine2}
                            </p>
                          )}
                        </div>
                      ) : (
                        <div className="mt-2 rounded-lg border border-amber-200 bg-amber-50/60 p-4 text-xs dark:border-amber-900/50 dark:bg-amber-950/30">
                          <div className="flex items-start gap-2">
                            <span className="text-amber-600 dark:text-amber-400">
                              ⚠️
                            </span>
                            <div>
                              <p className="font-semibold text-amber-800 dark:text-amber-300">
                                歷史訂單收件資訊不存在
                              </p>
                              <p className="mt-1 text-amber-700 dark:text-amber-400">
                                此訂單於配送地址快照機制啟用前建立，無可對應的配送地址。為確保發貨安全，系統已自動禁用直接發貨操作，請後續透過客服或人工流程進行核實。
                              </p>
                            </div>
                          </div>
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </main>
    </div>
  );
}

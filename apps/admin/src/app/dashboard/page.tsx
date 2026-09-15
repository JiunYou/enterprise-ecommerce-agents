import Link from "next/link";
import { auth0 } from "@/lib/auth0";
import { getAdminOrderOperationsOverview, AdminOrderOverviewRecentOrder } from "@/lib/orders";
import { AdminHeader } from "@/components/AdminHeader";

export const dynamic = "force-dynamic";

export default async function AdminDashboardPage() {
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
              請使用具備管理員權限的帳號登入，以存取系統營運總覽。
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

  // 2. 向後端取得營運總覽資料
  const overviewResult = await getAdminOrderOperationsOverview();

  // 3. 處理未授權（401）狀態
  if (overviewResult.status === "unauthenticated") {
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
  if (overviewResult.status === "forbidden") {
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
            當前登入之帳號未具備系統管理員 (Admin) 權限，無法存取營運總覽數據。
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
  if (overviewResult.status === "error") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-8 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            系統連線異常
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            {overviewResult.message}
          </p>
          <div className="mt-6">
            <Link
              href="/dashboard"
              className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
            >
              重新整理
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // 6. 成功載入營運總覽數據
  const overview = overviewResult.data;

  // 狀態顏色對應
  const getStatusBadgeClass = (status: string) => {
    switch (status) {
      case "Submitted":
        return "border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-800/60 dark:bg-amber-950/70 dark:text-amber-300";
      case "Paid":
        return "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-800/60 dark:bg-emerald-950/70 dark:text-emerald-300";
      case "Shipped":
        return "border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-800/60 dark:bg-blue-950/70 dark:text-blue-300";
      case "Cancelled":
        return "border-zinc-200 bg-zinc-100 text-zinc-600 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-300";
      default:
        return "border-zinc-200 bg-zinc-50 text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300";
    }
  };

  const formatDateTime = (dateString: string) => {
    try {
      const date = new Date(dateString);
      return date.toLocaleString("zh-TW", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
        hour12: false,
      });
    } catch {
      return dateString;
    }
  };

  return (
    <div className="min-h-screen bg-zinc-50 dark:bg-zinc-950">
      {/* 導航標頭 */}
      <AdminHeader
        activeSection="dashboard"
        title="營運總覽 (Operations Overview)"
        subtitle="掌握訂單營運狀態與近期正式訂單"
        userLabel={session.user.name || session.user.email || "管理員"}
      />

      {/* 主要內容區 */}
      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        {/* 1. 五項營運數值概覽卡片 */}
        <section aria-label="訂單營運狀態摘要">
          <h2 className="text-base font-semibold text-zinc-900 dark:text-zinc-100 mb-4">
            訂單營運狀態
          </h2>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
            {/* 正式訂單 */}
            <div className="rounded-xl border border-zinc-200 bg-white p-4 sm:p-5 shadow-xs dark:border-zinc-800 dark:bg-zinc-900">
              <span className="text-xs font-medium text-zinc-500 dark:text-zinc-400">
                正式訂單
              </span>
              <p className="mt-2 text-2xl sm:text-3xl font-bold tracking-tight text-zinc-900 dark:text-zinc-50">
                {overview.totalCount}
              </p>
              <p className="mt-1 text-[11px] text-zinc-400 dark:text-zinc-500">
                所有已提交之訂單總數
              </p>
            </div>

            {/* 待付款 */}
            <div className="rounded-xl border border-amber-200/80 bg-amber-50/30 p-4 sm:p-5 shadow-xs dark:border-amber-900/40 dark:bg-amber-950/10">
              <span className="text-xs font-medium text-amber-700 dark:text-amber-400">
                待付款
              </span>
              <p className="mt-2 text-2xl sm:text-3xl font-bold tracking-tight text-amber-900 dark:text-amber-300">
                {overview.submittedCount}
              </p>
              <p className="mt-1 text-[11px] text-amber-600/80 dark:text-amber-400/70">
                已提交待顧客付款
              </p>
            </div>

            {/* 待出貨 */}
            <div className="rounded-xl border border-emerald-200/80 bg-emerald-50/30 p-4 sm:p-5 shadow-xs dark:border-emerald-900/40 dark:bg-emerald-950/10">
              <span className="text-xs font-medium text-emerald-700 dark:text-emerald-400">
                待出貨
              </span>
              <p className="mt-2 text-2xl sm:text-3xl font-bold tracking-tight text-emerald-900 dark:text-emerald-300">
                {overview.paidCount}
              </p>
              <p className="mt-1 text-[11px] text-emerald-600/80 dark:text-emerald-400/70">
                已付款待出貨處理
              </p>
            </div>

            {/* 已出貨 */}
            <div className="rounded-xl border border-blue-200/80 bg-blue-50/30 p-4 sm:p-5 shadow-xs dark:border-blue-900/40 dark:bg-blue-950/10">
              <span className="text-xs font-medium text-blue-700 dark:text-blue-400">
                已出貨
              </span>
              <p className="mt-2 text-2xl sm:text-3xl font-bold tracking-tight text-blue-900 dark:text-blue-300">
                {overview.shippedCount}
              </p>
              <p className="mt-1 text-[11px] text-blue-600/80 dark:text-blue-400/70">
                已完成出貨配送
              </p>
            </div>

            {/* 已取消 */}
            <div className="rounded-xl border border-zinc-200 bg-zinc-50/60 p-4 sm:p-5 shadow-xs col-span-2 sm:col-span-1 dark:border-zinc-800 dark:bg-zinc-900/60">
              <span className="text-xs font-medium text-zinc-500 dark:text-zinc-400">
                已取消
              </span>
              <p className="mt-2 text-2xl sm:text-3xl font-bold tracking-tight text-zinc-700 dark:text-zinc-300">
                {overview.cancelledCount}
              </p>
              <p className="mt-1 text-[11px] text-zinc-400 dark:text-zinc-500">
                已終止或取消之訂單
              </p>
            </div>
          </div>
        </section>

        {/* 2. 近期正式訂單 (Recent Orders) */}
        <section aria-label="近期正式訂單" className="mt-10">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <h2 className="text-base font-semibold text-zinc-900 dark:text-zinc-100">
                近期正式訂單
              </h2>
              <p className="text-xs text-zinc-500 dark:text-zinc-400">
                顯示最新提交之 5 筆正式訂單（依提交時間降冪排序）
              </p>
            </div>
            <Link
              href="/orders"
              className="text-xs font-semibold text-indigo-600 hover:text-indigo-500 dark:text-indigo-400 dark:hover:text-indigo-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 rounded-sm"
            >
              前往訂單管理 →
            </Link>
          </div>

          {/* 空狀態處理 */}
          {overview.recentOrders.length === 0 ? (
            <div className="flex min-h-[260px] flex-col items-center justify-center rounded-xl border border-dashed border-zinc-300 bg-white p-8 text-center shadow-xs dark:border-zinc-800 dark:bg-zinc-900">
              <div className="flex h-12 w-12 items-center justify-center rounded-full bg-zinc-100 text-zinc-400 dark:bg-zinc-800 dark:text-zinc-500">
                <svg
                  className="h-6 w-6"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                  aria-hidden="true"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth="2"
                    d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"
                  />
                </svg>
              </div>
              <h3 className="mt-3 text-sm font-semibold text-zinc-900 dark:text-zinc-100">
                目前尚無正式訂單
              </h3>
              <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400 max-w-sm">
                系統中尚未有已提交的正式訂單記錄。當顧客提交訂單後，將即時在此呈現最新營運動態。
              </p>
            </div>
          ) : (
            <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-xs dark:border-zinc-800 dark:bg-zinc-900">
              {/* 桌面/平板表格檢視 (>= 640px) */}
              <div className="hidden sm:block overflow-x-auto">
                <table className="min-w-full divide-y divide-zinc-200 text-left text-xs dark:divide-zinc-800">
                  <thead className="bg-zinc-50/80 font-semibold text-zinc-600 dark:bg-zinc-800/60 dark:text-zinc-300">
                    <tr>
                      <th scope="col" className="px-4 py-3 sm:px-6">
                        訂單編號 (Order ID)
                      </th>
                      <th scope="col" className="px-4 py-3">
                        當前狀態
                      </th>
                      <th scope="col" className="px-4 py-3">
                        訂單金額
                      </th>
                      <th scope="col" className="px-4 py-3">
                        提交時間
                      </th>
                      <th scope="col" className="px-4 py-3 text-right sm:px-6">
                        操作
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800/80">
                    {overview.recentOrders.map((order: AdminOrderOverviewRecentOrder) => (
                      <tr
                        key={order.id}
                        className="transition-colors hover:bg-zinc-50/50 dark:hover:bg-zinc-800/40"
                      >
                        <td className="px-4 py-3.5 sm:px-6 font-mono font-medium text-zinc-900 dark:text-zinc-100">
                          <Link
                            href={`/orders/${order.id}`}
                            className="hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 rounded-sm"
                          >
                            {order.id}
                          </Link>
                        </td>
                        <td className="px-4 py-3.5 whitespace-nowrap">
                          <span
                            className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${getStatusBadgeClass(
                              order.status
                            )}`}
                          >
                            {order.status}
                          </span>
                        </td>
                        <td className="px-4 py-3.5 whitespace-nowrap font-bold text-zinc-900 dark:text-zinc-50">
                          {order.currency} {order.totalAmount.toLocaleString()}
                        </td>
                        <td className="px-4 py-3.5 whitespace-nowrap text-zinc-500 dark:text-zinc-400">
                          {formatDateTime(order.submittedAt)}
                        </td>
                        <td className="px-4 py-3.5 text-right whitespace-nowrap sm:px-6">
                          <Link
                            href={`/orders/${order.id}`}
                            className="inline-flex items-center justify-center rounded-md border border-zinc-200 bg-white px-2.5 py-1 text-xs font-medium text-zinc-700 shadow-2xs hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500"
                          >
                            檢視詳情
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* 行動端卡片式檢視 (< 640px，確保 375px 下舒適易讀且無溢出) */}
              <div className="divide-y divide-zinc-100 sm:hidden dark:divide-zinc-800/80">
                {overview.recentOrders.map((order: AdminOrderOverviewRecentOrder) => (
                  <div key={order.id} className="p-4 space-y-2.5">
                    <div className="flex items-start justify-between gap-2">
                      <Link
                        href={`/orders/${order.id}`}
                        className="font-mono text-xs font-bold text-zinc-900 dark:text-zinc-100 break-all hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 rounded-sm"
                      >
                        {order.id}
                      </Link>
                      <span
                        className={`inline-flex shrink-0 items-center rounded-full border px-2 py-0.5 text-[11px] font-medium ${getStatusBadgeClass(
                          order.status
                        )}`}
                      >
                        {order.status}
                      </span>
                    </div>

                    <div className="flex items-baseline justify-between text-xs pt-1">
                      <span className="text-zinc-500 dark:text-zinc-400">訂單金額</span>
                      <span className="font-bold text-zinc-900 dark:text-zinc-50">
                        {order.currency} {order.totalAmount.toLocaleString()}
                      </span>
                    </div>

                    <div className="flex items-baseline justify-between text-xs">
                      <span className="text-zinc-500 dark:text-zinc-400">提交時間</span>
                      <span className="text-zinc-600 dark:text-zinc-300">
                        {formatDateTime(order.submittedAt)}
                      </span>
                    </div>

                    <div className="pt-2 flex justify-end">
                      <Link
                        href={`/orders/${order.id}`}
                        className="inline-flex min-h-[44px] items-center justify-center rounded-md border border-zinc-200 bg-white px-3 py-1.5 text-xs font-semibold text-zinc-700 shadow-2xs hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500"
                      >
                        檢視訂單詳情
                      </Link>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </section>
      </main>
    </div>
  );
}

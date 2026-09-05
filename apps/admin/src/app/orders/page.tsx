import Link from "next/link";
import { auth0 } from "@/lib/auth0";
import { getAdminOrders, AdminOrderSummary } from "@/lib/orders";

export const dynamic = "force-dynamic";

interface OrdersPageProps {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}

export default async function AdminOrdersPage({ searchParams }: OrdersPageProps) {
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
              請使用具備管理員權限的帳號登入，以存取訂單管理與查詢系統。
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

  // 2. 解析 Next.js 16 Promise-based searchParams
  const resolvedParams = await searchParams;

  const rawPage = Array.isArray(resolvedParams.page)
    ? resolvedParams.page[0]
    : resolvedParams.page;
  const page = rawPage && parseInt(rawPage, 10) > 0 ? parseInt(rawPage, 10) : 1;

  const rawStatus = Array.isArray(resolvedParams.status)
    ? resolvedParams.status[0]
    : resolvedParams.status;
  const currentStatus = rawStatus && rawStatus.trim() !== "" && rawStatus.toLowerCase() !== "all"
    ? rawStatus.trim()
    : undefined;

  const rawOrderId = Array.isArray(resolvedParams.orderId)
    ? resolvedParams.orderId[0]
    : resolvedParams.orderId;
  const currentOrderId = rawOrderId && rawOrderId.trim() !== ""
    ? rawOrderId.trim()
    : undefined;

  const pageSize = 25;

  // 3. 呼叫伺服端訂單列表 API
  const result = await getAdminOrders({
    page,
    pageSize,
    status: currentStatus,
    orderId: currentOrderId,
  });

  // 4. 處理未授權（401）狀態
  if (result.status === "unauthenticated") {
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

  // 5. 處理權限不足（403 Forbidden）狀態：嚴格禁止暴露任何訂單資料與 PII
  if (result.status === "forbidden") {
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
            當前登入之帳號未具備系統管理員 (Admin) 權限，無法存取訂單管理系統。
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

  // 6. 處理 400 Bad Request（無效篩選參數）
  if (result.status === "badRequest") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-amber-200 bg-white p-8 text-center shadow-sm dark:border-amber-900/50 dark:bg-zinc-900">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-amber-100 text-amber-600 dark:bg-amber-950 dark:text-amber-400">
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
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
          </div>
          <h2 className="mt-4 text-lg font-bold text-zinc-900 dark:text-zinc-50">
            查詢參數無效
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            {result.message}
          </p>
          <div className="mt-6">
            <Link
              href="/orders"
              className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
            >
              重設篩選條件
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // 7. 處理伺服器連線/未知錯誤
  if (result.status === "error") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-8 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            系統連線異常
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            {result.message}
          </p>
          <div className="mt-6">
            <Link
              href="/orders"
              className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
            >
              重新整理
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const { items, totalCount } = result.data;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  // 建構分頁輔助連結
  const createPaginationUrl = (targetPage: number) => {
    const params = new URLSearchParams();
    params.set("page", targetPage.toString());
    if (currentStatus) {
      params.set("status", currentStatus);
    }
    if (currentOrderId) {
      params.set("orderId", currentOrderId);
    }
    return `/orders?${params.toString()}`;
  };

  const statusOptions = [
    { label: "全部", value: "All" },
    { label: "待處理 (Pending)", value: "Pending" },
    { label: "已提交 (Submitted)", value: "Submitted" },
    { label: "已付款 (Paid)", value: "Paid" },
    { label: "已出貨 (Shipped)", value: "Shipped" },
    { label: "已取消 (Cancelled)", value: "Cancelled" },
  ];

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
                訂單管理系統 (Admin Orders)
              </h1>
              <p className="text-xs text-zinc-500 dark:text-zinc-400">
                全域訂單唯讀檢視與客服查詢
              </p>
            </div>
            <nav className="ml-6 hidden items-center gap-2 sm:flex">
              <Link
                href="/"
                className="rounded-md px-3 py-1.5 text-xs font-medium text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"
              >
                訂單履約
              </Link>
              <Link
                href="/orders"
                className="rounded-md bg-zinc-100 px-3 py-1.5 text-xs font-semibold text-zinc-900 dark:bg-zinc-800 dark:text-zinc-100"
              >
                訂單管理
              </Link>
            </nav>
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

      {/* 主內容區 */}
      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        {/* 篩選工具列 (原生 GET Form，URL 狀態驅動) */}
        <div className="mb-6 rounded-xl border border-zinc-200 bg-white p-5 shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          <form method="GET" action="/orders" className="flex flex-wrap items-end gap-4">
            {/* 狀態篩選 */}
            <div className="w-full sm:w-auto">
              <label htmlFor="status" className="block text-xs font-semibold text-zinc-700 dark:text-zinc-300 mb-1">
                訂單狀態
              </label>
              <select
                id="status"
                name="status"
                defaultValue={currentStatus || "All"}
                className="block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-xs text-zinc-900 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
              >
                {statusOptions.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </div>

            {/* 訂單編號篩選 */}
            <div className="w-full sm:w-80">
              <label htmlFor="orderId" className="block text-xs font-semibold text-zinc-700 dark:text-zinc-300 mb-1">
                精確訂單編號 (Order ID)
              </label>
              <input
                id="orderId"
                name="orderId"
                type="text"
                defaultValue={currentOrderId || ""}
                placeholder="例如: 9801f1b3-4e0f-..."
                className="block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-xs font-mono text-zinc-900 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
              />
            </div>

            <div className="flex items-center gap-2">
              <button
                type="submit"
                className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold text-white shadow-sm transition-colors hover:bg-indigo-500 dark:bg-indigo-500 dark:hover:bg-indigo-400"
              >
                篩選查詢
              </button>
              {(currentStatus || currentOrderId) && (
                <Link
                  href="/orders"
                  className="rounded-lg border border-zinc-300 px-3 py-2 text-xs font-medium text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
                >
                  清除篩選
                </Link>
              )}
            </div>
          </form>
        </div>

        {/* 列表資訊列 */}
        <div className="mb-4 flex items-center justify-between">
          <div>
            <h2 className="text-base font-semibold text-zinc-900 dark:text-zinc-100">
              所有訂單清單
            </h2>
            <p className="text-xs text-zinc-500 dark:text-zinc-400">
              依提交時間由新至舊排序 (SubmittedAt DESC, Id ASC)。
            </p>
          </div>
          <span className="inline-flex items-center rounded-md bg-zinc-100 px-2.5 py-1 text-xs font-medium text-zinc-800 dark:bg-zinc-800 dark:text-zinc-200">
            總計：{totalCount} 筆
          </span>
        </div>

        {/* 訂單列表清單 */}
        {items.length === 0 ? (
          <div className="flex min-h-[300px] flex-col items-center justify-center rounded-xl border border-dashed border-zinc-300 bg-white p-8 text-center dark:border-zinc-800 dark:bg-zinc-900">
            <div className="flex h-12 w-12 items-center justify-center rounded-full bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">
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
                  d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"
                />
              </svg>
            </div>
            <h3 className="mt-3 text-sm font-semibold text-zinc-900 dark:text-zinc-100">
              查無符合條件之訂單
            </h3>
            <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
              目前條件下沒有任何訂單記錄，請調整篩選條件或重設篩選。
            </p>
          </div>
        ) : (
          <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-zinc-200 text-left text-xs dark:divide-zinc-800">
                <thead className="bg-zinc-50 text-zinc-600 dark:bg-zinc-800/50 dark:text-zinc-400">
                  <tr>
                    <th scope="col" className="px-6 py-3.5 font-semibold">
                      訂單編號 (Order ID)
                    </th>
                    <th scope="col" className="px-6 py-3.5 font-semibold">
                      顧客識別碼 (Customer ID)
                    </th>
                    <th scope="col" className="px-6 py-3.5 font-semibold">
                      狀態
                    </th>
                    <th scope="col" className="px-6 py-3.5 font-semibold">
                      總金額
                    </th>
                    <th scope="col" className="px-6 py-3.5 font-semibold">
                      提交時間
                    </th>
                    <th scope="col" className="px-6 py-3.5 text-right font-semibold">
                      操作
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800/60">
                  {items.map((order: AdminOrderSummary) => (
                    <tr
                      key={order.id}
                      className="transition-colors hover:bg-zinc-50/80 dark:hover:bg-zinc-800/40"
                    >
                      <td className="whitespace-nowrap px-6 py-4 font-mono font-medium text-zinc-900 dark:text-zinc-100">
                        {order.id}
                      </td>
                      <td className="whitespace-nowrap px-6 py-4 font-mono text-zinc-500 dark:text-zinc-400">
                        {order.customerId}
                      </td>
                      <td className="whitespace-nowrap px-6 py-4">
                        <span
                          className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                            order.status === "Paid"
                              ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300"
                              : order.status === "Shipped"
                              ? "bg-blue-50 text-blue-700 dark:bg-blue-950 dark:text-blue-300"
                              : order.status === "Cancelled"
                              ? "bg-rose-50 text-rose-700 dark:bg-rose-950 dark:text-rose-300"
                              : order.status === "Submitted"
                              ? "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-300"
                              : "bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300"
                          }`}
                        >
                          {order.status}
                        </span>
                      </td>
                      <td className="whitespace-nowrap px-6 py-4 font-semibold text-zinc-900 dark:text-zinc-100">
                        {order.currency} {order.totalAmount.toLocaleString()}
                      </td>
                      <td className="whitespace-nowrap px-6 py-4 text-zinc-500 dark:text-zinc-400">
                        {order.submittedAt
                          ? new Date(order.submittedAt).toLocaleString("zh-TW", {
                              timeZone: "Asia/Taipei",
                            })
                          : "尚未提交"}
                      </td>
                      <td className="whitespace-nowrap px-6 py-4 text-right">
                        <Link
                          href={`/orders/${order.id}`}
                          className="inline-flex items-center rounded-md bg-zinc-100 px-2.5 py-1.5 text-xs font-medium text-zinc-800 transition-colors hover:bg-zinc-200 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
                        >
                          查看明細
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* 分頁導航列 */}
            <div className="flex items-center justify-between border-t border-zinc-200 bg-white px-6 py-3.5 dark:border-zinc-800 dark:bg-zinc-900">
              <div className="text-xs text-zinc-500 dark:text-zinc-400">
                第 <span className="font-semibold text-zinc-800 dark:text-zinc-200">{page}</span> 頁 / 共{" "}
                <span className="font-semibold text-zinc-800 dark:text-zinc-200">{totalPages}</span> 頁 (總計 {totalCount} 筆)
              </div>
              <div className="flex items-center gap-2">
                {page > 1 ? (
                  <Link
                    href={createPaginationUrl(page - 1)}
                    className="rounded-md border border-zinc-300 bg-white px-3 py-1.5 text-xs font-semibold text-zinc-700 shadow-sm transition-colors hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
                  >
                    上一頁
                  </Link>
                ) : (
                  <span className="cursor-not-allowed rounded-md border border-zinc-200 bg-zinc-50 px-3 py-1.5 text-xs font-semibold text-zinc-400 dark:border-zinc-800 dark:bg-zinc-900/50 dark:text-zinc-600">
                    上一頁
                  </span>
                )}

                {page < totalPages ? (
                  <Link
                    href={createPaginationUrl(page + 1)}
                    className="rounded-md border border-zinc-300 bg-white px-3 py-1.5 text-xs font-semibold text-zinc-700 shadow-sm transition-colors hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
                  >
                    下一頁
                  </Link>
                ) : (
                  <span className="cursor-not-allowed rounded-md border border-zinc-200 bg-zinc-50 px-3 py-1.5 text-xs font-semibold text-zinc-400 dark:border-zinc-800 dark:bg-zinc-900/50 dark:text-zinc-600">
                    下一頁
                  </span>
                )}
              </div>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}

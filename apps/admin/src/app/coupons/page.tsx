import { auth0 } from "@/lib/auth0";
import { getAdminCoupons } from "@/lib/coupons";
import { AdminHeader } from "@/components/AdminHeader";
import { CouponManagement } from "@/components/CouponManagement";

export const dynamic = "force-dynamic";

export default async function AdminCouponsPage() {
  const session = await auth0.getSession();

  // 1. 未登入處理
  if (!session || !session.user) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-6 sm:p-8 shadow-xs dark:border-zinc-800 dark:bg-zinc-900">
          <div className="flex flex-col items-center text-center">
            <h1 className="text-xl font-bold tracking-tight text-zinc-900 dark:text-zinc-50">
              Enterprise Commerce 管理後台
            </h1>
            <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
              請使用具備管理員權限的帳號登入，以存取優惠券管理系統。
            </p>
            <a
              href="/auth/login"
              className="mt-6 inline-flex min-h-[44px] w-full touch-manipulation items-center justify-center rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:bg-indigo-500 dark:hover:bg-indigo-400"
            >
              管理員登入
            </a>
          </div>
        </div>
      </div>
    );
  }

  // 2. 呼叫優惠券列表 API
  const result = await getAdminCoupons();

  // 3. 處理未授權（401）
  if (result.status === "unauthenticated") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-8 text-center shadow-xs dark:border-zinc-800 dark:bg-zinc-900">
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

  // 4. 處理權限不足（403）
  if (result.status === "forbidden") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-lg rounded-xl border border-rose-200 bg-rose-50/50 p-8 text-center shadow-xs dark:border-rose-900/50 dark:bg-rose-950/20">
          <h2 className="text-lg font-bold text-rose-900 dark:text-rose-100">
            權限不足 (403 Forbidden)
          </h2>
          <p className="mt-2 text-sm text-rose-700 dark:text-rose-300">
            您的帳號缺少管理員（Admin）存取權限。此區域僅限授權管理人員存取。
          </p>
          <div className="mt-6 flex justify-center gap-4">
            <a
              href="/auth/logout"
              className="rounded-lg bg-rose-600 px-4 py-2 text-sm font-semibold text-white hover:bg-rose-500"
            >
              登出切換帳號
            </a>
          </div>
        </div>
      </div>
    );
  }

  // 5. 處理一般錯誤
  if (result.status === "error") {
    return (
      <div className="min-h-screen bg-zinc-50 dark:bg-zinc-950">
        <AdminHeader
          activeSection="coupons"
          title="優惠券管理"
          subtitle="行銷優惠券建立與營運設定"
          userLabel={session.user.name || session.user.email || "Administrator"}
        />
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
          <div className="rounded-xl border border-rose-200 bg-rose-50/50 p-6 text-rose-800 dark:border-rose-900/50 dark:bg-rose-950/30 dark:text-rose-300">
            <h2 className="text-base font-semibold">無法載入優惠券資訊</h2>
            <p className="mt-1 text-sm">{result.message}</p>
          </div>
        </main>
      </div>
    );
  }

  // 6. 成功載入
  return (
    <div className="min-h-screen bg-zinc-50 dark:bg-zinc-950">
      <AdminHeader
        activeSection="coupons"
        title="優惠券管理"
        subtitle="行銷優惠券建立、啟用清單與停用營運"
        userLabel={session.user.name || session.user.email || "Administrator"}
      />
      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        <CouponManagement initialCoupons={result.data} />
      </main>
    </div>
  );
}

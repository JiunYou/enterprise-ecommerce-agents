import Link from "next/link";
import { auth0 } from "@/lib/auth0";
import { CreateProductForm } from "@/components/CreateProductForm";
import { AdminHeader } from "@/components/AdminHeader";

export const dynamic = "force-dynamic";

export default async function NewProductPage() {
  const session = await auth0.getSession();

  // 1. 未登入處理
  if (!session || !session.user) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-6 text-center shadow-xs sm:p-8 dark:border-zinc-800 dark:bg-zinc-900">
          <div className="flex flex-col items-center text-center">
            <div className="flex h-12 w-12 items-center justify-center rounded-full bg-indigo-50 text-indigo-600 dark:bg-indigo-950 dark:text-indigo-400">
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
                  d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"
                />
              </svg>
            </div>
            <h1 className="mt-4 text-xl font-bold tracking-tight text-zinc-900 dark:text-zinc-50">
              請先登入管理後台
            </h1>
            <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
              建立商品需要系統管理員權限。
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

  return (
    <div className="min-h-screen bg-zinc-50 dark:bg-zinc-950">
      {/* 導航標頭 */}
      <AdminHeader
        activeSection="products"
        title="商品目錄管理 (Admin Catalog)"
        subtitle="安全建立商品與初始庫存"
        userLabel={session.user.name || session.user.email || "管理員"}
      />

      {/* 主要內容區 */}
      <main className="mx-auto max-w-3xl px-4 py-6 sm:px-6 sm:py-8 lg:px-8">
        <div className="mb-6">
          <div className="flex items-center">
            <Link
              href="/products"
              className="inline-flex min-h-[44px] touch-manipulation items-center gap-1.5 text-xs font-semibold text-zinc-600 transition-colors hover:text-zinc-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:text-zinc-400 dark:hover:text-zinc-100"
            >
              <svg
                className="h-4 w-4 shrink-0"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                aria-hidden="true"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth="2"
                  d="M10 19l-7-7m0 0l7-7m-7 7h18"
                />
              </svg>
              <span>返回商品列表</span>
            </Link>
          </div>

          <div className="mt-2 flex flex-col gap-1 sm:flex-row sm:items-baseline sm:justify-between">
            <h2 className="text-xl font-bold tracking-tight text-zinc-900 sm:text-2xl dark:text-zinc-50">
              建立新商品
            </h2>
            <span className="inline-flex w-fit items-center rounded-md bg-zinc-100 px-2.5 py-1 text-xs font-medium text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">
              目錄管理工作台
            </span>
          </div>
          <p className="mt-1.5 text-xs text-zinc-600 sm:text-sm dark:text-zinc-400">
            建立商品時將同時原子化初始化其庫存紀錄。
          </p>
        </div>

        <div className="rounded-xl border border-zinc-200/80 bg-white p-5 shadow-xs sm:p-7 lg:p-8 dark:border-zinc-800 dark:bg-zinc-900">
          <CreateProductForm />
        </div>
      </main>
    </div>
  );
}

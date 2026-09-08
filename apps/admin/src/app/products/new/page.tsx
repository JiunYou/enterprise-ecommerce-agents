import Link from "next/link";
import { auth0 } from "@/lib/auth0";
import { CreateProductForm } from "@/components/CreateProductForm";

export const dynamic = "force-dynamic";

export default async function NewProductPage() {
  const session = await auth0.getSession();

  // 1. 未登入處理
  if (!session || !session.user) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-8 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            請先登入管理後台
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            建立商品需要系統管理員權限。
          </p>
          <a
            href="/auth/login"
            className="mt-6 inline-flex w-full items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
          >
            管理員登入
          </a>
        </div>
      </div>
    );
  }

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
                商品目錄管理 (Admin Catalog)
              </h1>
              <p className="text-xs text-zinc-500 dark:text-zinc-400">
                安全建立商品與初始庫存
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
                className="rounded-md px-3 py-1.5 text-xs font-medium text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"
              >
                訂單管理
              </Link>
              <Link
                href="/products"
                className="rounded-md bg-zinc-100 px-3 py-1.5 text-xs font-semibold text-zinc-900 dark:bg-zinc-800 dark:text-zinc-100"
              >
                商品管理
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

      {/* 主要內容區 */}
      <main className="mx-auto max-w-3xl px-4 py-8 sm:px-6 lg:px-8">
        <div className="mb-6">
          <Link
            href="/products"
            className="inline-flex items-center text-sm font-medium text-indigo-600 hover:text-indigo-500 dark:text-indigo-400"
          >
            ← 返回商品列表
          </Link>
          <h2 className="mt-2 text-2xl font-bold tracking-tight text-zinc-900 dark:text-zinc-50">
            建立新商品
          </h2>
          <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">
            建立商品時將同時原子化初始化其庫存紀錄。
          </p>
        </div>

        <div className="rounded-xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-900 sm:p-8">
          <CreateProductForm />
        </div>
      </main>
    </div>
  );
}

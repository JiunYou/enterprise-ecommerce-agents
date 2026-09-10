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
      <AdminHeader
        activeSection="products"
        title="商品目錄管理 (Admin Catalog)"
        subtitle="安全建立商品與初始庫存"
        userLabel={session.user.name || session.user.email || "管理員"}
      />

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

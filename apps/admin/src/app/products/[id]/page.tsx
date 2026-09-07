import Link from "next/link";
import { auth0 } from "@/lib/auth0";
import { getAdminProductById } from "@/lib/products";
import { UpdateProductPriceForm } from "@/components/UpdateProductPriceForm";
import { DeactivateProductButton } from "@/components/DeactivateProductButton";

export const dynamic = "force-dynamic";

interface ProductDetailPageProps {
  params: Promise<{ id: string }>;
}

export default async function AdminProductDetailPage({
  params,
}: ProductDetailPageProps) {
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
            存取商品詳情需要系統管理員權限。
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

  const { id } = await params;

  // 2. 呼叫管理員商品詳情 API
  const result = await getAdminProductById(id);

  // 3. 處理 401 未授權
  if (result.status === "unauthenticated") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-8 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            登入階段已過期
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            請重新登入以驗證身分。
          </p>
          <div className="mt-6 flex justify-center gap-4">
            <a
              href="/auth/login"
              className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
            >
              重新登入
            </a>
          </div>
        </div>
      </div>
    );
  }

  // 4. 處理 403 權限不足
  if (result.status === "forbidden") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-lg rounded-xl border border-rose-200 bg-rose-50/50 p-8 text-center shadow-sm dark:border-rose-900/50 dark:bg-rose-950/20">
          <h2 className="text-xl font-bold text-zinc-900 dark:text-zinc-50">
            存取被拒 (403 Forbidden)
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            當前帳號無權限檢視管理員商品詳情。
          </p>
          <div className="mt-6">
            <a
              href="/auth/logout"
              className="rounded-lg border border-zinc-300 bg-white px-4 py-2 text-sm font-semibold text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200"
            >
              登出
            </a>
          </div>
        </div>
      </div>
    );
  }

  // 5. 處理 404 查無商品
  if (result.status === "notFound") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-8 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            查無此商品 (404 Not Found)
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            系統中找不到識別碼為 <span className="font-mono text-zinc-800 dark:text-zinc-200">{id}</span> 的商品。
          </p>
          <div className="mt-6">
            <Link
              href="/products"
              className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
            >
              返回商品列表
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // 6. 處理伺服器連線錯誤
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
          <div className="mt-6 flex justify-center gap-4">
            <Link
              href={`/products/${id}`}
              className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
            >
              重新整理
            </Link>
            <Link
              href="/products"
              className="rounded-lg border border-zinc-300 px-4 py-2 text-sm font-semibold text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-200"
            >
              返回列表
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const product = result.product;

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
                商品詳情管理 (Admin Product Detail)
              </h1>
              <p className="text-xs text-zinc-500 dark:text-zinc-400">
                檢視商品資訊、調整售價與生命週期下架
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
      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        <div className="mb-6">
          <Link
            href="/products"
            className="inline-flex items-center text-xs font-medium text-indigo-600 hover:text-indigo-500 dark:text-indigo-400"
          >
            &larr; 返回商品列表
          </Link>
        </div>

        <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
          {/* 左側：商品基本資料卡片 */}
          <div className="space-y-6 lg:col-span-2">
            <div className="rounded-xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
              <div className="flex items-start justify-between">
                <div>
                  <h2 className="text-xl font-bold text-zinc-900 dark:text-zinc-50">
                    {product.name}
                  </h2>
                  <p className="mt-1 font-mono text-xs text-zinc-500 dark:text-zinc-400">
                    ID: {product.id}
                  </p>
                </div>
                <div>
                  {product.isActive ? (
                    <span className="inline-flex items-center rounded-full bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300">
                      上架中 (Active)
                    </span>
                  ) : (
                    <span className="inline-flex items-center rounded-full bg-zinc-100 px-3 py-1 text-xs font-semibold text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400">
                      已下架 (Inactive)
                    </span>
                  )}
                </div>
              </div>

              <dl className="mt-6 grid grid-cols-1 gap-x-6 gap-y-4 sm:grid-cols-2">
                <div>
                  <dt className="text-xs font-medium text-zinc-500 dark:text-zinc-400">
                    庫存單位編號 (SKU)
                  </dt>
                  <dd className="mt-1 font-mono text-sm font-semibold text-zinc-900 dark:text-zinc-50">
                    {product.sku}
                  </dd>
                </div>

                <div>
                  <dt className="text-xs font-medium text-zinc-500 dark:text-zinc-400">
                    當前售價
                  </dt>
                  <dd className="mt-1 text-sm font-bold text-zinc-900 dark:text-zinc-50">
                    {product.currency} {product.price.toLocaleString("zh-TW", { minimumFractionDigits: 2 })}
                  </dd>
                </div>

                <div>
                  <dt className="text-xs font-medium text-zinc-500 dark:text-zinc-400">
                    計價幣別
                  </dt>
                  <dd className="mt-1 font-mono text-sm text-zinc-900 dark:text-zinc-50">
                    {product.currency}
                  </dd>
                </div>

                <div>
                  <dt className="text-xs font-medium text-zinc-500 dark:text-zinc-400">
                    公開目錄顯示狀態
                  </dt>
                  <dd className="mt-1 text-sm text-zinc-900 dark:text-zinc-50">
                    {product.isActive ? "公開可見，可加入購物車" : "已隱藏下架，禁止加入購物車"}
                  </dd>
                </div>
              </dl>
            </div>

            {/* 下架狀態備註說明卡片 */}
            {!product.isActive && (
              <div className="rounded-xl border border-zinc-200 bg-zinc-50/80 p-5 dark:border-zinc-800 dark:bg-zinc-900/60">
                <h4 className="text-xs font-semibold text-zinc-900 dark:text-zinc-100">
                  商品生命週期提示
                </h4>
                <p className="mt-1.5 text-xs text-zinc-600 dark:text-zinc-400">
                  此商品目前處於停用下架狀態。前台顧客無法在目錄中搜尋或瀏覽此商品，亦無法將其加入購物車。當前版本 (v1) 不提供重新上架 (Reactivate) 功能。
                </p>
              </div>
            )}
          </div>

          {/* 右側：管理操作區 */}
          <div className="space-y-6">
            {/* 調整價格表單 */}
            <UpdateProductPriceForm
              productId={product.id}
              currentPrice={product.price}
              currency={product.currency}
            />

            {/* 停用商品按鈕（僅在商品為 Active 狀態時顯示） */}
            {product.isActive && (
              <DeactivateProductButton
                productId={product.id}
                productName={product.name}
              />
            )}
          </div>
        </div>
      </main>
    </div>
  );
}

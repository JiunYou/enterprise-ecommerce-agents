import Link from "next/link";
import { auth0 } from "@/lib/auth0";
import { getAdminProductById, getAdminInventory } from "@/lib/products";
import { UpdateProductPriceForm } from "@/components/UpdateProductPriceForm";
import { DeactivateProductButton } from "@/components/DeactivateProductButton";
import { AdjustInventoryStockForm } from "@/components/AdjustInventoryStockForm";
import { AdminHeader } from "@/components/AdminHeader";

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
              存取商品詳情需要系統管理員權限。
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

  const { id } = await params;

  // 2. 呼叫管理員商品詳情 API
  const result = await getAdminProductById(id);

  // 3. 處理 401 未授權
  if (result.status === "unauthenticated") {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-6 text-center shadow-xs sm:p-8 dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            登入階段已過期
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            請重新登入以驗證身分。
          </p>
          <div className="mt-6 flex flex-wrap justify-center gap-3">
            <a
              href="/auth/login"
              className="inline-flex min-h-[44px] touch-manipulation items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:bg-indigo-500 dark:hover:bg-indigo-400"
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
        <div className="w-full max-w-lg rounded-xl border border-rose-200 bg-rose-50/50 p-6 text-center shadow-xs sm:p-8 dark:border-rose-900/50 dark:bg-rose-950/20">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-rose-100 text-rose-600 dark:bg-rose-900/50 dark:text-rose-400">
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
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
          </div>
          <h2 className="mt-4 text-xl font-bold tracking-tight text-zinc-900 dark:text-zinc-50">
            存取被拒 (403 Forbidden)
          </h2>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            當前帳號無權限檢視管理員商品詳情。
          </p>
          <div className="mt-6 flex justify-center">
            <a
              href="/auth/logout"
              className="inline-flex min-h-[44px] touch-manipulation items-center justify-center rounded-lg border border-zinc-300 bg-white px-4 py-2 text-sm font-semibold text-zinc-700 shadow-xs transition-colors hover:bg-zinc-50 active:bg-zinc-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
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
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-6 text-center shadow-xs sm:p-8 dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            查無此商品 (404 Not Found)
          </h2>
          <p className="mt-2 text-sm text-zinc-600 break-words dark:text-zinc-400">
            系統中找不到識別碼為 <span className="font-mono break-all text-zinc-800 dark:text-zinc-200">{id}</span> 的商品。
          </p>
          <div className="mt-6">
            <Link
              href="/products"
              className="inline-flex min-h-[44px] touch-manipulation items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:bg-indigo-500 dark:hover:bg-indigo-400"
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
        <div className="w-full max-w-md rounded-xl border border-zinc-200 bg-white p-6 text-center shadow-xs sm:p-8 dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-lg font-bold text-zinc-900 dark:text-zinc-50">
            系統連線異常
          </h2>
          <p className="mt-2 text-sm text-zinc-600 break-words dark:text-zinc-400">
            {result.message}
          </p>
          <div className="mt-6 flex flex-wrap justify-center gap-3">
            <Link
              href={`/products/${id}`}
              className="inline-flex min-h-[44px] touch-manipulation items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:bg-indigo-500 dark:hover:bg-indigo-400"
            >
              重新整理
            </Link>
            <Link
              href="/products"
              className="inline-flex min-h-[44px] touch-manipulation items-center justify-center rounded-lg border border-zinc-300 bg-white px-4 py-2 text-sm font-semibold text-zinc-700 shadow-xs transition-colors hover:bg-zinc-50 active:bg-zinc-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
            >
              返回列表
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const product = result.product;
  const inventoryResult = await getAdminInventory(id);

  return (
    <div className="min-h-screen bg-zinc-50 dark:bg-zinc-950">
      {/* 導航標頭 */}
      <AdminHeader
        activeSection="products"
        title="商品詳情管理 (Admin Product Detail)"
        subtitle="檢視商品資訊、調整售價與生命週期下架"
        userLabel={session.user.name || session.user.email || "管理員"}
      />

      {/* 主要內容區 */}
      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 sm:py-8 lg:px-8">
        <div className="mb-5 sm:mb-6">
          <Link
            href="/products"
            className="inline-flex min-h-[36px] touch-manipulation items-center gap-1.5 text-xs font-semibold text-indigo-600 transition-colors hover:text-indigo-500 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:text-indigo-400"
          >
            <span aria-hidden="true">&larr;</span> 返回商品列表
          </Link>
        </div>

        <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
          {/* 左側：商品基本資料卡片與庫存資訊 */}
          <div className="space-y-6 lg:col-span-2">
            <section
              aria-labelledby="product-summary-heading"
              className="rounded-xl border border-zinc-200 bg-white p-5 shadow-xs sm:p-6 dark:border-zinc-800 dark:bg-zinc-900"
            >
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0 flex-1">
                  <h2
                    id="product-summary-heading"
                    className="text-xl font-bold tracking-tight text-zinc-900 break-words dark:text-zinc-50 sm:text-2xl"
                  >
                    {product.name}
                  </h2>
                  <p className="mt-1.5 font-mono text-xs text-zinc-500 break-all dark:text-zinc-400">
                    ID: {product.id}
                  </p>
                </div>
                <div className="shrink-0">
                  {product.isActive ? (
                    <span className="inline-flex items-center rounded-full border border-emerald-200/60 bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700 dark:border-emerald-800/60 dark:bg-emerald-950/60 dark:text-emerald-300">
                      <span className="mr-1.5 h-1.5 w-1.5 rounded-full bg-emerald-500" aria-hidden="true" />
                      上架中 (Active)
                    </span>
                  ) : (
                    <span className="inline-flex items-center rounded-full border border-zinc-200/80 bg-zinc-100 px-3 py-1 text-xs font-semibold text-zinc-600 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-400">
                      <span className="mr-1.5 h-1.5 w-1.5 rounded-full bg-zinc-400" aria-hidden="true" />
                      已下架 (Inactive)
                    </span>
                  )}
                </div>
              </div>

              <dl className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div className="rounded-lg border border-zinc-100 bg-zinc-50/60 p-3.5 dark:border-zinc-800/70 dark:bg-zinc-800/40">
                  <dt className="text-xs font-medium text-zinc-500 dark:text-zinc-400">
                    庫存單位編號 (SKU)
                  </dt>
                  <dd className="mt-1 font-mono text-sm font-semibold text-zinc-900 break-all dark:text-zinc-50">
                    {product.sku}
                  </dd>
                </div>

                <div className="rounded-lg border border-zinc-100 bg-zinc-50/60 p-3.5 dark:border-zinc-800/70 dark:bg-zinc-800/40">
                  <dt className="text-xs font-medium text-zinc-500 dark:text-zinc-400">
                    當前售價
                  </dt>
                  <dd className="mt-1 text-base font-bold text-zinc-900 dark:text-zinc-50">
                    {product.currency} {product.price.toLocaleString("zh-TW", { minimumFractionDigits: 2 })}
                  </dd>
                </div>

                <div className="rounded-lg border border-zinc-100 bg-zinc-50/60 p-3.5 dark:border-zinc-800/70 dark:bg-zinc-800/40">
                  <dt className="text-xs font-medium text-zinc-500 dark:text-zinc-400">
                    計價幣別
                  </dt>
                  <dd className="mt-1 font-mono text-sm font-medium text-zinc-900 dark:text-zinc-50">
                    {product.currency}
                  </dd>
                </div>

                <div className="rounded-lg border border-zinc-100 bg-zinc-50/60 p-3.5 dark:border-zinc-800/70 dark:bg-zinc-800/40">
                  <dt className="text-xs font-medium text-zinc-500 dark:text-zinc-400">
                    公開目錄顯示狀態
                  </dt>
                  <dd className="mt-1 text-sm font-medium text-zinc-900 dark:text-zinc-50">
                    {product.isActive ? "公開可見，可加入購物車" : "已隱藏下架，禁止加入購物車"}
                  </dd>
                </div>
              </dl>
            </section>

            {/* 下架狀態備註說明卡片 */}
            {!product.isActive && (
              <div className="rounded-xl border border-zinc-200 bg-zinc-50/80 p-5 shadow-xs dark:border-zinc-800 dark:bg-zinc-900/60">
                <h4 className="text-xs font-semibold text-zinc-900 dark:text-zinc-100">
                  商品生命週期提示
                </h4>
                <p className="mt-1.5 text-xs text-zinc-600 dark:text-zinc-400">
                  此商品目前處於停用下架狀態。前台顧客無法在目錄中搜尋或瀏覽此商品，亦無法將其加入購物車。當前版本 (v1) 不提供重新上架 (Reactivate) 功能。
                </p>
              </div>
            )}

            {/* 庫存狀態區塊 (Inventory Section) */}
            {inventoryResult.status === "success" && (
              <section
                aria-labelledby="inventory-status-heading"
                className="rounded-xl border border-zinc-200 bg-white p-5 shadow-xs sm:p-6 dark:border-zinc-800 dark:bg-zinc-900"
              >
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <h3
                      id="inventory-status-heading"
                      className="text-base font-bold text-zinc-900 dark:text-zinc-50"
                    >
                      商品庫存狀態 (Inventory)
                    </h3>
                    <p className="mt-0.5 text-xs text-zinc-500 dark:text-zinc-400">
                      即時可用庫存與訂單保留庫存狀況
                    </p>
                  </div>
                  <span className="inline-flex w-fit items-center rounded-full border border-blue-200/60 bg-blue-50 px-2.5 py-0.5 text-xs font-semibold text-blue-700 dark:border-blue-800/60 dark:bg-blue-950/60 dark:text-blue-300">
                    已建立庫存
                  </span>
                </div>

                <dl className="mt-5 grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <div className="rounded-lg border border-zinc-200 bg-zinc-50/70 p-4 shadow-xs dark:border-zinc-800 dark:bg-zinc-800/40">
                    <dt className="text-xs font-medium text-zinc-500 dark:text-zinc-400">
                      可用庫存 (Available Quantity)
                    </dt>
                    <dd className="mt-1.5 text-2xl font-black text-zinc-900 sm:text-3xl dark:text-zinc-50">
                      {inventoryResult.inventory.availableQuantity}
                    </dd>
                    <p className="mt-1 text-[11px] text-zinc-500 dark:text-zinc-400">
                      目前可直接供顧客購買與扣減的現貨數量
                    </p>
                  </div>

                  <div className="rounded-lg border border-amber-200/60 bg-amber-50/40 p-4 shadow-xs dark:border-amber-900/40 dark:bg-amber-950/20">
                    <dt className="text-xs font-medium text-amber-800 dark:text-amber-300">
                      預留庫存 (Reserved Quantity)
                    </dt>
                    <dd className="mt-1.5 text-2xl font-black text-amber-600 sm:text-3xl dark:text-amber-400">
                      {inventoryResult.inventory.reservedQuantity}
                    </dd>
                    <p className="mt-1 text-[11px] text-amber-700/80 dark:text-amber-300/80">
                      訂單成立保留中，受預留流程獨立保護
                    </p>
                  </div>
                </dl>
              </section>
            )}

            {inventoryResult.status === "notFound" && (
              <div className="rounded-xl border border-dashed border-zinc-300 bg-zinc-50/60 p-6 text-center shadow-xs dark:border-zinc-700 dark:bg-zinc-900/40">
                <div className="mx-auto flex h-10 w-10 items-center justify-center rounded-full bg-zinc-100 dark:bg-zinc-800">
                  <span className="text-sm font-semibold text-zinc-500 dark:text-zinc-400">i</span>
                </div>
                <h3 className="mt-3 text-sm font-semibold text-zinc-900 dark:text-zinc-100">
                  此商品尚未建立庫存紀錄。
                </h3>
                <p className="mt-1.5 text-xs text-zinc-500 dark:text-zinc-400">
                  目前系統尚未為此商品初始化或建立庫存紀錄。在當前版本中，不支援手動建立或自動建立庫存。
                </p>
              </div>
            )}

            {inventoryResult.status !== "success" && inventoryResult.status !== "notFound" && (
              <div className="rounded-xl border border-zinc-200 bg-zinc-50/60 p-5 text-center shadow-xs dark:border-zinc-800 dark:bg-zinc-900/40">
                <p className="text-xs text-zinc-500 dark:text-zinc-400">
                  暫時無法取得庫存狀態資訊。
                </p>
              </div>
            )}
          </div>

          {/* 右側：管理操作區 */}
          <div className="space-y-6 lg:col-span-1">
            {/* 庫存調整作業表單（僅在庫存存在時顯示） */}
            {inventoryResult.status === "success" && (
              <AdjustInventoryStockForm
                productId={product.id}
                availableQuantity={inventoryResult.inventory.availableQuantity}
              />
            )}

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

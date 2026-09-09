import Link from "next/link";
import { revalidatePath } from "next/cache";
import { getCart, updateCartItemQuantity, removeCartItem } from "@/lib/cart";
import { CustomerHeader } from "@/components/CustomerHeader";
import { CartItemList } from "@/components/CartItemList";

export default async function CartPage() {
  const result = await getCart();

  async function handleUpdateQuantity(productId: string, quantity: number) {
    "use server";
    const res = await updateCartItemQuantity(productId, quantity);
    revalidatePath("/cart");
    return res;
  }

  async function handleRemoveItem(productId: string) {
    "use server";
    const res = await removeCartItem(productId);
    revalidatePath("/cart");
    return res;
  }

  return (
    <div className="min-h-screen bg-stone-50 text-stone-900 dark:bg-stone-950 dark:text-stone-100">
      <CustomerHeader />

      {/* 主要內容區 */}
      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
        {/* 頂部導覽與頁面標題 */}
        <div className="mb-8">
          <Link
            href="/"
            className="inline-flex items-center gap-1.5 text-sm font-medium text-stone-600 transition-colors hover:text-stone-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-sm dark:text-stone-400 dark:hover:text-stone-200"
          >
            <span aria-hidden="true">&larr;</span> 繼續選購商品
          </Link>
          <div className="mt-4 flex flex-col gap-1 sm:flex-row sm:items-baseline sm:justify-between">
            <h1 className="text-2xl font-bold tracking-tight text-stone-950 dark:text-stone-50 sm:text-3xl">
              我的購物車
            </h1>
            {result.success && result.data.items.length > 0 && (
              <p className="text-sm text-stone-500 dark:text-stone-400">
                共 {result.data.items.length} 項商品
              </p>
            )}
          </div>
        </div>

        {/* 狀態渲染：未登入 / 系統錯誤 / 空購物車 / 購物車清單 */}
        {!result.success ? (
          result.unauthorized ? (
            <section
              aria-label="需要登入"
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
              <h2 className="mt-4 text-lg font-semibold text-stone-950 dark:text-stone-50">
                尚未登入會員
              </h2>
              <p className="mx-auto mt-2 max-w-sm text-sm text-stone-500 dark:text-stone-400">
                請先登入以檢視並保留您的專屬購物車品項。
              </p>
              <div className="mt-6">
                <a
                  href="/auth/login?returnTo=/cart"
                  className="inline-flex min-h-[44px] items-center justify-center rounded-lg bg-stone-900 px-6 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
                >
                  登入會員
                </a>
              </div>
            </section>
          ) : (
            <section
              aria-label="系統錯誤訊息"
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
                  <h2 className="text-base font-semibold text-red-950 dark:text-red-100">
                    無法載入購物車資訊
                  </h2>
                  <p className="mt-1 break-words text-sm text-red-700 dark:text-red-300">
                    {result.error}
                  </p>
                  <div className="mt-5 flex flex-wrap gap-3">
                    <Link
                      href="/cart"
                      className="inline-flex min-h-[44px] items-center justify-center rounded-lg bg-red-100 px-4 py-2 text-sm font-medium text-red-950 transition-colors hover:bg-red-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:ring-offset-2 dark:bg-red-900/70 dark:text-red-100 dark:hover:bg-red-900"
                    >
                      重新整理
                    </Link>
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
        ) : result.data.items.length === 0 ? (
          <section
            aria-label="購物車為空"
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
                  d="M2.25 3h1.386c.51 0 .955.343 1.087.835l.383 1.437M7.5 14.25a3 3 0 0 0-3 3h15.75m-12.75-3h11.218c1.121-2.3 2.1-4.684 2.924-7.138a60.114 60.114 0 0 0-16.536-1.84M7.5 14.25 5.106 5.272M6 20.25a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0Zm12.75 0a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0Z"
                />
              </svg>
            </div>
            <h2 className="mt-4 text-lg font-semibold text-stone-950 dark:text-stone-50">
              您的購物車目前是空的
            </h2>
            <p className="mx-auto mt-2 max-w-sm text-sm text-stone-500 dark:text-stone-400">
              瀏覽我們的商品型錄，挑選心儀的商品並加入購物車！
            </p>
            <div className="mt-6">
              <Link
                href="/"
                className="inline-flex min-h-[44px] items-center justify-center rounded-lg bg-stone-900 px-6 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
              >
                前往商品型錄
              </Link>
            </div>
          </section>
        ) : (
          <CartItemList
            items={result.data.items}
            currency={result.data.currency}
            totalAmount={result.data.totalAmount}
            onUpdateQuantity={handleUpdateQuantity}
            onRemoveItem={handleRemoveItem}
          />
        )}
      </main>
    </div>
  );
}

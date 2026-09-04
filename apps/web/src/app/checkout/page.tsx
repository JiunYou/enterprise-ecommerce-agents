import Link from "next/link";
import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { getCart } from "@/lib/cart";
import { submitOrder, type SubmitOrderResult, type ShippingAddress } from "@/lib/orders";
import { AuthControls } from "@/components/AuthControls";
import { CheckoutReview } from "@/components/CheckoutReview";

export default async function CheckoutPage() {
  const result = await getCart();

  async function handleSubmitOrder(
    orderId: string,
    shippingAddress: ShippingAddress
  ): Promise<SubmitOrderResult> {
    "use server";
    const res = await submitOrder(orderId, shippingAddress);
    if (res.success) {
      revalidatePath("/cart");
      revalidatePath("/checkout");
      redirect(`/orders/${encodeURIComponent(orderId)}`);
    }
    return res;
  }

  return (
    <div className="min-h-screen bg-zinc-50 text-zinc-900 dark:bg-zinc-950 dark:text-zinc-100">
      {/* 頂部導航列 */}
      <header className="border-b border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:px-8">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <Link href="/" className="inline-block">
                <h1 className="text-2xl font-bold tracking-tight text-zinc-900 dark:text-white">
                  Enterprise Commerce
                </h1>
              </Link>
              <p className="text-sm text-zinc-500 dark:text-zinc-400">
                結帳與訂單確認 (付款前)
              </p>
            </div>
            <div className="flex items-center gap-4">
              <Link
                href="/cart"
                className="inline-flex items-center text-sm font-semibold text-zinc-600 transition hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-zinc-100"
              >
                購物車
              </Link>
              <AuthControls />
            </div>
          </div>
        </div>
      </header>

      {/* 主要內容區 */}
      <main className="mx-auto max-w-4xl px-4 py-8 sm:px-6 lg:px-8">
        <div className="mb-6 flex items-center justify-between">
          <Link
            href="/cart"
            className="inline-flex items-center text-sm font-medium text-zinc-600 transition hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-zinc-100"
          >
            &larr; 返回購物車
          </Link>
          <h2 className="text-xl font-bold tracking-tight text-zinc-900 dark:text-zinc-100">
            結帳確認
          </h2>
        </div>

        {/* 狀態渲染：未登入 / 系統錯誤 / 購物車為空 / 結帳審核 */}
        {!result.success ? (
          result.unauthorized ? (
            <section
              aria-label="需要登入"
              className="rounded-xl border border-zinc-200 bg-white p-12 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
            >
              <h3 className="text-lg font-medium text-zinc-900 dark:text-zinc-100">
                請先登入以進行結帳
              </h3>
              <p className="mt-2 text-sm text-zinc-500 dark:text-zinc-400">
                結帳流程需要驗證您的會員身分，以綁定並送出您的專屬訂單。
              </p>
              <div className="mt-6">
                <a
                  href="/auth/login?returnTo=/checkout"
                  className="inline-flex items-center rounded-lg bg-zinc-900 px-5 py-2.5 text-sm font-medium text-white shadow-sm transition hover:bg-zinc-800 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
                >
                  登入會員
                </a>
              </div>
            </section>
          ) : (
            <section
              aria-label="系統錯誤訊息"
              className="rounded-xl border border-red-200 bg-red-50 p-6 text-red-800 dark:border-red-900/50 dark:bg-red-950/40 dark:text-red-300"
            >
              <h3 className="text-base font-semibold">無法載入結帳資訊</h3>
              <p className="mt-1 text-sm">{result.error}</p>
              <div className="mt-4 flex gap-3">
                <Link
                  href="/cart"
                  className="inline-flex items-center rounded-md bg-red-100 px-3 py-1.5 text-sm font-medium text-red-900 transition hover:bg-red-200 dark:bg-red-900/60 dark:text-red-200 dark:hover:bg-red-900"
                >
                  返回購物車
                </Link>
              </div>
            </section>
          )
        ) : !result.data.id || result.data.items.length === 0 ? (
          <section
            aria-label="購物車為空無法結帳"
            className="rounded-xl border border-zinc-200 bg-white p-12 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
          >
            <h3 className="text-lg font-medium text-zinc-900 dark:text-zinc-100">
              購物車目前沒有商品
            </h3>
            <p className="mt-2 text-sm text-zinc-500 dark:text-zinc-400">
              您的購物車目前是空的，請先挑選商品加入購物車後再進行結帳。
            </p>
            <div className="mt-6 flex justify-center gap-4">
              <Link
                href="/cart"
                className="inline-flex items-center rounded-lg border border-zinc-300 bg-white px-5 py-2.5 text-sm font-medium text-zinc-700 shadow-sm transition hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
              >
                前往購物車
              </Link>
              <Link
                href="/"
                className="inline-flex items-center rounded-lg bg-zinc-900 px-5 py-2.5 text-sm font-medium text-white shadow-sm transition hover:bg-zinc-800 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
              >
                前往商品型錄
              </Link>
            </div>
          </section>
        ) : (
          <CheckoutReview
            orderId={result.data.id}
            items={result.data.items}
            currency={result.data.currency}
            totalAmount={result.data.totalAmount}
            onSubmitOrder={handleSubmitOrder}
          />
        )}
      </main>
    </div>
  );
}

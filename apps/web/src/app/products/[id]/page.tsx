import Link from "next/link";
import { revalidatePath } from "next/cache";
import { getProductById } from "@/lib/catalog";
import { formatPrice } from "@/lib/format";
import { auth0 } from "@/lib/auth0";
import { addItemToCart } from "@/lib/cart";
import { AddToCartForm } from "@/components/AddToCartForm";
import { CustomerHeader } from "@/components/CustomerHeader";

interface ProductDetailPageProps {
  params: Promise<{
    id: string;
  }>;
}

export default async function ProductDetailPage({
  params,
}: ProductDetailPageProps) {
  const { id } = await params;
  const result = await getProductById(id);
  const session = await auth0.getSession();
  const isLoggedIn = Boolean(session && session.user);

  async function handleAddToCart(productId: string, quantity: number) {
    "use server";
    const res = await addItemToCart(productId, quantity);
    revalidatePath("/cart");
    return res;
  }

  return (
    <div className="min-h-screen bg-stone-50 text-stone-900 dark:bg-stone-950 dark:text-stone-100">
      <CustomerHeader />

      {/* 主要內容區 */}
      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
        {/* 返回目錄導覽 */}
        <div className="mb-6 sm:mb-8">
          <Link
            href="/"
            className="group inline-flex items-center gap-1.5 text-sm font-medium text-stone-600 transition-colors hover:text-stone-950 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-sm dark:text-stone-400 dark:hover:text-stone-100"
          >
            <span aria-hidden="true" className="transition-transform group-hover:-translate-x-0.5">&larr;</span>
            <span>返回商品目錄</span>
          </Link>
        </div>

        {/* 狀態渲染：商品不存在 / 系統錯誤 / 成功取得商品 */}
        {!result.success ? (
          result.notFound ? (
            <section
              aria-label="商品不存在"
              className="rounded-2xl border border-stone-200/90 bg-white p-8 text-center shadow-xs dark:border-stone-800 dark:bg-stone-900 sm:p-12"
            >
              <div
                className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-stone-100 text-stone-400 dark:bg-stone-800 dark:text-stone-500"
                aria-hidden="true"
              >
                <span className="text-xl leading-none">⊘</span>
              </div>
              <h1 className="mt-4 text-lg font-semibold text-stone-900 dark:text-stone-100 sm:text-xl">
                商品不存在或目前未上架
              </h1>
              <p className="mx-auto mt-2 max-w-md text-sm text-stone-500 dark:text-stone-400">
                找不到所要求的商品，該商品可能已被移除或尚未公開上架。
              </p>
              <div className="mt-6">
                <Link
                  href="/"
                  className="inline-flex items-center justify-center rounded-lg bg-stone-900 px-5 py-2.5 text-sm font-medium text-white shadow-xs transition hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
                >
                  返回商品目錄
                </Link>
              </div>
            </section>
          ) : (
            <section
              aria-label="系統訊息"
              className="rounded-2xl border border-red-200 bg-red-50/70 p-6 text-red-900 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-200 sm:p-8"
            >
              <h1 className="text-lg font-semibold sm:text-xl">
                無法載入商品資訊
              </h1>
              <p className="mt-2 text-sm text-red-700 dark:text-red-300">
                {result.error}
              </p>
              <div className="mt-6 flex flex-wrap gap-3">
                <Link
                  href={`/products/${encodeURIComponent(id)}`}
                  className="inline-flex items-center justify-center rounded-lg border border-red-300 bg-white px-4 py-2 text-sm font-medium text-red-800 shadow-xs transition hover:bg-red-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:ring-offset-2 dark:border-red-800 dark:bg-stone-900 dark:text-red-200 dark:hover:bg-stone-800"
                >
                  重新整理
                </Link>
                <Link
                  href="/"
                  className="inline-flex items-center justify-center rounded-lg border border-stone-300 bg-white px-4 py-2 text-sm font-medium text-stone-700 shadow-xs transition hover:bg-stone-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:border-stone-700 dark:bg-stone-900 dark:text-stone-300 dark:hover:bg-stone-800"
                >
                  返回商品目錄
                </Link>
              </div>
            </section>
          )
        ) : (
          <article
            aria-label="商品詳細資訊"
            className="grid grid-cols-1 gap-8 md:grid-cols-2 md:gap-10 lg:gap-12"
          >
            {/* 展示性商品視覺區塊 (Product Visual Area) */}
            <div className="flex flex-col">
              <div
                aria-hidden="true"
                className="relative flex aspect-square w-full items-center justify-center rounded-2xl border border-stone-200/90 bg-radial from-stone-50 to-stone-100/90 shadow-2xs dark:border-stone-800 dark:from-stone-900 dark:to-stone-850"
              >
                <span className="text-6xl font-bold tracking-widest text-stone-300 select-none dark:text-stone-700 sm:text-7xl lg:text-8xl">
                  {result.data.name.trim().charAt(0).toUpperCase() || "P"}
                </span>
              </div>
            </div>

            {/* 商品資訊與購買操作面板 */}
            <div className="flex flex-col justify-between">
              <div className="space-y-6">
                {/* 標題與 SKU */}
                <div className="border-b border-stone-200/80 pb-6 dark:border-stone-800">
                  <h1 className="text-2xl font-bold tracking-tight text-stone-900 dark:text-stone-50 sm:text-3xl lg:text-4xl">
                    {result.data.name}
                  </h1>
                  <p className="mt-3 text-xs text-stone-500 dark:text-stone-400">
                    SKU: <span className="font-mono text-stone-700 dark:text-stone-300">{result.data.sku}</span>
                  </p>
                </div>

                {/* 價格資訊 */}
                <div>
                  <span className="sr-only">售價</span>
                  <div className="text-3xl font-extrabold tracking-tight text-stone-950 dark:text-stone-50 sm:text-4xl">
                    {formatPrice(result.data.price, result.data.currency)}
                  </div>
                </div>

                {/* 商品描述 (僅在非空時渲染) */}
                {result.data.description && result.data.description.trim().length > 0 && (
                  <div className="border-t border-stone-200/80 pt-6 dark:border-stone-800">
                    <h2 className="text-sm font-semibold text-stone-900 dark:text-stone-100">
                      商品描述
                    </h2>
                    <p className="mt-2 text-sm leading-relaxed text-stone-600 break-words whitespace-pre-line dark:text-stone-300">
                      {result.data.description.trim()}
                    </p>
                  </div>
                )}

                {/* 購買操作區塊 */}
                <div className="pt-2">
                  <AddToCartForm
                    productId={result.data.id}
                    isLoggedIn={isLoggedIn}
                    onAddToCart={handleAddToCart}
                  />
                </div>
              </div>
            </div>
          </article>
        )}
      </main>
    </div>
  );
}

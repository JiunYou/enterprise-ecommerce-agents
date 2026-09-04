import Link from "next/link";
import { revalidatePath } from "next/cache";
import { getProductById } from "@/lib/catalog";
import { formatPrice } from "@/lib/format";
import { auth0 } from "@/lib/auth0";
import { addItemToCart } from "@/lib/cart";
import { AddToCartForm } from "@/components/AddToCartForm";
import { AuthControls } from "@/components/AuthControls";

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
                商品型錄與線上商務展示
              </p>
            </div>
            <div className="flex items-center gap-4">
              <Link
                href="/cart"
                className="inline-flex items-center text-sm font-medium text-zinc-700 hover:text-zinc-900 dark:text-zinc-300 dark:hover:text-zinc-100"
              >
                購物車
              </Link>
              <AuthControls />
            </div>
          </div>
        </div>
      </header>

      {/* 主要內容區 */}
      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
        <div className="mb-6">
          <Link
            href="/"
            className="inline-flex items-center text-sm font-medium text-zinc-600 transition hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-zinc-100"
          >
            &larr; 返回商品型錄
          </Link>
        </div>

        {/* 狀態渲染：商品不存在 / 系統錯誤 / 成功取得商品 */}
        {!result.success ? (
          result.notFound ? (
            <section
              aria-label="商品不存在"
              className="rounded-xl border border-zinc-200 bg-white p-12 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
            >
              <h2 className="text-lg font-medium text-zinc-900 dark:text-zinc-100">
                商品不存在或目前未上架
              </h2>
              <p className="mt-2 text-sm text-zinc-500 dark:text-zinc-400">
                找不到所要求的商品，該商品可能已被移除或尚未公開上架。
              </p>
              <div className="mt-6">
                <Link
                  href="/"
                  className="inline-flex items-center rounded-lg bg-zinc-900 px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-zinc-800 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
                >
                  返回商品型錄
                </Link>
              </div>
            </section>
          ) : (
            <section
              aria-label="系統訊息"
              className="rounded-xl border border-red-200 bg-red-50 p-6 text-red-800 dark:border-red-900/50 dark:bg-red-950/40 dark:text-red-300"
            >
              <h2 className="text-base font-semibold">無法載入商品資訊</h2>
              <p className="mt-1 text-sm">{result.error}</p>
              <div className="mt-4 flex gap-3">
                <Link
                  href={`/products/${encodeURIComponent(id)}`}
                  className="inline-flex items-center rounded-md bg-red-100 px-3 py-1.5 text-sm font-medium text-red-900 transition hover:bg-red-200 dark:bg-red-900/60 dark:text-red-200 dark:hover:bg-red-900"
                >
                  重新整理
                </Link>
                <Link
                  href="/"
                  className="inline-flex items-center rounded-md border border-red-300 bg-white px-3 py-1.5 text-sm font-medium text-red-800 transition hover:bg-red-50 dark:border-red-800 dark:bg-zinc-900 dark:text-red-300 dark:hover:bg-zinc-800"
                >
                  返回商品型錄
                </Link>
              </div>
            </section>
          )
        ) : (
          <article
            aria-label="商品詳細資訊"
            className="rounded-xl border border-zinc-200 bg-white p-6 shadow-sm sm:p-8 dark:border-zinc-800 dark:bg-zinc-900"
          >
            <div className="border-b border-zinc-200 pb-6 dark:border-zinc-800">
              <h2 className="text-2xl font-bold tracking-tight text-zinc-900 sm:text-3xl dark:text-zinc-100">
                {result.data.name}
              </h2>
              <div className="mt-3 flex items-center gap-2">
                <span className="text-xs font-semibold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
                  SKU:
                </span>
                <span className="font-mono text-sm text-zinc-700 dark:text-zinc-300">
                  {result.data.sku}
                </span>
              </div>
            </div>

            <div className="pt-6">
              <div className="flex items-baseline gap-2">
                <span className="text-xs font-semibold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
                  售價
                </span>
                <span className="text-3xl font-extrabold text-zinc-900 dark:text-zinc-100">
                  {formatPrice(result.data.price, result.data.currency)}
                </span>
              </div>

              {/* 加入購物車區塊 */}
              <AddToCartForm
                productId={result.data.id}
                isLoggedIn={isLoggedIn}
                onAddToCart={handleAddToCart}
              />
            </div>
          </article>
        )}
      </main>
    </div>
  );
}

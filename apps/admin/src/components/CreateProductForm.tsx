"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { createProductAction } from "@/app/actions";

export function CreateProductForm() {
  const router = useRouter();
  const [name, setName] = useState("");
  const [sku, setSku] = useState("");
  const [price, setPrice] = useState("");
  const [currency, setCurrency] = useState("TWD");
  const [initialStock, setInitialStock] = useState("1");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    const trimmedName = name.trim();
    if (!trimmedName) {
      setErrorMessage("請輸入商品名稱。");
      return;
    }

    const trimmedSku = sku.trim();
    if (!trimmedSku) {
      setErrorMessage("請輸入商品 SKU。");
      return;
    }

    const parsedPrice = parseFloat(price);
    if (isNaN(parsedPrice) || parsedPrice <= 0) {
      setErrorMessage("商品價格必須為大於 0 的數值。");
      return;
    }

    const trimmedCurrency = currency.trim().toUpperCase();
    if (!trimmedCurrency || trimmedCurrency.length !== 3) {
      setErrorMessage("幣別代碼必須為 3 碼英文字母（例如 TWD, USD）。");
      return;
    }

    const parsedStock = parseInt(initialStock, 10);
    if (isNaN(parsedStock) || !Number.isInteger(parsedStock) || parsedStock <= 0) {
      setErrorMessage("初始庫存必須為大於 0 的正整數。");
      return;
    }

    startTransition(async () => {
      const result = await createProductAction({
        name: trimmedName,
        sku: trimmedSku,
        price: parsedPrice,
        currency: trimmedCurrency,
        initialStock: parsedStock,
      });

      if (!result.success) {
        setErrorMessage(result.error || "建立商品失敗，請稍後重試。");
        return;
      }

      if (result.productId) {
        router.push(`/products/${encodeURIComponent(result.productId)}`);
      } else {
        router.push("/products");
      }
    });
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {errorMessage && (
        <div
          role="alert"
          aria-live="polite"
          className="flex items-start gap-3 rounded-lg border border-rose-200 bg-rose-50/80 p-4 text-sm text-rose-800 dark:border-rose-900/50 dark:bg-rose-950/30 dark:text-rose-300"
        >
          <svg
            className="mt-0.5 h-5 w-5 shrink-0 text-rose-600 dark:text-rose-400"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            aria-hidden="true"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth="2"
              d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
            />
          </svg>
          <span className="flex-1 break-words font-medium">{errorMessage}</span>
        </div>
      )}

      {/* 區塊 1: 商品基本識別 */}
      <div className="space-y-4">
        <div className="border-b border-zinc-200/80 pb-2 dark:border-zinc-800">
          <h3 className="text-xs font-semibold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
            商品識別資訊
          </h3>
        </div>

        <div>
          <label
            htmlFor="product-name"
            className="block text-sm font-medium text-zinc-900 dark:text-zinc-100"
          >
            商品名稱
          </label>
          <div className="mt-1.5">
            <input
              id="product-name"
              name="name"
              type="text"
              required
              maxLength={255}
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={isPending}
              placeholder="請輸入商品名稱"
              className="block min-h-[44px] w-full rounded-lg border border-zinc-300 bg-white px-3.5 py-2.5 text-sm text-zinc-900 placeholder-zinc-400 shadow-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:border-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder-zinc-500"
            />
          </div>
        </div>

        <div>
          <label
            htmlFor="product-sku"
            className="block text-sm font-medium text-zinc-900 dark:text-zinc-100"
          >
            SKU
          </label>
          <div className="mt-1.5">
            <input
              id="product-sku"
              name="sku"
              type="text"
              required
              maxLength={100}
              value={sku}
              onChange={(e) => setSku(e.target.value)}
              disabled={isPending}
              placeholder="例如 PROD-001"
              className="block min-h-[44px] w-full rounded-lg border border-zinc-300 bg-white px-3.5 py-2.5 text-sm font-mono text-zinc-900 placeholder-zinc-400 shadow-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:border-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder-zinc-500"
            />
          </div>
        </div>
      </div>

      {/* 區塊 2: 商業定價 */}
      <div className="space-y-4 pt-2">
        <div className="border-b border-zinc-200/80 pb-2 dark:border-zinc-800">
          <h3 className="text-xs font-semibold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
            商業定價
          </h3>
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label
              htmlFor="product-price"
              className="block text-sm font-medium text-zinc-900 dark:text-zinc-100"
            >
              價格
            </label>
            <div className="mt-1.5">
              <input
                id="product-price"
                name="price"
                type="number"
                required
                step="any"
                min="0.01"
                value={price}
                onChange={(e) => setPrice(e.target.value)}
                disabled={isPending}
                placeholder="0.00"
                className="block min-h-[44px] w-full rounded-lg border border-zinc-300 bg-white px-3.5 py-2.5 text-sm text-zinc-900 placeholder-zinc-400 shadow-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:border-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder-zinc-500"
              />
            </div>
          </div>

          <div>
            <label
              htmlFor="product-currency"
              className="block text-sm font-medium text-zinc-900 dark:text-zinc-100"
            >
              幣別
            </label>
            <div className="mt-1.5">
              <input
                id="product-currency"
                name="currency"
                type="text"
                required
                maxLength={3}
                value={currency}
                onChange={(e) => setCurrency(e.target.value.toUpperCase())}
                disabled={isPending}
                placeholder="TWD"
                className="block min-h-[44px] w-full rounded-lg border border-zinc-300 bg-white px-3.5 py-2.5 text-sm font-mono uppercase text-zinc-900 placeholder-zinc-400 shadow-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:border-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder-zinc-500"
              />
            </div>
          </div>
        </div>
      </div>

      {/* 區塊 3: 初始庫存設定 */}
      <div className="space-y-4 pt-2">
        <div className="border-b border-zinc-200/80 pb-2 dark:border-zinc-800">
          <h3 className="text-xs font-semibold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
            初始庫存設定
          </h3>
        </div>

        <div>
          <label
            htmlFor="product-initial-stock"
            className="block text-sm font-medium text-zinc-900 dark:text-zinc-100"
          >
            初始庫存
          </label>
          <div className="mt-1.5">
            <input
              id="product-initial-stock"
              name="initialStock"
              type="number"
              required
              step="1"
              min="1"
              value={initialStock}
              onChange={(e) => setInitialStock(e.target.value)}
              disabled={isPending}
              placeholder="1"
              aria-describedby="initial-stock-helper"
              className="block min-h-[44px] w-full rounded-lg border border-zinc-300 bg-white px-3.5 py-2.5 text-sm text-zinc-900 placeholder-zinc-400 shadow-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:border-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder-zinc-500"
            />
          </div>
          <p
            id="initial-stock-helper"
            className="mt-1.5 text-xs text-zinc-500 dark:text-zinc-400"
          >
            初始庫存必須為大於 0 的整數，建立後將作為此商品的起始可用庫存。
          </p>
        </div>
      </div>

      {/* 區塊 4: 動作操作區 */}
      <div className="flex flex-col-reverse gap-3 pt-6 border-t border-zinc-200 sm:flex-row sm:items-center sm:justify-end dark:border-zinc-800">
        <button
          type="button"
          onClick={() => router.push("/products")}
          disabled={isPending}
          className="inline-flex min-h-[44px] touch-manipulation items-center justify-center rounded-lg border border-zinc-300 bg-white px-5 py-2.5 text-sm font-medium text-zinc-700 shadow-xs transition-colors hover:bg-zinc-50 active:bg-zinc-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
        >
          取消
        </button>
        <button
          type="submit"
          disabled={isPending}
          className="inline-flex min-h-[44px] touch-manipulation items-center justify-center gap-2 rounded-lg bg-indigo-600 px-6 py-2.5 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 disabled:opacity-50 dark:bg-indigo-500 dark:hover:bg-indigo-400"
        >
          {isPending ? (
            <>
              <svg
                className="h-4 w-4 animate-spin text-white"
                fill="none"
                viewBox="0 0 24 24"
              >
                <circle
                  className="opacity-25"
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="currentColor"
                  strokeWidth="4"
                />
                <path
                  className="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                />
              </svg>
              <span>建立中...</span>
            </>
          ) : (
            <span>建立商品</span>
          )}
        </button>
      </div>
    </form>
  );
}

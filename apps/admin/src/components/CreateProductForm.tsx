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
        <div className="rounded-lg border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700 dark:border-rose-900/50 dark:bg-rose-950/20 dark:text-rose-400">
          {errorMessage}
        </div>
      )}

      <div>
        <label
          htmlFor="product-name"
          className="block text-sm font-medium text-zinc-900 dark:text-zinc-100"
        >
          商品名稱
        </label>
        <div className="mt-1">
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
            className="block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 placeholder-zinc-400 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder-zinc-500"
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
        <div className="mt-1">
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
            className="block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm font-mono text-zinc-900 placeholder-zinc-400 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder-zinc-500"
          />
        </div>
      </div>

      <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
        <div>
          <label
            htmlFor="product-price"
            className="block text-sm font-medium text-zinc-900 dark:text-zinc-100"
          >
            價格
          </label>
          <div className="mt-1">
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
              className="block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 placeholder-zinc-400 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder-zinc-500"
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
          <div className="mt-1">
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
              className="block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm font-mono uppercase text-zinc-900 placeholder-zinc-400 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder-zinc-500"
            />
          </div>
        </div>
      </div>

      <div>
        <label
          htmlFor="product-initial-stock"
          className="block text-sm font-medium text-zinc-900 dark:text-zinc-100"
        >
          初始庫存
        </label>
        <div className="mt-1">
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
            className="block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 placeholder-zinc-400 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder-zinc-500"
          />
        </div>
        <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
          初始庫存必須為大於 0 的整數，建立後將作為此商品的起始可用庫存。
        </p>
      </div>

      <div className="flex items-center justify-end gap-3 pt-4 border-t border-zinc-200 dark:border-zinc-800">
        <button
          type="button"
          onClick={() => router.push("/products")}
          disabled={isPending}
          className="rounded-lg border border-zinc-300 bg-white px-4 py-2 text-sm font-medium text-zinc-700 hover:bg-zinc-50 focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
        >
          取消
        </button>
        <button
          type="submit"
          disabled={isPending}
          className="inline-flex items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 disabled:opacity-50 dark:bg-indigo-500 dark:hover:bg-indigo-400"
        >
          {isPending ? "建立中..." : "建立商品"}
        </button>
      </div>
    </form>
  );
}

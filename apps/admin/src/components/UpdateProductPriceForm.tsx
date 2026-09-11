"use client";

import { useState, useTransition } from "react";
import { updateProductPriceAction } from "@/app/actions";

interface UpdateProductPriceFormProps {
  productId: string;
  currentPrice: number;
  currency: string;
}

export function UpdateProductPriceForm({
  productId,
  currentPrice,
  currency,
}: UpdateProductPriceFormProps) {
  const [priceInput, setPriceInput] = useState(currentPrice.toString());
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    setSuccessMessage(null);

    const parsedPrice = parseFloat(priceInput);
    if (isNaN(parsedPrice) || !isFinite(parsedPrice) || parsedPrice <= 0) {
      setErrorMessage("請輸入大於零的有效價格數值。");
      return;
    }

    if (parsedPrice === currentPrice) {
      setErrorMessage("新價格與現有價格相同，無須調整。");
      return;
    }

    startTransition(async () => {
      const result = await updateProductPriceAction(productId, parsedPrice);
      if (!result.success) {
        setErrorMessage(result.error || "價格調整失敗，請稍後重試。");
      } else {
        setSuccessMessage("商品價格已成功更新！");
      }
    });
  };

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-5 shadow-xs sm:p-6 dark:border-zinc-800 dark:bg-zinc-900">
      <h3 className="text-base font-semibold text-zinc-900 dark:text-zinc-50">
        調整商品售價
      </h3>
      <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
        直接更新商品在目錄與購物車的計價金額。僅限輸入正數金額。
      </p>

      <form onSubmit={handleSubmit} className="mt-4 space-y-4">
        <div>
          <label
            htmlFor="newPrice"
            className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
          >
            新售價 ({currency})
          </label>
          <div className="mt-1.5">
            <input
              type="number"
              id="newPrice"
              name="newPrice"
              step="0.01"
              min="0.01"
              required
              disabled={isPending}
              value={priceInput}
              onChange={(e) => setPriceInput(e.target.value)}
              className="block min-h-[44px] w-full rounded-lg border border-zinc-300 px-3 py-2 text-sm text-zinc-900 placeholder-zinc-400 shadow-xs transition-colors focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:bg-zinc-100 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-50 dark:placeholder-zinc-500 dark:disabled:bg-zinc-800/50"
              placeholder="例如 150.00"
            />
          </div>
        </div>

        {errorMessage && (
          <div
            role="alert"
            className="rounded-lg border border-rose-200 bg-rose-50 p-3 text-xs font-medium text-rose-700 dark:border-rose-900/60 dark:bg-rose-950/40 dark:text-rose-300"
          >
            {errorMessage}
          </div>
        )}

        {successMessage && (
          <div
            role="alert"
            className="rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-xs font-medium text-emerald-700 dark:border-emerald-900/60 dark:bg-emerald-950/40 dark:text-emerald-300"
          >
            {successMessage}
          </div>
        )}

        <button
          type="submit"
          disabled={isPending}
          className="inline-flex min-h-[44px] w-full touch-manipulation items-center justify-center rounded-lg bg-indigo-600 px-4 py-2.5 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto dark:bg-indigo-500 dark:hover:bg-indigo-400 dark:focus-visible:ring-offset-zinc-900"
        >
          {isPending ? "儲存更新中..." : "確認更新價格"}
        </button>
      </form>
    </div>
  );
}

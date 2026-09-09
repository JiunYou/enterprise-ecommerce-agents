"use client";

import { useState, useTransition } from "react";
import Link from "next/link";

interface AddToCartFormProps {
  productId: string;
  isLoggedIn: boolean;
  onAddToCart: (
    productId: string,
    quantity: number
  ) => Promise<{ success: boolean; error?: string }>;
}

export function AddToCartForm({
  productId,
  isLoggedIn,
  onAddToCart,
}: AddToCartFormProps) {
  const [quantity, setQuantity] = useState(1);
  const [isPending, startTransition] = useTransition();
  const [statusMessage, setStatusMessage] = useState<{
    type: "success" | "error";
    text: string;
  } | null>(null);

  if (!isLoggedIn) {
    return (
      <div className="rounded-xl border border-stone-200/90 bg-stone-100/60 p-5 dark:border-stone-800 dark:bg-stone-900/50 sm:p-6">
        <p className="text-sm text-stone-600 dark:text-stone-400">
          如需將此商品加入購物車，請先登入顧客帳號。
        </p>
        <div className="mt-4">
          <a
            href={`/auth/login?returnTo=/products/${encodeURIComponent(productId)}`}
            className="inline-flex w-full items-center justify-center rounded-lg bg-stone-900 px-5 py-3 text-sm font-medium text-white shadow-xs transition hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-500 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200 sm:w-auto"
          >
            登入以加入購物車
          </a>
        </div>
      </div>
    );
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (quantity <= 0) return;

    setStatusMessage(null);
    startTransition(async () => {
      const res = await onAddToCart(productId, quantity);
      if (res.success) {
        setStatusMessage({
          type: "success",
          text: "已成功加入購物車！",
        });
      } else {
        setStatusMessage({
          type: "error",
          text: res.error || "加入購物車失敗，請稍後再試。",
        });
      }
    });
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <div className="w-full sm:w-auto">
          <label
            htmlFor="quantity"
            className="block text-xs font-semibold uppercase tracking-wider text-stone-500 dark:text-stone-400"
          >
            數量
          </label>
          <div className="mt-1.5 flex items-center">
            <input
              type="number"
              id="quantity"
              name="quantity"
              min="1"
              max="999"
              value={quantity}
              onChange={(e) => {
                const val = parseInt(e.target.value, 10);
                setQuantity(isNaN(val) || val < 1 ? 1 : val);
              }}
              className="h-11 w-full rounded-lg border border-stone-300 bg-white px-3 text-center text-sm font-semibold text-stone-900 shadow-2xs transition focus:border-stone-500 focus:outline-none focus:ring-2 focus:ring-stone-400/30 disabled:opacity-50 dark:border-stone-700 dark:bg-stone-900 dark:text-stone-100 dark:focus:border-stone-400 dark:focus:ring-stone-600/30 sm:w-28"
              disabled={isPending}
            />
          </div>
        </div>

        <button
          type="submit"
          disabled={isPending}
          className="h-11 inline-flex w-full items-center justify-center rounded-lg bg-stone-900 px-6 text-sm font-medium text-white shadow-xs transition hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200 sm:flex-1"
        >
          {isPending ? "加入中..." : "加入購物車"}
        </button>
      </div>

      {statusMessage && (
        <div
          className={`rounded-lg p-3.5 text-sm flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between ${
            statusMessage.type === "success"
              ? "bg-emerald-50/80 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800/40"
              : "bg-red-50/80 text-red-800 dark:bg-red-950/40 dark:text-red-300 border border-red-200 dark:border-red-800/40"
          }`}
        >
          <span className="leading-relaxed">{statusMessage.text}</span>
          {statusMessage.type === "success" && (
            <Link
              href="/cart"
              className="inline-flex shrink-0 font-semibold underline underline-offset-4 hover:no-underline"
            >
              前往購物車 &rarr;
            </Link>
          )}
        </div>
      )}
    </form>
  );
}

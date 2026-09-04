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
      <div className="mt-8 rounded-xl border border-zinc-200 bg-zinc-50 p-6 dark:border-zinc-800 dark:bg-zinc-900/50">
        <p className="text-sm text-zinc-600 dark:text-zinc-400">
          如需將此商品加入購物車，請先登入顧客帳號。
        </p>
        <div className="mt-4">
          <a
            href={`/auth/login?returnTo=/products/${encodeURIComponent(productId)}`}
            className="inline-flex items-center justify-center rounded-lg bg-zinc-900 px-5 py-2.5 text-sm font-medium text-white shadow-sm transition hover:bg-zinc-800 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
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
    <form onSubmit={handleSubmit} className="mt-8">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end">
        <div>
          <label
            htmlFor="quantity"
            className="block text-xs font-semibold uppercase tracking-wider text-zinc-500 dark:text-zinc-400"
          >
            數量
          </label>
          <div className="mt-1 flex items-center">
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
              className="w-24 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-center text-sm font-semibold text-zinc-900 shadow-sm focus:border-zinc-900 focus:outline-none focus:ring-1 focus:ring-zinc-900 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:focus:border-zinc-100 dark:focus:ring-zinc-100"
              disabled={isPending}
            />
          </div>
        </div>

        <button
          type="submit"
          disabled={isPending}
          className="inline-flex items-center justify-center rounded-lg bg-zinc-900 px-6 py-2.5 text-sm font-medium text-white shadow-sm transition hover:bg-zinc-800 disabled:opacity-50 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
        >
          {isPending ? "加入中..." : "加入購物車"}
        </button>
      </div>

      {statusMessage && (
        <div
          className={`mt-4 rounded-lg p-3 text-sm flex items-center justify-between ${
            statusMessage.type === "success"
              ? "bg-emerald-50 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800/40"
              : "bg-red-50 text-red-800 dark:bg-red-950/40 dark:text-red-300 border border-red-200 dark:border-red-800/40"
          }`}
        >
          <span>{statusMessage.text}</span>
          {statusMessage.type === "success" && (
            <Link
              href="/cart"
              className="ml-4 font-semibold underline hover:no-underline"
            >
              前往購物車 &rarr;
            </Link>
          )}
        </div>
      )}
    </form>
  );
}

"use client";

import { useState, useTransition } from "react";
import Link from "next/link";
import type { CartItem } from "@/lib/cart";
import type { SubmitOrderResult } from "@/lib/orders";
import { formatPrice } from "@/lib/format";

interface CheckoutReviewProps {
  orderId: string;
  items: CartItem[];
  currency: string;
  totalAmount: number;
  onSubmitOrder: (orderId: string) => Promise<SubmitOrderResult>;
}

export function CheckoutReview({
  orderId,
  items,
  currency,
  totalAmount,
  onSubmitOrder,
}: CheckoutReviewProps) {
  const [isPending, startTransition] = useTransition();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (isPending) return;
    setErrorMessage(null);

    startTransition(async () => {
      const res = await onSubmitOrder(orderId);
      if (!res.success) {
        setErrorMessage(res.error);
      }
    });
  };

  return (
    <div className="space-y-6">
      {errorMessage && (
        <section
          aria-label="結帳失敗提示"
          className="rounded-xl border border-red-200 bg-red-50 p-5 text-sm text-red-800 dark:border-red-900/50 dark:bg-red-950/40 dark:text-red-300"
        >
          <div className="flex items-start gap-3">
            <svg
              className="h-5 w-5 flex-shrink-0 text-red-500 mt-0.5"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
              />
            </svg>
            <div className="flex-1">
              <h4 className="font-semibold">送出訂單失敗</h4>
              <p className="mt-1">{errorMessage}</p>
              <div className="mt-3">
                <Link
                  href="/cart"
                  className="inline-flex items-center text-xs font-semibold text-red-700 underline hover:text-red-900 dark:text-red-300 dark:hover:text-red-100"
                >
                  &larr; 返回購物車查看或調整商品
                </Link>
              </div>
            </div>
          </div>
        </section>
      )}

      {/* 訂單品項清單審閱 */}
      <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
        <div className="border-b border-zinc-200 bg-zinc-50/50 px-6 py-4 dark:border-zinc-800 dark:bg-zinc-900/50">
          <h3 className="text-base font-semibold text-zinc-900 dark:text-zinc-100">
            訂單明細審核 (共 {items.length} 項商品)
          </h3>
        </div>

        <ul role="list" className="divide-y divide-zinc-200 dark:divide-zinc-800">
          {items.map((item) => (
            <li
              key={item.productId}
              className="flex flex-col gap-3 p-6 sm:flex-row sm:items-center sm:justify-between"
            >
              <div className="flex-1">
                <h4 className="text-base font-semibold text-zinc-900 dark:text-zinc-100">
                  {item.productName || "商品 (" + item.productId.slice(0, 8) + "...)"}
                </h4>
                <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">
                  單價：{formatPrice(item.unitPrice, item.currency)}
                </p>
              </div>

              <div className="flex items-center gap-8">
                <div className="text-sm text-zinc-600 dark:text-zinc-400">
                  <span>數量：</span>
                  <span className="font-mono font-semibold text-zinc-900 dark:text-zinc-100">
                    {item.quantity}
                  </span>
                </div>
                <div className="w-28 text-right font-mono text-base font-semibold text-zinc-900 dark:text-zinc-100">
                  {formatPrice(item.totalPrice, item.currency)}
                </div>
              </div>
            </li>
          ))}
        </ul>

        {/* 總計與付款前說明 */}
        <div className="border-t border-zinc-200 bg-zinc-50 px-6 py-5 dark:border-zinc-800 dark:bg-zinc-900/60">
          <div className="flex items-center justify-between">
            <span className="text-base font-semibold text-zinc-700 dark:text-zinc-300">
              訂單應付總額
            </span>
            <span className="font-mono text-2xl font-extrabold text-zinc-900 dark:text-zinc-100">
              {formatPrice(totalAmount, currency)}
            </span>
          </div>

          <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 p-4 text-xs text-amber-800 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-300">
            <p className="font-medium">
              注意事項：此步驟為「付款前確認」。點擊確認送出後，系統將正式建立訂單並保留庫存；付款流程將於後續階段進行。
            </p>
          </div>
        </div>
      </div>

      {/* 送出與返回動作列 */}
      <form onSubmit={handleSubmit} className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <Link
          href="/cart"
          className="inline-flex items-center text-sm font-medium text-zinc-600 transition hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-zinc-100"
        >
          &larr; 返回購物車修改
        </Link>

        <button
          type="submit"
          disabled={isPending}
          className="inline-flex items-center justify-center rounded-lg bg-zinc-900 px-8 py-3.5 text-base font-semibold text-white shadow-sm transition hover:bg-zinc-800 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
        >
          {isPending ? (
            <span className="flex items-center gap-2">
              <svg
                className="h-5 w-5 animate-spin text-current"
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
              訂單處理與庫存保留中...
            </span>
          ) : (
            "確認送出訂單"
          )}
        </button>
      </form>
    </div>
  );
}

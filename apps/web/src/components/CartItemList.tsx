"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import type { CartItem } from "@/lib/cart";
import { formatPrice } from "@/lib/format";

interface CartItemListProps {
  items: CartItem[];
  currency: string;
  totalAmount: number;
  onUpdateQuantity: (
    productId: string,
    quantity: number
  ) => Promise<{ success: boolean; error?: string }>;
  onRemoveItem: (
    productId: string
  ) => Promise<{ success: boolean; error?: string }>;
}

export function CartItemList({
  items,
  currency,
  totalAmount,
  onUpdateQuantity,
  onRemoveItem,
}: CartItemListProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [activeItemId, setActiveItemId] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleQuantityChange = (productId: string, newQuantity: number) => {
    if (newQuantity < 1) return;
    setErrorMessage(null);
    setActiveItemId(productId);

    startTransition(async () => {
      const res = await onUpdateQuantity(productId, newQuantity);
      setActiveItemId(null);
      if (res.success) {
        router.refresh();
      } else {
        setErrorMessage(res.error || "更新數量失敗，請稍後再試。");
      }
    });
  };

  const handleRemove = (productId: string) => {
    setErrorMessage(null);
    setActiveItemId(productId);

    startTransition(async () => {
      const res = await onRemoveItem(productId);
      setActiveItemId(null);
      if (res.success) {
        router.refresh();
      } else {
        setErrorMessage(res.error || "移除商品失敗，請稍後再試。");
      }
    });
  };

  return (
    <div className="mt-6 space-y-6">
      {errorMessage && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-800 dark:border-red-900/50 dark:bg-red-950/40 dark:text-red-300">
          {errorMessage}
        </div>
      )}

      <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
        <ul role="list" className="divide-y divide-zinc-200 dark:divide-zinc-800">
          {items.map((item) => {
            const isItemUpdating = isPending && activeItemId === item.productId;
            return (
              <li
                key={item.productId}
                className="flex flex-col gap-4 p-6 sm:flex-row sm:items-center sm:justify-between"
              >
                <div className="flex-1">
                  <Link
                    href={`/products/${encodeURIComponent(item.productId)}`}
                    className="text-base font-semibold text-zinc-900 transition hover:underline dark:text-zinc-100"
                  >
                    {item.productName || "商品 (" + item.productId.slice(0, 8) + "...)"}
                  </Link>
                  <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">
                    單價：{formatPrice(item.unitPrice, item.currency)}
                  </p>
                </div>

                <div className="flex items-center gap-6">
                  {/* 數量調整 */}
                  <div className="flex items-center gap-2">
                    <label
                      htmlFor={`quantity-${item.productId}`}
                      className="text-xs font-semibold uppercase text-zinc-500 dark:text-zinc-400"
                    >
                      數量
                    </label>
                    <div className="flex items-center rounded-lg border border-zinc-300 bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800">
                      <button
                        type="button"
                        disabled={isPending || item.quantity <= 1}
                        onClick={() =>
                          handleQuantityChange(item.productId, item.quantity - 1)
                        }
                        className="px-2.5 py-1 text-sm font-bold text-zinc-600 hover:text-zinc-900 disabled:opacity-30 dark:text-zinc-400 dark:hover:text-zinc-100"
                        aria-label="減少數量"
                      >
                        -
                      </button>
                      <span className="w-10 text-center font-mono text-sm font-semibold text-zinc-900 dark:text-zinc-100">
                        {item.quantity}
                      </span>
                      <button
                        type="button"
                        disabled={isPending}
                        onClick={() =>
                          handleQuantityChange(item.productId, item.quantity + 1)
                        }
                        className="px-2.5 py-1 text-sm font-bold text-zinc-600 hover:text-zinc-900 disabled:opacity-30 dark:text-zinc-400 dark:hover:text-zinc-100"
                        aria-label="增加數量"
                      >
                        +
                      </button>
                    </div>
                  </div>

                  {/* 小計 */}
                  <div className="w-24 text-right">
                    <span className="text-xs block font-semibold uppercase text-zinc-500 dark:text-zinc-400">
                      小計
                    </span>
                    <span className="font-mono text-sm font-bold text-zinc-900 dark:text-zinc-100">
                      {formatPrice(item.totalPrice, item.currency)}
                    </span>
                  </div>

                  {/* 移除按鈕 */}
                  <button
                    type="button"
                    disabled={isPending}
                    onClick={() => handleRemove(item.productId)}
                    className="text-sm font-medium text-red-600 transition hover:text-red-800 disabled:opacity-50 dark:text-red-400 dark:hover:text-red-300"
                  >
                    {isItemUpdating ? "處理中..." : "移除"}
                  </button>
                </div>
              </li>
            );
          })}
        </ul>

        {/* 總計資訊列 */}
        <div className="border-t border-zinc-200 bg-zinc-50 px-6 py-4 flex items-center justify-between dark:border-zinc-800 dark:bg-zinc-900/60">
          <span className="text-base font-medium text-zinc-700 dark:text-zinc-300">
            購物車總計
          </span>
          <span className="text-2xl font-extrabold text-zinc-900 dark:text-zinc-100 font-mono">
            {formatPrice(totalAmount, currency)}
          </span>
        </div>
      </div>

      {/* 結帳動作連結 */}
      <div className="flex justify-end">
        <Link
          href="/checkout"
          className="inline-flex items-center justify-center rounded-lg bg-zinc-900 px-6 py-3 text-base font-semibold text-white shadow-sm transition hover:bg-zinc-800 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
        >
          前往結帳 &rarr;
        </Link>
      </div>
    </div>
  );
}

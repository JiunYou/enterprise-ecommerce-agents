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

function getProductMonogram(name?: string): string {
  if (!name || !name.trim()) return "商品";
  const trimmed = name.trim();
  return trimmed.slice(0, 2).toUpperCase();
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
    <div className="space-y-6">
      {errorMessage && (
        <div
          role="alert"
          aria-live="polite"
          className="rounded-xl border border-red-200 bg-red-50/80 p-4 text-sm font-medium text-red-800 break-words dark:border-red-900/50 dark:bg-red-950/40 dark:text-red-300"
        >
          {errorMessage}
        </div>
      )}

      <div className="lg:grid lg:grid-cols-[minmax(0,1fr)_22rem] lg:items-start lg:gap-8">
        {/* 購物車品項清單 */}
        <section
          aria-label="購物車品項"
          className="overflow-hidden rounded-2xl border border-stone-200 bg-white shadow-sm dark:border-stone-800 dark:bg-stone-900"
        >
          <ul
            role="list"
            className="divide-y divide-stone-200 dark:divide-stone-800"
          >
            {items.map((item) => {
              const isItemUpdating = isPending && activeItemId === item.productId;
              return (
                <li
                  key={item.productId}
                  className="flex flex-col gap-4 p-5 sm:p-6 md:flex-row md:items-center md:justify-between"
                >
                  {/* 左側：商品圖片/裝飾圖塊 + 商品名稱與單價 */}
                  <div className="flex items-start gap-4 min-w-0 flex-1">
                    {item.productImageUrl && item.productImageUrl.trim().length > 0 ? (
                      <div className="relative h-14 w-14 shrink-0 overflow-hidden rounded-xl border border-stone-200 bg-stone-100 dark:border-stone-700 dark:bg-stone-800">
                        {/* eslint-disable-next-line @next/next/no-img-element -- Bounded external product image metadata rendering without server-side proxy */}
                        <img
                          src={item.productImageUrl.trim()}
                          alt={item.productName || "商品"}
                          loading="lazy"
                          decoding="async"
                          referrerPolicy="no-referrer"
                          className="h-full w-full object-cover"
                        />
                      </div>
                    ) : (
                      <div
                        aria-hidden="true"
                        className="flex h-14 w-14 shrink-0 items-center justify-center rounded-xl border border-stone-200 bg-stone-100 text-sm font-bold tracking-wider text-stone-600 select-none dark:border-stone-700 dark:bg-stone-800 dark:text-stone-300"
                      >
                        {getProductMonogram(item.productName)}
                      </div>
                    )}
                    <div className="min-w-0 flex-1">
                      <Link
                        href={`/products/${encodeURIComponent(item.productId)}`}
                        className="text-base font-semibold text-stone-900 transition-colors hover:text-stone-700 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-sm break-words dark:text-stone-100 dark:hover:text-stone-300"
                      >
                        {item.productName ||
                          "商品 (" + item.productId.slice(0, 8) + "...)"}
                      </Link>
                      <p className="mt-1 text-sm text-stone-500 dark:text-stone-400">
                        單價：{formatPrice(item.unitPrice, item.currency)}
                      </p>
                    </div>
                  </div>

                  {/* 右側：數量調整 + 小計 + 移除 */}
                  <div className="flex flex-wrap items-center justify-between gap-4 border-t border-stone-100 pt-4 md:border-t-0 md:pt-0 md:justify-end md:gap-6 dark:border-stone-800/60">
                    {/* 數量調整 */}
                    <div className="flex items-center gap-2">
                      <label
                        htmlFor={`quantity-${item.productId}`}
                        className="sr-only"
                      >
                        數量
                      </label>
                      <div className="flex items-center rounded-lg border border-stone-300 bg-stone-50/50 shadow-xs dark:border-stone-700 dark:bg-stone-800">
                        <button
                          type="button"
                          id={`decrement-${item.productId}`}
                          disabled={isPending || item.quantity <= 1}
                          onClick={() =>
                            handleQuantityChange(
                              item.productId,
                              item.quantity - 1
                            )
                          }
                          className="inline-flex min-h-[44px] min-w-[44px] items-center justify-center text-base font-bold text-stone-700 transition-colors hover:text-stone-950 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-l-lg disabled:cursor-not-allowed disabled:opacity-30 dark:text-stone-300 dark:hover:text-stone-100"
                          aria-label="減少數量"
                        >
                          -
                        </button>
                        <span
                          id={`quantity-${item.productId}`}
                          aria-live="polite"
                          className="w-10 text-center font-mono text-sm font-semibold text-stone-900 dark:text-stone-100"
                        >
                          {item.quantity}
                        </span>
                        <button
                          type="button"
                          id={`increment-${item.productId}`}
                          disabled={isPending}
                          onClick={() =>
                            handleQuantityChange(
                              item.productId,
                              item.quantity + 1
                            )
                          }
                          className="inline-flex min-h-[44px] min-w-[44px] items-center justify-center text-base font-bold text-stone-700 transition-colors hover:text-stone-950 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-r-lg disabled:cursor-not-allowed disabled:opacity-30 dark:text-stone-300 dark:hover:text-stone-100"
                          aria-label="增加數量"
                        >
                          +
                        </button>
                      </div>
                    </div>

                    {/* 小計 */}
                    <div className="text-right min-w-[5.5rem]">
                      <span className="block text-xs font-medium uppercase tracking-wider text-stone-500 dark:text-stone-400">
                        小計
                      </span>
                      <span className="font-mono text-base font-bold text-stone-950 dark:text-stone-50 whitespace-nowrap">
                        {formatPrice(item.totalPrice, item.currency)}
                      </span>
                    </div>

                    {/* 移除按鈕 */}
                    <div>
                      <button
                        type="button"
                        disabled={isPending}
                        onClick={() => handleRemove(item.productId)}
                        className="inline-flex min-h-[44px] min-w-[44px] items-center justify-center rounded-lg px-2 text-sm font-medium text-stone-500 transition-colors hover:text-red-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:ring-offset-2 disabled:opacity-50 dark:text-stone-400 dark:hover:text-red-400"
                        aria-label={`移除 ${item.productName || "商品"}`}
                      >
                        {isItemUpdating ? "處理中..." : "移除"}
                      </button>
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>
        </section>

        {/* 訂單摘要 / 總計卡片 */}
        <section
          aria-label="訂單摘要"
          className="mt-6 lg:mt-0 rounded-2xl border border-stone-200 bg-white p-6 shadow-sm dark:border-stone-800 dark:bg-stone-900 lg:sticky lg:top-8"
        >
          <h2 className="text-lg font-bold text-stone-950 dark:text-stone-50">
            訂單摘要
          </h2>

          <div className="mt-6 border-t border-stone-200 pt-4 dark:border-stone-800">
            <div className="flex items-baseline justify-between">
              <span className="text-base font-semibold text-stone-900 dark:text-stone-100">
                購物車總計
              </span>
              <span className="font-mono text-2xl font-extrabold tracking-tight text-stone-950 dark:text-stone-50">
                {formatPrice(totalAmount, currency)}
              </span>
            </div>
          </div>

          <Link
            href="/checkout"
            className="mt-6 flex min-h-[48px] w-full items-center justify-center rounded-xl bg-stone-900 px-6 py-3.5 text-base font-semibold text-white shadow-sm transition-colors hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
          >
            前往結帳 &rarr;
          </Link>
        </section>
      </div>
    </div>
  );
}

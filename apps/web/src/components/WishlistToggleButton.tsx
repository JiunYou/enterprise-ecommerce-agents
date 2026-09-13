"use client";

import React, { useState, useTransition } from "react";
import Link from "next/link";
import { addWishlistItemAction, removeWishlistItemAction } from "@/app/wishlist/actions";

interface WishlistToggleButtonProps {
  productId: string;
  isLoggedIn: boolean;
  initialIsWishlisted?: boolean;
}

export function WishlistToggleButton({
  productId,
  isLoggedIn,
  initialIsWishlisted = false,
}: WishlistToggleButtonProps) {
  const [isWishlisted, setIsWishlisted] = useState<boolean>(initialIsWishlisted);
  const [isPending, startTransition] = useTransition();
  const [feedbackMessage, setFeedbackMessage] = useState<string | null>(null);
  const [feedbackType, setFeedbackType] = useState<"success" | "info" | "error" | null>(null);

  if (!isLoggedIn) {
    return (
      <Link
        href={`/auth/login?returnTo=${encodeURIComponent(`/products/${productId}`)}`}
        className="inline-flex items-center justify-center px-4 py-2 border border-stone-300 dark:border-stone-700 text-sm font-medium rounded-md text-stone-700 dark:text-stone-300 bg-white dark:bg-stone-900 hover:bg-stone-50 dark:hover:bg-stone-800 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-stone-500 shadow-xs transition"
        aria-label="登入後收藏此商品"
      >
        登入後收藏
      </Link>
    );
  }

  const handleToggle = () => {
    setFeedbackMessage(null);
    setFeedbackType(null);

    startTransition(async () => {
      try {
        if (!isWishlisted) {
          // 加入收藏操作
          const result = await addWishlistItemAction(productId);
          if (result.success) {
            setIsWishlisted(true);
            setFeedbackType("success");
            setFeedbackMessage("已成功加入收藏清單");
          } else if (result.status === 409) {
            // 狀態收斂：伺服器已有該紀錄，同步為已收藏
            setIsWishlisted(true);
            setFeedbackType("info");
            setFeedbackMessage(result.error || "此商品已在收藏清單");
          } else {
            setFeedbackType("error");
            setFeedbackMessage(result.error || "加入收藏失敗");
          }
        } else {
          // 移除收藏操作
          const result = await removeWishlistItemAction(productId);
          if (result.success) {
            setIsWishlisted(false);
            setFeedbackType("success");
            setFeedbackMessage("已從收藏清單中移除");
          } else if (result.status === 404) {
            // 狀態收斂：伺服器已無該紀錄，同步為未收藏
            setIsWishlisted(false);
            setFeedbackType("info");
            setFeedbackMessage(result.error || "收藏清單中找不到此商品");
          } else {
            setFeedbackType("error");
            setFeedbackMessage(result.error || "移除收藏失敗");
          }
        }
      } catch {
        setFeedbackType("error");
        setFeedbackMessage("發生未預期的錯誤，請稍後再試");
      }
    });
  };

  return (
    <div className="flex flex-col gap-2">
      <button
        type="button"
        onClick={handleToggle}
        disabled={isPending}
        aria-disabled={isPending}
        aria-pressed={isWishlisted}
        aria-label={isWishlisted ? "移除收藏" : "加入收藏"}
        className={`inline-flex items-center justify-center px-4 py-2 text-sm font-medium rounded-md shadow-xs transition focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-stone-500 disabled:opacity-50 disabled:cursor-not-allowed ${
          isWishlisted
            ? "border border-rose-300 dark:border-rose-800 text-rose-700 dark:text-rose-300 bg-rose-50 dark:bg-rose-950/40 hover:bg-rose-100 dark:hover:bg-rose-950/70"
            : "border border-stone-300 dark:border-stone-700 text-stone-700 dark:text-stone-300 bg-white dark:bg-stone-900 hover:bg-stone-50 dark:hover:bg-stone-800"
        }`}
      >
        {isPending ? "處理中..." : isWishlisted ? "移除收藏" : "加入收藏"}
      </button>
      {feedbackMessage && (
        <p
          role="status"
          aria-live="polite"
          className={`text-sm ${
            feedbackType === "success"
              ? "text-emerald-600 dark:text-emerald-400"
              : feedbackType === "info"
              ? "text-blue-600 dark:text-blue-400"
              : "text-rose-600 dark:text-rose-400"
          }`}
        >
          {feedbackMessage}
        </p>
      )}
    </div>
  );
}

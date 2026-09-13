"use client";

import React, { useState, useTransition } from "react";
import Link from "next/link";
import { addWishlistItemAction } from "@/app/wishlist/actions";

interface AddToWishlistButtonProps {
  productId: string;
  isLoggedIn: boolean;
}

export function AddToWishlistButton({
  productId,
  isLoggedIn,
}: AddToWishlistButtonProps) {
  const [isPending, startTransition] = useTransition();
  const [feedbackMessage, setFeedbackMessage] = useState<string | null>(null);
  const [feedbackType, setFeedbackType] = useState<"success" | "info" | "error" | null>(null);

  if (!isLoggedIn) {
    return (
      <Link
        href={`/auth/login?returnTo=${encodeURIComponent(`/products/${productId}`)}`}
        className="inline-flex items-center justify-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 shadow-sm transition"
        aria-label="登入後收藏此商品"
      >
        登入後收藏
      </Link>
    );
  }

  const handleAddToWishlist = () => {
    setFeedbackMessage(null);
    setFeedbackType(null);

    startTransition(async () => {
      try {
        const result = await addWishlistItemAction(productId);
        if (result.success) {
          setFeedbackType("success");
          setFeedbackMessage("已成功加入收藏清單");
        } else if (result.status === 409) {
          setFeedbackType("info");
          setFeedbackMessage(result.error || "此商品已在收藏清單");
        } else {
          setFeedbackType("error");
          setFeedbackMessage(result.error || "加入收藏失敗");
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
        onClick={handleAddToWishlist}
        disabled={isPending}
        aria-disabled={isPending}
        aria-label="加入收藏"
        className="inline-flex items-center justify-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 shadow-sm transition disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {isPending ? "處理中..." : "加入收藏"}
      </button>
      {feedbackMessage && (
        <p
          role="status"
          aria-live="polite"
          className={`text-sm ${
            feedbackType === "success"
              ? "text-green-600"
              : feedbackType === "info"
              ? "text-blue-600"
              : "text-red-600"
          }`}
        >
          {feedbackMessage}
        </p>
      )}
    </div>
  );
}

"use client";

import React, { useTransition } from "react";
import { removeWishlistItemAction } from "@/app/wishlist/actions";

interface RemoveWishlistItemButtonProps {
  productId: string;
  productName?: string;
}

export function RemoveWishlistItemButton({
  productId,
  productName,
}: RemoveWishlistItemButtonProps) {
  const [isPending, startTransition] = useTransition();

  const handleRemove = () => {
    startTransition(async () => {
      try {
        await removeWishlistItemAction(productId);
      } catch (err) {
        console.error("移除收藏項目失敗:", err);
      }
    });
  };

  return (
    <button
      type="button"
      onClick={handleRemove}
      disabled={isPending}
      aria-disabled={isPending}
      aria-label={productName ? `從收藏清單移除 ${productName}` : "移除收藏"}
      className="inline-flex items-center justify-center px-3 py-1.5 border border-transparent text-xs font-medium rounded text-red-700 bg-red-50 hover:bg-red-100 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500 transition disabled:opacity-50 disabled:cursor-not-allowed"
    >
      {isPending ? "移除中..." : "移除收藏"}
    </button>
  );
}

"use server";

import { revalidatePath } from "next/cache";
import {
  addWishlistItem,
  removeWishlistItem,
  WishlistMutationResult,
} from "@/lib/wishlist";

/**
 * 加入收藏 Server Action
 */
export async function addWishlistItemAction(
  productId: string
): Promise<WishlistMutationResult> {
  const result = await addWishlistItem(productId);

  if (result.success) {
    revalidatePath("/wishlist");
    revalidatePath(`/products/${productId}`);
  }

  return result;
}

/**
 * 移除收藏 Server Action
 */
export async function removeWishlistItemAction(
  productId: string
): Promise<WishlistMutationResult> {
  const result = await removeWishlistItem(productId);

  if (result.success) {
    revalidatePath("/wishlist");
    revalidatePath(`/products/${productId}`);
  }

  return result;
}

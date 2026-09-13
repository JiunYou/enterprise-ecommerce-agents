"use server";

import { revalidatePath } from "next/cache";
import { createProductReview, CreateProductReviewResult } from "@/lib/reviews";

/**
 * 建立商品評論 Server Action
 */
export async function createProductReviewAction(
  productId: string,
  rating: number,
  comment: string
): Promise<CreateProductReviewResult> {
  const result = await createProductReview(productId, rating, comment);

  if (result.success) {
    revalidatePath(`/products/${productId}`);
  }

  return result;
}

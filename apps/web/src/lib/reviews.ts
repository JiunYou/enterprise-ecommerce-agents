import "server-only";
import { auth0 } from "@/lib/auth0";
import { authenticatedFetch } from "@/lib/authenticated-api";

export interface ProductReview {
  rating: number;
  comment: string;
  createdAt: string;
}

export interface ProductReviewsResponse {
  items: ProductReview[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export type ProductReviewsResult =
  | { success: true; data: ProductReviewsResponse }
  | { success: false; notFound?: boolean; error: string };

export interface CreateProductReviewResult {
  success: boolean;
  status: number;
  error?: string;
}

/**
 * 取得指定商品之公開評論列表（分頁）
 */
export async function getProductReviews(
  productId: string,
  page = 1,
  pageSize = 10
): Promise<ProductReviewsResult> {
  const baseUrl = process.env.API_BASE_URL || "http://localhost:5110";

  let url: URL;
  try {
    url = new URL(`/api/v1/products/${encodeURIComponent(productId)}/reviews`, baseUrl);
  } catch {
    return {
      success: false,
      error: "無效的 API 端點設定",
    };
  }

  url.searchParams.set("page", Math.max(1, page).toString());
  url.searchParams.set("pageSize", Math.min(50, Math.max(1, pageSize)).toString());

  try {
    const response = await fetch(url.toString(), {
      method: "GET",
      headers: {
        Accept: "application/json",
      },
      cache: "no-store",
    });

    if (!response.ok) {
      if (response.status === 404) {
        return { success: false, notFound: true, error: "找不到該商品或目前未上架" };
      }
      return { success: false, error: "無法載入評論列表" };
    }

    const data = (await response.json()) as ProductReviewsResponse;
    return { success: true, data };
  } catch {
    return { success: false, error: "無法連線至評論服務" };
  }
}

/**
 * 已登入顧客針對商品提交評價與留言
 */
export async function createProductReview(
  productId: string,
  rating: number,
  comment: string
): Promise<CreateProductReviewResult> {
  const session = await auth0.getSession();
  if (!session) {
    return { success: false, status: 401, error: "請先登入後再撰寫評論" };
  }

  if (rating < 1 || rating > 5) {
    return { success: false, status: 400, error: "評分必須介於 1 至 5 顆星" };
  }

  const trimmedComment = comment.trim();
  if (!trimmedComment || trimmedComment.length > 2000) {
    return { success: false, status: 400, error: "評論內容為必填，且不得超過 2000 個字元" };
  }

  try {
    const response = await authenticatedFetch(`/api/v1/products/${encodeURIComponent(productId)}/reviews`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        rating,
        comment: trimmedComment,
      }),
    });

    if (response.ok) {
      return { success: true, status: response.status };
    }

    if (response.status === 409) {
      return { success: false, status: 409, error: "你已評論過此商品" };
    }

    if (response.status === 404) {
      return { success: false, status: 404, error: "商品不存在或目前未上架" };
    }

    if (response.status === 401) {
      return { success: false, status: 401, error: "登入狀態已過期，請重新登入" };
    }

    if (response.status === 403) {
      return { success: false, status: 403, error: "身分驗證失敗，無法提交評論" };
    }

    return { success: false, status: response.status, error: "評論提交失敗，請稍後再試" };
  } catch {
    return { success: false, status: 500, error: "系統錯誤，無法提交評論" };
  }
}

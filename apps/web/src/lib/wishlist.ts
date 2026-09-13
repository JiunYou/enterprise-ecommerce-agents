import "server-only";
import { auth0 } from "@/lib/auth0";
import { authenticatedFetch } from "@/lib/authenticated-api";

export interface WishlistItem {
  productId: string;
  addedAt: string;
}

export interface WishlistResponse {
  items: WishlistItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export type WishlistFetchResult =
  | { success: true; data: WishlistResponse }
  | { success: false; unauthorized?: boolean; error?: string };

export interface WishlistMutationResult {
  success: boolean;
  status: number;
  error?: string;
}

/**
 * 取得顧客專屬收藏清單（分頁）
 */
export async function getWishlist(page = 1, pageSize = 25): Promise<WishlistFetchResult> {
  const session = await auth0.getSession();
  if (!session) {
    return { success: false, unauthorized: true, error: "請先登入後再查看收藏清單" };
  }

  const queryParams = new URLSearchParams({
    page: page.toString(),
    pageSize: pageSize.toString(),
  });

  const response = await authenticatedFetch(`/api/v1/wishlist?${queryParams.toString()}`, {
    method: "GET",
    headers: {
      Accept: "application/json",
    },
  });

  if (!response.ok) {
    if (response.status === 401) {
      return { success: false, unauthorized: true, error: "登入逾期，請重新登入" };
    }
    if (response.status === 403) {
      return { success: false, error: "身分驗證失敗，無法存取收藏清單" };
    }
    return { success: false, error: "無法載入收藏清單，請稍後再試" };
  }

  const data = (await response.json()) as WishlistResponse;
  return { success: true, data };
}

/**
 * 將商品加入顧客收藏清單
 */
export async function addWishlistItem(productId: string): Promise<WishlistMutationResult> {
  const session = await auth0.getSession();
  if (!session) {
    return { success: false, status: 401, error: "請先登入後再收藏商品" };
  }

  const response = await authenticatedFetch(`/api/v1/wishlist/items/${productId}`, {
    method: "POST",
  });

  if (response.ok) {
    return { success: true, status: response.status };
  }

  if (response.status === 409) {
    return { success: false, status: 409, error: "此商品已在收藏清單" };
  }

  if (response.status === 404) {
    return { success: false, status: 404, error: "商品不存在或目前無法瀏覽" };
  }

  if (response.status === 401) {
    return { success: false, status: 401, error: "登入狀態已失效，請重新登入" };
  }

  if (response.status === 403) {
    return { success: false, status: 403, error: "顧客身分驗證失敗，無法操作收藏清單" };
  }

  return { success: false, status: response.status, error: "加入收藏失敗，請稍後再試" };
}

/**
 * 將商品從顧客收藏清單中移除
 */
export async function removeWishlistItem(productId: string): Promise<WishlistMutationResult> {
  const session = await auth0.getSession();
  if (!session) {
    return { success: false, status: 401, error: "請先登入後再進行操作" };
  }

  const response = await authenticatedFetch(`/api/v1/wishlist/items/${productId}`, {
    method: "DELETE",
  });

  if (response.ok) {
    return { success: true, status: response.status };
  }

  if (response.status === 404) {
    return { success: false, status: 404, error: "收藏清單中找不到此商品" };
  }

  if (response.status === 401) {
    return { success: false, status: 401, error: "登入狀態已失效，請重新登入" };
  }

  if (response.status === 403) {
    return { success: false, status: 403, error: "顧客身分驗證失敗，無法操作收藏清單" };
  }

  return { success: false, status: response.status, error: "移除收藏失敗，請稍後再試" };
}

import "server-only";
import { auth0 } from "@/lib/auth0";
import { authenticatedFetch } from "@/lib/authenticated-api";
import { getProductById } from "@/lib/catalog";

export interface CartItem {
  productId: string;
  unitPrice: number;
  currency: string;
  quantity: number;
  totalPrice: number;
  productName?: string;
}

export interface Cart {
  id: string | null;
  currency: string;
  totalAmount: number;
  items: CartItem[];
}

export type GetCartResult =
  | { success: true; data: Cart }
  | { success: false; unauthorized?: boolean; error: string };

export type CartMutationResult =
  | { success: true }
  | { success: false; unauthorized?: boolean; error: string };

export async function getCart(): Promise<GetCartResult> {
  const session = await auth0.getSession();
  if (!session || !session.user) {
    return {
      success: false,
      unauthorized: true,
      error: "尚未登入，請登入後查看購物車",
    };
  }

  try {
    const response = await authenticatedFetch("/api/v1/cart", {
      cache: "no-store",
    });

    if (response.status === 401 || response.status === 403) {
      return {
        success: false,
        unauthorized: true,
        error: "登入狀態無效或已過期，請重新登入",
      };
    }

    if (!response.ok) {
      return {
        success: false,
        error: `取得購物車失敗 (HTTP ${response.status})`,
      };
    }

    const data: Cart = await response.json();

    // 依據第 16 節，補齊商品名稱
    const enrichedItems = await Promise.all(
      data.items.map(async (item) => {
        try {
          const productDetail = await getProductById(item.productId);
          if (productDetail.success) {
            return {
              ...item,
              productName: productDetail.data.name,
            };
          }
        } catch {
          // 降級回傳不含名稱的 item
        }
        return item;
      })
    );

    return {
      success: true,
      data: {
        ...data,
        items: enrichedItems,
      },
    };
  } catch (err: unknown) {
    const errorMessage = err instanceof Error ? err.message : "連線錯誤";
    if (errorMessage.includes("Unauthorized")) {
      return {
        success: false,
        unauthorized: true,
        error: "尚未登入或存取權杖不可用",
      };
    }
    return {
      success: false,
      error: "目前無法連線至購物車服務，請稍後再試",
    };
  }
}

export async function addItemToCart(
  productId: string,
  quantity: number
): Promise<CartMutationResult> {
  const session = await auth0.getSession();
  if (!session || !session.user) {
    return {
      success: false,
      unauthorized: true,
      error: "請先登入會員以加入購物車",
    };
  }

  if (!productId || quantity <= 0) {
    return {
      success: false,
      error: "無效的商品識別碼或購買數量",
    };
  }

  try {
    const response = await authenticatedFetch("/api/v1/cart/items", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ productId, quantity }),
    });

    if (response.status === 401 || response.status === 403) {
      return {
        success: false,
        unauthorized: true,
        error: "登入狀態無效或已過期，請重新登入",
      };
    }

    if (!response.ok) {
      const errorBody = await response.text();
      return {
        success: false,
        error: errorBody || `加入購物車失敗 (HTTP ${response.status})`,
      };
    }

    return { success: true };
  } catch (err: unknown) {
    const errorMessage = err instanceof Error ? err.message : "連線錯誤";
    return {
      success: false,
      error: errorMessage.includes("Unauthorized")
        ? "尚未登入，請先登入"
        : "目前無法連線至購物車服務，請稍後再試",
    };
  }
}

export async function updateCartItemQuantity(
  productId: string,
  quantity: number
): Promise<CartMutationResult> {
  const session = await auth0.getSession();
  if (!session || !session.user) {
    return {
      success: false,
      unauthorized: true,
      error: "請先登入會員以修改購物車",
    };
  }

  if (!productId || quantity <= 0) {
    return {
      success: false,
      error: "無效的商品識別碼或數量必須大於 0",
    };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/cart/items/${encodeURIComponent(productId)}`,
      {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ quantity }),
      }
    );

    if (response.status === 401 || response.status === 403) {
      return {
        success: false,
        unauthorized: true,
        error: "登入狀態無效或已過期，請重新登入",
      };
    }

    if (!response.ok) {
      return {
        success: false,
        error: `修改品項數量失敗 (HTTP ${response.status})`,
      };
    }

    return { success: true };
  } catch {
    return {
      success: false,
      error: "連線錯誤，無法更新數量",
    };
  }
}

export async function removeCartItem(
  productId: string
): Promise<CartMutationResult> {
  const session = await auth0.getSession();
  if (!session || !session.user) {
    return {
      success: false,
      unauthorized: true,
      error: "請先登入會員以刪除購物車品項",
    };
  }

  if (!productId) {
    return {
      success: false,
      error: "無效的商品識別碼",
    };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/cart/items/${encodeURIComponent(productId)}`,
      {
        method: "DELETE",
      }
    );

    if (response.status === 401 || response.status === 403) {
      return {
        success: false,
        unauthorized: true,
        error: "登入狀態無效或已過期，請重新登入",
      };
    }

    if (!response.ok) {
      return {
        success: false,
        error: `刪除品項失敗 (HTTP ${response.status})`,
      };
    }

    return { success: true };
  } catch {
    return {
      success: false,
      error: "連線錯誤，無法移除品項",
    };
  }
}

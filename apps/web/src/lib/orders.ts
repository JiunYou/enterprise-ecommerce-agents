import "server-only";
import { auth0 } from "@/lib/auth0";
import { authenticatedFetch } from "@/lib/authenticated-api";
import { getProductById } from "@/lib/catalog";

export interface OrderItem {
  productId: string;
  unitPrice: number;
  currency: string;
  quantity: number;
  totalPrice: number;
  productName?: string;
}

export interface OrderDetail {
  id: string;
  customerId: string;
  status: string;
  currency: string;
  totalAmount: number;
  items: OrderItem[];
}

export type SubmitOrderResult =
  | { success: true }
  | {
      success: false;
      unauthorized?: boolean;
      notFound?: boolean;
      insufficientStock?: boolean;
      invalidState?: boolean;
      error: string;
    };

export type GetOrderResult =
  | { success: true; data: OrderDetail }
  | {
      success: false;
      unauthorized?: boolean;
      notFound?: boolean;
      error: string;
    };

export async function submitOrder(orderId: string): Promise<SubmitOrderResult> {
  const session = await auth0.getSession();
  if (!session || !session.user) {
    return {
      success: false,
      unauthorized: true,
      error: "尚未登入，請登入後繼續",
    };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/orders/${encodeURIComponent(orderId)}/submit`,
      {
        method: "PUT",
      }
    );

    if (response.status === 401 || response.status === 403) {
      return {
        success: false,
        unauthorized: true,
        error: "登入狀態無效或已過期，請重新登入",
      };
    }

    if (response.status === 404) {
      return {
        success: false,
        notFound: true,
        error: "找不到該訂單或您沒有存取權限",
      };
    }

    if (!response.ok) {
      try {
        const problem = await response.json();
        const detail = problem?.detail || problem?.title || "";
        if (detail.includes("Insufficient stock") || problem?.title?.includes("Insufficient stock")) {
          return {
            success: false,
            insufficientStock: true,
            error: "部分商品庫存不足，無法完成訂單送出，訂單仍維持在購物車中",
          };
        }
        if (detail.includes("Empty") || problem?.title?.includes("Empty")) {
          return {
            success: false,
            invalidState: true,
            error: "購物車或訂單為空，無法送出",
          };
        }
        if (detail.includes("Invalid status transition") || problem?.title?.includes("Invalid status")) {
          return {
            success: false,
            invalidState: true,
            error: "該訂單已經送出或處於無法再次送出的狀態",
          };
        }
      } catch {
        // Fallback to generic message
      }
      return {
        success: false,
        error: `送出訂單失敗 (HTTP ${response.status})，請稍後再試`,
      };
    }

    return { success: true };
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
      error: "目前無法連線至訂單服務，請稍後再試",
    };
  }
}

export async function getOrderById(orderId: string): Promise<GetOrderResult> {
  const session = await auth0.getSession();
  if (!session || !session.user) {
    return {
      success: false,
      unauthorized: true,
      error: "尚未登入，請登入後查看訂單",
    };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/orders/${encodeURIComponent(orderId)}`,
      {
        cache: "no-store",
      }
    );

    if (response.status === 401 || response.status === 403) {
      return {
        success: false,
        unauthorized: true,
        error: "登入狀態無效或已過期，請重新登入",
      };
    }

    if (response.status === 404) {
      return {
        success: false,
        notFound: true,
        error: "找不到該訂單或您沒有存取權限",
      };
    }

    if (!response.ok) {
      return {
        success: false,
        error: `取得訂單資訊失敗 (HTTP ${response.status})`,
      };
    }

    const data: OrderDetail = await response.json();

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
      error: "目前無法連線至訂單服務，請稍後再試",
    };
  }
}

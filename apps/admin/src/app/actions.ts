"use server";

import { revalidatePath } from "next/cache";
import { authenticatedFetch } from "@/lib/authenticated-api";

export interface ShipOrderResult {
  success: boolean;
  error?: string;
}

export async function shipOrderAction(orderId: string): Promise<ShipOrderResult> {
  if (!orderId || typeof orderId !== "string") {
    return { success: false, error: "無效的訂單編號。" };
  }

  try {
    const response = await authenticatedFetch(`/api/v1/orders/${encodeURIComponent(orderId)}/ship`, {
      method: "PUT",
    });

    if (response.status === 200) {
      revalidatePath("/");
      return { success: true };
    }

    if (response.status === 400) {
      revalidatePath("/");
      return {
        success: false,
        error: "訂單非可發貨狀態（可能已發貨或狀態已變更）。",
      };
    }

    if (response.status === 401) {
      return {
        success: false,
        error: "未授權或登入已逾期，請重新登入。",
      };
    }

    if (response.status === 403) {
      return {
        success: false,
        error: "權限不足，僅系統管理員（Admin）可執行發貨操作。",
      };
    }

    if (response.status === 404) {
      revalidatePath("/");
      return {
        success: false,
        error: "該訂單已不存在。",
      };
    }

    return {
      success: false,
      error: "發貨操作失敗，請稍後重試。",
    };
  } catch (error) {
    if (error instanceof Error && error.message.includes("Unauthorized")) {
      return {
        success: false,
        error: "未授權或登入已逾期，請重新登入。",
      };
    }

    return {
      success: false,
      error: "伺服器通訊錯誤，無法完成發貨操作。",
    };
  }
}

export interface CancelAdminOrderResult {
  success: boolean;
  error?: string;
}

export async function cancelAdminOrderAction(
  orderId: string,
  reason: string
): Promise<CancelAdminOrderResult> {
  if (!orderId || typeof orderId !== "string" || orderId.trim() === "") {
    return { success: false, error: "無效的訂單編號。" };
  }

  if (typeof reason !== "string") {
    return { success: false, error: "取消原因必須為文字。" };
  }

  const trimmedReason = reason.trim();
  if (trimmedReason.length === 0) {
    return { success: false, error: "取消原因不得為空。" };
  }

  if (trimmedReason.length > 500) {
    return { success: false, error: "取消原因長度不可超過 500 個字元。" };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/admin/orders/${encodeURIComponent(orderId.trim())}/cancel`,
      {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          reason: trimmedReason,
        }),
      }
    );

    if (response.status === 200) {
      revalidatePath("/orders");
      revalidatePath(`/orders/${orderId.trim()}`);
      return { success: true };
    }

    if (response.status === 400) {
      return {
        success: false,
        error: "取消原因無效或訂單當前狀態不可取消。",
      };
    }

    if (response.status === 401) {
      return {
        success: false,
        error: "未授權或登入已逾期，請重新登入。",
      };
    }

    if (response.status === 403) {
      return {
        success: false,
        error: "權限不足，僅系統管理員（Admin）可執行取消操作。",
      };
    }

    if (response.status === 404) {
      return {
        success: false,
        error: "該訂單已不存在。",
      };
    }

    if (response.status === 409) {
      return {
        success: false,
        error: "訂單狀態已被並發變更，請重新整理頁面後再試。",
      };
    }

    return {
      success: false,
      error: "取消操作失敗，請稍後重試。",
    };
  } catch (error) {
    if (error instanceof Error && error.message.includes("Unauthorized")) {
      return {
        success: false,
        error: "未授權或登入已逾期，請重新登入。",
      };
    }

    return {
      success: false,
      error: "伺服器通訊錯誤，無法完成取消操作。",
    };
  }
}

export interface RefundAdminPaymentResult {
  success: boolean;
  outcome?: string;
  refundStatus?: string | null;
  error?: string;
}

export async function refundAdminPaymentAction(
  paymentAttemptId: string,
  reason?: string | null
): Promise<RefundAdminPaymentResult> {
  if (!paymentAttemptId || typeof paymentAttemptId !== "string" || paymentAttemptId.trim() === "") {
    return { success: false, error: "無效的付款記錄識別碼。" };
  }

  let sanitizedReason: string | null = null;
  if (reason !== undefined && reason !== null) {
    if (typeof reason !== "string") {
      return { success: false, error: "退款原因必須為文字。" };
    }
    const trimmed = reason.trim();
    if (trimmed.length > 500) {
      return { success: false, error: "退款原因長度不可超過 500 個字元。" };
    }
    sanitizedReason = trimmed.length > 0 ? trimmed : null;
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/admin/payments/${encodeURIComponent(paymentAttemptId.trim())}/refund`,
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          reason: sanitizedReason,
        }),
      }
    );

    if (response.status === 200) {
      const data: {
        paymentAttemptId?: string;
        outcome?: string;
        refundStatus?: string | null;
      } = await response.json();

      revalidatePath("/orders");

      return {
        success: true,
        outcome: data.outcome,
        refundStatus: data.refundStatus ?? null,
      };
    }

    if (response.status === 400) {
      return {
        success: false,
        error: "退款條件不符合、原因無效，或訂單/付款狀態已變更。",
      };
    }

    if (response.status === 401) {
      return {
        success: false,
        error: "未授權或登入已逾期，請重新登入。",
      };
    }

    if (response.status === 403) {
      return {
        success: false,
        error: "權限不足，僅 Admin 可執行退款操作。",
      };
    }

    if (response.status === 404) {
      return {
        success: false,
        error: "找不到指定的付款記錄。",
      };
    }

    if (response.status === 409) {
      return {
        success: false,
        error: "退款意圖已由其他請求建立，請重新整理或重新檢查退款狀態。",
      };
    }

    return {
      success: false,
      error: "退款處理失敗，請稍後重試。",
    };
  } catch (error) {
    if (error instanceof Error && error.message.includes("Unauthorized")) {
      return {
        success: false,
        error: "未授權或登入已逾期，請重新登入。",
      };
    }

    return {
      success: false,
      error: "伺服器通訊錯誤，無法完成退款操作。",
    };
  }
}

export interface UpdateProductPriceResult {
  success: boolean;
  error?: string;
}

export async function updateProductPriceAction(
  productId: string,
  newPrice: number
): Promise<UpdateProductPriceResult> {
  if (!productId || typeof productId !== "string" || productId.trim() === "") {
    return { success: false, error: "無效的商品編號。" };
  }

  if (
    typeof newPrice !== "number" ||
    isNaN(newPrice) ||
    !isFinite(newPrice) ||
    newPrice <= 0
  ) {
    return { success: false, error: "商品價格必須為大於零的有效數值。" };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/products/${encodeURIComponent(productId.trim())}/price`,
      {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          newPrice,
        }),
      }
    );

    if (response.status === 200) {
      revalidatePath("/products");
      revalidatePath(`/products/${encodeURIComponent(productId.trim())}`);
      return { success: true };
    }

    if (response.status === 400) {
      const errorJson = await response.json().catch(() => null);
      return {
        success: false,
        error: errorJson?.detail || "商品價格無效或更新格式錯誤。",
      };
    }

    if (response.status === 401) {
      return {
        success: false,
        error: "未授權或登入已逾期，請重新登入。",
      };
    }

    if (response.status === 403) {
      return {
        success: false,
        error: "權限不足，僅系統管理員（Admin）可調整商品價格。",
      };
    }

    if (response.status === 404) {
      revalidatePath("/products");
      return {
        success: false,
        error: "指定的商品已不存在。",
      };
    }

    if (response.status === 409) {
      return {
        success: false,
        error: "商品已被其他操作修改，請重新整理後確認最新狀態。",
      };
    }

    return {
      success: false,
      error: "更新商品價格失敗，請稍後重試。",
    };
  } catch (error) {
    if (error instanceof Error && error.message.includes("Unauthorized")) {
      return {
        success: false,
        error: "未授權或登入已逾期，請重新登入。",
      };
    }

    return {
      success: false,
      error: "伺服器通訊錯誤，無法完成價格調整。",
    };
  }
}

export interface DeactivateProductResult {
  success: boolean;
  error?: string;
}

export async function deactivateProductAction(
  productId: string
): Promise<DeactivateProductResult> {
  if (!productId || typeof productId !== "string" || productId.trim() === "") {
    return { success: false, error: "無效的商品編號。" };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/products/${encodeURIComponent(productId.trim())}/deactivate`,
      {
        method: "PUT",
      }
    );

    if (response.status === 200) {
      revalidatePath("/products");
      revalidatePath(`/products/${encodeURIComponent(productId.trim())}`);
      return { success: true };
    }

    if (response.status === 400) {
      const errorJson = await response.json().catch(() => null);
      return {
        success: false,
        error: errorJson?.detail || "該商品已經處於停用下架狀態。",
      };
    }

    if (response.status === 401) {
      return {
        success: false,
        error: "未授權或登入已逾期，請重新登入。",
      };
    }

    if (response.status === 403) {
      return {
        success: false,
        error: "權限不足，僅系統管理員（Admin）可執行商品停用下架。",
      };
    }

    if (response.status === 404) {
      revalidatePath("/products");
      return {
        success: false,
        error: "指定的商品已不存在。",
      };
    }

    if (response.status === 409) {
      return {
        success: false,
        error: "商品已被其他操作修改，請重新整理後確認最新狀態。",
      };
    }

    return {
      success: false,
      error: "停用商品失敗，請稍後重試。",
    };
  } catch (error) {
    if (error instanceof Error && error.message.includes("Unauthorized")) {
      return {
        success: false,
        error: "未授權或登入已逾期，請重新登入。",
      };
    }

    return {
      success: false,
      error: "伺服器通訊錯誤，無法完成商品停用。",
    };
  }
}

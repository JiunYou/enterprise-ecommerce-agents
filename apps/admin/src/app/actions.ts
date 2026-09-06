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

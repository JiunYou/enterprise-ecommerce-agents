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

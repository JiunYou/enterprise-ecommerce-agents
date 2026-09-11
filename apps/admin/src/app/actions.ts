"use server";

import { revalidatePath } from "next/cache";
import { authenticatedFetch } from "@/lib/authenticated-api";

export interface ShipOrderResult {
  success: boolean;
  error?: string;
}

export async function shipOrderAction(
  orderId: string,
  carrier: string,
  trackingNumber: string
): Promise<ShipOrderResult> {
  if (!orderId || typeof orderId !== "string" || orderId.trim() === "") {
    return { success: false, error: "無效的訂單編號。" };
  }

  if (typeof carrier !== "string") {
    return { success: false, error: "物流業者必須為文字。" };
  }

  const trimmedCarrier = carrier.trim();
  if (trimmedCarrier.length === 0) {
    return { success: false, error: "請填寫物流業者名稱。" };
  }

  if (trimmedCarrier.length > 100) {
    return { success: false, error: "物流業者名稱長度不可超過 100 個字元。" };
  }

  // 檢查是否含有控制字元
  if (/[\x00-\x1F\x7F]/.test(trimmedCarrier)) {
    return { success: false, error: "物流業者名稱不可包含控制字元。" };
  }

  if (typeof trackingNumber !== "string") {
    return { success: false, error: "物流追蹤單號必須為文字。" };
  }

  const trimmedTrackingNumber = trackingNumber.trim();
  if (trimmedTrackingNumber.length === 0) {
    return { success: false, error: "請填寫物流追蹤單號。" };
  }

  if (trimmedTrackingNumber.length > 100) {
    return { success: false, error: "物流追蹤單號長度不可超過 100 個字元。" };
  }

  if (/[\x00-\x1F\x7F]/.test(trimmedTrackingNumber)) {
    return { success: false, error: "物流追蹤單號不可包含控制字元。" };
  }

  try {
    const response = await authenticatedFetch(`/api/v1/orders/${encodeURIComponent(orderId.trim())}/ship`, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        carrier: trimmedCarrier,
        trackingNumber: trimmedTrackingNumber,
      }),
    });

    if (response.status === 200) {
      revalidatePath("/");
      revalidatePath(`/orders/${orderId.trim()}`);
      return { success: true };
    }

    if (response.status === 400) {
      const errorJson = await response.json().catch(() => null);
      revalidatePath("/");
      return {
        success: false,
        error: errorJson?.detail || "出貨資料無效、缺少收件地址，或訂單非可出貨狀態。",
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

    if (response.status === 409) {
      revalidatePath("/");
      revalidatePath(`/orders/${orderId.trim()}`);
      return {
        success: false,
        error: "訂單狀態已被並發變更，請重新整理頁面確認最新狀態。",
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

export interface UpdateProductNameResult {
  success: boolean;
  error?: string;
}

export async function updateProductNameAction(
  productId: string,
  newName: string
): Promise<UpdateProductNameResult> {
  if (!productId || typeof productId !== "string" || productId.trim() === "") {
    return { success: false, error: "無效的商品編號。" };
  }

  const trimmedName = typeof newName === "string" ? newName.trim() : "";
  if (!trimmedName || trimmedName.length > 255) {
    return { success: false, error: "商品名稱不可為空白且長度不可超過 255 個字元。" };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/products/${encodeURIComponent(productId.trim())}/name`,
      {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          newName: trimmedName,
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
        error: errorJson?.detail || "商品名稱無效或更新格式錯誤。",
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
        error: "權限不足，僅系統管理員（Admin）可調整商品名稱。",
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
      error: "更新商品名稱失敗，請稍後重試。",
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
      error: "伺服器通訊錯誤，無法完成名稱更新。",
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

export interface ReactivateProductResult {
  success: boolean;
  error?: string;
}

export async function reactivateProductAction(
  productId: string
): Promise<ReactivateProductResult> {
  if (!productId || typeof productId !== "string" || productId.trim() === "") {
    return { success: false, error: "無效的商品編號。" };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/products/${encodeURIComponent(productId.trim())}/reactivate`,
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
        error: errorJson?.detail || "該商品已經處於啟用上架狀態。",
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
        error: "權限不足，僅系統管理員（Admin）可執行商品重新啟用上架。",
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
      error: "重新啟用商品失敗，請稍後重試。",
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
      error: "伺服器通訊錯誤，無法完成商品重新啟用。",
    };
  }
}

export interface AdjustInventoryStockResult {
  success: boolean;
  error?: string;
}

export async function increaseInventoryStockAction(
  productId: string,
  quantity: number
): Promise<AdjustInventoryStockResult> {
  if (!productId || typeof productId !== "string" || productId.trim() === "") {
    return { success: false, error: "無效的商品編號。" };
  }

  if (typeof quantity !== "number" || !Number.isInteger(quantity) || quantity <= 0) {
    return { success: false, error: "調整數量必須為大於 0 的正整數。" };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/admin/products/${encodeURIComponent(productId.trim())}/inventory/increase`,
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ quantity }),
      }
    );

    if (response.status === 200) {
      revalidatePath(`/products/${encodeURIComponent(productId.trim())}`);
      return { success: true };
    }

    if (response.status === 400) {
      const errorJson = await response.json().catch(() => null);
      return {
        success: false,
        error: errorJson?.detail || "增加庫存請求參數不正確。",
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
        error: "權限不足，僅系統管理員（Admin）可執行庫存調整。",
      };
    }

    if (response.status === 404) {
      revalidatePath(`/products/${encodeURIComponent(productId.trim())}`);
      return {
        success: false,
        error: "此商品尚未建立庫存紀錄或商品不存在。",
      };
    }

    if (response.status === 409) {
      return {
        success: false,
        error: "庫存已被其他操作修改，發生併發衝突，請重新整理後確認最新狀態。",
      };
    }

    return {
      success: false,
      error: "增加庫存失敗，請稍後重試。",
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
      error: "伺服器通訊錯誤，無法完成庫存增加。",
    };
  }
}

export async function decreaseInventoryStockAction(
  productId: string,
  quantity: number
): Promise<AdjustInventoryStockResult> {
  if (!productId || typeof productId !== "string" || productId.trim() === "") {
    return { success: false, error: "無效的商品編號。" };
  }

  if (typeof quantity !== "number" || !Number.isInteger(quantity) || quantity <= 0) {
    return { success: false, error: "調整數量必須為大於 0 的正整數。" };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/admin/products/${encodeURIComponent(productId.trim())}/inventory/decrease`,
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ quantity }),
      }
    );

    if (response.status === 200) {
      revalidatePath(`/products/${encodeURIComponent(productId.trim())}`);
      return { success: true };
    }

    if (response.status === 400) {
      const errorJson = await response.json().catch(() => null);
      return {
        success: false,
        error: errorJson?.detail || "扣減數量不可大於可用庫存，且數量須為正整數。",
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
        error: "權限不足，僅系統管理員（Admin）可執行庫存調整。",
      };
    }

    if (response.status === 404) {
      revalidatePath(`/products/${encodeURIComponent(productId.trim())}`);
      return {
        success: false,
        error: "此商品尚未建立庫存紀錄或商品不存在。",
      };
    }

    if (response.status === 409) {
      return {
        success: false,
        error: "庫存已被其他操作修改，發生併發衝突，請重新整理後確認最新狀態。",
      };
    }

    return {
      success: false,
      error: "扣減庫存失敗，請稍後重試。",
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
      error: "伺服器通訊錯誤，無法完成庫存扣減。",
    };
  }
}

export interface CreateProductInput {
  name: string;
  sku: string;
  price: number;
  currency: string;
  initialStock: number;
}

export interface CreateProductResult {
  success: boolean;
  productId?: string;
  error?: string;
}

export async function createProductAction(
  input: CreateProductInput
): Promise<CreateProductResult> {
  if (!input || typeof input !== "object") {
    return { success: false, error: "無效的請求資料。" };
  }

  const name = typeof input.name === "string" ? input.name.trim() : "";
  if (!name || name.length > 255) {
    return { success: false, error: "商品名稱為必填且不可超過 255 個字元。" };
  }

  const sku = typeof input.sku === "string" ? input.sku.trim() : "";
  if (!sku || sku.length > 100) {
    return { success: false, error: "商品 SKU 為必填且不可超過 100 個字元。" };
  }

  if (typeof input.price !== "number" || isNaN(input.price) || input.price <= 0) {
    return { success: false, error: "商品價格必須大於 0。" };
  }

  const currency = typeof input.currency === "string" ? input.currency.trim().toUpperCase() : "";
  if (!currency || currency.length !== 3) {
    return { success: false, error: "幣別代碼必須為 3 碼英文字母（例如 TWD, USD）。" };
  }

  if (typeof input.initialStock !== "number" || !Number.isInteger(input.initialStock) || input.initialStock <= 0) {
    return { success: false, error: "初始庫存必須為大於 0 的整數。" };
  }

  try {
    const response = await authenticatedFetch("/api/v1/products", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        name,
        sku,
        price: input.price,
        currency,
        initialStock: input.initialStock,
      }),
    });

    if (response.status === 201) {
      const productId = await response.json();
      revalidatePath("/products");
      if (productId && typeof productId === "string") {
        revalidatePath(`/products/${encodeURIComponent(productId)}`);
      }
      return { success: true, productId };
    }

    if (response.status === 400) {
      const errorJson = await response.json().catch(() => null);
      return {
        success: false,
        error: errorJson?.detail || errorJson?.title || "商品建立資料無效，請檢查後重試。",
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
        error: "權限不足，僅系統管理員（Admin）可建立商品。",
      };
    }

    if (response.status === 409) {
      return {
        success: false,
        error: "此 SKU 商品已存在，請使用不同的 SKU。",
      };
    }

    return {
      success: false,
      error: "建立商品失敗，請稍後重試。",
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
      error: "伺服器通訊錯誤，無法完成商品建立。",
    };
  }
}

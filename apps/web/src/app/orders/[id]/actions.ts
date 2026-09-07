"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { initiatePayment } from "@/lib/payments";
import { cancelOrder } from "@/lib/orders";

export type StartOrderPaymentResult =
  | { success: false; error: string }
  | {
      success: true;
      method: "POST";
      actionUrl: string;
      formFields: Record<string, string>;
    };

export async function startOrderPayment(orderId: string): Promise<StartOrderPaymentResult | undefined> {
  const result = await initiatePayment(orderId);

  if (!result.success) {
    return { success: false, error: result.error };
  }

  if (result.method === "GET") {
    // 伺服器端重定向至經過驗證的 Hosted Checkout URL
    redirect(result.actionUrl);
  }

  // POST 發起：回傳由後端簽章後的啟動指示供客戶端安全表單跳轉
  return {
    success: true,
    method: "POST",
    actionUrl: result.actionUrl,
    formFields: result.formFields ?? {},
  };
}

export type CancelCustomerOrderResult =
  | { success: true }
  | { success: false; error: string };

export async function cancelCustomerOrder(orderId: string): Promise<CancelCustomerOrderResult> {
  const trimmedId = typeof orderId === "string" ? orderId.trim() : "";
  if (!trimmedId) {
    return { success: false, error: "無效的訂單識別碼" };
  }

  const result = await cancelOrder(trimmedId);

  if (!result.success) {
    return {
      success: false,
      error: result.error,
    };
  }

  revalidatePath(`/orders/${encodeURIComponent(trimmedId)}`);
  revalidatePath("/orders");

  return { success: true };
}

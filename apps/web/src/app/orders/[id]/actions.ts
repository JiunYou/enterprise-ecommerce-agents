"use server";

import { redirect } from "next/navigation";
import { initiatePayment } from "@/lib/payments";

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

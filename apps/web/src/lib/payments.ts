import "server-only";
import { authenticatedFetch } from "@/lib/authenticated-api";

export type PaymentLaunchMethod = "GET" | "POST";

export interface InitiatePaymentResponseDto {
  providerTransactionId?: string | null;
  actionUrl: string;
  method: PaymentLaunchMethod;
  formFields?: Record<string, string> | null;
}

export type InitiatePaymentResult =
  | {
      success: true;
      actionUrl: string;
      method: PaymentLaunchMethod;
      formFields?: Record<string, string> | null;
      providerTransactionId?: string | null;
    }
  | { success: false; error: string };


/**
 * 驗證並確保 POST 提交目標為受信任的綠界科技 ECPay Stage Hosted Payment 安全網址
 * 依據 P4 最小安全原則，嚴格限定 scheme 為 https、hostname 為 payment-stage.ecpay.com.tw、path 為 /Cashier/AioCheckOut/V5
 */
export function isValidECPayStageActionUrl(urlStr: string): boolean {
  try {
    const parsed = new URL(urlStr);
    if (parsed.protocol !== "https:") {
      return false;
    }

    const hostname = parsed.hostname.toLowerCase();
    if (hostname !== "payment-stage.ecpay.com.tw") {
      return false;
    }

    if (parsed.pathname !== "/Cashier/AioCheckOut/V5") {
      return false;
    }

    if (parsed.search || parsed.hash) {
      return false;
    }

    return true;
  } catch {
    return false;
  }
}

/**
 * 發起付款請求並取得中立結帳啟動指示 (GET 或 POST)
 * 嚴格透過伺服器端憑證呼叫 WebApi，客戶端絕不接觸 Token、卡號或金鑰
 */
export async function initiatePayment(orderId: string): Promise<InitiatePaymentResult> {
  try {
    const idempotencyKey = crypto.randomUUID();

    const response = await authenticatedFetch("/api/v1/payments/initiate", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        orderId,
        idempotencyKey,
      }),
    });

    if (response.status === 401 || response.status === 403) {
      return {
        success: false,
        error: "您的登入憑證已過期或沒有權限，請重新登入",
      };
    }

    if (response.status === 404) {
      return {
        success: false,
        error: "找不到該筆訂單或該訂單不屬於當前帳號",
      };
    }

    if (!response.ok) {
      try {
        const problem = await response.json();
        return {
          success: false,
          error: problem.detail || problem.title || `付款發起失敗 (HTTP ${response.status})`,
        };
      } catch {
        return {
          success: false,
          error: `付款發起失敗 (HTTP ${response.status})`,
        };
      }
    }

    const data = await response.json();
    const actionUrl = data.actionUrl;
    const method: PaymentLaunchMethod = data.method === "POST" ? "POST" : "GET";
    const formFields = data.formFields ?? null;
    const providerTransactionId = data.providerTransactionId ?? null;

    if (!actionUrl || typeof actionUrl !== "string") {
      return {
        success: false,
        error: "後端服務未回傳有效的結帳跳轉網址",
      };
    }

    // 依啟動方法進行嚴格目標網址安全性檢驗
    if (method === "POST") {
      if (!isValidECPayStageActionUrl(actionUrl)) {
        return {
          success: false,
          error: "安全性檢查未通過：結帳發送網址並非受信任的綠界科技 Stage 網域或路徑",
        };
      }
    } else {
      return {
        success: false,
        error: "目前僅支援綠界科技安全表單結帳",
      };
    }

    return {
      success: true,
      actionUrl,
      method,
      formFields,
      providerTransactionId,
    };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : "連線異常";
    return {
      success: false,
      error: `發起付款連線失敗：${message}`,
    };
  }
}

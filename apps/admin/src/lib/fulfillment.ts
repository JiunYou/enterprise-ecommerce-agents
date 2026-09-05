import "server-only";
import { authenticatedFetch } from "@/lib/authenticated-api";

export interface OrderItem {
  productId: string;
  unitPrice: number;
  currency: string;
  quantity: number;
  totalPrice: number;
}

export interface ShippingAddress {
  recipientName: string;
  phone: string;
  countryCode: string;
  postalCode: string;
  city: string;
  addressLine1: string;
  addressLine2?: string | null;
}

export interface FulfillmentOrder {
  id: string;
  customerId: string;
  status: string;
  currency: string;
  totalAmount: number;
  items: OrderItem[];
  shippingAddress: ShippingAddress | null;
}

export type GetFulfillmentResult =
  | { status: "unauthenticated" }
  | { status: "forbidden" }
  | { status: "error"; message: string }
  | { status: "success"; orders: FulfillmentOrder[] };

export async function getFulfillmentQueue(
  limit: number = 50
): Promise<GetFulfillmentResult> {
  try {
    const response = await authenticatedFetch(
      `/api/v1/orders/fulfillment?limit=${limit}`,
      {
        cache: "no-store",
      }
    );

    if (response.status === 401) {
      return { status: "unauthenticated" };
    }

    if (response.status === 403) {
      return { status: "forbidden" };
    }

    if (!response.ok) {
      return {
        status: "error",
        message: `後端服務回應錯誤 (HTTP ${response.status})。`,
      };
    }

    const orders: FulfillmentOrder[] = await response.json();
    return { status: "success", orders };
  } catch (error) {
    if (
      error instanceof Error &&
      error.message.includes("Unauthorized")
    ) {
      return { status: "unauthenticated" };
    }

    return {
      status: "error",
      message: "無法與後端伺服器建立連線或授權驗證失敗。",
    };
  }
}

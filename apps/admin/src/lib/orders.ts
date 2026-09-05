import "server-only";
import { authenticatedFetch } from "@/lib/authenticated-api";

export interface AdminOrderSummary {
  id: string;
  customerId: string;
  status: string;
  currency: string;
  totalAmount: number;
  submittedAt: string | null;
}

export interface AdminOrderPageResponse {
  items: AdminOrderSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

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

export interface AdminOrderDetail {
  id: string;
  customerId: string;
  status: string;
  currency: string;
  totalAmount: number;
  submittedAt: string | null;
  items: OrderItem[];
  shippingAddress: ShippingAddress | null;
}

export type GetAdminOrdersResult =
  | { status: "unauthenticated" }
  | { status: "forbidden" }
  | { status: "badRequest"; message: string }
  | { status: "error"; message: string }
  | { status: "success"; data: AdminOrderPageResponse };

export type GetAdminOrderDetailResult =
  | { status: "unauthenticated" }
  | { status: "forbidden" }
  | { status: "notFound" }
  | { status: "error"; message: string }
  | { status: "success"; order: AdminOrderDetail };

const ALLOWED_STATUSES = ["Pending", "Submitted", "Paid", "Shipped", "Cancelled"] as const;

export async function getAdminOrders(params: {
  page?: number;
  pageSize?: number;
  status?: string;
  orderId?: string;
}): Promise<GetAdminOrdersResult> {
  try {
    const searchParams = new URLSearchParams();

    if (params.page !== undefined && params.page > 0) {
      searchParams.set("page", params.page.toString());
    }

    if (params.pageSize !== undefined && params.pageSize > 0) {
      searchParams.set("pageSize", params.pageSize.toString());
    }

    if (params.status && params.status.trim() !== "") {
      const trimmedStatus = params.status.trim();
      const matched = ALLOWED_STATUSES.find(
        (s) => s.toLowerCase() === trimmedStatus.toLowerCase()
      );
      if (matched) {
        searchParams.set("status", matched);
      } else {
        searchParams.set("status", trimmedStatus);
      }
    }

    if (params.orderId && params.orderId.trim() !== "") {
      searchParams.set("orderId", params.orderId.trim());
    }

    const query = searchParams.toString();
    const endpoint = query ? `/api/v1/admin/orders?${query}` : "/api/v1/admin/orders";

    const response = await authenticatedFetch(endpoint, {
      cache: "no-store",
    });

    if (response.status === 401) {
      return { status: "unauthenticated" };
    }

    if (response.status === 403) {
      return { status: "forbidden" };
    }

    if (response.status === 400) {
      return {
        status: "badRequest",
        message: "查詢參數無效或訂單狀態不存在，請確認篩選條件。",
      };
    }

    if (!response.ok) {
      return {
        status: "error",
        message: `後端服務回應錯誤 (HTTP ${response.status})。`,
      };
    }

    const data: AdminOrderPageResponse = await response.json();
    return { status: "success", data };
  } catch (error) {
    if (error instanceof Error && error.message.includes("Unauthorized")) {
      return { status: "unauthenticated" };
    }

    return {
      status: "error",
      message: "無法與後端伺服器建立連線或授權驗證失敗。",
    };
  }
}

export async function getAdminOrderById(
  id: string
): Promise<GetAdminOrderDetailResult> {
  try {
    if (!id || id.trim() === "") {
      return { status: "notFound" };
    }

    const endpoint = `/api/v1/admin/orders/${encodeURIComponent(id.trim())}`;
    const response = await authenticatedFetch(endpoint, {
      cache: "no-store",
    });

    if (response.status === 401) {
      return { status: "unauthenticated" };
    }

    if (response.status === 403) {
      return { status: "forbidden" };
    }

    if (response.status === 404) {
      return { status: "notFound" };
    }

    if (!response.ok) {
      return {
        status: "error",
        message: `後端服務回應錯誤 (HTTP ${response.status})。`,
      };
    }

    const order: AdminOrderDetail = await response.json();
    return { status: "success", order };
  } catch (error) {
    if (error instanceof Error && error.message.includes("Unauthorized")) {
      return { status: "unauthenticated" };
    }

    return {
      status: "error",
      message: "無法與後端伺服器建立連線或授權驗證失敗。",
    };
  }
}

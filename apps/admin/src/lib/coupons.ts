import "server-only";
import { authenticatedFetch } from "@/lib/authenticated-api";

export interface AdminCoupon {
  id: string;
  code: string;
  discountAmount: number;
  currency: string;
  startsAt: string;
  expiresAt: string;
  isActive: boolean;
  createdAt: string;
}

export type GetAdminCouponsResult =
  | { status: "success"; data: AdminCoupon[] }
  | { status: "unauthenticated" }
  | { status: "forbidden" }
  | { status: "error"; message: string };

export async function getAdminCoupons(): Promise<GetAdminCouponsResult> {
  try {
    const response = await authenticatedFetch("/api/v1/admin/coupons", {
      cache: "no-store",
    });

    if (response.status === 401) {
      return { status: "unauthenticated" };
    }

    if (response.status === 403) {
      return { status: "forbidden" };
    }

    if (!response.ok) {
      return {
        status: "error",
        message: `取得優惠券列表失敗 (HTTP ${response.status})`,
      };
    }

    const data: AdminCoupon[] = await response.json();
    return { status: "success", data };
  } catch (err: unknown) {
    const errorMessage = err instanceof Error ? err.message : "連線錯誤";
    if (errorMessage.includes("Unauthorized")) {
      return { status: "unauthenticated" };
    }
    return {
      status: "error",
      message: "無法連線至優惠券服務，請稍後再試。",
    };
  }
}

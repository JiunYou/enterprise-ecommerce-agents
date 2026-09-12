import "server-only";
import { authenticatedFetch } from "@/lib/authenticated-api";

export interface AdminProductSummary {
  id: string;
  name: string;
  sku: string;
  price: number;
  currency: string;
  isActive: boolean;
}

export interface AdminProductDetail extends AdminProductSummary {
  description: string;
}

export interface AdminProductPageResponse {
  items: AdminProductSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export type GetAdminProductsResult =
  | { status: "unauthenticated" }
  | { status: "forbidden" }
  | { status: "badRequest"; message: string }
  | { status: "error"; message: string }
  | { status: "success"; data: AdminProductPageResponse };

export type GetAdminProductByIdResult =
  | { status: "unauthenticated" }
  | { status: "forbidden" }
  | { status: "notFound" }
  | { status: "error"; message: string }
  | { status: "success"; product: AdminProductDetail };

export async function getAdminProducts(params: {
  page?: number;
  pageSize?: number;
  onlyActive?: boolean;
  searchTerm?: string;
  sortBy?: string;
  sortOrder?: string;
}): Promise<GetAdminProductsResult> {
  try {
    const searchParams = new URLSearchParams();

    if (params.page !== undefined && params.page > 0) {
      searchParams.set("page", params.page.toString());
    }

    if (params.pageSize !== undefined && params.pageSize > 0) {
      searchParams.set("pageSize", params.pageSize.toString());
    }

    if (params.onlyActive !== undefined) {
      searchParams.set("onlyActive", params.onlyActive.toString());
    }

    if (params.searchTerm && params.searchTerm.trim() !== "") {
      searchParams.set("searchTerm", params.searchTerm.trim());
    }

    if (params.sortBy && params.sortBy.trim() !== "") {
      searchParams.set("sortBy", params.sortBy.trim().toLowerCase());
    }

    if (params.sortOrder && params.sortOrder.trim() !== "") {
      searchParams.set("sortOrder", params.sortOrder.trim().toLowerCase());
    }

    const endpoint = `/api/v1/admin/products${
      searchParams.toString() ? `?${searchParams.toString()}` : ""
    }`;

    const response = await authenticatedFetch(endpoint);

    if (response.status === 401) {
      return { status: "unauthenticated" };
    }

    if (response.status === 403) {
      return { status: "forbidden" };
    }

    if (response.status === 400) {
      const errorJson = await response.json().catch(() => null);
      return {
        status: "badRequest",
        message: errorJson?.detail || "查詢參數格式錯誤，請檢查後重新整理。",
      };
    }

    if (!response.ok) {
      return {
        status: "error",
        message: `商品服務回應錯誤 (${response.status})。`,
      };
    }

    const data: AdminProductPageResponse = await response.json();
    return { status: "success", data };
  } catch (error) {
    if (error instanceof Error && error.message.includes("Unauthorized")) {
      return { status: "unauthenticated" };
    }

    return {
      status: "error",
      message: "無法與後端商品服務建立安全連線，請稍後重試。",
    };
  }
}

export async function getAdminProductById(
  id: string
): Promise<GetAdminProductByIdResult> {
  if (!id || typeof id !== "string") {
    return { status: "notFound" };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/admin/products/${encodeURIComponent(id)}`
    );

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
        message: `商品服務回應錯誤 (${response.status})。`,
      };
    }

    const product: AdminProductDetail = await response.json();
    return { status: "success", product };
  } catch (error) {
    if (error instanceof Error && error.message.includes("Unauthorized")) {
      return { status: "unauthenticated" };
    }

    return {
      status: "error",
      message: "無法與後端商品服務建立安全連線，請稍後重試。",
    };
  }
}

export interface AdminInventorySummary {
  productId: string;
  availableQuantity: number;
  reservedQuantity: number;
}

export type GetAdminInventoryResult =
  | { status: "unauthenticated" }
  | { status: "forbidden" }
  | { status: "notFound" }
  | { status: "error"; message: string }
  | { status: "success"; inventory: AdminInventorySummary };

export async function getAdminInventory(
  productId: string
): Promise<GetAdminInventoryResult> {
  if (!productId || typeof productId !== "string") {
    return { status: "notFound" };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/admin/products/${encodeURIComponent(productId)}/inventory`
    );

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
        message: `庫存服務回應錯誤 (${response.status})。`,
      };
    }

    const inventory: AdminInventorySummary = await response.json();
    return { status: "success", inventory };
  } catch (error) {
    if (error instanceof Error && error.message.includes("Unauthorized")) {
      return { status: "unauthenticated" };
    }

    return {
      status: "error",
      message: "無法與後端庫存服務建立安全連線，請稍後重試。",
    };
  }
}

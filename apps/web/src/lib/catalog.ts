export interface Product {
  id: string;
  name: string;
  sku: string;
  price: number;
  currency: string;
  isActive: boolean;
  imageUrl: string;
  category: string;
}

export interface ProductDetail extends Product {
  description: string;
}

export interface PagedList<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export type CatalogResult =
  | { success: true; data: PagedList<Product> }
  | { success: false; error: string };

export type ProductDetailResult =
  | { success: true; data: ProductDetail }
  | { success: false; notFound: true }
  | { success: false; notFound: false; error: string };

export interface GetProductsParams {
  page?: number;
  pageSize?: number;
  searchTerm?: string;
  sortBy?: "name" | "price";
  sortOrder?: "asc" | "desc";
  category?: string;
}

export async function getProducts(
  params: GetProductsParams = {}
): Promise<CatalogResult> {
  const baseUrl = process.env.API_BASE_URL || "http://localhost:5110";

  let url: URL;
  try {
    url = new URL("/api/v1/products", baseUrl);
  } catch {
    return {
      success: false,
      error: "無效的 API 端點設定",
    };
  }

  if (params.page && params.page > 0) {
    url.searchParams.set("page", params.page.toString());
  }

  if (params.pageSize && params.pageSize > 0) {
    url.searchParams.set("pageSize", params.pageSize.toString());
  }

  if (params.searchTerm && params.searchTerm.trim().length > 0) {
    url.searchParams.set("searchTerm", params.searchTerm.trim());
  }

  if (params.sortBy) {
    url.searchParams.set("sortBy", params.sortBy);
  }

  if (params.sortOrder) {
    url.searchParams.set("sortOrder", params.sortOrder);
  }

  if (params.category && params.category.trim().length > 0) {
    url.searchParams.set("category", params.category.trim());
  }

  try {
    const response = await fetch(url.toString(), {
      cache: "no-store",
      headers: {
        Accept: "application/json",
      },
    });

    if (!response.ok) {
      return {
        success: false,
        error: `服務端回應錯誤 (HTTP ${response.status})`,
      };
    }

    const data: PagedList<Product> = await response.json();
    return {
      success: true,
      data,
    };
  } catch {
    return {
      success: false,
      error: "目前無法連線至商品目錄服務，請確認後端服務是否已啟動。",
    };
  }
}

export async function getProductById(id: string): Promise<ProductDetailResult> {
  const trimmedId = id?.trim();
  if (!trimmedId) {
    return {
      success: false,
      notFound: true,
    };
  }

  const baseUrl = process.env.API_BASE_URL || "http://localhost:5110";

  let url: URL;
  try {
    url = new URL(`/api/v1/products/${encodeURIComponent(trimmedId)}`, baseUrl);
  } catch {
    return {
      success: false,
      notFound: false,
      error: "無效的 API 端點設定",
    };
  }

  try {
    const response = await fetch(url.toString(), {
      cache: "no-store",
      headers: {
        Accept: "application/json",
      },
    });

    if (response.status === 404) {
      return {
        success: false,
        notFound: true,
      };
    }

    if (!response.ok) {
      return {
        success: false,
        notFound: false,
        error: `服務端回應錯誤 (HTTP ${response.status})`,
      };
    }

    const data: ProductDetail = await response.json();
    return {
      success: true,
      data,
    };
  } catch {
    return {
      success: false,
      notFound: false,
      error: "目前無法連線至商品服務，請確認後端服務是否已啟動。",
    };
  }
}

export async function getProductCategories(): Promise<string[]> {
  const baseUrl = process.env.API_BASE_URL || "http://localhost:5110";

  let url: URL;
  try {
    url = new URL("/api/v1/products/categories", baseUrl);
  } catch {
    return [];
  }

  try {
    const response = await fetch(url.toString(), {
      cache: "no-store",
      headers: {
        Accept: "application/json",
      },
    });

    if (!response.ok) {
      return [];
    }

    const data: string[] = await response.json();
    return Array.isArray(data) ? data : [];
  } catch {
    return [];
  }
}

export interface ProductAvailability {
  productId: string;
  availableQuantity: number;
  inStock: boolean;
}

export type ProductAvailabilityResult =
  | { success: true; data: ProductAvailability }
  | { success: false; error: string };

export async function getProductAvailability(
  productId: string
): Promise<ProductAvailabilityResult> {
  const trimmedId = productId?.trim();
  if (!trimmedId) {
    return {
      success: false,
      error: "無效的商品識別碼",
    };
  }

  const baseUrl = process.env.API_BASE_URL || "http://localhost:5110";

  let url: URL;
  try {
    url = new URL(`/api/v1/products/${encodeURIComponent(trimmedId)}/availability`, baseUrl);
  } catch {
    return {
      success: false,
      error: "無效的 API 端點設定",
    };
  }

  try {
    const response = await fetch(url.toString(), {
      cache: "no-store",
      headers: {
        Accept: "application/json",
      },
    });

    if (!response.ok) {
      return {
        success: false,
        error: `服務端回應錯誤 (HTTP ${response.status})`,
      };
    }

    const data: ProductAvailability = await response.json();
    return {
      success: true,
      data,
    };
  } catch {
    return {
      success: false,
      error: "目前無法連線至商品庫存服務，請確認後端服務是否已啟動。",
    };
  }
}

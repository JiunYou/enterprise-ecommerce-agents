export interface Product {
  id: string;
  name: string;
  sku: string;
  price: number;
  currency: string;
  isActive: boolean;
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
  | { success: true; data: Product }
  | { success: false; notFound: true }
  | { success: false; notFound: false; error: string };

export interface GetProductsParams {
  page?: number;
  pageSize?: number;
  searchTerm?: string;
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

    const data: Product = await response.json();
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

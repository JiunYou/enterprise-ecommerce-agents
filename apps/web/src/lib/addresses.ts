import "server-only";
import { auth0 } from "@/lib/auth0";
import { authenticatedFetch } from "@/lib/authenticated-api";

export interface CustomerAddress {
  id: string;
  recipientName: string;
  phone: string;
  countryCode: string;
  postalCode: string;
  city: string;
  addressLine1: string;
  addressLine2?: string | null;
  isDefault: boolean;
}

export interface CreateCustomerAddressPayload {
  recipientName: string;
  phone: string;
  countryCode: string;
  postalCode: string;
  city: string;
  addressLine1: string;
  addressLine2?: string | null;
}

export type UpdateCustomerAddressPayload = CreateCustomerAddressPayload;

export type AddressListResult =
  | { success: true; data: CustomerAddress[] }
  | { success: false; unauthorized?: boolean; error?: string };

export type CreateAddressResult =
  | { success: true; data: CustomerAddress }
  | { success: false; unauthorized?: boolean; error?: string };

export type UpdateAddressResult =
  | { success: true; data: CustomerAddress }
  | { success: false; unauthorized?: boolean; error?: string };

export type DeleteAddressResult =
  | { success: true }
  | { success: false; unauthorized?: boolean; error?: string };

export type SetDefaultAddressResult =
  | { success: true; data: CustomerAddress }
  | { success: false; unauthorized?: boolean; error?: string };

/**
 * 取得顧客的所有已儲存收件地址
 */
export async function getCustomerAddresses(): Promise<AddressListResult> {
  const session = await auth0.getSession();
  if (!session) {
    return { success: false, unauthorized: true, error: "請先登入後再查看已儲存地址" };
  }

  try {
    const response = await authenticatedFetch("/api/v1/customer/addresses", {
      method: "GET",
      headers: {
        Accept: "application/json",
      },
    });

    if (!response.ok) {
      if (response.status === 401) {
        return { success: false, unauthorized: true, error: "登入逾期，請重新登入" };
      }
      if (response.status === 403) {
        return { success: false, error: "身分驗證失敗，無法存取地址資料" };
      }
      return { success: false, error: "無法載入已儲存地址，請稍後再試" };
    }

    const data = (await response.json()) as CustomerAddress[];
    return { success: true, data };
  } catch {
    return { success: false, error: "無法載入已儲存地址，請稍後再試" };
  }
}

/**
 * 建立新的已儲存收件地址
 */
export async function createCustomerAddress(
  payload: CreateCustomerAddressPayload
): Promise<CreateAddressResult> {
  const session = await auth0.getSession();
  if (!session) {
    return { success: false, unauthorized: true, error: "請先登入後再建立收件地址" };
  }

  try {
    const response = await authenticatedFetch("/api/v1/customer/addresses", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
      },
      body: JSON.stringify({
        recipientName: payload.recipientName,
        phone: payload.phone,
        countryCode: payload.countryCode,
        postalCode: payload.postalCode,
        city: payload.city,
        addressLine1: payload.addressLine1,
        addressLine2: payload.addressLine2 || null,
      }),
    });

    if (!response.ok) {
      if (response.status === 401) {
        return { success: false, unauthorized: true, error: "登入逾期，請重新登入" };
      }
      if (response.status === 403) {
        return { success: false, error: "身分驗證失敗，無法儲存地址" };
      }
      if (response.status === 400) {
        try {
          const problem = await response.json();
          return { success: false, error: problem.detail || "地址資料不符合格式要求" };
        } catch {
          return { success: false, error: "地址資料不符合格式要求" };
        }
      }
      return { success: false, error: "儲存地址失敗，請稍後再試" };
    }

    const data = (await response.json()) as CustomerAddress;
    return { success: true, data };
  } catch {
    return { success: false, error: "儲存地址失敗，請稍後再試" };
  }
}

/**
 * 刪除顧客的已儲存收件地址
 */
export async function deleteCustomerAddress(
  addressId: string
): Promise<DeleteAddressResult> {
  const session = await auth0.getSession();
  if (!session) {
    return { success: false, unauthorized: true, error: "請先登入後再執行刪除" };
  }

  try {
    const response = await authenticatedFetch(`/api/v1/customer/addresses/${encodeURIComponent(addressId)}`, {
      method: "DELETE",
    });

    if (!response.ok) {
      if (response.status === 401) {
        return { success: false, unauthorized: true, error: "登入逾期，請重新登入" };
      }
      if (response.status === 403) {
        return { success: false, error: "身分驗證失敗，無法刪除地址" };
      }
      if (response.status === 404) {
        return { success: false, error: "找不到指定的收件地址" };
      }
      return { success: false, error: "刪除地址失敗，請稍後再試" };
    }

    return { success: true };
  } catch {
    return { success: false, error: "刪除地址失敗，請稍後再試" };
  }
}

/**
 * 更新顧客已儲存收件地址
 */
export async function updateCustomerAddress(
  addressId: string,
  payload: UpdateCustomerAddressPayload
): Promise<UpdateAddressResult> {
  const session = await auth0.getSession();
  if (!session) {
    return { success: false, unauthorized: true, error: "請先登入後再更新收件地址" };
  }

  try {
    const response = await authenticatedFetch(`/api/v1/customer/addresses/${encodeURIComponent(addressId)}`, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
      },
      body: JSON.stringify({
        recipientName: payload.recipientName,
        phone: payload.phone,
        countryCode: payload.countryCode,
        postalCode: payload.postalCode,
        city: payload.city,
        addressLine1: payload.addressLine1,
        addressLine2: payload.addressLine2 || null,
      }),
    });

    if (!response.ok) {
      if (response.status === 401) {
        return { success: false, unauthorized: true, error: "登入逾期，請重新登入" };
      }
      if (response.status === 403) {
        return { success: false, error: "身分驗證失敗，無法更新地址" };
      }
      if (response.status === 404) {
        return { success: false, error: "找不到指定的收件地址，請重新整理後再試。" };
      }
      if (response.status === 400) {
        try {
          const problem = await response.json();
          return { success: false, error: problem.detail || "地址資料不符合格式要求" };
        } catch {
          return { success: false, error: "地址資料不符合格式要求" };
        }
      }
      return { success: false, error: "更新地址失敗，請稍後再試" };
    }

    const data = (await response.json()) as CustomerAddress;
    return { success: true, data };
  } catch {
    return { success: false, error: "更新地址失敗，請稍後再試" };
  }
}

/**
 * 設定顧客的指定收件地址為預設地址
 */
export async function setDefaultCustomerAddress(
  addressId: string
): Promise<SetDefaultAddressResult> {
  const session = await auth0.getSession();
  if (!session) {
    return { success: false, unauthorized: true, error: "請先登入後再設定預設地址" };
  }

  try {
    const response = await authenticatedFetch(
      `/api/v1/customer/addresses/${encodeURIComponent(addressId)}/default`,
      {
        method: "PUT",
        headers: {
          Accept: "application/json",
        },
      }
    );

    if (!response.ok) {
      if (response.status === 401) {
        return { success: false, unauthorized: true, error: "登入逾期，請重新登入" };
      }
      if (response.status === 403) {
        return { success: false, error: "身分驗證失敗，無法設定預設地址" };
      }
      if (response.status === 404) {
        return { success: false, error: "找不到指定的收件地址，請重新整理後再試。" };
      }
      return { success: false, error: "設定預設地址失敗，請稍後再試" };
    }

    const data = (await response.json()) as CustomerAddress;
    return { success: true, data };
  } catch {
    return { success: false, error: "設定預設地址失敗，請稍後再試" };
  }
}


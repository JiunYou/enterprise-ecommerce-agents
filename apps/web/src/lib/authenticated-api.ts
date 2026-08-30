import "server-only";
import { auth0 } from "@/lib/auth0";

const API_BASE_URL = process.env.API_BASE_URL || "http://localhost:5110";

export async function authenticatedFetch(
  endpoint: string,
  init?: RequestInit
): Promise<Response> {
  const apiBase = new URL(API_BASE_URL);

  // endpoint 必須相對於 API_BASE_URL 解析
  const targetUrl = new URL(endpoint, apiBase);

  // 嚴格限制：目標 URL origin 必須與配置的 API_BASE_URL origin 完全一致
  if (targetUrl.origin !== apiBase.origin) {
    throw new Error(
      "Security violation: Cannot forward authenticated token to an untrusted external origin."
    );
  }

  const { token } = await auth0.getAccessToken();
  if (!token) {
    throw new Error("Unauthorized: No access token available in current session.");
  }

  const headers = new Headers(init?.headers);
  headers.set("Authorization", `Bearer ${token}`);

  return await fetch(targetUrl.toString(), {
    ...init,
    headers,
  });
}

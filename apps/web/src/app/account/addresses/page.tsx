import Link from "next/link";
import { revalidatePath } from "next/cache";
import {
  getCustomerAddresses,
  createCustomerAddress,
  updateCustomerAddress,
  deleteCustomerAddress,
  type CreateCustomerAddressPayload,
  type UpdateCustomerAddressPayload,
} from "@/lib/addresses";
import { CustomerHeader } from "@/components/CustomerHeader";
import { AddressManagementClient } from "./AddressManagementClient";

export default async function AccountAddressesPage() {
  const result = await getCustomerAddresses();

  async function handleCreateAddress(payload: CreateCustomerAddressPayload) {
    "use server";
    const res = await createCustomerAddress(payload);
    if (res.success) {
      revalidatePath("/account/addresses");
    }
    return res;
  }

  async function handleUpdateAddress(
    addressId: string,
    payload: UpdateCustomerAddressPayload
  ) {
    "use server";
    const res = await updateCustomerAddress(addressId, payload);
    if (res.success) {
      revalidatePath("/account/addresses");
    }
    return res;
  }

  async function handleDeleteAddress(addressId: string) {
    "use server";
    const res = await deleteCustomerAddress(addressId);
    if (res.success) {
      revalidatePath("/account/addresses");
    }
    return res;
  }

  return (
    <div className="min-h-screen bg-stone-50 text-stone-900 dark:bg-stone-950 dark:text-stone-100">
      <CustomerHeader subtitle="收件地址簿管理" />

      <main className="mx-auto max-w-4xl px-4 py-8 sm:px-6 lg:px-8">
        {/* 麵包屑與標題 H1 */}
        <div className="mb-8">
          <Link
            href="/"
            className="inline-flex items-center gap-1.5 text-sm font-medium text-stone-600 transition-colors hover:text-stone-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-sm dark:text-stone-400 dark:hover:text-stone-200"
          >
            <span aria-hidden="true">&larr;</span> 返回首頁
          </Link>

          <div className="mt-4 flex flex-col gap-1 sm:flex-row sm:items-baseline sm:justify-between">
            <h1 className="text-2xl font-bold tracking-tight text-stone-950 dark:text-stone-50 sm:text-3xl">
              收件地址簿
            </h1>
          </div>
          <p className="mt-1 text-sm text-stone-600 dark:text-stone-400">
            管理您的個人常用收件地址，在結帳時可直接選取並自動填入。
          </p>
        </div>

        {/* 驗證狀態 */}
        {!result.success ? (
          result.unauthorized ? (
            <section
              aria-label="需要登入"
              className="rounded-2xl border border-stone-200 bg-white p-8 text-center shadow-sm dark:border-stone-800 dark:bg-stone-900 sm:p-12"
            >
              <div
                aria-hidden="true"
                className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-stone-100 text-stone-400 dark:bg-stone-800 dark:text-stone-500"
              >
                <svg
                  className="h-6 w-6"
                  fill="none"
                  viewBox="0 0 24 24"
                  strokeWidth="1.5"
                  stroke="currentColor"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.501 20.118a7.5 7.5 0 0 1 14.998 0A17.933 17.933 0 0 1 12 21.75c-2.676 0-5.216-.584-7.499-1.632Z"
                  />
                </svg>
              </div>
              <h2 className="mt-4 text-lg font-semibold text-stone-950 dark:text-stone-50">
                請先登入以管理收件地址
              </h2>
              <p className="mx-auto mt-2 max-w-sm text-sm text-stone-500 dark:text-stone-400">
                地址簿屬於您的個人隱私資料，需要驗證身分後方可檢視或儲存。
              </p>
              <div className="mt-6">
                <a
                  href="/auth/login?returnTo=/account/addresses"
                  className="inline-flex min-h-[44px] items-center justify-center rounded-lg bg-stone-900 px-6 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
                >
                  登入會員
                </a>
              </div>
            </section>
          ) : (
            <section
              aria-label="系統錯誤訊息"
              className="rounded-2xl border border-red-200 bg-red-50/60 p-6 text-red-900 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-200 sm:p-8"
            >
              <h2 className="text-lg font-semibold">無法載入地址簿</h2>
              <p className="mt-1 text-sm">{result.error || "請稍後再試。"}</p>
            </section>
          )
        ) : (
          <AddressManagementClient
            initialAddresses={result.data}
            onCreateAddress={handleCreateAddress}
            onUpdateAddress={handleUpdateAddress}
            onDeleteAddress={handleDeleteAddress}
          />
        )}
      </main>
    </div>
  );
}

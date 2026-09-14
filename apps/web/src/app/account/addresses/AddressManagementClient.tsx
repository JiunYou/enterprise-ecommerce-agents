"use client";

import { useState, useTransition } from "react";
import type { CustomerAddress, CreateCustomerAddressPayload } from "@/lib/addresses";

interface AddressManagementClientProps {
  initialAddresses: CustomerAddress[];
  onCreateAddress: (
    payload: CreateCustomerAddressPayload
  ) => Promise<{ success: boolean; error?: string }>;
  onDeleteAddress: (
    addressId: string
  ) => Promise<{ success: boolean; error?: string }>;
}

export function AddressManagementClient({
  initialAddresses,
  onCreateAddress,
  onDeleteAddress,
}: AddressManagementClientProps) {
  const [isPending, startTransition] = useTransition();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // 表單輸入狀態
  const [recipientName, setRecipientName] = useState("");
  const [phone, setPhone] = useState("");
  const [countryCode, setCountryCode] = useState("TW");
  const [postalCode, setPostalCode] = useState("");
  const [city, setCity] = useState("");
  const [addressLine1, setAddressLine1] = useState("");
  const [addressLine2, setAddressLine2] = useState("");
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const handleCreateSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (isPending) return;

    setErrorMessage(null);
    setSuccessMessage(null);

    const trimmedName = recipientName.trim();
    const trimmedPhone = phone.trim();
    const trimmedCountry = countryCode.trim().toUpperCase();
    const trimmedPostal = postalCode.trim();
    const trimmedCity = city.trim();
    const trimmedLine1 = addressLine1.trim();
    const trimmedLine2 = addressLine2.trim();

    if (!trimmedName || !trimmedPhone || !trimmedCountry || !trimmedPostal || !trimmedCity || !trimmedLine1) {
      setErrorMessage("請填寫所有必填欄位。");
      return;
    }

    startTransition(async () => {
      const res = await onCreateAddress({
        recipientName: trimmedName,
        phone: trimmedPhone,
        countryCode: trimmedCountry,
        postalCode: trimmedPostal,
        city: trimmedCity,
        addressLine1: trimmedLine1,
        addressLine2: trimmedLine2 || null,
      });

      if (res.success) {
        setSuccessMessage("成功新增收件地址。");
        setRecipientName("");
        setPhone("");
        setCountryCode("TW");
        setPostalCode("");
        setCity("");
        setAddressLine1("");
        setAddressLine2("");
      } else {
        setErrorMessage(res.error || "新增地址失敗，請檢視輸入資訊。");
      }
    });
  };

  const handleDelete = (addressId: string) => {
    if (isPending) return;
    if (!window.confirm("確定要刪除此收件地址嗎？")) {
      return;
    }

    setErrorMessage(null);
    setSuccessMessage(null);
    setDeletingId(addressId);

    startTransition(async () => {
      const res = await onDeleteAddress(addressId);
      setDeletingId(null);
      if (res.success) {
        setSuccessMessage("已成功刪除收件地址。");
      } else {
        setErrorMessage(res.error || "刪除地址失敗，請稍後再試。");
      }
    });
  };

  return (
    <div className="space-y-10">
      {/* 提示訊息 */}
      {errorMessage && (
        <section
          role="alert"
          aria-live="polite"
          aria-label="操作失敗提示"
          className="rounded-2xl border border-red-200 bg-red-50/70 p-5 text-sm text-red-900 shadow-xs dark:border-red-900/50 dark:bg-red-950/40 dark:text-red-200"
        >
          <p className="font-semibold">{errorMessage}</p>
        </section>
      )}

      {successMessage && (
        <section
          role="status"
          aria-live="polite"
          aria-label="操作成功提示"
          className="rounded-2xl border border-green-200 bg-green-50/70 p-5 text-sm text-green-900 shadow-xs dark:border-green-900/50 dark:bg-green-950/40 dark:text-green-200"
        >
          <p className="font-semibold">{successMessage}</p>
        </section>
      )}

      {/* 新增地址表單 */}
      <section
        aria-labelledby="add-address-heading"
        className="rounded-2xl border border-stone-200 bg-white p-6 shadow-sm dark:border-stone-800 dark:bg-stone-900 sm:p-8"
      >
        <h2 id="add-address-heading" className="text-lg font-bold text-stone-950 dark:text-stone-50">
          新增收件地址
        </h2>
        <p className="mt-1 text-sm text-stone-600 dark:text-stone-400">
          儲存您的常用收件地址，以便在結帳時快速自動填入。
        </p>

        <form onSubmit={handleCreateSubmit} className="mt-6 space-y-5">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label
                htmlFor="new-recipientName"
                className="block text-sm font-medium text-stone-800 dark:text-stone-200"
              >
                收件人姓名 <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                id="new-recipientName"
                name="recipientName"
                autoComplete="name"
                required
                maxLength={100}
                value={recipientName}
                onChange={(e) => setRecipientName(e.target.value)}
                placeholder="例：王小明"
                className="mt-1.5 block w-full min-h-[44px] rounded-xl border border-stone-300 bg-white px-3.5 py-2.5 text-sm text-stone-900 shadow-2xs transition-colors placeholder:text-stone-400 focus:border-stone-900 focus:outline-none focus:ring-2 focus:ring-stone-400/20 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-100 dark:placeholder:text-stone-500 dark:focus:border-stone-100 dark:focus:ring-stone-500/20"
              />
            </div>

            <div>
              <label
                htmlFor="new-phone"
                className="block text-sm font-medium text-stone-800 dark:text-stone-200"
              >
                聯絡電話 <span className="text-red-500">*</span>
              </label>
              <input
                type="tel"
                id="new-phone"
                name="phone"
                autoComplete="tel"
                required
                maxLength={30}
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="例：0912345678"
                className="mt-1.5 block w-full min-h-[44px] rounded-xl border border-stone-300 bg-white px-3.5 py-2.5 text-sm text-stone-900 shadow-2xs transition-colors placeholder:text-stone-400 focus:border-stone-900 focus:outline-none focus:ring-2 focus:ring-stone-400/20 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-100 dark:placeholder:text-stone-500 dark:focus:border-stone-100 dark:focus:ring-stone-500/20"
              />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-12">
            <div className="sm:col-span-3">
              <label
                htmlFor="new-countryCode"
                className="block text-sm font-medium text-stone-800 dark:text-stone-200"
              >
                國碼 (ISO-2) <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                id="new-countryCode"
                name="countryCode"
                autoComplete="country"
                required
                maxLength={2}
                value={countryCode}
                onChange={(e) => setCountryCode(e.target.value.toUpperCase())}
                placeholder="TW"
                className="mt-1.5 block w-full min-h-[44px] uppercase rounded-xl border border-stone-300 bg-white px-3.5 py-2.5 text-sm text-stone-900 shadow-2xs transition-colors placeholder:text-stone-400 focus:border-stone-900 focus:outline-none focus:ring-2 focus:ring-stone-400/20 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-100 dark:placeholder:text-stone-500 dark:focus:border-stone-100 dark:focus:ring-stone-500/20"
              />
            </div>

            <div className="sm:col-span-4">
              <label
                htmlFor="new-postalCode"
                className="block text-sm font-medium text-stone-800 dark:text-stone-200"
              >
                郵遞區號 <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                id="new-postalCode"
                name="postalCode"
                autoComplete="postal-code"
                required
                maxLength={20}
                value={postalCode}
                onChange={(e) => setPostalCode(e.target.value)}
                placeholder="例：100"
                className="mt-1.5 block w-full min-h-[44px] rounded-xl border border-stone-300 bg-white px-3.5 py-2.5 text-sm text-stone-900 shadow-2xs transition-colors placeholder:text-stone-400 focus:border-stone-900 focus:outline-none focus:ring-2 focus:ring-stone-400/20 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-100 dark:placeholder:text-stone-500 dark:focus:border-stone-100 dark:focus:ring-stone-500/20"
              />
            </div>

            <div className="sm:col-span-5">
              <label
                htmlFor="new-city"
                className="block text-sm font-medium text-stone-800 dark:text-stone-200"
              >
                城市 / 縣市 <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                id="new-city"
                name="city"
                autoComplete="address-level2"
                required
                maxLength={100}
                value={city}
                onChange={(e) => setCity(e.target.value)}
                placeholder="例：台北市"
                className="mt-1.5 block w-full min-h-[44px] rounded-xl border border-stone-300 bg-white px-3.5 py-2.5 text-sm text-stone-900 shadow-2xs transition-colors placeholder:text-stone-400 focus:border-stone-900 focus:outline-none focus:ring-2 focus:ring-stone-400/20 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-100 dark:placeholder:text-stone-500 dark:focus:border-stone-100 dark:focus:ring-stone-500/20"
              />
            </div>
          </div>

          <div>
            <label
              htmlFor="new-addressLine1"
              className="block text-sm font-medium text-stone-800 dark:text-stone-200"
            >
              街道地址 (第一行) <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              id="new-addressLine1"
              name="addressLine1"
              autoComplete="address-line1"
              required
              maxLength={200}
              value={addressLine1}
              onChange={(e) => setAddressLine1(e.target.value)}
              placeholder="例：中正區重慶南路一段 122 號"
              className="mt-1.5 block w-full min-h-[44px] rounded-xl border border-stone-300 bg-white px-3.5 py-2.5 text-sm text-stone-900 shadow-2xs transition-colors placeholder:text-stone-400 focus:border-stone-900 focus:outline-none focus:ring-2 focus:ring-stone-400/20 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-100 dark:placeholder:text-stone-500 dark:focus:border-stone-100 dark:focus:ring-stone-500/20"
            />
          </div>

          <div>
            <label
              htmlFor="new-addressLine2"
              className="block text-sm font-medium text-stone-800 dark:text-stone-200"
            >
              建築、樓層、室號 (選填)
            </label>
            <input
              type="text"
              id="new-addressLine2"
              name="addressLine2"
              autoComplete="address-line2"
              maxLength={200}
              value={addressLine2}
              onChange={(e) => setAddressLine2(e.target.value)}
              placeholder="例：3 樓之 1"
              className="mt-1.5 block w-full min-h-[44px] rounded-xl border border-stone-300 bg-white px-3.5 py-2.5 text-sm text-stone-900 shadow-2xs transition-colors placeholder:text-stone-400 focus:border-stone-900 focus:outline-none focus:ring-2 focus:ring-stone-400/20 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-100 dark:placeholder:text-stone-500 dark:focus:border-stone-100 dark:focus:ring-stone-500/20"
            />
          </div>

          <div className="pt-2">
            <button
              type="submit"
              disabled={isPending}
              className="inline-flex min-h-[44px] items-center justify-center rounded-xl bg-stone-900 px-6 py-2.5 text-sm font-medium text-white shadow-xs transition-colors hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
            >
              {isPending ? "儲存中..." : "新增地址"}
            </button>
          </div>
        </form>
      </section>

      {/* 已儲存地址列表 */}
      <section aria-labelledby="saved-addresses-heading" className="space-y-4">
        <h2 id="saved-addresses-heading" className="text-xl font-bold text-stone-950 dark:text-stone-50">
          已儲存地址清單 ({initialAddresses.length})
        </h2>

        {initialAddresses.length === 0 ? (
          <div className="rounded-2xl border border-dashed border-stone-300 bg-white p-12 text-center shadow-xs dark:border-stone-800 dark:bg-stone-900/60">
            <div
              aria-hidden="true"
              className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-stone-100 text-stone-400 dark:bg-stone-800 dark:text-stone-500"
            >
              <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" strokeWidth="1.5" stroke="currentColor">
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M15 10.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
                />
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1 1 15 0Z"
                />
              </svg>
            </div>
            <h3 className="mt-4 text-base font-semibold text-stone-950 dark:text-stone-50">
              目前尚無儲存的收件地址
            </h3>
            <p className="mt-1 text-sm text-stone-500 dark:text-stone-400">
              您可以透過上方的表單新增常用地址，以利在結帳時更迅速完成訂單。
            </p>
          </div>
        ) : (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            {initialAddresses.map((addr) => (
              <div
                key={addr.id}
                className="flex flex-col justify-between rounded-2xl border border-stone-200 bg-white p-6 shadow-xs transition-shadow hover:shadow-sm dark:border-stone-800 dark:bg-stone-900"
              >
                <div className="space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-base font-semibold text-stone-950 dark:text-stone-50">
                      {addr.recipientName}
                    </span>
                    <span className="rounded-md bg-stone-100 px-2 py-0.5 text-xs font-medium text-stone-600 dark:bg-stone-800 dark:text-stone-400">
                      {addr.countryCode}
                    </span>
                  </div>

                  <p className="text-sm text-stone-600 dark:text-stone-400">
                    電話：{addr.phone}
                  </p>

                  <p className="text-sm text-stone-700 dark:text-stone-300">
                    {addr.postalCode} {addr.city} {addr.addressLine1}
                    {addr.addressLine2 && ` ${addr.addressLine2}`}
                  </p>
                </div>

                <div className="mt-5 border-t border-stone-100 pt-4 dark:border-stone-800">
                  <button
                    type="button"
                    onClick={() => handleDelete(addr.id)}
                    disabled={isPending && deletingId === addr.id}
                    aria-label={`刪除收件人為 ${addr.recipientName} 的地址`}
                    className="inline-flex min-h-[44px] items-center text-sm font-medium text-red-600 transition-colors hover:text-red-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 dark:text-red-400 dark:hover:text-red-300"
                  >
                    {isPending && deletingId === addr.id ? "刪除中..." : "刪除地址"}
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

"use client";

import { useState, useTransition } from "react";
import Link from "next/link";
import type { CartItem } from "@/lib/cart";
import type { SubmitOrderResult, ShippingAddress } from "@/lib/orders";
import type {
  CustomerAddress,
  CreateCustomerAddressPayload,
  CreateAddressResult,
} from "@/lib/addresses";
import { formatPrice } from "@/lib/format";

interface CheckoutReviewProps {
  orderId: string;
  items: CartItem[];
  currency: string;
  totalAmount: number;
  onSubmitOrder: (
    orderId: string,
    shippingAddress: ShippingAddress
  ) => Promise<SubmitOrderResult>;
  onSaveAddress: (
    payload: CreateCustomerAddressPayload
  ) => Promise<CreateAddressResult>;
  savedAddresses?: CustomerAddress[];
  addressBookFailed?: boolean;
}

function getProductMonogram(name?: string): string {
  if (!name || !name.trim()) return "商品";
  const trimmed = name.trim();
  return trimmed.slice(0, 2).toUpperCase();
}

export function CheckoutReview({
  orderId,
  items,
  currency,
  totalAmount,
  onSubmitOrder,
  onSaveAddress,
  savedAddresses = [],
  addressBookFailed = false,
}: CheckoutReviewProps) {
  const [isPending, startTransition] = useTransition();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [availableAddresses, setAvailableAddresses] = useState<CustomerAddress[]>(savedAddresses);
  const [isSaving, setIsSaving] = useState(false);
  const [saveMessage, setSaveMessage] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const [selectedAddressId, setSelectedAddressId] = useState<string>("");
  const [recipientName, setRecipientName] = useState("");
  const [phone, setPhone] = useState("");
  const [countryCode, setCountryCode] = useState("TW");
  const [postalCode, setPostalCode] = useState("");
  const [city, setCity] = useState("");
  const [addressLine1, setAddressLine1] = useState("");
  const [addressLine2, setAddressLine2] = useState("");

  const handleSelectSavedAddress = (addressId: string) => {
    setSelectedAddressId(addressId);
    setSaveMessage(null);
    setSaveError(null);
    if (!addressId) return;

    const matched = availableAddresses.find((a) => a.id === addressId);
    if (matched) {
      setRecipientName(matched.recipientName);
      setPhone(matched.phone);
      setCountryCode(matched.countryCode);
      setPostalCode(matched.postalCode);
      setCity(matched.city);
      setAddressLine1(matched.addressLine1);
      setAddressLine2(matched.addressLine2 || "");
    }
  };

  const getNormalizedAddress = () => {
    const trimmedName = recipientName.trim();
    const trimmedPhone = phone.trim();
    const trimmedCountry = countryCode.trim().toUpperCase();
    const trimmedPostal = postalCode.trim();
    const trimmedCity = city.trim();
    const trimmedLine1 = addressLine1.trim();
    const trimmedLine2 = addressLine2.trim();

    if (
      !trimmedName ||
      !trimmedPhone ||
      !trimmedCountry ||
      !trimmedPostal ||
      !trimmedCity ||
      !trimmedLine1
    ) {
      return null;
    }

    return {
      recipientName: trimmedName,
      phone: trimmedPhone,
      countryCode: trimmedCountry,
      postalCode: trimmedPostal,
      city: trimmedCity,
      addressLine1: trimmedLine1,
      addressLine2: trimmedLine2 || undefined,
    };
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (isPending || isSaving) return;
    setErrorMessage(null);

    const normalized = getNormalizedAddress();
    if (!normalized) {
      setErrorMessage("請填寫所有必填收件資訊欄位。");
      return;
    }

    const shippingAddress: ShippingAddress = {
      recipientName: normalized.recipientName,
      phone: normalized.phone,
      countryCode: normalized.countryCode,
      postalCode: normalized.postalCode,
      city: normalized.city,
      addressLine1: normalized.addressLine1,
      addressLine2: normalized.addressLine2,
    };

    startTransition(async () => {
      const res = await onSubmitOrder(orderId, shippingAddress);
      if (!res.success) {
        setErrorMessage(res.error);
      }
    });
  };

  const handleSaveAddress = async () => {
    if (isPending || isSaving) return;
    setSaveMessage(null);
    setSaveError(null);

    const normalized = getNormalizedAddress();
    if (!normalized) {
      setSaveError("請填寫所有必填收件資訊欄位。");
      return;
    }

    const payload: CreateCustomerAddressPayload = {
      recipientName: normalized.recipientName,
      phone: normalized.phone,
      countryCode: normalized.countryCode,
      postalCode: normalized.postalCode,
      city: normalized.city,
      addressLine1: normalized.addressLine1,
      addressLine2: normalized.addressLine2 || null,
    };

    setIsSaving(true);
    try {
      const res = await onSaveAddress(payload);
      if (res.success) {
        setAvailableAddresses((prev) => [...prev, res.data]);
        setSelectedAddressId(res.data.id);
        setSaveMessage("已儲存至我的地址。");
      } else {
        const errorDetail = res.error
          ? `${res.error}，您仍可繼續完成結帳。`
          : "無法儲存地址，您仍可繼續完成結帳。";
        setSaveError(errorDetail);
      }
    } catch {
      setSaveError("無法儲存地址，您仍可繼續完成結帳。");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-8">
      {/* 提交錯誤訊息提示 */}
      {errorMessage && (
        <section
          role="alert"
          aria-live="polite"
          aria-label="送出訂單失敗提示"
          className="rounded-2xl border border-red-200 bg-red-50/70 p-5 text-sm text-red-900 shadow-xs dark:border-red-900/50 dark:bg-red-950/40 dark:text-red-200"
        >
          <div className="flex items-start gap-3">
            <div
              aria-hidden="true"
              className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-red-100 text-red-600 dark:bg-red-900/60 dark:text-red-300"
            >
              <svg
                className="h-4 w-4"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
                />
              </svg>
            </div>
            <div className="flex-1 min-w-0">
              <h3 className="font-semibold text-red-950 dark:text-red-100">
                送出訂單失敗
              </h3>
              <p className="mt-1 break-words text-red-800 dark:text-red-300">
                {errorMessage}
              </p>
              <div className="mt-3">
                <Link
                  href="/cart"
                  className="inline-flex min-h-[44px] items-center text-xs font-semibold text-red-800 underline transition-colors hover:text-red-950 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-400 focus-visible:ring-offset-2 rounded-sm dark:text-red-300 dark:hover:text-red-100"
                >
                  <span aria-hidden="true">&larr;&nbsp;</span>返回購物車查看或調整商品
                </Link>
              </div>
            </div>
          </div>
        </section>
      )}

      <div className="grid grid-cols-1 gap-8 lg:grid-cols-[minmax(0,1fr)_24rem] lg:items-start">
        {/* 左側主要區域：收件與配送資訊 (桌面 order-1，行動端 order-2) */}
        <div className="order-2 space-y-6 lg:order-1">
          <section
            aria-label="收件與配送資訊"
            className="overflow-hidden rounded-2xl border border-stone-200 bg-white shadow-sm dark:border-stone-800 dark:bg-stone-900"
          >
            <div className="border-b border-stone-200 bg-stone-50/50 px-6 py-4 dark:border-stone-800 dark:bg-stone-900/50">
              <h2 className="text-base font-semibold text-stone-950 dark:text-stone-50">
                收件與配送資訊
              </h2>
              <p className="mt-1 text-xs text-stone-500 dark:text-stone-400">
                請填寫此筆訂單的收件人與送貨地址。訂單送出後，收件資訊將作為不可變快照留存。
              </p>
            </div>

            <div className="p-6 space-y-5">
              {/* 已儲存地址選取與降級提示 */}
              {availableAddresses.length > 0 ? (
                <div className="rounded-xl border border-stone-200 bg-stone-50/60 p-4 dark:border-stone-800 dark:bg-stone-900/60">
                  <div className="flex items-center justify-between pb-2">
                    <label
                      htmlFor="savedAddressSelect"
                      className="text-sm font-medium text-stone-800 dark:text-stone-200"
                    >
                      已儲存地址
                    </label>
                    <Link
                      href="/account/addresses"
                      className="text-xs font-medium text-stone-600 underline underline-offset-2 hover:text-stone-900 dark:text-stone-400 dark:hover:text-stone-200"
                    >
                      管理已儲存地址
                    </Link>
                  </div>
                  <select
                    id="savedAddressSelect"
                    value={selectedAddressId}
                    onChange={(e) => handleSelectSavedAddress(e.target.value)}
                    className="block w-full min-h-[44px] rounded-xl border border-stone-300 bg-white px-3.5 py-2.5 text-sm text-stone-900 shadow-2xs transition-colors focus:border-stone-900 focus:outline-none focus:ring-2 focus:ring-stone-400/20 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-100 dark:focus:border-stone-100 dark:focus:ring-stone-500/20"
                  >
                    <option value="">手動填寫</option>
                    {availableAddresses.map((addr) => (
                      <option key={addr.id} value={addr.id}>
                        {addr.recipientName} ({addr.phone}) - {addr.postalCode} {addr.city} {addr.addressLine1}
                      </option>
                    ))}
                  </select>
                </div>
              ) : addressBookFailed ? (
                <div className="rounded-xl border border-stone-200 bg-stone-50/70 p-4 text-xs text-stone-600 dark:border-stone-800 dark:bg-stone-900/60 dark:text-stone-400">
                  <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
                    <span>已儲存地址暫時無法載入，可直接手動填寫。</span>
                    <Link
                      href="/account/addresses"
                      className="font-medium text-stone-700 underline underline-offset-2 hover:text-stone-900 dark:text-stone-300 dark:hover:text-stone-100"
                    >
                      管理已儲存地址
                    </Link>
                  </div>
                </div>
              ) : (
                <div className="flex items-center justify-between rounded-xl border border-dashed border-stone-200 bg-stone-50/40 px-4 py-3 text-xs text-stone-500 dark:border-stone-800 dark:bg-stone-900/40 dark:text-stone-400">
                  <span>尚未儲存常用地址，可於下方手動填寫。</span>
                  <Link
                    href="/account/addresses"
                    className="font-medium text-stone-700 underline underline-offset-2 hover:text-stone-900 dark:text-stone-300 dark:hover:text-stone-100"
                  >
                    管理已儲存地址
                  </Link>
                </div>
              )}

              {/* 姓名與電話 */}
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <label
                    htmlFor="recipientName"
                    className="block text-sm font-medium text-stone-800 dark:text-stone-200"
                  >
                    收件人姓名 <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    id="recipientName"
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
                    htmlFor="phone"
                    className="block text-sm font-medium text-stone-800 dark:text-stone-200"
                  >
                    聯絡電話 <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="tel"
                    id="phone"
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

              {/* 國碼、郵遞區號與城市 */}
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-12">
                <div className="sm:col-span-3">
                  <label
                    htmlFor="countryCode"
                    className="block text-sm font-medium text-stone-800 dark:text-stone-200"
                  >
                    國碼 (ISO-2) <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    id="countryCode"
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
                    htmlFor="postalCode"
                    className="block text-sm font-medium text-stone-800 dark:text-stone-200"
                  >
                    郵遞區號 <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    id="postalCode"
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
                    htmlFor="city"
                    className="block text-sm font-medium text-stone-800 dark:text-stone-200"
                  >
                    城市 / 縣市 <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    id="city"
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

              {/* 地址第一行 */}
              <div>
                <label
                  htmlFor="addressLine1"
                  className="block text-sm font-medium text-stone-800 dark:text-stone-200"
                >
                  街道地址行 1 <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  id="addressLine1"
                  name="addressLine1"
                  autoComplete="street-address"
                  required
                  maxLength={200}
                  value={addressLine1}
                  onChange={(e) => setAddressLine1(e.target.value)}
                  placeholder="例：中正區重慶南路一段 122 號"
                  className="mt-1.5 block w-full min-h-[44px] rounded-xl border border-stone-300 bg-white px-3.5 py-2.5 text-sm text-stone-900 shadow-2xs transition-colors placeholder:text-stone-400 focus:border-stone-900 focus:outline-none focus:ring-2 focus:ring-stone-400/20 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-100 dark:placeholder:text-stone-500 dark:focus:border-stone-100 dark:focus:ring-stone-500/20"
                />
              </div>

              {/* 地址第二行 (選填) */}
              <div>
                <label
                  htmlFor="addressLine2"
                  className="block text-sm font-medium text-stone-800 dark:text-stone-200"
                >
                  街道地址行 2{" "}
                  <span className="text-xs text-stone-500 dark:text-stone-400">
                    (選填，如樓層、室號)
                  </span>
                </label>
                <input
                  type="text"
                  id="addressLine2"
                  name="addressLine2"
                  autoComplete="address-line2"
                  maxLength={200}
                  value={addressLine2}
                  onChange={(e) => setAddressLine2(e.target.value)}
                  placeholder="例：3 樓之 1"
                  className="mt-1.5 block w-full min-h-[44px] rounded-xl border border-stone-300 bg-white px-3.5 py-2.5 text-sm text-stone-900 shadow-2xs transition-colors placeholder:text-stone-400 focus:border-stone-900 focus:outline-none focus:ring-2 focus:ring-stone-400/20 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-100 dark:placeholder:text-stone-500 dark:focus:border-stone-100 dark:focus:ring-stone-500/20"
                />
              </div>

              {/* 手動輸入模式下的地址儲存動作 */}
              {selectedAddressId === "" && (
                <div className="pt-2 border-t border-stone-100 dark:border-stone-800/80">
                  <div className="flex flex-wrap items-center gap-3">
                    <button
                      type="button"
                      onClick={handleSaveAddress}
                      disabled={isSaving || isPending}
                      className="inline-flex min-h-[44px] items-center justify-center rounded-xl border border-stone-300 bg-white px-4 py-2 text-sm font-medium text-stone-700 shadow-2xs transition-colors hover:bg-stone-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-200 dark:hover:bg-stone-700"
                    >
                      {isSaving ? (
                        <span className="flex items-center gap-2">
                          <svg
                            className="h-4 w-4 animate-spin text-current shrink-0"
                            fill="none"
                            viewBox="0 0 24 24"
                            aria-hidden="true"
                          >
                            <circle
                              className="opacity-25"
                              cx="12"
                              cy="12"
                              r="10"
                              stroke="currentColor"
                              strokeWidth="4"
                            />
                            <path
                              className="opacity-75"
                              fill="currentColor"
                              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                            />
                          </svg>
                          <span>儲存中...</span>
                        </span>
                      ) : (
                        "儲存目前地址"
                      )}
                    </button>
                    <span className="text-xs text-stone-500 dark:text-stone-400">
                      可將目前填寫的地址儲存至常用地址簿，方便日後選用。
                    </span>
                  </div>
                </div>
              )}

              {/* 儲存成功反饋 */}
              {saveMessage && (
                <div
                  role="status"
                  aria-live="polite"
                  className="flex items-center gap-2 rounded-xl bg-emerald-50 px-4 py-3 text-xs font-medium text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300"
                >
                  <svg
                    className="h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                    aria-hidden="true"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M5 13l4 4L19 7"
                    />
                  </svg>
                  <span>{saveMessage}</span>
                </div>
              )}

              {/* 儲存失敗反饋 */}
              {saveError && (
                <div
                  role="alert"
                  aria-live="polite"
                  className="flex items-start gap-2 rounded-xl bg-red-50 px-4 py-3 text-xs text-red-800 dark:bg-red-950/40 dark:text-red-300"
                >
                  <svg
                    className="h-4 w-4 shrink-0 mt-0.5 text-red-600 dark:text-red-400"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                    aria-hidden="true"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
                    />
                  </svg>
                  <span>{saveError}</span>
                </div>
              )}
            </div>
          </section>

          {/* 送出與返回動作列 */}
          <div className="flex flex-col-reverse gap-4 sm:flex-row sm:items-center sm:justify-between">
            <Link
              href="/cart"
              className="inline-flex min-h-[44px] items-center justify-center text-sm font-medium text-stone-600 transition-colors hover:text-stone-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-lg dark:text-stone-400 dark:hover:text-stone-200"
            >
              <span aria-hidden="true">&larr;&nbsp;</span>返回購物車修改
            </Link>

            <button
              type="submit"
              disabled={isPending || isSaving}
              className="inline-flex min-h-[48px] w-full sm:w-auto sm:min-w-[220px] items-center justify-center rounded-xl bg-stone-900 px-8 py-3.5 text-base font-semibold text-white shadow-sm transition-colors hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
            >
              {isPending ? (
                <span className="flex items-center justify-center gap-2">
                  <svg
                    className="h-5 w-5 animate-spin text-current shrink-0"
                    fill="none"
                    viewBox="0 0 24 24"
                    aria-hidden="true"
                  >
                    <circle
                      className="opacity-25"
                      cx="12"
                      cy="12"
                      r="10"
                      stroke="currentColor"
                      strokeWidth="4"
                    />
                    <path
                      className="opacity-75"
                      fill="currentColor"
                      d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                    />
                  </svg>
                  <span className="break-words">訂單處理與庫存保留中...</span>
                </span>
              ) : (
                "確認送出訂單"
              )}
            </button>
          </div>
        </div>

        {/* 右側側欄區域：訂單明細審核與總額 (桌面 order-2，行動端 order-1 先行確認) */}
        <div className="order-1 space-y-6 lg:order-2 lg:sticky lg:top-8">
          <section
            aria-label="訂單摘要"
            className="overflow-hidden rounded-2xl border border-stone-200 bg-white shadow-sm dark:border-stone-800 dark:bg-stone-900"
          >
            <div className="border-b border-stone-200 bg-stone-50/50 px-6 py-4 dark:border-stone-800 dark:bg-stone-900/50">
              <h2 className="text-base font-semibold text-stone-950 dark:text-stone-50">
                訂單明細審核 (共 {items.length} 項商品)
              </h2>
            </div>

            {/* 品項清單 */}
            <ul
              role="list"
              className="divide-y divide-stone-200 dark:divide-stone-800 max-h-[380px] overflow-y-auto"
            >
              {items.map((item) => (
                <li
                  key={item.productId}
                  className="flex items-start gap-4 p-5 sm:p-6"
                >
                  {/* 商品圖片 / 裝飾性商品縮寫磚 */}
                  {item.productImageUrl && item.productImageUrl.trim().length > 0 ? (
                    <div className="relative h-12 w-12 shrink-0 overflow-hidden rounded-xl border border-stone-200 bg-stone-100 dark:border-stone-700 dark:bg-stone-800">
                      {/* eslint-disable-next-line @next/next/no-img-element -- Bounded external product image metadata rendering without server-side proxy */}
                      <img
                        src={item.productImageUrl.trim()}
                        alt={item.productName || "商品"}
                        loading="lazy"
                        decoding="async"
                        referrerPolicy="no-referrer"
                        className="h-full w-full object-cover"
                      />
                    </div>
                  ) : (
                    <div
                      aria-hidden="true"
                      className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl border border-stone-200 bg-stone-100 text-xs font-bold tracking-wider text-stone-600 select-none dark:border-stone-700 dark:bg-stone-800 dark:text-stone-300"
                    >
                      {getProductMonogram(item.productName)}
                    </div>
                  )}

                  <div className="min-w-0 flex-1">
                    <h3 className="text-sm font-semibold text-stone-900 break-words dark:text-stone-100">
                      {item.productName ||
                        "商品 (" + item.productId.slice(0, 8) + "...)"}
                    </h3>
                    <div className="mt-1 flex flex-wrap items-center justify-between gap-2 text-xs text-stone-500 dark:text-stone-400">
                      <span>
                        單價：{formatPrice(item.unitPrice, item.currency)} &times;{" "}
                        <span className="font-mono font-medium text-stone-800 dark:text-stone-200">
                          {item.quantity}
                        </span>
                      </span>
                      <span className="font-mono text-sm font-bold text-stone-950 dark:text-stone-50 whitespace-nowrap">
                        {formatPrice(item.totalPrice, item.currency)}
                      </span>
                    </div>
                  </div>
                </li>
              ))}
            </ul>

            {/* 總計與付款前說明 */}
            <div className="border-t border-stone-200 bg-stone-50/50 p-6 dark:border-stone-800 dark:bg-stone-900/60 space-y-4">
              <div className="flex items-baseline justify-between">
                <span className="text-base font-semibold text-stone-700 dark:text-stone-300">
                  訂單應付總額
                </span>
                <span className="font-mono text-2xl font-extrabold tracking-tight text-stone-950 dark:text-stone-50 whitespace-nowrap">
                  {formatPrice(totalAmount, currency)}
                </span>
              </div>

              <div className="rounded-xl border border-amber-200 bg-amber-50/80 p-4 text-xs text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-300">
                <p className="font-medium leading-relaxed">
                  注意事項：此步驟為「付款前確認」。點擊確認送出後，系統將正式建立訂單並保留庫存；付款流程將於後續階段進行。
                </p>
              </div>
            </div>
          </section>
        </div>
      </div>
    </form>
  );
}

"use client";

import { useState, useTransition } from "react";
import Link from "next/link";
import type { CartItem } from "@/lib/cart";
import type { SubmitOrderResult, ShippingAddress } from "@/lib/orders";
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
}: CheckoutReviewProps) {
  const [isPending, startTransition] = useTransition();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [recipientName, setRecipientName] = useState("");
  const [phone, setPhone] = useState("");
  const [countryCode, setCountryCode] = useState("TW");
  const [postalCode, setPostalCode] = useState("");
  const [city, setCity] = useState("");
  const [addressLine1, setAddressLine1] = useState("");
  const [addressLine2, setAddressLine2] = useState("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (isPending) return;
    setErrorMessage(null);

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
      setErrorMessage("請填寫所有必填收件資訊欄位。");
      return;
    }

    const shippingAddress: ShippingAddress = {
      recipientName: trimmedName,
      phone: trimmedPhone,
      countryCode: trimmedCountry,
      postalCode: trimmedPostal,
      city: trimmedCity,
      addressLine1: trimmedLine1,
      addressLine2: trimmedLine2 || undefined,
    };

    startTransition(async () => {
      const res = await onSubmitOrder(orderId, shippingAddress);
      if (!res.success) {
        setErrorMessage(res.error);
      }
    });
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
              disabled={isPending}
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

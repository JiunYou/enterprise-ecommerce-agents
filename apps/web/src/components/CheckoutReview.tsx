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

    if (!trimmedName || !trimmedPhone || !trimmedCountry || !trimmedPostal || !trimmedCity || !trimmedLine1) {
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
    <div className="space-y-6">
      {errorMessage && (
        <section
          aria-label="結帳失敗提示"
          className="rounded-xl border border-red-200 bg-red-50 p-5 text-sm text-red-800 dark:border-red-900/50 dark:bg-red-950/40 dark:text-red-300"
        >
          <div className="flex items-start gap-3">
            <svg
              className="h-5 w-5 flex-shrink-0 text-red-500 mt-0.5"
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
            <div className="flex-1">
              <h4 className="font-semibold">送出訂單失敗</h4>
              <p className="mt-1">{errorMessage}</p>
              <div className="mt-3">
                <Link
                  href="/cart"
                  className="inline-flex items-center text-xs font-semibold text-red-700 underline hover:text-red-900 dark:text-red-300 dark:hover:text-red-100"
                >
                  &larr; 返回購物車查看或調整商品
                </Link>
              </div>
            </div>
          </div>
        </section>
      )}

      {/* 訂單品項清單審閱 */}
      <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
        <div className="border-b border-zinc-200 bg-zinc-50/50 px-6 py-4 dark:border-zinc-800 dark:bg-zinc-900/50">
          <h3 className="text-base font-semibold text-zinc-900 dark:text-zinc-100">
            訂單明細審核 (共 {items.length} 項商品)
          </h3>
        </div>

        <ul role="list" className="divide-y divide-zinc-200 dark:divide-zinc-800">
          {items.map((item) => (
            <li
              key={item.productId}
              className="flex flex-col gap-3 p-6 sm:flex-row sm:items-center sm:justify-between"
            >
              <div className="flex-1">
                <h4 className="text-base font-semibold text-zinc-900 dark:text-zinc-100">
                  {item.productName || "商品 (" + item.productId.slice(0, 8) + "...)"}
                </h4>
                <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">
                  單價：{formatPrice(item.unitPrice, item.currency)}
                </p>
              </div>

              <div className="flex items-center gap-8">
                <div className="text-sm text-zinc-600 dark:text-zinc-400">
                  <span>數量：</span>
                  <span className="font-mono font-semibold text-zinc-900 dark:text-zinc-100">
                    {item.quantity}
                  </span>
                </div>
                <div className="w-28 text-right font-mono text-base font-semibold text-zinc-900 dark:text-zinc-100">
                  {formatPrice(item.totalPrice, item.currency)}
                </div>
              </div>
            </li>
          ))}
        </ul>

        {/* 總計與付款前說明 */}
        <div className="border-t border-zinc-200 bg-zinc-50 px-6 py-5 dark:border-zinc-800 dark:bg-zinc-900/60">
          <div className="flex items-center justify-between">
            <span className="text-base font-semibold text-zinc-700 dark:text-zinc-300">
              訂單應付總額
            </span>
            <span className="font-mono text-2xl font-extrabold text-zinc-900 dark:text-zinc-100">
              {formatPrice(totalAmount, currency)}
            </span>
          </div>

          <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 p-4 text-xs text-amber-800 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-300">
            <p className="font-medium">
              注意事項：此步驟為「付款前確認」。點擊確認送出後，系統將正式建立訂單並保留庫存；付款流程將於後續階段進行。
            </p>
          </div>
        </div>
      </div>

      {/* 收件資訊表單 */}
      <form onSubmit={handleSubmit} className="space-y-6">
        <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          <div className="border-b border-zinc-200 bg-zinc-50/50 px-6 py-4 dark:border-zinc-800 dark:bg-zinc-900/50">
            <h3 className="text-base font-semibold text-zinc-900 dark:text-zinc-100">
              收件與配送資訊
            </h3>
            <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
              請填寫此筆訂單的收件人與送貨地址。訂單送出後，收件資訊將作為不可變快照留存。
            </p>
          </div>

          <div className="p-6 space-y-4">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label
                  htmlFor="recipientName"
                  className="block text-sm font-medium text-zinc-700 dark:text-zinc-300"
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
                  className="mt-1 block w-full rounded-lg border border-zinc-300 px-3.5 py-2 text-sm text-zinc-900 shadow-sm focus:border-zinc-900 focus:outline-none focus:ring-1 focus:ring-zinc-900 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:focus:border-zinc-100 dark:focus:ring-zinc-100"
                />
              </div>

              <div>
                <label
                  htmlFor="phone"
                  className="block text-sm font-medium text-zinc-700 dark:text-zinc-300"
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
                  className="mt-1 block w-full rounded-lg border border-zinc-300 px-3.5 py-2 text-sm text-zinc-900 shadow-sm focus:border-zinc-900 focus:outline-none focus:ring-1 focus:ring-zinc-900 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:focus:border-zinc-100 dark:focus:ring-zinc-100"
                />
              </div>
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              <div>
                <label
                  htmlFor="countryCode"
                  className="block text-sm font-medium text-zinc-700 dark:text-zinc-300"
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
                  className="mt-1 block w-full rounded-lg border border-zinc-300 px-3.5 py-2 text-sm uppercase text-zinc-900 shadow-sm focus:border-zinc-900 focus:outline-none focus:ring-1 focus:ring-zinc-900 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:focus:border-zinc-100 dark:focus:ring-zinc-100"
                />
              </div>

              <div>
                <label
                  htmlFor="postalCode"
                  className="block text-sm font-medium text-zinc-700 dark:text-zinc-300"
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
                  className="mt-1 block w-full rounded-lg border border-zinc-300 px-3.5 py-2 text-sm text-zinc-900 shadow-sm focus:border-zinc-900 focus:outline-none focus:ring-1 focus:ring-zinc-900 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:focus:border-zinc-100 dark:focus:ring-zinc-100"
                />
              </div>

              <div>
                <label
                  htmlFor="city"
                  className="block text-sm font-medium text-zinc-700 dark:text-zinc-300"
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
                  className="mt-1 block w-full rounded-lg border border-zinc-300 px-3.5 py-2 text-sm text-zinc-900 shadow-sm focus:border-zinc-900 focus:outline-none focus:ring-1 focus:ring-zinc-900 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:focus:border-zinc-100 dark:focus:ring-zinc-100"
                />
              </div>
            </div>

            <div>
              <label
                htmlFor="addressLine1"
                className="block text-sm font-medium text-zinc-700 dark:text-zinc-300"
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
                className="mt-1 block w-full rounded-lg border border-zinc-300 px-3.5 py-2 text-sm text-zinc-900 shadow-sm focus:border-zinc-900 focus:outline-none focus:ring-1 focus:ring-zinc-900 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:focus:border-zinc-100 dark:focus:ring-zinc-100"
              />
            </div>

            <div>
              <label
                htmlFor="addressLine2"
                className="block text-sm font-medium text-zinc-700 dark:text-zinc-300"
              >
                街道地址行 2 <span className="text-xs text-zinc-500 dark:text-zinc-400">(選填，如樓層、室號)</span>
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
                className="mt-1 block w-full rounded-lg border border-zinc-300 px-3.5 py-2 text-sm text-zinc-900 shadow-sm focus:border-zinc-900 focus:outline-none focus:ring-1 focus:ring-zinc-900 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:focus:border-zinc-100 dark:focus:ring-zinc-100"
              />
            </div>
          </div>
        </div>

        {/* 送出與返回動作列 */}
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <Link
            href="/cart"
            className="inline-flex items-center text-sm font-medium text-zinc-600 transition hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-zinc-100"
          >
            &larr; 返回購物車修改
          </Link>

          <button
            type="submit"
            disabled={isPending}
            className="inline-flex items-center justify-center rounded-lg bg-zinc-900 px-8 py-3.5 text-base font-semibold text-white shadow-sm transition hover:bg-zinc-800 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
          >
            {isPending ? (
              <span className="flex items-center gap-2">
                <svg
                  className="h-5 w-5 animate-spin text-current"
                  fill="none"
                  viewBox="0 0 24 24"
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
                訂單處理與庫存保留中...
              </span>
            ) : (
              "確認送出訂單"
            )}
          </button>
        </div>
      </form>
    </div>
  );
}

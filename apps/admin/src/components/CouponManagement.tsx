"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import type { AdminCoupon } from "@/lib/coupons";
import { createCouponAction, deactivateCouponAction } from "@/app/actions";

interface CouponManagementProps {
  initialCoupons: AdminCoupon[];
}

export function CouponManagement({ initialCoupons }: CouponManagementProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  // 建立優惠券表單狀態
  const [code, setCode] = useState("");
  const [discountAmount, setDiscountAmount] = useState("");
  const [currency, setCurrency] = useState("TWD");
  const [startsAt, setStartsAt] = useState("");
  const [expiresAt, setExpiresAt] = useState("");

  const [formError, setFormError] = useState<string | null>(null);
  const [formSuccess, setFormSuccess] = useState<string | null>(null);

  // 停用操作狀態
  const [actionError, setActionError] = useState<string | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);

  const handleCreateCoupon = (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);
    setFormSuccess(null);

    const parsedAmount = parseFloat(discountAmount);
    if (isNaN(parsedAmount) || parsedAmount <= 0) {
      setFormError("請輸入有效的折扣金額（必須大於 0）。");
      return;
    }

    startTransition(async () => {
      const res = await createCouponAction({
        code,
        discountAmount: parsedAmount,
        currency,
        startsAt,
        expiresAt,
      });

      if (res.success) {
        setFormSuccess(`優惠券代碼 ${code.trim().toUpperCase()} 建立成功！`);
        setCode("");
        setDiscountAmount("");
        setStartsAt("");
        setExpiresAt("");
        router.refresh();
      } else {
        setFormError(res.error || "建立優惠券失敗，請重試。");
      }
    });
  };

  const handleDeactivate = (couponId: string, couponCode: string) => {
    if (!confirm(`確定要停用優惠券「${couponCode}」嗎？停用後將無法再次啟用。`)) {
      return;
    }

    setActionError(null);
    setDeactivatingId(couponId);

    startTransition(async () => {
      const res = await deactivateCouponAction(couponId);
      setDeactivatingId(null);
      if (res.success) {
        router.refresh();
      } else {
        setActionError(res.error || "停用優惠券失敗。");
      }
    });
  };

  return (
    <div className="space-y-8">
      {/* 建立優惠券卡片 */}
      <section
        aria-label="新增優惠券"
        className="rounded-xl border border-zinc-200 bg-white p-6 shadow-xs dark:border-zinc-800 dark:bg-zinc-900"
      >
        <h2 className="text-base font-semibold text-zinc-900 dark:text-zinc-50">
          新增固定金額優惠券
        </h2>
        <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
          建立全館手動折抵優惠券，代碼將自動轉為大寫並去除空白。
        </p>

        {formSuccess && (
          <div
            role="status"
            className="mt-4 rounded-lg border border-emerald-200 bg-emerald-50/70 p-3 text-sm font-medium text-emerald-800 dark:border-emerald-900/50 dark:bg-emerald-950/40 dark:text-emerald-300"
          >
            {formSuccess}
          </div>
        )}

        {formError && (
          <div
            role="alert"
            className="mt-4 rounded-lg border border-rose-200 bg-rose-50/80 p-3 text-sm font-medium text-rose-800 dark:border-rose-900/50 dark:bg-rose-950/40 dark:text-rose-300"
          >
            {formError}
          </div>
        )}

        <form onSubmit={handleCreateCoupon} className="mt-5 space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <div>
              <label
                htmlFor="coupon-code"
                className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
              >
                優惠券代碼 <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                id="coupon-code"
                required
                value={code}
                onChange={(e) => setCode(e.target.value)}
                placeholder="例如：WELCOME100"
                className="mt-1.5 block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm uppercase text-zinc-900 shadow-xs placeholder:normal-case placeholder:text-zinc-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
              />
            </div>

            <div>
              <label
                htmlFor="coupon-discount"
                className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
              >
                折扣金額 <span className="text-rose-500">*</span>
              </label>
              <input
                type="number"
                id="coupon-discount"
                required
                min="0.01"
                step="any"
                value={discountAmount}
                onChange={(e) => setDiscountAmount(e.target.value)}
                placeholder="例如：100"
                className="mt-1.5 block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 shadow-xs placeholder:text-zinc-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
              />
            </div>

            <div>
              <label
                htmlFor="coupon-currency"
                className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
              >
                幣別 (3 碼) <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                id="coupon-currency"
                required
                maxLength={3}
                value={currency}
                onChange={(e) => setCurrency(e.target.value.toUpperCase())}
                placeholder="TWD"
                className="mt-1.5 block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm uppercase text-zinc-900 shadow-xs placeholder:text-zinc-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
              />
            </div>

            <div>
              <label
                htmlFor="coupon-starts-at"
                className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
              >
                生效開始時間 <span className="text-rose-500">*</span>
              </label>
              <input
                type="datetime-local"
                id="coupon-starts-at"
                required
                value={startsAt}
                onChange={(e) => setStartsAt(e.target.value)}
                className="mt-1.5 block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 shadow-xs focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
              />
            </div>

            <div>
              <label
                htmlFor="coupon-expires-at"
                className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
              >
                結束過期時間 <span className="text-rose-500">*</span>
              </label>
              <input
                type="datetime-local"
                id="coupon-expires-at"
                required
                value={expiresAt}
                onChange={(e) => setExpiresAt(e.target.value)}
                className="mt-1.5 block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 shadow-xs focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
              />
            </div>
          </div>

          <div className="flex justify-end pt-2">
            <button
              type="submit"
              disabled={isPending}
              className="inline-flex min-h-[40px] items-center justify-center rounded-lg bg-indigo-600 px-5 py-2 text-sm font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-indigo-500 dark:hover:bg-indigo-400"
            >
              {isPending ? "處理中..." : "建立優惠券"}
            </button>
          </div>
        </form>
      </section>

      {/* 優惠券清單表格 */}
      <section
        aria-label="優惠券清單"
        className="rounded-xl border border-zinc-200 bg-white shadow-xs dark:border-zinc-800 dark:bg-zinc-900 overflow-hidden"
      >
        <div className="px-6 py-4 border-b border-zinc-200 dark:border-zinc-800">
          <h2 className="text-base font-semibold text-zinc-900 dark:text-zinc-50">
            優惠券列表 (共 {initialCoupons.length} 筆)
          </h2>
        </div>

        {actionError && (
          <div
            role="alert"
            className="m-4 rounded-lg border border-rose-200 bg-rose-50/80 p-3 text-sm font-medium text-rose-800 dark:border-rose-900/50 dark:bg-rose-950/40 dark:text-rose-300"
          >
            {actionError}
          </div>
        )}

        {initialCoupons.length === 0 ? (
          <div className="p-8 text-center text-sm text-zinc-500 dark:text-zinc-400">
            目前尚未建立任何優惠券。
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm text-zinc-600 dark:text-zinc-400">
              <thead className="border-b border-zinc-200 bg-zinc-50/50 text-xs uppercase text-zinc-500 dark:border-zinc-800 dark:bg-zinc-800/40 dark:text-zinc-400">
                <tr>
                  <th scope="col" className="px-6 py-3 font-semibold">代碼</th>
                  <th scope="col" className="px-6 py-3 font-semibold">折扣金額</th>
                  <th scope="col" className="px-6 py-3 font-semibold">有效期間</th>
                  <th scope="col" className="px-6 py-3 font-semibold">狀態</th>
                  <th scope="col" className="px-6 py-3 font-semibold">建立時間</th>
                  <th scope="col" className="px-6 py-3 font-semibold text-right">操作</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-200 dark:divide-zinc-800">
                {initialCoupons.map((coupon) => {
                  const isItemDeactivating = isPending && deactivatingId === coupon.id;
                  const isCurrentActive = coupon.isActive;

                  return (
                    <tr
                      key={coupon.id}
                      className="hover:bg-zinc-50/50 dark:hover:bg-zinc-800/30"
                    >
                      <td className="px-6 py-4 font-mono font-bold text-zinc-900 dark:text-zinc-100">
                        {coupon.code}
                      </td>
                      <td className="px-6 py-4 font-mono font-medium text-zinc-900 dark:text-zinc-100">
                        {coupon.discountAmount} {coupon.currency}
                      </td>
                      <td className="px-6 py-4 text-xs">
                        <div>起：{new Date(coupon.startsAt).toLocaleString("zh-TW")}</div>
                        <div>迄：{new Date(coupon.expiresAt).toLocaleString("zh-TW")}</div>
                      </td>
                      <td className="px-6 py-4">
                        {isCurrentActive ? (
                          <span className="inline-flex items-center rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-semibold text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300">
                            啟用中
                          </span>
                        ) : (
                          <span className="inline-flex items-center rounded-full bg-zinc-100 px-2.5 py-0.5 text-xs font-medium text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400">
                            已停用
                          </span>
                        )}
                      </td>
                      <td className="px-6 py-4 text-xs font-mono text-zinc-500 dark:text-zinc-400">
                        {new Date(coupon.createdAt).toLocaleString("zh-TW")}
                      </td>
                      <td className="px-6 py-4 text-right">
                        {isCurrentActive ? (
                          <button
                            type="button"
                            disabled={isPending}
                            onClick={() => handleDeactivate(coupon.id, coupon.code)}
                            className="inline-flex min-h-[32px] items-center justify-center rounded-md border border-rose-300 bg-white px-3 py-1 text-xs font-medium text-rose-700 shadow-2xs transition-colors hover:bg-rose-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-500 disabled:opacity-50 dark:border-rose-800 dark:bg-zinc-800 dark:text-rose-400 dark:hover:bg-rose-950/50"
                          >
                            {isItemDeactivating ? "處理中..." : "停用"}
                          </button>
                        ) : (
                          <span className="text-xs text-zinc-400 dark:text-zinc-600">
                            無動作
                          </span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

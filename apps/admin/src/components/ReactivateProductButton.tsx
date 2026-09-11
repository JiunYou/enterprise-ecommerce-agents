"use client";

import { useState, useTransition } from "react";
import { reactivateProductAction } from "@/app/actions";

interface ReactivateProductButtonProps {
  productId: string;
  productName: string;
}

export function ReactivateProductButton({
  productId,
  productName,
}: ReactivateProductButtonProps) {
  const [showConfirm, setShowConfirm] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const handleReactivate = () => {
    setErrorMessage(null);
    startTransition(async () => {
      const result = await reactivateProductAction(productId);
      if (!result.success) {
        setErrorMessage(result.error || "重新啟用操作失敗，請稍後重試。");
      } else {
        setShowConfirm(false);
      }
    });
  };

  return (
    <div className="rounded-xl border border-emerald-200/80 bg-emerald-50/40 p-5 shadow-xs sm:p-6 dark:border-emerald-900/50 dark:bg-emerald-950/20">
      <h3 className="text-base font-semibold text-emerald-900 dark:text-emerald-200">
        商品重新上架與啟用
      </h3>
      <p className="mt-1 text-xs text-emerald-700 dark:text-emerald-300">
        重新啟用後商品將恢復公開目錄可見性，並重新允許加入購物車。
      </p>

      {!showConfirm ? (
        <div className="mt-4">
          <button
            type="button"
            onClick={() => setShowConfirm(true)}
            className="inline-flex min-h-[44px] w-full touch-manipulation items-center justify-center rounded-lg bg-emerald-600 px-4 py-2.5 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-emerald-500 active:bg-emerald-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2 sm:w-auto dark:bg-emerald-600 dark:hover:bg-emerald-500 dark:focus-visible:ring-offset-zinc-900"
          >
            重新啟用此商品
          </button>
        </div>
      ) : (
        <div className="mt-4 space-y-3 rounded-lg border border-emerald-200 bg-white p-4 shadow-xs sm:p-5 dark:border-emerald-900/60 dark:bg-zinc-900">
          <div className="text-xs text-zinc-700 dark:text-zinc-300">
            <p className="font-semibold text-emerald-600 dark:text-emerald-400">
              確認重新啟用商品「{productName}」？
            </p>
            <ul className="mt-2 list-disc space-y-1.5 pl-4 text-zinc-600 dark:text-zinc-400">
              <li>商品狀態將變更為上架中 (Active)，前台目錄重新恢復可見。</li>
              <li>恢復顧客將此商品加入購物車的資格（受現有庫存約束）。</li>
              <li>既有庫存數量完全不受影響，不會自動修改或修復庫存。</li>
              <li>既有商品識別碼、SKU、售價與幣別等屬性均保持不變。</li>
            </ul>
          </div>

          {errorMessage && (
            <div
              role="alert"
              className="rounded-lg border border-rose-200 bg-rose-50 p-2.5 text-xs font-medium text-rose-700 dark:border-rose-900/60 dark:bg-rose-950/40 dark:text-rose-300"
            >
              {errorMessage}
            </div>
          )}

          <div className="flex flex-col-reverse gap-2.5 pt-1 sm:flex-row sm:items-center sm:gap-3">
            <button
              type="button"
              disabled={isPending}
              onClick={() => {
                setShowConfirm(false);
                setErrorMessage(null);
              }}
              className="inline-flex min-h-[44px] w-full touch-manipulation items-center justify-center rounded-lg border border-zinc-300 bg-white px-4 py-2 text-xs font-semibold text-zinc-700 shadow-xs transition-colors hover:bg-zinc-50 active:bg-zinc-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-zinc-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700 dark:focus-visible:ring-offset-zinc-900"
            >
              取消
            </button>
            <button
              type="button"
              disabled={isPending}
              onClick={handleReactivate}
              className="inline-flex min-h-[44px] w-full touch-manipulation items-center justify-center rounded-lg bg-emerald-600 px-4 py-2 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-emerald-500 active:bg-emerald-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto dark:bg-emerald-600 dark:hover:bg-emerald-500 dark:focus-visible:ring-offset-zinc-900"
            >
              {isPending ? "啟用執行中..." : "確認重新上架啟用"}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

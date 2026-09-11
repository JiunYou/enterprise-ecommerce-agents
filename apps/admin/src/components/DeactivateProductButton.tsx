"use client";

import { useState, useTransition } from "react";
import { deactivateProductAction } from "@/app/actions";

interface DeactivateProductButtonProps {
  productId: string;
  productName: string;
}

export function DeactivateProductButton({
  productId,
  productName,
}: DeactivateProductButtonProps) {
  const [showConfirm, setShowConfirm] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const handleDeactivate = () => {
    setErrorMessage(null);
    startTransition(async () => {
      const result = await deactivateProductAction(productId);
      if (!result.success) {
        setErrorMessage(result.error || "停用操作失敗，請稍後重試。");
      } else {
        setShowConfirm(false);
      }
    });
  };

  return (
    <div className="rounded-xl border border-rose-200/80 bg-rose-50/40 p-5 shadow-xs sm:p-6 dark:border-rose-900/50 dark:bg-rose-950/20">
      <h3 className="text-base font-semibold text-rose-900 dark:text-rose-200">
        商品下架與停用
      </h3>
      <p className="mt-1 text-xs text-rose-700 dark:text-rose-300">
        停用後商品將自公開目錄中移除，並阻止未來新的加入購物車操作。
      </p>

      {!showConfirm ? (
        <div className="mt-4">
          <button
            type="button"
            onClick={() => setShowConfirm(true)}
            className="inline-flex min-h-[44px] w-full touch-manipulation items-center justify-center rounded-lg bg-rose-600 px-4 py-2.5 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-rose-500 active:bg-rose-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-500 focus-visible:ring-offset-2 sm:w-auto dark:bg-rose-600 dark:hover:bg-rose-500 dark:focus-visible:ring-offset-zinc-900"
          >
            停用此商品
          </button>
        </div>
      ) : (
        <div className="mt-4 space-y-3 rounded-lg border border-rose-200 bg-white p-4 shadow-xs sm:p-5 dark:border-rose-900/60 dark:bg-zinc-900">
          <div className="text-xs text-zinc-700 dark:text-zinc-300">
            <p className="font-semibold text-rose-600 dark:text-rose-400">
              確認停用商品「{productName}」？
            </p>
            <ul className="mt-2 list-disc space-y-1.5 pl-4 text-zinc-600 dark:text-zinc-400">
              <li>商品將立即自公開目錄中移除，顧客無法於商店檢視。</li>
              <li>未來將嚴格禁止顧客將此商品加入購物車。</li>
              <li>注意：當前版本 (v1) 尚未提供「重新上架 (Reactivate)」功能。</li>
              <li>備註：既有已加入待處理購物車中之項目不會被此操作強制清除。</li>
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
              onClick={handleDeactivate}
              className="inline-flex min-h-[44px] w-full touch-manipulation items-center justify-center rounded-lg bg-rose-600 px-4 py-2 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-rose-500 active:bg-rose-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto dark:bg-rose-600 dark:hover:bg-rose-500 dark:focus-visible:ring-offset-zinc-900"
            >
              {isPending ? "停用執行中..." : "確認下架停用"}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

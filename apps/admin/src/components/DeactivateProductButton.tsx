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
    <div className="rounded-xl border border-rose-200 bg-rose-50/30 p-6 shadow-sm dark:border-rose-900/40 dark:bg-rose-950/10">
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
            className="inline-flex items-center justify-center rounded-lg bg-rose-600 px-4 py-2 text-xs font-semibold text-white shadow-sm transition-colors hover:bg-rose-500 active:bg-rose-700 dark:bg-rose-600 dark:hover:bg-rose-500"
          >
            停用此商品
          </button>
        </div>
      ) : (
        <div className="mt-4 space-y-3 rounded-lg border border-rose-200 bg-white p-4 dark:border-rose-900/60 dark:bg-zinc-900">
          <div className="text-xs text-zinc-700 dark:text-zinc-300">
            <p className="font-semibold text-rose-600 dark:text-rose-400">
              確認停用商品「{productName}」？
            </p>
            <ul className="mt-2 list-disc space-y-1 pl-4 text-zinc-600 dark:text-zinc-400">
              <li>商品將立即自公開目錄中移除，顧客無法於商店檢視。</li>
              <li>未來將嚴格禁止顧客將此商品加入購物車。</li>
              <li>注意：當前版本 (v1) 尚未提供「重新上架 (Reactivate)」功能。</li>
              <li>備註：既有已加入待處理購物車中之項目不會被此操作強制清除。</li>
            </ul>
          </div>

          {errorMessage && (
            <div className="rounded-md bg-rose-50 p-2.5 text-xs text-rose-700 dark:bg-rose-950/40 dark:text-rose-300">
              {errorMessage}
            </div>
          )}

          <div className="flex items-center gap-2 pt-1">
            <button
              type="button"
              disabled={isPending}
              onClick={handleDeactivate}
              className="inline-flex items-center justify-center rounded-lg bg-rose-600 px-3 py-1.5 text-xs font-semibold text-white transition-colors hover:bg-rose-500 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-rose-600 dark:hover:bg-rose-500"
            >
              {isPending ? "停用執行中..." : "確認下架停用"}
            </button>
            <button
              type="button"
              disabled={isPending}
              onClick={() => {
                setShowConfirm(false);
                setErrorMessage(null);
              }}
              className="inline-flex items-center justify-center rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-xs font-semibold text-zinc-700 transition-colors hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
            >
              取消
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

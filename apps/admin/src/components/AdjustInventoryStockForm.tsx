"use client";

import { useState, useTransition } from "react";
import {
  increaseInventoryStockAction,
  decreaseInventoryStockAction,
} from "@/app/actions";

interface AdjustInventoryStockFormProps {
  productId: string;
  availableQuantity: number;
}

export function AdjustInventoryStockForm({
  productId,
  availableQuantity,
}: AdjustInventoryStockFormProps) {
  const [quantityInput, setQuantityInput] = useState<string>("1");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const handleAdjust = (actionType: "increase" | "decrease") => {
    setErrorMessage(null);
    setSuccessMessage(null);

    const parsedQuantity = parseInt(quantityInput, 10);
    if (
      isNaN(parsedQuantity) ||
      !Number.isInteger(parsedQuantity) ||
      parsedQuantity <= 0
    ) {
      setErrorMessage("調整數量必須為大於 0 的正整數。");
      return;
    }

    if (actionType === "decrease") {
      if (parsedQuantity > availableQuantity) {
        setErrorMessage(
          `扣減數量 (${parsedQuantity}) 不得超過當前可用庫存 (${availableQuantity})。`
        );
        return;
      }

      const confirmed = window.confirm(
        `確定要扣減可用庫存 ${parsedQuantity} 單位嗎？\n注意：此操作僅會扣減可用庫存，絕不會影響預留庫存。`
      );
      if (!confirmed) {
        return;
      }
    }

    startTransition(async () => {
      const result =
        actionType === "increase"
          ? await increaseInventoryStockAction(productId, parsedQuantity)
          : await decreaseInventoryStockAction(productId, parsedQuantity);

      if (!result.success) {
        setErrorMessage(result.error || "庫存調整失敗，請稍後重試。");
      } else {
        setSuccessMessage(
          actionType === "increase"
            ? `成功增加可用庫存 ${parsedQuantity} 單位！`
            : `成功扣減可用庫存 ${parsedQuantity} 單位！`
        );
        setQuantityInput("1");
      }
    });
  };

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-5 shadow-xs sm:p-6 dark:border-zinc-800 dark:bg-zinc-900">
      <h3 className="text-base font-semibold text-zinc-900 dark:text-zinc-50">
        庫存調整作業
      </h3>
      <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
        僅限輸入正整數數量。扣減庫存時僅影響「可用庫存」，絕不會修改或影響任何「預留庫存」。
      </p>

      <div className="mt-4 space-y-4">
        <div>
          <label
            htmlFor="adjustQuantity"
            className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
          >
            調整數量 (正整數)
          </label>
          <div className="mt-1.5">
            <input
              type="number"
              id="adjustQuantity"
              name="adjustQuantity"
              step="1"
              min="1"
              required
              disabled={isPending}
              value={quantityInput}
              onChange={(e) => setQuantityInput(e.target.value)}
              className="block min-h-[44px] w-full rounded-lg border border-zinc-300 px-3 py-2 text-sm text-zinc-900 placeholder-zinc-400 shadow-xs transition-colors focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:bg-zinc-100 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-50 dark:placeholder-zinc-500 dark:disabled:bg-zinc-800/50"
              placeholder="例如 10"
            />
          </div>
        </div>

        {errorMessage && (
          <div
            role="alert"
            className="rounded-lg border border-rose-200 bg-rose-50 p-3 text-xs font-medium text-rose-700 dark:border-rose-900/60 dark:bg-rose-950/40 dark:text-rose-300"
          >
            {errorMessage}
          </div>
        )}

        {successMessage && (
          <div
            role="alert"
            className="rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-xs font-medium text-emerald-700 dark:border-emerald-900/60 dark:bg-emerald-950/40 dark:text-emerald-300"
          >
            {successMessage}
          </div>
        )}

        <div className="flex flex-col gap-2.5 sm:flex-row sm:items-center sm:gap-3">
          <button
            type="button"
            disabled={isPending}
            onClick={() => handleAdjust("increase")}
            className="inline-flex min-h-[44px] w-full touch-manipulation items-center justify-center rounded-lg bg-emerald-600 px-4 py-2.5 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-emerald-500 active:bg-emerald-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto dark:bg-emerald-500 dark:hover:bg-emerald-400 dark:focus-visible:ring-offset-zinc-900"
          >
            {isPending ? "處理中..." : "+ 增加庫存 (Increase Stock)"}
          </button>

          <button
            type="button"
            disabled={isPending || availableQuantity <= 0}
            onClick={() => handleAdjust("decrease")}
            className="inline-flex min-h-[44px] w-full touch-manipulation items-center justify-center rounded-lg border border-rose-300 bg-white px-4 py-2.5 text-xs font-semibold text-rose-700 shadow-xs transition-colors hover:bg-rose-50 active:bg-rose-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto dark:border-rose-900/60 dark:bg-zinc-900 dark:text-rose-300 dark:hover:bg-rose-950/30 dark:focus-visible:ring-offset-zinc-900"
          >
            {isPending ? "處理中..." : "- 扣減庫存 (Decrease Stock)"}
          </button>
        </div>

        <p className="text-[11px] text-zinc-500 dark:text-zinc-400">
          * 提醒：扣減操作僅針對「可用庫存」進行，若數量大於當前可用數量將會遭系統拒絕。
        </p>
      </div>
    </div>
  );
}

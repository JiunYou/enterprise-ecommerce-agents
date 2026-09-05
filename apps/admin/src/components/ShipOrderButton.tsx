"use client";

import { useState, useTransition } from "react";
import { shipOrderAction } from "@/app/actions";

interface ShipOrderButtonProps {
  orderId: string;
  hasShippingAddress: boolean;
}

export function ShipOrderButton({
  orderId,
  hasShippingAddress,
}: ShipOrderButtonProps) {
  const [isPending, startTransition] = useTransition();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleShip = () => {
    if (!hasShippingAddress || isPending) {
      return;
    }

    setErrorMessage(null);

    startTransition(async () => {
      const result = await shipOrderAction(orderId);
      if (!result.success && result.error) {
        setErrorMessage(result.error);
      }
    });
  };

  if (!hasShippingAddress) {
    return (
      <div className="flex flex-col items-start gap-1">
        <button
          type="button"
          disabled
          className="cursor-not-allowed rounded-md bg-zinc-200 px-3.5 py-1.5 text-xs font-semibold text-zinc-400 dark:bg-zinc-800 dark:text-zinc-600"
        >
          無法發貨（無地址）
        </button>
        <span className="text-[11px] text-amber-600 dark:text-amber-400">
          歷史訂單無收件資訊
        </span>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-start gap-1">
      <button
        type="button"
        onClick={handleShip}
        disabled={isPending}
        className={`inline-flex items-center justify-center rounded-md px-4 py-2 text-xs font-semibold text-white shadow-sm transition-colors ${
          isPending
            ? "cursor-wait bg-indigo-400 dark:bg-indigo-600"
            : "bg-indigo-600 hover:bg-indigo-500 active:bg-indigo-700 dark:bg-indigo-500 dark:hover:bg-indigo-400"
        }`}
      >
        {isPending ? (
          <>
            <svg
              className="-ml-1 mr-2 h-3.5 w-3.5 animate-spin text-white"
              xmlns="http://www.w3.org/2000/svg"
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
              ></circle>
              <path
                className="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
              ></path>
            </svg>
            處理中...
          </>
        ) : (
          "標記為已出貨"
        )}
      </button>
      {errorMessage && (
        <span className="max-w-xs text-xs text-rose-600 dark:text-rose-400">
          {errorMessage}
        </span>
      )}
    </div>
  );
}

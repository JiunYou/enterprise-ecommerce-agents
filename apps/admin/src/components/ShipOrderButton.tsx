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
      <div className="flex w-full sm:w-auto flex-col items-start gap-1">
        <button
          type="button"
          disabled
          aria-disabled="true"
          className="flex min-h-[44px] w-full sm:w-auto cursor-not-allowed items-center justify-center rounded-lg border border-zinc-200 bg-zinc-100 px-4 py-2 text-xs font-semibold text-zinc-400 shadow-xs dark:border-zinc-800 dark:bg-zinc-800/80 dark:text-zinc-500"
        >
          無法發貨（無地址）
        </button>
        <span className="text-[11px] font-medium text-amber-600 dark:text-amber-400">
          歷史訂單無收件資訊
        </span>
      </div>
    );
  }

  return (
    <div className="flex w-full sm:w-auto flex-col items-start gap-1">
      <button
        type="button"
        onClick={handleShip}
        disabled={isPending}
        className={`inline-flex min-h-[44px] w-full sm:w-auto items-center justify-center rounded-lg px-4 py-2 text-xs font-semibold text-white shadow-xs transition-colors touch-manipulation focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 ${
          isPending
            ? "cursor-wait bg-indigo-500 opacity-90 dark:bg-indigo-600"
            : "bg-indigo-600 hover:bg-indigo-500 active:bg-indigo-700 dark:bg-indigo-500 dark:hover:bg-indigo-400"
        }`}
      >
        {isPending ? (
          <>
            <svg
              className="-ml-1 mr-2 h-4 w-4 animate-spin text-white"
              xmlns="http://www.w3.org/2000/svg"
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
        <span
          role="alert"
          className="max-w-xs break-words text-xs font-medium text-rose-600 dark:text-rose-400"
        >
          {errorMessage}
        </span>
      )}
    </div>
  );
}

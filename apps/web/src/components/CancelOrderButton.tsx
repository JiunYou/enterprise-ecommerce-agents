"use client";

import { useState, useTransition } from "react";
import { cancelCustomerOrder } from "@/app/orders/[id]/actions";

interface CancelOrderButtonProps {
  orderId: string;
}

export function CancelOrderButton({ orderId }: CancelOrderButtonProps) {
  const [isPending, startTransition] = useTransition();
  const [showConfirm, setShowConfirm] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleConfirmCancel = () => {
    setErrorMessage(null);
    startTransition(async () => {
      const res = await cancelCustomerOrder(orderId);
      if (!res.success) {
        setErrorMessage(res.error);
      } else {
        setShowConfirm(false);
      }
    });
  };

  if (!showConfirm) {
    return (
      <div className="flex flex-col items-end gap-1">
        <button
          type="button"
          onClick={() => {
            setErrorMessage(null);
            setShowConfirm(true);
          }}
          className="inline-flex items-center justify-center rounded-lg border border-zinc-300 bg-white px-4 py-2 text-sm font-medium text-zinc-700 shadow-sm transition hover:bg-zinc-50 hover:text-red-600 focus:outline-none focus:ring-2 focus:ring-red-500 focus:ring-offset-2 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-700 dark:hover:text-red-400"
        >
          取消訂單
        </button>
        {errorMessage && (
          <p className="text-xs text-red-600 dark:text-red-400">{errorMessage}</p>
        )}
      </div>
    );
  }

  return (
    <div className="w-full max-w-md rounded-xl border border-amber-200 bg-amber-50/80 p-4 text-left dark:border-amber-900/50 dark:bg-amber-950/30">
      <div className="flex items-start gap-3">
        <div className="rounded-full bg-amber-100 p-1.5 text-amber-700 dark:bg-amber-900/80 dark:text-amber-300">
          <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
            />
          </svg>
        </div>
        <div className="flex-1">
          <h4 className="text-sm font-bold text-amber-900 dark:text-amber-100">
            確定要取消此筆訂單嗎？
          </h4>
          <ul className="mt-2 list-disc space-y-1 pl-4 text-xs text-amber-800 dark:text-amber-200">
            <li>此待付款訂單將會被立即取消。</li>
            <li>系統將立即釋放此訂單所為您保留的商品庫存。</li>
            <li>此取消操作完成後將無法透過線上介面復原。</li>
          </ul>

          <div className="mt-4 flex items-center gap-3">
            <button
              type="button"
              onClick={handleConfirmCancel}
              disabled={isPending}
              className="inline-flex items-center justify-center rounded-lg bg-red-600 px-3.5 py-2 text-xs font-semibold text-white shadow-sm transition hover:bg-red-500 focus:outline-none focus:ring-2 focus:ring-red-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-red-500 dark:hover:bg-red-400"
            >
              {isPending ? (
                <>
                  <svg
                    className="-ml-0.5 mr-2 h-4 w-4 animate-spin text-white"
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
                      d="M4 12a8 8 0 018-8v8H4z"
                    ></path>
                  </svg>
                  正在取消訂單...
                </>
              ) : (
                "確認取消訂單"
              )}
            </button>
            <button
              type="button"
              onClick={() => {
                setShowConfirm(false);
                setErrorMessage(null);
              }}
              disabled={isPending}
              className="inline-flex items-center justify-center rounded-lg border border-zinc-300 bg-white px-3.5 py-2 text-xs font-medium text-zinc-700 shadow-sm transition hover:bg-zinc-50 disabled:cursor-not-allowed disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-700"
            >
              保留訂單
            </button>
          </div>

          {errorMessage && (
            <p className="mt-2 text-xs text-red-600 dark:text-red-400">{errorMessage}</p>
          )}
        </div>
      </div>
    </div>
  );
}

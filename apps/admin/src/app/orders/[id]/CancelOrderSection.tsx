"use client";

import { useState, useTransition } from "react";
import { cancelAdminOrderAction } from "@/app/actions";

interface CancelOrderSectionProps {
  orderId: string;
  status: string;
}

export function CancelOrderSection({ orderId, status }: CancelOrderSectionProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [validationError, setValidationError] = useState<string | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const isPaid = status === "Paid";
  const isCancellable = status === "Pending" || status === "Submitted" || isPaid;

  if (!isCancellable) {
    return null;
  }

  const handleOpen = () => {
    setIsOpen(true);
    setValidationError(null);
    setServerError(null);
  };

  const handleClose = () => {
    if (isPending) return;
    setIsOpen(false);
    setReason("");
    setValidationError(null);
    setServerError(null);
  };

  const handleConfirmCancel = () => {
    if (isPending) return;

    const trimmed = reason.trim();
    if (trimmed.length === 0) {
      setValidationError("請輸入取消原因。");
      return;
    }

    if (trimmed.length > 500) {
      setValidationError("取消原因長度不可超過 500 個字元。");
      return;
    }

    setValidationError(null);
    setServerError(null);

    startTransition(async () => {
      const result = await cancelAdminOrderAction(orderId, reason);
      if (!result.success && result.error) {
        setServerError(result.error);
      } else if (result.success) {
        setIsOpen(false);
        setReason("");
      }
    });
  };

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-5 sm:p-6 shadow-xs dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="min-w-0">
            <h3 className="text-sm font-bold text-zinc-900 dark:text-zinc-50">
              訂單操作 (Order Actions)
            </h3>
            <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
              管理員可對待處理、已提交或已付款狀態之訂單執行取消操作。
            </p>
          </div>
          {!isOpen && (
            <button
              type="button"
              onClick={handleOpen}
              className="inline-flex min-h-[44px] sm:min-h-[36px] touch-manipulation shrink-0 items-center justify-center rounded-lg bg-rose-600 px-4 py-2 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-rose-500 active:bg-rose-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-500 dark:bg-rose-600 dark:hover:bg-rose-500"
            >
              取消訂單
            </button>
          )}
        </div>

        {isOpen && (
          <div className="mt-2 rounded-lg border border-rose-200 bg-rose-50/40 p-4 sm:p-5 dark:border-rose-900/40 dark:bg-rose-950/20">
            <h4 className="text-xs font-bold text-rose-900 dark:text-rose-300">
              確認取消此訂單？
            </h4>
            <p className="mt-1 text-xs text-rose-700 dark:text-rose-400">
              取消後訂單狀態將轉為已取消（Cancelled），並依規則釋放預留庫存。此操作無法復原。
            </p>

            {isPaid && (
              <div className="mt-3 rounded-md border border-amber-300/80 bg-amber-50/80 p-3 text-xs text-amber-900 dark:border-amber-800/60 dark:bg-amber-950/40 dark:text-amber-200">
                <div className="flex items-start gap-2">
                  <span aria-hidden="true" className="font-bold text-amber-600 dark:text-amber-400">
                    ⚠️
                  </span>
                  <div className="space-y-1">
                    <p className="font-semibold">已付款訂單取消與退款義務說明：</p>
                    <ul className="list-disc pl-4 space-y-0.5 text-[11px] text-amber-800 dark:text-amber-300">
                      <li>訂單狀態將轉為已取消（Cancelled）。</li>
                      <li>預留庫存將遵循正常取消事件流程非同步釋放。</li>
                      <li>成功付款將轉為全額退款義務（RefundRequired）。</li>
                      <li>本次取消操作並不代表金流商已完成退款。</li>
                      <li>取消後，實際退款執行與對帳仍為獨立操作，需至下方待退款項（Refund Required）區塊手動執行。</li>
                    </ul>
                  </div>
                </div>
              </div>
            )}

            <div className="mt-3">
              <label
                htmlFor="cancel-reason"
                className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
              >
                取消原因 <span className="text-rose-600">*</span>
              </label>
              <textarea
                id="cancel-reason"
                rows={3}
                value={reason}
                onChange={(e) => {
                  setReason(e.target.value);
                  if (validationError) setValidationError(null);
                  if (serverError) setServerError(null);
                }}
                placeholder="請輸入詳細取消原因說明（必填）..."
                className="mt-1.5 w-full rounded-md border border-zinc-300 bg-white p-2.5 text-xs text-zinc-900 shadow-xs focus:border-rose-500 focus:outline-none focus:ring-1 focus:ring-rose-500 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
                maxLength={500}
                disabled={isPending}
              />
              <div className="mt-1 flex items-center justify-between text-[11px] text-zinc-500 dark:text-zinc-400">
                <span>剩餘上限：500 字元</span>
                <span>{reason.length} / 500</span>
              </div>
            </div>

            <div className="mt-3 rounded-md border border-amber-200 bg-amber-50 p-2.5 text-[11px] text-amber-800 dark:border-amber-900/50 dark:bg-amber-950/40 dark:text-amber-300">
              <span className="font-semibold">
                <span aria-hidden="true">⚠️ </span>資訊安全警告：
              </span>
              請勿輸入密碼、金鑰/機密、信用卡/支付卡資訊或非必要之個人隱私資料。
            </div>

            {validationError && (
              <p className="mt-2 text-xs font-medium text-rose-600 dark:text-rose-400">
                {validationError}
              </p>
            )}

            {serverError && (
              <p className="mt-2 text-xs font-medium text-rose-600 dark:text-rose-400">
                {serverError}
              </p>
            )}

            <div className="mt-4 flex flex-col-reverse gap-2 sm:flex-row sm:items-center sm:justify-end">
              <button
                type="button"
                onClick={handleClose}
                disabled={isPending}
                className="inline-flex min-h-[44px] sm:min-h-[36px] touch-manipulation items-center justify-center rounded-lg border border-zinc-300 bg-white px-4 py-2 text-xs font-semibold text-zinc-700 shadow-xs hover:bg-zinc-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-700"
              >
                放棄
              </button>
              <button
                type="button"
                onClick={handleConfirmCancel}
                disabled={isPending}
                className="inline-flex min-h-[44px] sm:min-h-[36px] touch-manipulation items-center justify-center rounded-lg bg-rose-600 px-4 py-2 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-rose-500 active:bg-rose-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-500 disabled:cursor-wait disabled:bg-rose-400 dark:bg-rose-600 dark:hover:bg-rose-500"
              >
                {isPending ? (
                  <>
                    <svg
                      aria-hidden="true"
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
                    取消執行中...
                  </>
                ) : (
                  "確認取消訂單"
                )}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

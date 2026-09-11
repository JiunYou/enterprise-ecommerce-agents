"use client";

import { useState, useTransition, type FormEvent } from "react";
import { shipOrderAction } from "@/app/actions";

interface ShipOrderButtonProps {
  orderId: string;
  hasShippingAddress: boolean;
}

export function ShipOrderButton({
  orderId,
  hasShippingAddress,
}: ShipOrderButtonProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [carrier, setCarrier] = useState("");
  const [trackingNumber, setTrackingNumber] = useState("");
  const [validationError, setValidationError] = useState<string | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

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

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (isPending) return;

    setValidationError(null);
    setServerError(null);

    const trimmedCarrier = carrier.trim();
    if (!trimmedCarrier) {
      setValidationError("請輸入物流業者名稱。");
      return;
    }
    if (trimmedCarrier.length > 100) {
      setValidationError("物流業者名稱不可超過 100 個字元。");
      return;
    }
    if (/[\x00-\x1F\x7F]/.test(trimmedCarrier)) {
      setValidationError("物流業者名稱不可包含控制字元。");
      return;
    }

    const trimmedTrackingNumber = trackingNumber.trim();
    if (!trimmedTrackingNumber) {
      setValidationError("請輸入物流追蹤單號。");
      return;
    }
    if (trimmedTrackingNumber.length > 100) {
      setValidationError("物流追蹤單號不可超過 100 個字元。");
      return;
    }
    if (/[\x00-\x1F\x7F]/.test(trimmedTrackingNumber)) {
      setValidationError("物流追蹤單號不可包含控制字元。");
      return;
    }

    startTransition(async () => {
      const result = await shipOrderAction(orderId, trimmedCarrier, trimmedTrackingNumber);
      if (!result.success && result.error) {
        setServerError(result.error);
      } else if (result.success) {
        setIsOpen(false);
        setCarrier("");
        setTrackingNumber("");
      }
    });
  };

  const handleCancel = () => {
    setIsOpen(false);
    setValidationError(null);
    setServerError(null);
  };

  if (!isOpen) {
    return (
      <div className="flex w-full sm:w-auto flex-col items-start gap-1">
        <button
          type="button"
          onClick={() => setIsOpen(true)}
          className="inline-flex min-h-[44px] w-full sm:w-auto items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 touch-manipulation focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:bg-indigo-500 dark:hover:bg-indigo-400"
        >
          出貨並登記物流追蹤
        </button>
      </div>
    );
  }

  return (
    <div className="w-full sm:w-80 rounded-xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
      <div className="mb-3">
        <h4 className="text-xs font-bold text-zinc-900 dark:text-zinc-100">
          登記物流追蹤並標記出貨
        </h4>
        <p className="mt-0.5 text-[11px] text-zinc-500 dark:text-zinc-400">
          出貨後訂單將轉為 Shipped 狀態，並保存此物流追蹤紀錄。
        </p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-3">
        <div>
          <label
            htmlFor={`carrier-${orderId}`}
            className="block text-[11px] font-medium text-zinc-700 dark:text-zinc-300"
          >
            物流業者 (Carrier) <span className="text-rose-500">*</span>
          </label>
          <input
            id={`carrier-${orderId}`}
            type="text"
            maxLength={100}
            required
            disabled={isPending}
            value={carrier}
            onChange={(e) => setCarrier(e.target.value)}
            placeholder="例如：黑貓宅急便、順豐速運"
            className="mt-1 block w-full rounded-md border border-zinc-300 px-2.5 py-1.5 text-xs text-zinc-900 placeholder:text-zinc-400 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder:text-zinc-500"
          />
        </div>

        <div>
          <label
            htmlFor={`tracking-${orderId}`}
            className="block text-[11px] font-medium text-zinc-700 dark:text-zinc-300"
          >
            追蹤單號 (Tracking Number) <span className="text-rose-500">*</span>
          </label>
          <input
            id={`tracking-${orderId}`}
            type="text"
            maxLength={100}
            required
            disabled={isPending}
            value={trackingNumber}
            onChange={(e) => setTrackingNumber(e.target.value)}
            placeholder="例如：9876543210"
            className="mt-1 block w-full rounded-md border border-zinc-300 px-2.5 py-1.5 text-xs text-zinc-900 placeholder:text-zinc-400 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100 dark:placeholder:text-zinc-500"
          />
        </div>

        {validationError && (
          <p role="alert" className="text-[11px] font-medium text-rose-600 dark:text-rose-400">
            {validationError}
          </p>
        )}

        {serverError && (
          <p role="alert" className="text-[11px] font-medium text-rose-600 dark:text-rose-400 break-words">
            {serverError}
          </p>
        )}

        <div className="flex items-center gap-2 pt-1">
          <button
            type="submit"
            disabled={isPending}
            className={`inline-flex min-h-[44px] flex-1 items-center justify-center rounded-lg px-3 py-2 text-xs font-semibold text-white shadow-xs transition-colors touch-manipulation focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 ${
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
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
                處理中...
              </>
            ) : (
              "確認出貨並登記"
            )}
          </button>

          <button
            type="button"
            disabled={isPending}
            onClick={handleCancel}
            className="inline-flex min-h-[44px] items-center justify-center rounded-lg border border-zinc-300 bg-white px-3 py-2 text-xs font-semibold text-zinc-700 shadow-xs transition-colors hover:bg-zinc-50 active:bg-zinc-100 touch-manipulation focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-zinc-400 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
          >
            取消
          </button>
        </div>
      </form>
    </div>
  );
}

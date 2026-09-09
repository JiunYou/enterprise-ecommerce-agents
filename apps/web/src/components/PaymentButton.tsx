"use client";

import { useState, useTransition } from "react";
import { startOrderPayment } from "@/app/orders/[id]/actions";

interface PaymentButtonProps {
  orderId: string;
}

export function PaymentButton({ orderId }: PaymentButtonProps) {
  const [isPending, startTransition] = useTransition();
  const [redirecting, setRedirecting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handlePay = () => {
    setErrorMessage(null);
    startTransition(async () => {
      const res = await startOrderPayment(orderId);
      if (!res) {
        return;
      }

      if (!res.success) {
        setErrorMessage(res.error);
        return;
      }

      if (res.method === "POST" && res.actionUrl && res.formFields) {
        setRedirecting(true);
        // 建立真實 DOM form 執行安全 POST 提交至 ECPay Hosted Payment Page
        const form = document.createElement("form");
        form.method = "POST";
        form.action = res.actionUrl;
        form.style.display = "none";

        for (const [key, value] of Object.entries(res.formFields)) {
          const input = document.createElement("input");
          input.type = "hidden";
          input.name = key;
          input.value = String(value);
          form.appendChild(input);
        }

        document.body.appendChild(form);
        form.submit();
      }
    });
  };

  const isLoading = isPending || redirecting;

  return (
    <div className="flex w-full flex-col gap-2 sm:w-auto">
      <button
        type="button"
        onClick={handlePay}
        disabled={isLoading}
        className="inline-flex min-h-[44px] w-full items-center justify-center rounded-xl bg-emerald-600 px-6 py-3 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-emerald-500 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-emerald-500 dark:hover:bg-emerald-400 dark:focus-visible:ring-offset-stone-900 sm:w-auto"
      >
        {isLoading ? (
          <>
            <svg
              className="-ml-1 mr-2.5 h-4 w-4 animate-spin text-white"
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
                d="M4 12a8 8 0 018-8v8H4z"
              ></path>
            </svg>
            正在前往安全付款頁面...
          </>
        ) : (
          "前往安全付款"
        )}
      </button>
      {errorMessage && (
        <p className="break-words text-xs text-red-600 dark:text-red-400">{errorMessage}</p>
      )}
    </div>
  );
}

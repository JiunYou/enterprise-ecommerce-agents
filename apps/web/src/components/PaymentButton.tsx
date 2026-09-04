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
    <div className="flex flex-col gap-2">
      <button
        type="button"
        onClick={handlePay}
        disabled={isLoading}
        className="inline-flex items-center justify-center rounded-lg bg-emerald-600 px-6 py-3 text-base font-semibold text-white shadow-sm transition hover:bg-emerald-500 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-emerald-500 dark:hover:bg-emerald-400"
      >
        {isLoading ? (
          <>
            <svg
              className="-ml-1 mr-3 h-5 w-5 animate-spin text-white"
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
            正在前往安全付款頁面...
          </>
        ) : (
          "前往安全付款"
        )}
      </button>
      {errorMessage && (
        <p className="text-xs text-red-600 dark:text-red-400">{errorMessage}</p>
      )}
    </div>
  );
}

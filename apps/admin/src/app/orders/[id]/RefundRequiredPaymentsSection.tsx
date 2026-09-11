"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { refundAdminPaymentAction } from "@/app/actions";
import { AdminRefundRequiredPayment } from "@/lib/orders";

interface RefundRequiredPaymentsSectionProps {
  orderId?: string;
  refundRequiredPayments?: AdminRefundRequiredPayment[];
}

export function RefundRequiredPaymentsSection({
  refundRequiredPayments,
}: RefundRequiredPaymentsSectionProps) {
  const router = useRouter();
  const [activePaymentId, setActivePaymentId] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [validationError, setValidationError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<{ [key: string]: string }>({});
  const [actionOutcome, setActionOutcome] = useState<{ [key: string]: string }>({});
  const [isPending, startTransition] = useTransition();

  if (!refundRequiredPayments || refundRequiredPayments.length === 0) {
    return null;
  }

  const handleOpenForm = (paymentAttemptId: string) => {
    setActivePaymentId(paymentAttemptId);
    setReason("");
    setValidationError(null);
    setActionError((prev) => ({ ...prev, [paymentAttemptId]: "" }));
    setActionOutcome((prev) => ({ ...prev, [paymentAttemptId]: "" }));
  };

  const handleCloseForm = () => {
    if (isPending) return;
    setActivePaymentId(null);
    setReason("");
    setValidationError(null);
  };

  const handleExecuteRefund = (paymentAttemptId: string) => {
    if (isPending) return;

    const trimmed = reason.trim();
    if (trimmed.length === 0) {
      setValidationError("請輸入退款原因。");
      return;
    }

    if (trimmed.length > 500) {
      setValidationError("退款原因長度不可超過 500 個字元。");
      return;
    }

    setValidationError(null);
    setActionError((prev) => ({ ...prev, [paymentAttemptId]: "" }));
    setActionOutcome((prev) => ({ ...prev, [paymentAttemptId]: "" }));

    startTransition(async () => {
      const result = await refundAdminPaymentAction(paymentAttemptId, trimmed);
      if (!result.success && result.error) {
        setActionError((prev) => ({ ...prev, [paymentAttemptId]: result.error! }));
      } else if (result.success) {
        setActivePaymentId(null);
        setReason("");
        let outcomeMsg = "退款已順利完成。";
        if (result.outcome === "AlreadyRefunded") {
          outcomeMsg = "此筆款項先前已完成退款。";
        } else if (result.outcome === "ManualProviderResolutionRequired") {
          outcomeMsg = "金流狀態異常，需由人工與金流商核對確認。";
        } else if (result.outcome === "Unresolved") {
          outcomeMsg = "金流結果目前無法確認，系統不會自動重送退款。可稍後手動重新檢查。";
        } else if (result.outcome === "ExecutionTemporarilyBlocked") {
          outcomeMsg = "目前暫時無法執行退款，請稍後再手動嘗試。";
        }
        setActionOutcome((prev) => ({ ...prev, [paymentAttemptId]: outcomeMsg }));
        router.refresh();
      }
    });
  };

  const handleReconcile = (paymentAttemptId: string) => {
    if (isPending) return;

    setActionError((prev) => ({ ...prev, [paymentAttemptId]: "" }));
    setActionOutcome((prev) => ({ ...prev, [paymentAttemptId]: "" }));

    startTransition(async () => {
      const result = await refundAdminPaymentAction(paymentAttemptId, null);
      if (!result.success && result.error) {
        setActionError((prev) => ({ ...prev, [paymentAttemptId]: result.error! }));
      } else if (result.success) {
        let outcomeMsg = "退款狀態檢查完成。";
        if (result.outcome === "RefundCompleted") {
          outcomeMsg = "退款已順利完成。";
        } else if (result.outcome === "AlreadyRefunded") {
          outcomeMsg = "此筆款項先前已完成退款。";
        } else if (result.outcome === "ManualProviderResolutionRequired") {
          outcomeMsg = "金流狀態異常，需由人工與金流商核對確認。";
        } else if (result.outcome === "Unresolved") {
          outcomeMsg = "金流結果目前無法確認，系統不會自動重送退款。可稍後手動重新檢查。";
        } else if (result.outcome === "ExecutionTemporarilyBlocked") {
          outcomeMsg = "目前暫時無法執行退款，請稍後再手動嘗試。";
        }
        setActionOutcome((prev) => ({ ...prev, [paymentAttemptId]: outcomeMsg }));
        router.refresh();
      }
    });
  };

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-5 sm:p-6 shadow-xs dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex flex-col gap-4">
        <div>
          <h3 className="text-sm font-bold text-zinc-900 dark:text-zinc-50">
            待退款付款項目 (Refund Required Payments)
          </h3>
          <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
            此訂單包含需要退款處理之付款紀錄。所有退款金額皆由系統付款紀錄唯讀鎖定，無法手動調整。
          </p>
        </div>

        <div className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
          {refundRequiredPayments.map((payment) => {
            const isFormOpen = activePaymentId === payment.paymentAttemptId;
            const errorMsg = actionError[payment.paymentAttemptId];
            const outcomeMsg = actionOutcome[payment.paymentAttemptId];

            const isEligible =
              payment.refundCapability === "Eligible" && payment.refundStatus === null;
            const isCompleted = payment.refundCapability === "Completed";
            const isManualResolution =
              payment.refundCapability === "ManualProviderResolutionRequired";
            const isNotEligible = payment.refundCapability === "NotEligible";
            const isReconciliationOnly =
              payment.refundCapability === "ReconciliationOnly";

            return (
              <div key={payment.paymentAttemptId} className="p-4 text-xs space-y-3">
                {/* 付款資訊純顯示卡片 */}
                <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div className="min-w-0 space-y-1.5">
                    <div>
                      <span className="block text-[10px] font-medium uppercase tracking-wider text-zinc-400 dark:text-zinc-500">
                        付款識別碼 (Payment Attempt ID)
                      </span>
                      <span className="mt-0.5 block break-all font-mono font-semibold text-zinc-900 dark:text-zinc-100">
                        {payment.paymentAttemptId}
                      </span>
                    </div>
                    <div className="flex flex-wrap items-center gap-2 pt-0.5">
                      <span className="font-semibold text-zinc-600 dark:text-zinc-400">
                        退款能力狀態：
                      </span>
                      <span className="inline-flex items-center rounded-full border border-zinc-200/60 bg-zinc-100 px-2 py-0.5 text-[11px] font-medium text-zinc-800 dark:border-zinc-700/60 dark:bg-zinc-800 dark:text-zinc-200">
                        {payment.refundCapability}
                      </span>
                      {payment.refundStatus && (
                        <>
                          <span className="font-semibold text-zinc-600 sm:ml-2 dark:text-zinc-400">
                            退款狀態：
                          </span>
                          <span
                            className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium ${
                              payment.refundStatus === "Succeeded"
                                ? "border border-emerald-200/60 bg-emerald-50 text-emerald-700 dark:border-emerald-800/60 dark:bg-emerald-950 dark:text-emerald-300"
                                : payment.refundStatus === "Failed"
                                ? "border border-rose-200/60 bg-rose-50 text-rose-700 dark:border-rose-800/60 dark:bg-rose-950 dark:text-rose-300"
                                : "border border-amber-200/60 bg-amber-50 text-amber-700 dark:border-amber-800/60 dark:bg-amber-950 dark:text-amber-300"
                            }`}
                          >
                            {payment.refundStatus}
                          </span>
                        </>
                      )}
                    </div>
                  </div>

                  <div className="shrink-0 rounded-lg bg-zinc-50/80 p-2.5 sm:bg-transparent sm:p-0 sm:text-right dark:bg-zinc-800/40 sm:dark:bg-transparent">
                    <span className="block text-[10px] font-semibold uppercase tracking-wider text-zinc-400 dark:text-zinc-500">
                      全額退款金額（唯讀）
                    </span>
                    <p className="mt-0.5 text-sm font-bold text-zinc-900 sm:text-base dark:text-zinc-50">
                      {payment.currency} {payment.amount.toLocaleString()}
                    </p>
                  </div>
                </div>

                {/* 狀態提示與操作區塊 */}
                {isCompleted && (
                  <div className="rounded-md border border-emerald-200 bg-emerald-50/60 p-3 text-emerald-800 dark:border-emerald-900/40 dark:bg-emerald-950/30 dark:text-emerald-300">
                    <div className="flex items-center gap-2">
                      <span aria-hidden="true" className="font-bold">✓</span>
                      <span>退款已完成（Succeeded）。此款項無需再進行任何退款操作。</span>
                    </div>
                  </div>
                )}

                {isManualResolution && (
                  <div className="rounded-md border border-rose-200 bg-rose-50/60 p-3 text-rose-800 dark:border-rose-900/40 dark:bg-rose-950/30 dark:text-rose-300">
                    <div className="flex items-start gap-2">
                      <span aria-hidden="true" className="font-bold">⚠️</span>
                      <div>
                        <p className="font-semibold">需人工確認金流商狀態</p>
                        <p className="mt-0.5 text-[11px] text-rose-700 dark:text-rose-400">
                          金流處理狀態發生不可自癒之異常（如金流拒絕或明確失敗），請由人工洽詢金流服務商確認帳戶明細，請勿重複盲目提交。
                        </p>
                      </div>
                    </div>
                  </div>
                )}

                {isNotEligible && (
                  <div className="rounded-md border border-zinc-200 bg-zinc-50 p-3 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-800/50 dark:text-zinc-400">
                    <p>此付款目前不符合退款條件，無法執行退款操作。</p>
                  </div>
                )}

                {isReconciliationOnly && (
                  <div className="rounded-md border border-amber-200 bg-amber-50/60 p-3 text-amber-800 dark:border-amber-900/40 dark:bg-amber-950/30 dark:text-amber-300 space-y-2">
                    <div className="flex flex-col gap-2.5 sm:flex-row sm:items-start sm:justify-between">
                      <div>
                        <p className="font-semibold">退款處理中或結果待查（Reconciliation Only）</p>
                        <p className="mt-0.5 text-[11px] text-amber-700 dark:text-amber-400">
                          此退款已有本地處理紀錄；重新檢查只會查詢金流狀態，不會重新送出退款。
                        </p>
                      </div>
                      <button
                        type="button"
                        onClick={() => handleReconcile(payment.paymentAttemptId)}
                        disabled={isPending}
                        className="inline-flex min-h-[44px] sm:min-h-[36px] touch-manipulation shrink-0 items-center justify-center rounded-lg border border-amber-300 bg-white px-3.5 py-1.5 text-xs font-semibold text-amber-900 shadow-xs hover:bg-amber-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500 disabled:opacity-50 dark:border-amber-700 dark:bg-zinc-800 dark:text-amber-200 dark:hover:bg-zinc-700"
                      >
                        {isPending ? "檢查中..." : "重新檢查退款狀態"}
                      </button>
                    </div>
                  </div>
                )}

                {isEligible && (
                  <div className="space-y-3">
                    {!isFormOpen ? (
                      <div className="flex flex-col gap-2.5 pt-1 sm:flex-row sm:items-center sm:justify-between">
                        <span className="text-[11px] text-zinc-500 dark:text-zinc-400">
                          此付款符合退款條件，可發動全額退款。
                        </span>
                        <button
                          type="button"
                          onClick={() => handleOpenForm(payment.paymentAttemptId)}
                          disabled={isPending}
                          className="inline-flex min-h-[44px] sm:min-h-[36px] touch-manipulation shrink-0 items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:opacity-50 dark:bg-indigo-600 dark:hover:bg-indigo-500"
                        >
                          執行全額退款
                        </button>
                      </div>
                    ) : (
                      <div className="rounded-lg border border-indigo-200 bg-indigo-50/30 p-4 sm:p-5 dark:border-indigo-900/40 dark:bg-indigo-950/20 space-y-3">
                        <div>
                          <h4 className="font-bold text-indigo-950 dark:text-indigo-200">
                            確認執行全額退款？
                          </h4>
                          <p className="mt-1 text-[11px] text-indigo-800 dark:text-indigo-300">
                            退款金額為全額 <span className="font-bold">{payment.currency} {payment.amount.toLocaleString()}</span>，金額直接由系統紀錄鎖定且無法修改。在正式環境下此操作將向金流服務商發動真實退款交易。
                          </p>
                        </div>

                        <div>
                          <label
                            htmlFor={`refund-reason-${payment.paymentAttemptId}`}
                            className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
                          >
                            退款原因說明 <span className="text-rose-600">*</span>
                          </label>
                          <textarea
                            id={`refund-reason-${payment.paymentAttemptId}`}
                            rows={3}
                            value={reason}
                            onChange={(e) => {
                              setReason(e.target.value);
                              if (validationError) setValidationError(null);
                            }}
                            placeholder="請輸入詳細退款原因（必填）..."
                            className="mt-1.5 w-full rounded-md border border-zinc-300 bg-white p-2.5 text-xs text-zinc-900 shadow-xs focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
                            maxLength={500}
                            disabled={isPending}
                          />
                          <div className="mt-1 flex items-center justify-between text-[11px] text-zinc-500 dark:text-zinc-400">
                            <span>剩餘上限：500 字元</span>
                            <span>{reason.length} / 500</span>
                          </div>
                        </div>

                        <div className="rounded-md border border-amber-200 bg-amber-50 p-2.5 text-[11px] text-amber-800 dark:border-amber-900/50 dark:bg-amber-950/40 dark:text-amber-300">
                          <span className="font-semibold">
                            <span aria-hidden="true">⚠️ </span>資訊安全警告：
                          </span>
                          請勿輸入密碼、API 金鑰/機密、信用卡/支付卡資訊或非必要之個人隱私資料。
                        </div>

                        {validationError && (
                          <p className="text-xs font-medium text-rose-600 dark:text-rose-400">
                            {validationError}
                          </p>
                        )}

                        <div className="flex flex-col-reverse gap-2 pt-1 sm:flex-row sm:items-center sm:justify-end">
                          <button
                            type="button"
                            onClick={handleCloseForm}
                            disabled={isPending}
                            className="inline-flex min-h-[44px] sm:min-h-[36px] touch-manipulation items-center justify-center rounded-lg border border-zinc-300 bg-white px-4 py-2 text-xs font-semibold text-zinc-700 shadow-xs hover:bg-zinc-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-700"
                          >
                            取消
                          </button>
                          <button
                            type="button"
                            onClick={() => handleExecuteRefund(payment.paymentAttemptId)}
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
                                退款執行中...
                              </>
                            ) : (
                              "確認執行全額退款"
                            )}
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {/* 錯誤或結果反饋 */}
                {errorMsg && (
                  <p className="mt-2 text-xs font-medium text-rose-600 dark:text-rose-400">
                    {errorMsg}
                  </p>
                )}
                {outcomeMsg && (
                  <p className="mt-2 text-xs font-medium text-emerald-600 dark:text-emerald-400">
                    {outcomeMsg}
                  </p>
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

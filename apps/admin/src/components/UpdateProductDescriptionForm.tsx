"use client";

import { useState, useTransition } from "react";
import { updateProductDescriptionAction } from "@/app/actions";

interface UpdateProductDescriptionFormProps {
  productId: string;
  currentDescription: string;
}

export function UpdateProductDescriptionForm({
  productId,
  currentDescription,
}: UpdateProductDescriptionFormProps) {
  const [descriptionInput, setDescriptionInput] = useState(currentDescription);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    setSuccessMessage(null);

    const trimmedInput = descriptionInput.trim();
    if (trimmedInput.length > 2000) {
      setErrorMessage("商品描述長度不可超過 2000 個字元。");
      return;
    }

    if (trimmedInput === currentDescription.trim()) {
      setErrorMessage("新商品描述與現有描述相同，無須更新。");
      return;
    }

    startTransition(async () => {
      const result = await updateProductDescriptionAction(productId, descriptionInput);
      if (!result.success) {
        setErrorMessage(result.error || "商品描述更新失敗，請稍後重試。");
      } else {
        setSuccessMessage(
          trimmedInput === ""
            ? "商品描述已成功清除！"
            : "商品描述已成功更新！"
        );
      }
    });
  };

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-5 shadow-xs sm:p-6 dark:border-zinc-800 dark:bg-zinc-900">
      <h3 className="text-base font-semibold text-zinc-900 dark:text-zinc-50">
        編輯商品描述
      </h3>
      <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
        更新前台展示的詳細說明。長度上限為 2000 個字元；若留空提交則視為清空描述。
      </p>

      <form onSubmit={handleSubmit} className="mt-4 space-y-4">
        <div>
          <div className="flex items-center justify-between">
            <label
              htmlFor="productDescription"
              className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
            >
              商品描述
            </label>
            <span className="text-[11px] text-zinc-400 dark:text-zinc-500">
              {descriptionInput.trim().length} / 2000 字
            </span>
          </div>
          <div className="mt-1.5">
            <textarea
              id="productDescription"
              name="description"
              rows={4}
              maxLength={2000}
              disabled={isPending}
              value={descriptionInput}
              onChange={(e) => setDescriptionInput(e.target.value)}
              className="block w-full rounded-lg border border-zinc-300 px-3 py-2 text-sm text-zinc-900 placeholder-zinc-400 shadow-xs transition-colors focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:bg-zinc-100 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-50 dark:placeholder-zinc-500 dark:disabled:bg-zinc-800/50"
              placeholder="請輸入商品詳細描述，支援換行；若欲清空請留白"
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

        <button
          type="submit"
          disabled={isPending}
          className="inline-flex min-h-[44px] w-full touch-manipulation items-center justify-center rounded-lg bg-indigo-600 px-4 py-2.5 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto dark:bg-indigo-500 dark:hover:bg-indigo-400 dark:focus-visible:ring-offset-zinc-900"
        >
          {isPending ? "儲存更新中..." : "確認更新描述"}
        </button>
      </form>
    </div>
  );
}

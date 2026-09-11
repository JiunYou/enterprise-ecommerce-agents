"use client";

import { useState, useTransition } from "react";
import { updateProductNameAction } from "@/app/actions";

interface UpdateProductNameFormProps {
  productId: string;
  currentName: string;
}

export function UpdateProductNameForm({
  productId,
  currentName,
}: UpdateProductNameFormProps) {
  const [nameInput, setNameInput] = useState(currentName);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    setSuccessMessage(null);

    const trimmedName = nameInput.trim();
    if (!trimmedName) {
      setErrorMessage("請輸入有效的商品名稱。");
      return;
    }

    if (trimmedName.length > 255) {
      setErrorMessage("商品名稱長度不可超過 255 個字元。");
      return;
    }

    if (trimmedName === currentName.trim()) {
      setErrorMessage("新商品名稱與現有名稱相同，無須更新。");
      return;
    }

    startTransition(async () => {
      const result = await updateProductNameAction(productId, trimmedName);
      if (!result.success) {
        setErrorMessage(result.error || "商品名稱更新失敗，請稍後重試。");
      } else {
        setSuccessMessage("商品名稱已成功更新！");
      }
    });
  };

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-5 shadow-xs sm:p-6 dark:border-zinc-800 dark:bg-zinc-900">
      <h3 className="text-base font-semibold text-zinc-900 dark:text-zinc-50">
        修改商品名稱
      </h3>
      <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
        更新商品在公開目錄與後台顯示的名稱。長度上限為 255 個字元。
      </p>

      <form onSubmit={handleSubmit} className="mt-4 space-y-4">
        <div>
          <label
            htmlFor="newName"
            className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
          >
            商品名稱
          </label>
          <div className="mt-1.5">
            <input
              type="text"
              id="newName"
              name="newName"
              maxLength={255}
              required
              disabled={isPending}
              value={nameInput}
              onChange={(e) => setNameInput(e.target.value)}
              className="block min-h-[44px] w-full rounded-lg border border-zinc-300 px-3 py-2 text-sm text-zinc-900 placeholder-zinc-400 shadow-xs transition-colors focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:bg-zinc-100 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-50 dark:placeholder-zinc-500 dark:disabled:bg-zinc-800/50"
              placeholder="請輸入商品名稱"
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
          {isPending ? "儲存更新中..." : "確認更新名稱"}
        </button>
      </form>
    </div>
  );
}

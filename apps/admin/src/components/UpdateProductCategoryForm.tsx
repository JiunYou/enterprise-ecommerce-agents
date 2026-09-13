"use client";

import { useState, useTransition } from "react";
import { updateProductCategoryAction } from "@/app/actions";

interface UpdateProductCategoryFormProps {
  productId: string;
  currentCategory: string;
}

export function UpdateProductCategoryForm({
  productId,
  currentCategory,
}: UpdateProductCategoryFormProps) {
  const [categoryInput, setCategoryInput] = useState(currentCategory || "");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const trimmedCurrent = (currentCategory || "").trim();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    setSuccessMessage(null);

    const trimmedInput = categoryInput.trim();

    if (trimmedInput.length > 100) {
      setErrorMessage("商品分類長度不可超過 100 個字元。");
      return;
    }

    if (trimmedInput === trimmedCurrent) {
      setErrorMessage("新商品分類與現有分類相同，無須更新。");
      return;
    }

    startTransition(async () => {
      const result = await updateProductCategoryAction(productId, categoryInput);
      if (!result.success) {
        setErrorMessage(result.error || "商品分類更新失敗，請稍後重試。");
      } else {
        setSuccessMessage(
          trimmedInput === ""
            ? "商品分類已成功清除！"
            : "商品分類已成功更新！"
        );
      }
    });
  };

  const handleClear = () => {
    setCategoryInput("");
    setErrorMessage(null);
    setSuccessMessage(null);
  };

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-5 shadow-xs sm:p-6 dark:border-zinc-800 dark:bg-zinc-900">
      <h3 className="text-base font-semibold text-zinc-900 dark:text-zinc-50">
        編輯商品分類
      </h3>
      <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
        設定商品所屬的單一分類。長度上限 100 字元；留空並儲存即可清空商品分類。
      </p>

      <form onSubmit={handleSubmit} className="mt-4 space-y-4">
        <div>
          <div className="flex items-center justify-between">
            <label
              htmlFor="productCategory"
              className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
            >
              商品分類 (Category)
            </label>
            <span className="text-[11px] text-zinc-400 dark:text-zinc-500">
              {categoryInput.trim().length} / 100 字
            </span>
          </div>
          <div className="mt-1.5 flex gap-2">
            <input
              id="productCategory"
              name="category"
              type="text"
              maxLength={100}
              disabled={isPending}
              value={categoryInput}
              onChange={(e) => setCategoryInput(e.target.value)}
              className="block w-full rounded-lg border border-zinc-300 px-3 py-2 text-sm text-zinc-900 placeholder-zinc-400 shadow-xs transition-colors focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:bg-zinc-100 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-50 dark:placeholder-zinc-500 dark:disabled:bg-zinc-800/50"
              placeholder="例如：3C 數位、書籍、家居生活"
            />
            {categoryInput.length > 0 && (
              <button
                type="button"
                onClick={handleClear}
                disabled={isPending}
                className="inline-flex min-h-[40px] items-center justify-center rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-xs font-medium text-zinc-700 shadow-2xs transition-colors hover:bg-zinc-50 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-700"
              >
                清空
              </button>
            )}
          </div>
          <p className="mt-1 text-[11px] text-zinc-500 dark:text-zinc-400">
            支援輸入任意繁體中文或英數字分類名稱，長度不可超過 100 字元。
          </p>
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

        <div className="flex justify-end">
          <button
            type="submit"
            disabled={isPending}
            className="inline-flex min-h-[40px] items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold text-white shadow-xs transition-colors hover:bg-indigo-500 active:bg-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:opacity-50 dark:bg-indigo-500 dark:hover:bg-indigo-400"
          >
            {isPending ? "更新中..." : "儲存商品分類"}
          </button>
        </div>
      </form>
    </div>
  );
}

"use client";

import { useState, useTransition } from "react";
import { updateProductImageUrlAction } from "@/app/actions";

interface UpdateProductImageUrlFormProps {
  productId: string;
  currentImageUrl: string;
}

export function UpdateProductImageUrlForm({
  productId,
  currentImageUrl,
}: UpdateProductImageUrlFormProps) {
  const [imageUrlInput, setImageUrlInput] = useState(currentImageUrl || "");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [previewError, setPreviewError] = useState(false);
  const [isPending, startTransition] = useTransition();

  const trimmedCurrent = (currentImageUrl || "").trim();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    setSuccessMessage(null);

    const trimmedInput = imageUrlInput.trim();

    if (trimmedInput.length > 2048) {
      setErrorMessage("圖片網址長度不可超過 2048 個字元。");
      return;
    }

    if (trimmedInput.length > 0) {
      try {
        const parsedUrl = new URL(trimmedInput);
        if (parsedUrl.protocol.toLowerCase() !== "https:") {
          setErrorMessage("圖片網址通訊協定僅允許 HTTPS。");
          return;
        }
        if (!parsedUrl.hostname) {
          setErrorMessage("圖片網址必須包含有效的主機名稱。");
          return;
        }
        if (parsedUrl.username || parsedUrl.password) {
          setErrorMessage("圖片網址不可包含使用者帳號密碼資訊。");
          return;
        }
      } catch {
        setErrorMessage("圖片網址格式無效，必須為合法的 HTTPS 絕對網址。");
        return;
      }
    }

    if (trimmedInput === trimmedCurrent) {
      setErrorMessage("新圖片網址與現有網址相同，無須更新。");
      return;
    }

    startTransition(async () => {
      const result = await updateProductImageUrlAction(productId, imageUrlInput);
      if (!result.success) {
        setErrorMessage(result.error || "商品圖片網址更新失敗，請稍後重試。");
      } else {
        setPreviewError(false);
        setSuccessMessage(
          trimmedInput === ""
            ? "商品圖片網址已成功清除！"
            : "商品圖片網址已成功更新！"
        );
      }
    });
  };

  const handleClear = () => {
    setImageUrlInput("");
    setErrorMessage(null);
    setSuccessMessage(null);
  };

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-5 shadow-xs sm:p-6 dark:border-zinc-800 dark:bg-zinc-900">
      <h3 className="text-base font-semibold text-zinc-900 dark:text-zinc-50">
        編輯商品圖片網址
      </h3>
      <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
        設定外部託管的商品主要圖片。必須為 HTTPS 絕對網址，長度上限 2048 字元；若清空輸入並送出則視為移除圖片。
      </p>

      {/* 圖片預覽區塊 */}
      <div className="mt-4">
        <span className="block text-xs font-medium text-zinc-700 dark:text-zinc-300">
          當前圖片預覽
        </span>
        <div className="mt-1.5 flex items-center justify-center overflow-hidden rounded-lg border border-zinc-200 bg-zinc-50 p-2 dark:border-zinc-800 dark:bg-zinc-850">
          {trimmedCurrent && !previewError ? (
            <div className="relative aspect-4/3 w-full max-w-xs overflow-hidden rounded-md bg-white shadow-2xs dark:bg-zinc-900">
              {/* eslint-disable-next-line @next/next/no-img-element -- Bounded external product image preview without server-side proxy */}
              <img
                src={trimmedCurrent}
                alt="商品圖片預覽"
                decoding="async"
                referrerPolicy="no-referrer"
                onError={() => setPreviewError(true)}
                className="h-full w-full object-cover"
              />
            </div>
          ) : (
            <div className="flex h-32 w-full max-w-xs flex-col items-center justify-center rounded-md border border-dashed border-zinc-300 bg-zinc-100/70 text-zinc-400 dark:border-zinc-700 dark:bg-zinc-800/50 dark:text-zinc-500">
              <svg
                className="h-8 w-8"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                aria-hidden="true"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={1.5}
                  d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"
                />
              </svg>
              <span className="mt-1 text-xs">
                {previewError ? "外部圖片無法載入" : "尚未設定商品圖片"}
              </span>
            </div>
          )}
        </div>
      </div>

      <form onSubmit={handleSubmit} className="mt-4 space-y-4">
        <div>
          <div className="flex items-center justify-between">
            <label
              htmlFor="productImageUrl"
              className="block text-xs font-medium text-zinc-700 dark:text-zinc-300"
            >
              圖片網址 (Image URL)
            </label>
            <span className="text-[11px] text-zinc-400 dark:text-zinc-500">
              {imageUrlInput.trim().length} / 2048 字
            </span>
          </div>
          <div className="mt-1.5 flex gap-2">
            <input
              id="productImageUrl"
              name="imageUrl"
              type="url"
              maxLength={2048}
              disabled={isPending}
              value={imageUrlInput}
              onChange={(e) => setImageUrlInput(e.target.value)}
              className="block w-full rounded-lg border border-zinc-300 px-3 py-2 text-sm text-zinc-900 placeholder-zinc-400 shadow-xs transition-colors focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:bg-zinc-100 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-50 dark:placeholder-zinc-500 dark:disabled:bg-zinc-800/50"
              placeholder="https://example.com/image.jpg"
            />
            {imageUrlInput.length > 0 && (
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
            僅支援 HTTPS 協定，不可含有使用者憑證，留空提交即可清空圖片。
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
            {isPending ? "更新中..." : "儲存圖片網址"}
          </button>
        </div>
      </form>
    </div>
  );
}

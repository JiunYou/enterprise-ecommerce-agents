"use client";

import React, { useState, useTransition } from "react";
import { createProductReviewAction } from "@/app/products/[id]/actions";

interface ProductReviewFormProps {
  productId: string;
}

export function ProductReviewForm({ productId }: ProductReviewFormProps) {
  const [rating, setRating] = useState<number>(5);
  const [comment, setComment] = useState<string>("");
  const [isPending, startTransition] = useTransition();
  const [feedback, setFeedback] = useState<{
    type: "success" | "error" | "conflict";
    message: string;
  } | null>(null);

  const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setFeedback(null);

    const trimmed = comment.trim();
    if (!trimmed) {
      setFeedback({ type: "error", message: "請輸入評論內容" });
      return;
    }

    if (trimmed.length > 2000) {
      setFeedback({ type: "error", message: "評論內容不得超過 2000 個字元" });
      return;
    }

    startTransition(async () => {
      const result = await createProductReviewAction(productId, rating, trimmed);

      if (result.success) {
        setFeedback({ type: "success", message: "評論發布成功！感謝您的寶貴意見。" });
        setComment("");
        setRating(5);
      } else if (result.status === 409) {
        setFeedback({ type: "conflict", message: "你已評論過此商品" });
      } else {
        setFeedback({
          type: "error",
          message: result.error || "評論發布失敗，請稍後再試",
        });
      }
    });
  };

  return (
    <div className="rounded-xl border border-stone-200/90 bg-white p-6 shadow-2xs dark:border-stone-800 dark:bg-stone-900">
      <h3 className="text-base font-semibold text-stone-900 dark:text-stone-100">
        撰寫商品評論
      </h3>

      <form onSubmit={handleSubmit} className="mt-4 space-y-4">
        {/* 評分選擇 */}
        <div>
          <label
            htmlFor="rating-select"
            className="block text-xs font-semibold uppercase tracking-wider text-stone-600 dark:text-stone-300"
          >
            商品評分
          </label>
          <div className="mt-1 flex items-center gap-2">
            <select
              id="rating-select"
              name="rating"
              value={rating}
              onChange={(e) => setRating(Number(e.target.value))}
              disabled={isPending}
              aria-label="選擇商品評分 (1 至 5 顆星)"
              className="rounded-lg border border-stone-300 bg-white px-3 py-2 text-sm text-stone-900 shadow-2xs focus:border-stone-900 focus:outline-none focus:ring-1 focus:ring-stone-900 disabled:opacity-50 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-100 dark:focus:border-stone-100 dark:focus:ring-stone-100"
            >
              <option value={5}>5 顆星 (極佳)</option>
              <option value={4}>4 顆星 (很好)</option>
              <option value={3}>3 顆星 (普通)</option>
              <option value={2}>2 顆星 (尚可)</option>
              <option value={1}>1 顆星 (不滿意)</option>
            </select>
            <span className="text-sm font-medium text-stone-600 dark:text-stone-400">
              {rating} / 5
            </span>
          </div>
        </div>

        {/* 評論內容 */}
        <div>
          <div className="flex items-center justify-between">
            <label
              htmlFor="comment-textarea"
              className="block text-xs font-semibold uppercase tracking-wider text-stone-600 dark:text-stone-300"
            >
              評論心得 (純文字，上限 2000 字)
            </label>
            <span className="text-xs text-stone-400 dark:text-stone-500">
              {comment.length} / 2000
            </span>
          </div>
          <textarea
            id="comment-textarea"
            name="comment"
            rows={4}
            maxLength={2000}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            disabled={isPending}
            placeholder="分享您對商品的評價與使用心得..."
            aria-label="評論內容輸入框"
            className="mt-1 block w-full rounded-lg border border-stone-300 bg-white p-3 text-sm text-stone-900 shadow-2xs placeholder:text-stone-400 focus:border-stone-900 focus:outline-none focus:ring-1 focus:ring-stone-900 disabled:opacity-50 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-100 dark:placeholder:text-stone-500 dark:focus:border-stone-100 dark:focus:ring-stone-100"
          />
        </div>

        {/* 狀態訊息提示 */}
        {feedback && (
          <div
            role="status"
            aria-live="polite"
            className={`rounded-lg p-3 text-sm font-medium ${
              feedback.type === "success"
                ? "border border-emerald-200 bg-emerald-50 text-emerald-800 dark:border-emerald-800/40 dark:bg-emerald-950/40 dark:text-emerald-300"
                : feedback.type === "conflict"
                ? "border border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-800/40 dark:bg-amber-950/40 dark:text-amber-300"
                : "border border-rose-200 bg-rose-50 text-rose-800 dark:border-rose-800/40 dark:bg-rose-950/40 dark:text-rose-300"
            }`}
          >
            {feedback.message}
          </div>
        )}

        {/* 提交按鈕 */}
        <div className="flex justify-end">
          <button
            type="submit"
            disabled={isPending || comment.trim().length === 0}
            className="inline-flex items-center justify-center rounded-lg bg-stone-900 px-5 py-2.5 text-sm font-medium text-white shadow-2xs transition hover:bg-stone-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
          >
            {isPending ? "送出中..." : "送出評論"}
          </button>
        </div>
      </form>
    </div>
  );
}

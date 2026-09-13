import React from "react";
import Link from "next/link";
import { ProductReviewsResult } from "@/lib/reviews";
import { ProductReviewForm } from "./ProductReviewForm";

interface ProductReviewsSectionProps {
  productId: string;
  isLoggedIn: boolean;
  reviewsResult: ProductReviewsResult | null;
  currentPage: number;
}

export function ProductReviewsSection({
  productId,
  isLoggedIn,
  reviewsResult,
  currentPage,
}: ProductReviewsSectionProps) {
  return (
    <section
      aria-label="顧客商品評論"
      className="mt-12 border-t border-stone-200/80 pt-10 dark:border-stone-800"
    >
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-stone-900 dark:text-stone-50 sm:text-2xl">
            顧客評論
          </h2>
          <p className="mt-1 text-xs text-stone-500 dark:text-stone-400">
            真實購買顧客的心得回饋
          </p>
        </div>

        {/* 未登入狀態引導登入按鈕 */}
        {!isLoggedIn && (
          <div>
            <Link
              href={`/auth/login?returnTo=${encodeURIComponent(`/products/${productId}`)}`}
              className="inline-flex items-center justify-center rounded-lg border border-stone-300 bg-white px-4 py-2 text-sm font-medium text-stone-700 shadow-2xs transition hover:bg-stone-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 dark:border-stone-700 dark:bg-stone-900 dark:text-stone-300 dark:hover:bg-stone-800"
            >
              登入後撰寫評論
            </Link>
          </div>
        )}
      </div>

      {/* 已登入撰寫評論區塊 */}
      {isLoggedIn && (
        <div className="mt-6">
          <ProductReviewForm productId={productId} />
        </div>
      )}

      {/* 評論載入狀態 / 清單區塊 */}
      <div className="mt-8">
        {!reviewsResult || !reviewsResult.success ? (
          <div
            role="status"
            className="rounded-xl border border-stone-200/90 bg-stone-50/60 p-6 text-center text-sm text-stone-500 dark:border-stone-800 dark:bg-stone-900/40 dark:text-stone-400"
          >
            評論暫時無法載入
          </div>
        ) : reviewsResult.data.items.length === 0 ? (
          <div className="rounded-xl border border-dashed border-stone-200 p-8 text-center text-sm text-stone-500 dark:border-stone-800 dark:text-stone-400">
            目前尚無商品評論，歡迎撰寫第一則評論！
          </div>
        ) : (
          <div className="space-y-4">
            <ul className="divide-y divide-stone-200/80 rounded-xl border border-stone-200/90 bg-white shadow-2xs dark:divide-stone-800 dark:border-stone-800 dark:bg-stone-900">
              {reviewsResult.data.items.map((review, index) => {
                const dateText = new Date(review.createdAt).toLocaleDateString("zh-TW", {
                  year: "numeric",
                  month: "2-digit",
                  day: "2-digit",
                });

                return (
                  <li key={`${review.createdAt}-${index}`} className="p-6">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <span
                          className="inline-flex items-center rounded-md bg-stone-100 px-2 py-0.5 text-xs font-semibold text-stone-800 dark:bg-stone-800 dark:text-stone-200"
                          aria-label={`評分 ${review.rating} 顆星，最高 5 顆星`}
                        >
                          ★ {review.rating} / 5
                        </span>
                      </div>
                      <time
                        dateTime={review.createdAt}
                        className="text-xs text-stone-400 dark:text-stone-500"
                      >
                        {dateText}
                      </time>
                    </div>

                    {/* 純文字留言渲染，嚴格杜絕任何 HTML 轉譯，保留換行並安全斷詞 */}
                    <p className="mt-3 text-sm leading-relaxed text-stone-700 whitespace-pre-wrap break-words dark:text-stone-300">
                      {review.comment}
                    </p>
                  </li>
                );
              })}
            </ul>

            {/* 分頁導覽 */}
            {reviewsResult.data.totalCount > reviewsResult.data.pageSize && (
              <nav
                aria-label="評論分頁導覽"
                className="flex items-center justify-between border-t border-stone-200/60 pt-4 dark:border-stone-800"
              >
                <div className="text-xs text-stone-500 dark:text-stone-400">
                  第 {reviewsResult.data.page} 頁 / 共{" "}
                  {Math.ceil(reviewsResult.data.totalCount / reviewsResult.data.pageSize)} 頁
                  （共 {reviewsResult.data.totalCount} 則評論）
                </div>

                <div className="flex items-center gap-2">
                  {currentPage > 1 ? (
                    <Link
                      href={`/products/${productId}?reviewPage=${currentPage - 1}`}
                      scroll={false}
                      className="rounded-lg border border-stone-300 bg-white px-3 py-1.5 text-xs font-medium text-stone-700 shadow-2xs hover:bg-stone-50 dark:border-stone-700 dark:bg-stone-900 dark:text-stone-300 dark:hover:bg-stone-800"
                    >
                      上一頁
                    </Link>
                  ) : (
                    <span className="rounded-lg border border-stone-200 bg-stone-100 px-3 py-1.5 text-xs font-medium text-stone-400 dark:border-stone-800 dark:bg-stone-850 dark:text-stone-600">
                      上一頁
                    </span>
                  )}

                  {currentPage * reviewsResult.data.pageSize < reviewsResult.data.totalCount ? (
                    <Link
                      href={`/products/${productId}?reviewPage=${currentPage + 1}`}
                      scroll={false}
                      className="rounded-lg border border-stone-300 bg-white px-3 py-1.5 text-xs font-medium text-stone-700 shadow-2xs hover:bg-stone-50 dark:border-stone-700 dark:bg-stone-900 dark:text-stone-300 dark:hover:bg-stone-800"
                    >
                      下一頁
                    </Link>
                  ) : (
                    <span className="rounded-lg border border-stone-200 bg-stone-100 px-3 py-1.5 text-xs font-medium text-stone-400 dark:border-stone-800 dark:bg-stone-850 dark:text-stone-600">
                      下一頁
                    </span>
                  )}
                </div>
              </nav>
            )}
          </div>
        )}
      </div>
    </section>
  );
}

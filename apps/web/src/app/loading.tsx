export default function Loading() {
  return (
    <div
      aria-busy="true"
      aria-label="正在載入商品目錄"
      className="min-h-screen bg-stone-50 text-stone-900 dark:bg-stone-950 dark:text-stone-100"
    >
      {/* 頂部導航骨架 */}
      <header className="border-b border-stone-200 bg-white dark:border-stone-800 dark:bg-stone-900">
        <div className="mx-auto max-w-6xl px-4 py-4 sm:px-6 sm:py-5 lg:px-8">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <span className="block text-xl font-bold tracking-tight text-stone-950 dark:text-stone-50 sm:text-2xl">
                Enterprise Commerce
              </span>
              <p className="mt-0.5 text-xs text-stone-500 dark:text-stone-400 sm:text-sm">
                商品型錄與線上商務展示
              </p>
            </div>
            <div className="flex items-center gap-4 sm:gap-6">
              <div className="h-5 w-12 animate-pulse rounded bg-stone-200 dark:bg-stone-800" />
              <div className="h-9 w-24 animate-pulse rounded-md bg-stone-200 dark:bg-stone-800" />
            </div>
          </div>
        </div>
      </header>

      {/* 主要內容載入區 */}
      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
        {/* 目錄標題與導言骨架 */}
        <div className="mb-8 space-y-2">
          <div className="h-8 w-40 animate-pulse rounded-md bg-stone-200 dark:bg-stone-800 sm:h-9" />
          <div className="h-4 w-72 animate-pulse rounded bg-stone-200/80 dark:bg-stone-800/70" />
        </div>

        {/* 搜尋與排序工具列骨架 */}
        <div className="mb-8 rounded-2xl border border-stone-200/80 bg-white/80 p-4 dark:border-stone-800/80 dark:bg-stone-900/60 sm:p-5">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
            <div className="h-10 flex-1 animate-pulse rounded-lg bg-stone-200 dark:bg-stone-800" />
            <div className="flex gap-2">
              <div className="h-10 w-28 animate-pulse rounded-lg bg-stone-200 dark:bg-stone-800" />
              <div className="h-10 w-20 animate-pulse rounded-lg bg-stone-200 dark:bg-stone-800" />
            </div>
          </div>
        </div>

        <div className="mb-5 flex items-center justify-between">
          <div className="h-4 w-48 animate-pulse rounded bg-stone-200 dark:bg-stone-800" />
        </div>

        {/* 商品卡片骨架網格 */}
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {Array.from({ length: 8 }).map((_, index) => (
            <div
              key={index}
              className="flex flex-col rounded-xl border border-stone-200/90 bg-white p-4 shadow-2xs dark:border-stone-800 dark:bg-stone-900"
            >
              {/* 展示性視覺磚骨架 */}
              <div className="aspect-4/3 w-full animate-pulse rounded-lg bg-stone-200/90 dark:bg-stone-800/80" />

              {/* 資訊骨架 */}
              <div className="mt-3.5 flex flex-1 flex-col justify-between">
                <div className="space-y-2">
                  <div className="h-4.5 w-4/5 animate-pulse rounded bg-stone-200 dark:bg-stone-800" />
                  <div className="h-3 w-1/3 animate-pulse rounded bg-stone-200/70 dark:bg-stone-800/60" />
                </div>
                <div className="mt-4 border-t border-stone-100 pt-3 dark:border-stone-800/80">
                  <div className="h-5 w-24 animate-pulse rounded bg-stone-200 dark:bg-stone-800" />
                </div>
              </div>
            </div>
          ))}
        </div>
      </main>
    </div>
  );
}

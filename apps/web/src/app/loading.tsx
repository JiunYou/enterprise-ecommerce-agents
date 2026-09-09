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
        <div className="mb-8">
          <div className="h-10 w-full max-w-md animate-pulse rounded-lg bg-stone-200 dark:bg-stone-800" />
        </div>

        <div className="mb-4 flex items-center justify-between">
          <span className="text-sm text-stone-500 dark:text-stone-400">
            正在載入商品目錄...
          </span>
        </div>

        {/* 商品卡片骨架網格 */}
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {Array.from({ length: 8 }).map((_, index) => (
            <div
              key={index}
              className="flex flex-col justify-between rounded-xl border border-stone-200 bg-white p-6 shadow-sm dark:border-stone-800 dark:bg-stone-900"
            >
              <div className="space-y-2">
                <div className="h-5 w-3/4 animate-pulse rounded bg-stone-200 dark:bg-stone-800" />
                <div className="h-3 w-1/2 animate-pulse rounded bg-stone-200 dark:bg-stone-800" />
              </div>
              <div className="mt-6 border-t border-stone-100 pt-4 dark:border-stone-800">
                <div className="h-6 w-1/3 animate-pulse rounded bg-stone-200 dark:bg-stone-800" />
              </div>
            </div>
          ))}
        </div>
      </main>
    </div>
  );
}

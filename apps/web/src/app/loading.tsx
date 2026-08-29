export default function Loading() {
  return (
    <div
      aria-busy="true"
      aria-label="正在載入商品目錄"
      className="min-h-screen bg-zinc-50 text-zinc-900 dark:bg-zinc-950 dark:text-zinc-100"
    >
      {/* 頂部導航骨架 */}
      <header className="border-b border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:px-8">
          <div className="flex flex-col gap-2">
            <h1 className="text-2xl font-bold tracking-tight text-zinc-900 dark:text-white">
              Enterprise Commerce
            </h1>
            <p className="text-sm text-zinc-500 dark:text-zinc-400">
              商品型錄與線上商務展示
            </p>
          </div>
        </div>
      </header>

      {/* 主要內容載入區 */}
      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
        <div className="mb-8">
          <div className="h-10 w-full max-w-md animate-pulse rounded-lg bg-zinc-200 dark:bg-zinc-800" />
        </div>

        <div className="mb-4 flex items-center justify-between">
          <span className="text-sm text-zinc-500 dark:text-zinc-400">
            正在載入商品目錄...
          </span>
        </div>

        {/* 商品卡片骨架網格 */}
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {Array.from({ length: 8 }).map((_, index) => (
            <div
              key={index}
              className="flex flex-col justify-between rounded-xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
            >
              <div className="space-y-2">
                <div className="h-5 w-3/4 animate-pulse rounded bg-zinc-200 dark:bg-zinc-800" />
                <div className="h-3 w-1/2 animate-pulse rounded bg-zinc-200 dark:bg-zinc-800" />
              </div>
              <div className="mt-6 border-t border-zinc-100 pt-4 dark:border-zinc-800">
                <div className="h-6 w-1/3 animate-pulse rounded bg-zinc-200 dark:bg-zinc-800" />
              </div>
            </div>
          ))}
        </div>
      </main>
    </div>
  );
}

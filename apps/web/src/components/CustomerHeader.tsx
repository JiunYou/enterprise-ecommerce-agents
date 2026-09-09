import Link from "next/link";
import { AuthControls } from "@/components/AuthControls";

interface CustomerHeaderProps {
  subtitle?: string;
}

export function CustomerHeader({
  subtitle = "商品型錄與線上商務展示",
}: CustomerHeaderProps) {
  return (
    <header className="border-b border-stone-200 bg-white dark:border-stone-800 dark:bg-stone-900">
      <div className="mx-auto max-w-6xl px-4 py-4 sm:px-6 sm:py-5 lg:px-8">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <Link
              href="/"
              aria-label="Enterprise Commerce 首頁"
              className="group inline-block rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2"
            >
              <span className="block text-xl font-bold tracking-tight text-stone-950 transition-colors group-hover:text-stone-700 dark:text-stone-50 dark:group-hover:text-stone-200 sm:text-2xl">
                Enterprise Commerce
              </span>
            </Link>
            <p className="mt-0.5 text-xs text-stone-500 dark:text-stone-400 sm:text-sm">
              {subtitle}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-4 sm:gap-6">
            <Link
              href="/cart"
              className="inline-flex items-center text-sm font-medium text-stone-700 transition-colors hover:text-stone-950 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-stone-400 focus-visible:ring-offset-2 rounded-sm dark:text-stone-300 dark:hover:text-stone-50"
            >
              購物車
            </Link>
            <AuthControls />
          </div>
        </div>
      </div>
    </header>
  );
}

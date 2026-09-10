import Link from "next/link";
import React from "react";

export type AdminNavSection = "fulfillment" | "orders" | "products";

export interface AdminHeaderProps {
  activeSection: AdminNavSection;
  title: string;
  subtitle?: string;
  userLabel: string;
  action?: React.ReactNode;
}

interface NavItem {
  id: AdminNavSection;
  label: string;
  href: string;
}

const NAV_ITEMS: readonly NavItem[] = [
  { id: "fulfillment", label: "訂單履約", href: "/" },
  { id: "orders", label: "訂單管理", href: "/orders" },
  { id: "products", label: "商品管理", href: "/products" },
] as const;

export function AdminHeader({
  activeSection,
  title,
  subtitle,
  userLabel,
  action,
}: AdminHeaderProps) {
  return (
    <header className="border-b border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
      <div className="mx-auto max-w-7xl px-4 py-3 sm:px-6 lg:px-8">
        {/* 主要作業標頭列 */}
        <div className="flex items-center justify-between gap-3">
          {/* 左側：品牌標誌、標題與桌面/平板導航 */}
          <div className="flex min-w-0 items-center gap-3">
            <span
              aria-hidden="true"
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-indigo-600 text-sm font-bold text-white shadow-sm sm:text-base"
            >
              EC
            </span>
            <div className="min-w-0">
              <h1 className="truncate text-base font-bold text-zinc-900 sm:text-lg dark:text-zinc-50">
                {title}
              </h1>
              {subtitle && (
                <p className="truncate text-xs text-zinc-500 dark:text-zinc-400">
                  {subtitle}
                </p>
              )}
            </div>

            {/* 平板與桌面導航（>= 768px） */}
            <nav
              aria-label="管理員主導航"
              className="ml-4 hidden items-center gap-1.5 md:flex lg:ml-6 lg:gap-2"
            >
              {NAV_ITEMS.map((item) => {
                const isActive = item.id === activeSection;
                return (
                  <Link
                    key={item.id}
                    href={item.href}
                    className={`rounded-md px-3 py-1.5 text-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 ${
                      isActive
                        ? "border border-zinc-300/80 bg-zinc-100 font-semibold text-zinc-900 shadow-xs dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-100"
                        : "border border-transparent font-medium text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"
                    }`}
                  >
                    {item.label}
                  </Link>
                );
              })}
            </nav>
          </div>

          {/* 右側：自訂動作、管理者身分與登出 */}
          <div className="flex shrink-0 items-center gap-2.5 sm:gap-4">
            {action && <div>{action}</div>}

            <div className="hidden text-right sm:block">
              <p className="max-w-[160px] truncate text-xs font-medium text-zinc-900 dark:text-zinc-100">
                {userLabel}
              </p>
              <span className="inline-flex items-center rounded-full bg-indigo-50 px-2 py-0.5 text-[10px] font-medium text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300">
                Admin Role
              </span>
            </div>

            <a
              href="/auth/logout"
              className="inline-flex min-h-[36px] items-center justify-center rounded-md border border-zinc-300 bg-white px-3 py-1.5 text-xs font-semibold text-zinc-700 shadow-xs transition-colors hover:bg-zinc-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
            >
              登出
            </a>
          </div>
        </div>

        {/* 行動端核心導航（< 768px，確保在 375px 下 3 個目的地均直接可見且觸控友好） */}
        <nav
          aria-label="管理員行動端導航"
          className="mt-2.5 grid grid-cols-3 gap-1.5 border-t border-zinc-100 pt-2.5 md:hidden dark:border-zinc-800/80"
        >
          {NAV_ITEMS.map((item) => {
            const isActive = item.id === activeSection;
            return (
              <Link
                key={item.id}
                href={item.href}
                className={`flex min-h-[44px] touch-manipulation items-center justify-center rounded-md px-2 py-2 text-center text-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 ${
                  isActive
                    ? "border border-indigo-200 bg-indigo-50/70 font-semibold text-indigo-700 shadow-xs dark:border-indigo-800/60 dark:bg-indigo-950/50 dark:text-indigo-300"
                    : "border border-zinc-200/60 font-medium text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900 dark:border-zinc-800 dark:text-zinc-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"
                }`}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>
      </div>
    </header>
  );
}

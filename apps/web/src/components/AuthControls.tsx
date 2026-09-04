import { auth0 } from "@/lib/auth0";

export async function AuthControls() {
  const session = await auth0.getSession();

  if (!session || !session.user) {
    return (
      <div className="flex items-center gap-4">
        <a
          href="/auth/login"
          className="inline-flex items-center justify-center rounded-md bg-zinc-900 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-zinc-700 dark:bg-zinc-50 dark:text-zinc-900 dark:hover:bg-zinc-200"
        >
          登入 / 註冊
        </a>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-3">
      <span className="inline-flex items-center rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-medium text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300">
        已登入顧客
      </span>
      <a
        href="/auth/logout"
        className="inline-flex items-center justify-center rounded-md border border-zinc-300 bg-white px-3 py-1.5 text-sm font-medium text-zinc-700 transition-colors hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200 dark:hover:bg-zinc-700"
      >
        登出
      </a>
    </div>
  );
}

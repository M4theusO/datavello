/** Only allow in-app relative redirects (prevents open redirects via ?redirect=https://...). */
export function safeRedirect(target: string | null): string {
  if (!target || !target.startsWith("/") || target.startsWith("//")) return "/"
  return target
}

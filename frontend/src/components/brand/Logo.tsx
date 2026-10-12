import { cn } from "@/lib/utils"

/**
 * Datavello mark: rising bars topped by a trend line whose last point is highlighted.
 * Self-contained (own background), so it reads on light and dark surfaces alike.
 */
export function LogoMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 32 32" fill="none" aria-hidden="true" className={cn("size-8 shrink-0", className)}>
      <rect width="32" height="32" rx="9" fill="#145ec1" />
      <rect x="7" y="18" width="4" height="7" rx="1.5" fill="#fff" fillOpacity=".28" />
      <rect x="14" y="14" width="4" height="11" rx="1.5" fill="#fff" fillOpacity=".28" />
      <rect x="21" y="10" width="4" height="15" rx="1.5" fill="#fff" fillOpacity=".28" />
      <path d="M9 15.5 16 11.5 23 7" stroke="#fff" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="9" cy="15.5" r="2.1" fill="#fff" />
      <circle cx="16" cy="11.5" r="2.1" fill="#fff" />
      <circle cx="23" cy="7" r="2.6" fill="#85bcf5" stroke="#fff" strokeWidth="1.6" />
    </svg>
  )
}

/**
 * Mark + wordmark. "Data" uses currentColor; use tone="inverted" on dark or brand-colored
 * surfaces so the "vello" accent keeps its contrast.
 */
export function Logo({
  className,
  markClassName,
  tone = "default",
}: {
  className?: string
  markClassName?: string
  tone?: "default" | "inverted"
}) {
  return (
    <span className={cn("inline-flex items-center gap-2.5", className)}>
      <LogoMark className={markClassName} />
      <span className="text-lg font-semibold tracking-tight">
        Data<span className={tone === "inverted" ? "text-[#85bcf5]" : "text-primary"}>vello</span>
      </span>
    </span>
  )
}

import type { ReactNode } from "react"
import { ArrowDownRight, ArrowUpRight, Info, Minus } from "lucide-react"
import { Card, CardContent } from "@/components/ui/card"
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip"
import { cn } from "@/lib/utils"

export type DeltaTone = "good" | "bad" | "neutral"

export interface KpiDelta {
  /** Already formatted, e.g. "12,5%" or "R$ 250,00". */
  text: string
  direction: "up" | "down" | "flat"
  tone: DeltaTone
}

/** Stat tile: label · value · delta vs the previous period (icon + text, never color alone). */
export function KpiCard({
  label,
  value,
  icon,
  delta,
  deltaFallback,
  info,
}: {
  label: string
  value: string
  icon: ReactNode
  delta: KpiDelta | null
  /** Shown when there is nothing to compare with. */
  deltaFallback: string
  info?: string
}) {
  const DeltaIcon = delta?.direction === "up" ? ArrowUpRight : delta?.direction === "down" ? ArrowDownRight : Minus

  return (
    <Card className="h-full gap-0 py-5">
      <CardContent className="space-y-3 px-5">
        <div className="flex items-center justify-between gap-2">
          <span className="flex items-center gap-1.5 text-sm font-medium text-muted-foreground">
            {label}
            {info && (
              <Tooltip>
                <TooltipTrigger
                  render={<button type="button" className="rounded-full outline-none focus-visible:ring-2 focus-visible:ring-ring" />}
                  aria-label={`Sobre: ${label}`}
                >
                  <Info className="size-3.5" />
                </TooltipTrigger>
                <TooltipContent className="max-w-64">{info}</TooltipContent>
              </Tooltip>
            )}
          </span>
          <span className="flex size-8 items-center justify-center rounded-lg bg-brand-soft text-primary [&_svg]:size-4" aria-hidden="true">
            {icon}
          </span>
        </div>
        <p className="truncate text-2xl font-semibold tracking-tight" title={value}>
          {value}
        </p>
        {delta ? (
          <p className="flex items-center gap-1 text-xs">
            <span
              className={cn(
                "inline-flex items-center gap-0.5 font-medium",
                delta.tone === "good" && "text-success",
                delta.tone === "bad" && "text-destructive",
                delta.tone === "neutral" && "text-muted-foreground",
              )}
            >
              <DeltaIcon className="size-3.5" aria-hidden="true" />
              {delta.text}
            </span>
            <span className="text-muted-foreground">vs. período anterior</span>
          </p>
        ) : (
          <p className="text-xs text-muted-foreground">{deltaFallback}</p>
        )}
      </CardContent>
    </Card>
  )
}

import { type ReactNode, useState } from "react"
import { BarChart3, Table2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { cn } from "@/lib/utils"

/**
 * Card shell shared by every chart: title, optional legend, and a chart/table toggle
 * so every value is reachable without hovering (accessibility requirement).
 */
export function ChartCard({
  title,
  description,
  legend,
  chart,
  table,
  className,
}: {
  title: string
  description?: string
  legend?: ReactNode
  chart: ReactNode
  table: ReactNode
  className?: string
}) {
  const [showTable, setShowTable] = useState(false)

  return (
    <Card className={cn("h-full gap-3", className)}>
      <CardHeader>
        <CardTitle className="text-base">{title}</CardTitle>
        {description && <CardDescription>{description}</CardDescription>}
        <CardAction>
          <Button
            variant="ghost"
            size="sm"
            className="text-muted-foreground"
            onClick={() => setShowTable((v) => !v)}
            aria-pressed={showTable}
          >
            {showTable ? <BarChart3 data-icon="inline-start" /> : <Table2 data-icon="inline-start" />}
            {showTable ? "Gráfico" : "Tabela"}
          </Button>
        </CardAction>
      </CardHeader>
      <CardContent className="flex-1">
        {!showTable && legend && <div className="mb-3">{legend}</div>}
        {showTable ? <div className="max-h-72 overflow-auto">{table}</div> : chart}
      </CardContent>
    </Card>
  )
}

/** Legend entry: mirrors the mark (rect for bars/areas, short line for lines). Text stays in text tokens. */
export function LegendItem({ color, label, shape = "rect" }: { color: string; label: string; shape?: "rect" | "line" }) {
  return (
    <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
      {shape === "rect" ? (
        <span className="size-2.5 rounded-[3px]" style={{ background: color }} aria-hidden="true" />
      ) : (
        <span className="h-0.5 w-3.5 rounded-full" style={{ background: color }} aria-hidden="true" />
      )}
      {label}
    </span>
  )
}

export function Legend({ children }: { children: ReactNode }) {
  return <div className="flex flex-wrap items-center gap-x-4 gap-y-1">{children}</div>
}

export interface TooltipRow {
  label: string
  value: string
  color?: string
  shape?: "rect" | "line"
}

/** Tooltip body: values lead (strong), series names follow; rows keyed by a short line. */
export function ChartTooltipBox({ title, rows }: { title: string; rows: TooltipRow[] }) {
  return (
    <div className="min-w-44 rounded-lg border bg-popover px-3 py-2 text-xs shadow-md">
      <p className="mb-1.5 font-medium text-muted-foreground">{title}</p>
      <div className="space-y-1">
        {rows.map((row) => (
          <div key={row.label} className="flex items-center gap-2">
            {row.color && <span className="h-0.5 w-3 shrink-0 rounded-full" style={{ background: row.color }} aria-hidden="true" />}
            <span className="tabular font-semibold text-foreground">{row.value}</span>
            <span className="text-muted-foreground">{row.label}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

/** Compact data table used as the accessible alternative to each chart. */
export function DataTable({ columns, rows }: { columns: { label: string; align?: "right" }[]; rows: ReactNode[][] }) {
  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="border-b text-xs text-muted-foreground">
          {columns.map((c) => (
            <th key={c.label} className={cn("py-2 font-medium", c.align === "right" ? "text-right" : "text-left")}>
              {c.label}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {rows.map((cells, i) => (
          <tr key={i} className="border-b last:border-0">
            {cells.map((cell, j) => (
              <td key={j} className={cn("tabular py-1.5", columns[j]?.align === "right" && "text-right")}>
                {cell}
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  )
}

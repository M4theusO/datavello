import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  LabelList,
  Line,
  LineChart,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts"
import { formatCurrency } from "@/lib/format"
import type { CategoryTotal, MonthlyPoint } from "../api"
import { formatCompact, formatCompactCurrency, formatMonthLong, formatMonthShort, formatPercent, niceScale } from "../chartFormat"
import { ChartCard, ChartTooltipBox, DataTable, Legend, LegendItem } from "./ChartCard"

const INCOME = "var(--series-income)"
const EXPENSE = "var(--series-expense)"
const BALANCE = "var(--chart-1)"

const tick = { fill: "var(--muted-foreground)", fontSize: 12 }
const grid = <CartesianGrid vertical={false} stroke="var(--chart-grid)" />
const margin = { top: 8, right: 12, left: 0, bottom: 0 }

function monthAxis(points: number) {
  return (
    <XAxis
      dataKey="month"
      tickFormatter={formatMonthShort}
      tick={tick}
      tickLine={false}
      axisLine={{ stroke: "var(--border)" }}
      interval={points > 12 ? "preserveStartEnd" : 0}
      padding={{ left: 12, right: 12 }}
      minTickGap={8}
    />
  )
}

function valueAxis(values: number[]) {
  const { domain, ticks } = niceScale(values)
  return <YAxis tickFormatter={formatCompact} tick={tick} tickLine={false} axisLine={false} width={52} domain={domain} ticks={ticks} />
}

interface TooltipArgs {
  active?: boolean
  payload?: ReadonlyArray<{ payload?: unknown }>
}

function monthTooltip(rows: (p: MonthlyPoint) => { label: string; value: number; color?: string }[]) {
  return function MonthTooltip({ active, payload }: TooltipArgs) {
    const point = payload?.[0]?.payload as MonthlyPoint | undefined
    if (!active || !point) return null
    return (
      <ChartTooltipBox
        title={formatMonthLong(point.month)}
        rows={rows(point).map((r) => ({ label: r.label, value: formatCurrency(r.value), color: r.color }))}
      />
    )
  }
}

function monthlyTable(points: MonthlyPoint[]) {
  return (
    <DataTable
      columns={[{ label: "Mês" }, { label: "Receitas", align: "right" }, { label: "Despesas", align: "right" }, { label: "Saldo", align: "right" }]}
      rows={points.map((p) => [formatMonthLong(p.month), formatCurrency(p.income), formatCurrency(p.expense), formatCurrency(p.balance)])}
    />
  )
}

/** Evolução mensal de receitas e despesas — two lines, crosshair tooltip. */
export function MonthlyTrendChart({ data }: { data: MonthlyPoint[] }) {
  return (
    <ChartCard
      title="Evolução mensal"
      description="Receitas e despesas por mês (R$)"
      legend={
        <Legend>
          <LegendItem color={INCOME} label="Receitas" shape="line" />
          <LegendItem color={EXPENSE} label="Despesas" shape="line" />
        </Legend>
      }
      chart={
        <div className="h-64">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={data} margin={margin}>
              {grid}
              {monthAxis(data.length)}
              {valueAxis(data.flatMap((p) => [p.income, p.expense]))}
              <Tooltip
                cursor={{ stroke: "var(--muted-foreground)", strokeWidth: 1, strokeOpacity: 0.4 }}
                content={monthTooltip((p) => [
                  { label: "Receitas", value: p.income, color: INCOME },
                  { label: "Despesas", value: p.expense, color: EXPENSE },
                ])}
              />
              {(["income", "expense"] as const).map((key) => (
                <Line
                  key={key}
                  type="monotone"
                  dataKey={key}
                  name={key === "income" ? "Receitas" : "Despesas"}
                  stroke={key === "income" ? INCOME : EXPENSE}
                  strokeWidth={2}
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  dot={data.length === 1 ? { r: 4, strokeWidth: 2, stroke: "var(--card)", fill: key === "income" ? INCOME : EXPENSE } : false}
                  activeDot={{ r: 4, strokeWidth: 2, stroke: "var(--card)" }}
                  isAnimationActive={false}
                />
              ))}
            </LineChart>
          </ResponsiveContainer>
        </div>
      }
      table={monthlyTable(data)}
    />
  )
}

/** Comparativo de receitas e despesas — grouped columns per month. */
export function IncomeVsExpenseChart({ data }: { data: MonthlyPoint[] }) {
  return (
    <ChartCard
      title="Receitas x despesas"
      description="Comparativo mês a mês (R$)"
      legend={
        <Legend>
          <LegendItem color={INCOME} label="Receitas" />
          <LegendItem color={EXPENSE} label="Despesas" />
        </Legend>
      }
      chart={
        <div className="h-64">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={data} margin={margin} barGap={2} barCategoryGap="28%">
              {grid}
              {monthAxis(data.length)}
              {valueAxis(data.flatMap((p) => [p.income, p.expense]))}
              <Tooltip
                cursor={{ fill: "var(--muted)", fillOpacity: 0.6 }}
                content={monthTooltip((p) => [
                  { label: "Receitas", value: p.income, color: INCOME },
                  { label: "Despesas", value: p.expense, color: EXPENSE },
                  { label: "Saldo", value: p.balance },
                ])}
              />
              <Bar dataKey="income" name="Receitas" fill={INCOME} radius={[4, 4, 0, 0]} maxBarSize={24} isAnimationActive={false} />
              <Bar dataKey="expense" name="Despesas" fill={EXPENSE} radius={[4, 4, 0, 0]} maxBarSize={24} isAnimationActive={false} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      }
      table={monthlyTable(data)}
    />
  )
}

/** Histórico do saldo — cumulative balance across the selected period. */
export function BalanceHistoryChart({ data }: { data: MonthlyPoint[] }) {
  const hasNegative = data.some((p) => p.cumulativeBalance < 0)
  return (
    <ChartCard
      title="Saldo acumulado"
      description="Soma dos saldos mensais desde o início do período (R$)"
      chart={
        <div className="h-64">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={data} margin={margin}>
              {grid}
              {monthAxis(data.length)}
              {valueAxis(data.map((p) => p.cumulativeBalance))}
              {hasNegative && <ReferenceLine y={0} stroke="var(--muted-foreground)" strokeOpacity={0.5} />}
              <Tooltip
                cursor={{ stroke: "var(--muted-foreground)", strokeWidth: 1, strokeOpacity: 0.4 }}
                content={monthTooltip((p) => [
                  { label: "Saldo acumulado", value: p.cumulativeBalance, color: BALANCE },
                  { label: "Saldo do mês", value: p.balance },
                ])}
              />
              <Area
                type="monotone"
                dataKey="cumulativeBalance"
                name="Saldo acumulado"
                stroke={BALANCE}
                strokeWidth={2}
                fill={BALANCE}
                fillOpacity={0.1}
                dot={data.length === 1 ? { r: 4, strokeWidth: 2, stroke: "var(--card)", fill: BALANCE } : false}
                activeDot={{ r: 4, strokeWidth: 2, stroke: "var(--card)" }}
                isAnimationActive={false}
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      }
      table={
        <DataTable
          columns={[{ label: "Mês" }, { label: "Saldo do mês", align: "right" }, { label: "Saldo acumulado", align: "right" }]}
          rows={data.map((p) => [formatMonthLong(p.month), formatCurrency(p.balance), formatCurrency(p.cumulativeBalance)])}
        />
      }
    />
  )
}

/** Value at the bar tip, always on one line (Recharts would wrap long labels). */
function BarValueLabel(props: { x?: number | string; y?: number | string; width?: number | string; height?: number | string; value?: unknown }) {
  const x = Number(props.x) + Number(props.width) + 6
  const y = Number(props.y) + Number(props.height) / 2
  return (
    <text x={x} y={y} dominantBaseline="central" fill="var(--muted-foreground)" fontSize={12} style={{ whiteSpace: "pre" }}>
      {formatCompactCurrency(Number(props.value))}
    </text>
  )
}

const MAX_CATEGORY_BARS = 6

/** Top categories plus everything else folded into "Outras" (never more bars than fit). */
function foldCategories(data: CategoryTotal[]) {
  if (data.length <= MAX_CATEGORY_BARS) return data
  const top = data.slice(0, MAX_CATEGORY_BARS - 1)
  const rest = data.slice(MAX_CATEGORY_BARS - 1)
  return [
    ...top,
    {
      categoryId: "others",
      categoryName: `Outras (${rest.length})`,
      total: rest.reduce((sum, c) => sum + c.total, 0),
      share: rest.reduce((sum, c) => sum + c.share, 0),
    },
  ]
}

/** Despesas por categoria — sorted horizontal bars, one hue (all are expenses). */
export function ExpenseByCategoryChart({ data }: { data: CategoryTotal[] }) {
  const bars = foldCategories(data)
  return (
    <ChartCard
      title="Despesas por categoria"
      description="Total do período (R$)"
      chart={
        bars.length === 0 ? (
          <p className="flex h-64 items-center justify-center text-sm text-muted-foreground">Nenhuma despesa no período.</p>
        ) : (
          <div style={{ height: Math.max(160, bars.length * 40 + 16) }}>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={bars} layout="vertical" margin={{ top: 0, right: 88, left: 0, bottom: 0 }} barCategoryGap="30%">
                <XAxis type="number" hide domain={[0, "dataMax"]} />
                <YAxis
                  type="category"
                  dataKey="categoryName"
                  width={150}
                  tick={tick}
                  tickLine={false}
                  axisLine={false}
                  tickFormatter={(name: string) => (name.length > 22 ? `${name.slice(0, 21)}…` : name)}
                />
                <Tooltip
                  cursor={{ fill: "var(--muted)", fillOpacity: 0.6 }}
                  content={({ active, payload }: TooltipArgs) => {
                    const item = payload?.[0]?.payload as CategoryTotal | undefined
                    if (!active || !item) return null
                    return (
                      <ChartTooltipBox
                        title={item.categoryName}
                        rows={[
                          { label: "no período", value: formatCurrency(item.total), color: EXPENSE },
                          { label: "das despesas", value: formatPercent(item.share) },
                        ]}
                      />
                    )
                  }}
                />
                <Bar dataKey="total" name="Despesas" fill={EXPENSE} radius={[0, 4, 4, 0]} maxBarSize={20} isAnimationActive={false}>
                  <LabelList dataKey="total" content={BarValueLabel} />
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        )
      }
      table={
        <DataTable
          columns={[{ label: "Categoria" }, { label: "Total", align: "right" }, { label: "Participação", align: "right" }]}
          rows={data.map((c) => [c.categoryName, formatCurrency(c.total), formatPercent(c.share)])}
        />
      }
    />
  )
}

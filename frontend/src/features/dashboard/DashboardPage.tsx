import { type ReactNode, useMemo, useState } from "react"
import { ArrowDownLeft, ArrowLeftRight, ArrowRight, ArrowUpRight, FileUp, Hash, Plus, Scale, SlidersHorizontal, X } from "lucide-react"
import { Link, useSearchParams } from "react-router"
import { PeriodFilter, type PeriodValue } from "@/components/filters/PeriodFilter"
import { SimpleSelect } from "@/components/SimpleSelect"
import { EmptyState, ErrorState } from "@/components/feedback"
import { Button, buttonVariants } from "@/components/ui/button"
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { formatCurrency, formatDate, formatInteger, toIsoDate } from "@/lib/format"
import { dashboardPresets, isPeriodPreset, type PeriodPreset, rangeForPreset } from "@/lib/periods"
import { cn } from "@/lib/utils"
import { useCategories } from "@/features/categories/api"
import { toQueryString, type Transaction, type TransactionType } from "@/features/transactions/api"
import { Amount, TypeBadge } from "@/features/transactions/TransactionBits"
import { type DashboardSummary, type Kpis, type WidgetType, useDashboardConfig, useDashboardSummary } from "./api"
import { BalanceHistoryChart, ExpenseByCategoryChart, IncomeVsExpenseChart, MonthlyTrendChart } from "./components/Charts"
import { type KpiDelta, KpiCard } from "./components/KpiCard"
import { formatPercent } from "./chartFormat"

const BALANCE_INFO =
  "Receitas menos despesas cadastradas no período. Não representa lucro contábil nem fluxo de caixa completo: o resultado depende dos dados registrados no Datavello."

export function DashboardPage() {
  const config = useDashboardConfig()
  const categories = useCategories()
  const [params, setParams] = useSearchParams()

  const defaultPreset: PeriodPreset = config.data?.defaultPeriod ?? "Last6Months"
  const period: PeriodValue = {
    preset: isPeriodPreset(params.get("periodo")) && params.get("periodo") !== "all" ? (params.get("periodo") as PeriodPreset) : defaultPreset,
    custom: { from: params.get("de") ?? undefined, to: params.get("ate") ?? undefined },
  }
  const range = period.preset === "custom" ? period.custom : rangeForPreset(period.preset)
  const typeParam = params.get("tipo")
  const type: TransactionType | undefined = typeParam === "Income" || typeParam === "Expense" ? typeParam : undefined
  const categoryId = params.get("categoria") ?? undefined

  const customIncomplete = period.preset === "custom" && (!range?.from || !range?.to || range.from > range.to)
  const summaryParams = config.isSuccess && range?.from && range?.to && !customIncomplete ? { from: range.from, to: range.to, categoryId, type } : null
  const summary = useDashboardSummary(summaryParams)

  function update(changes: Record<string, string | undefined>) {
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        for (const [key, value] of Object.entries(changes)) {
          if (value) next.set(key, value)
          else next.delete(key)
        }
        return next
      },
      { replace: true },
    )
  }

  const categoryOptions = useMemo(
    () => [{ value: "all", label: "Todas as categorias" }, ...(categories.data ?? []).map((c) => ({ value: c.id, label: c.name }))],
    [categories.data],
  )

  const enabledWidgets = (config.data?.widgets ?? []).filter((w) => w.isEnabled).map((w) => w.type)
  const hasFilters = !!categoryId || !!type

  return (
    <div className="mx-auto max-w-7xl space-y-5">
      {/* Filters scope everything below them. */}
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex flex-wrap items-center gap-2">
          <PeriodFilter
            value={period}
            presets={dashboardPresets}
            onChange={(value) =>
              update({
                periodo: value.preset === defaultPreset ? undefined : value.preset,
                de: value.preset === "custom" ? value.custom.from : undefined,
                ate: value.preset === "custom" ? value.custom.to : undefined,
              })
            }
          />
          <SimpleSelect
            aria-label="Categoria"
            value={categoryId ?? "all"}
            onChange={(v) => update({ categoria: v === "all" ? undefined : v })}
            options={categoryOptions}
            className="h-9 w-full bg-card sm:w-52"
          />
          <SimpleSelect
            aria-label="Tipo"
            value={type ?? "all"}
            onChange={(v) => update({ tipo: v === "all" ? undefined : v })}
            options={[
              { value: "all", label: "Receitas e despesas" },
              { value: "Income", label: "Somente receitas" },
              { value: "Expense", label: "Somente despesas" },
            ]}
            className="h-9 w-full bg-card sm:w-48"
          />
          {hasFilters && (
            <Button variant="ghost" size="sm" onClick={() => update({ categoria: undefined, tipo: undefined })}>
              <X data-icon="inline-start" />
              Limpar filtros
            </Button>
          )}
        </div>
        <Link to="/personalizar" className={cn(buttonVariants({ variant: "outline", size: "sm" }), "self-start lg:self-auto")}>
          <SlidersHorizontal data-icon="inline-start" />
          Personalizar
        </Link>
      </div>

      {summary.data && <PeriodCaption summary={summary.data} />}

      {customIncomplete ? (
        <Card>
          <EmptyState icon={<ArrowLeftRight />} title="Defina o período" description="Escolha a data inicial e a data final (a inicial não pode ser posterior à final)." />
        </Card>
      ) : config.isError || summary.isError ? (
        <Card>
          <ErrorState
            error={config.error ?? summary.error}
            onRetry={() => (config.isError ? config.refetch() : summary.refetch())}
            className="mx-auto py-12"
          />
        </Card>
      ) : !summary.data ? (
        <DashboardSkeleton />
      ) : summary.data.current.transactionCount === 0 ? (
        <Card>
          <EmptyState
            icon={<ArrowLeftRight />}
            title="Nenhuma transação neste período"
            description={
              hasFilters
                ? "Nenhum registro corresponde aos filtros selecionados. Ajuste o período ou limpe os filtros."
                : "Os indicadores são calculados a partir das transações cadastradas. Cadastre ou importe seus dados para começar."
            }
            action={
              <div className="flex flex-wrap justify-center gap-2">
                <Link to="/transacoes" className={buttonVariants()}>
                  <Plus data-icon="inline-start" />
                  Cadastrar transação
                </Link>
                <Link to="/importar" className={buttonVariants({ variant: "outline" })}>
                  <FileUp data-icon="inline-start" />
                  Importar planilha
                </Link>
              </div>
            }
          />
        </Card>
      ) : enabledWidgets.length === 0 ? (
        <Card>
          <EmptyState
            icon={<SlidersHorizontal />}
            title="Nenhum widget ativo"
            description="Ative os indicadores e gráficos que deseja acompanhar."
            action={
              <Link to="/personalizar" className={buttonVariants({ variant: "outline" })}>
                Personalizar dashboard
              </Link>
            }
          />
        </Card>
      ) : (
        <div
          className={cn("grid grid-cols-1 gap-4 transition-opacity sm:grid-cols-2 xl:grid-cols-4", summary.isPlaceholderData && "opacity-60")}
          aria-busy={summary.isFetching}
        >
          {enabledWidgets.map((widget) => (
            <Widget key={widget} type={widget} summary={summary.data!} filtersQuery={toQueryString({ categoria: categoryId, tipo: type })} />
          ))}
        </div>
      )}
    </div>
  )
}

const widgetSpan: Record<WidgetType, string> = {
  "kpi-income": "",
  "kpi-expense": "",
  "kpi-balance": "",
  "kpi-count": "",
  "chart-monthly": "sm:col-span-2",
  "chart-income-vs-expense": "sm:col-span-2",
  "chart-balance-history": "sm:col-span-2",
  "chart-expense-by-category": "sm:col-span-2",
  "table-recent": "col-span-full",
}

function Widget({ type, summary, filtersQuery }: { type: WidgetType; summary: DashboardSummary; filtersQuery: string }) {
  const { current, previous } = summary
  let content: ReactNode
  switch (type) {
    case "kpi-income":
      content = (
        <KpiCard
          label="Receitas"
          value={formatCurrency(current.income)}
          icon={<ArrowUpRight />}
          delta={percentDelta(current.income, previous?.income, "up-is-good")}
          deltaFallback={fallback(previous, previous?.income)}
        />
      )
      break
    case "kpi-expense":
      content = (
        <KpiCard
          label="Despesas"
          value={formatCurrency(current.expense)}
          icon={<ArrowDownLeft />}
          delta={percentDelta(current.expense, previous?.expense, "up-is-bad")}
          deltaFallback={fallback(previous, previous?.expense)}
        />
      )
      break
    case "kpi-balance":
      content = (
        <KpiCard
          label="Saldo do período"
          value={formatCurrency(current.balance)}
          icon={<Scale />}
          info={BALANCE_INFO}
          delta={previous ? absoluteDelta(current.balance, previous.balance) : null}
          deltaFallback={fallback(previous, previous?.balance)}
        />
      )
      break
    case "kpi-count":
      content = (
        <KpiCard
          label="Transações"
          value={formatInteger(current.transactionCount)}
          icon={<Hash />}
          delta={percentDelta(current.transactionCount, previous?.transactionCount, "neutral")}
          deltaFallback={fallback(previous, previous?.transactionCount)}
        />
      )
      break
    case "chart-monthly":
      content = <MonthlyTrendChart data={summary.monthly} />
      break
    case "chart-income-vs-expense":
      content = <IncomeVsExpenseChart data={summary.monthly} />
      break
    case "chart-balance-history":
      content = <BalanceHistoryChart data={summary.monthly} />
      break
    case "chart-expense-by-category":
      content = <ExpenseByCategoryChart data={summary.expensesByCategory} />
      break
    case "table-recent":
      content = <RecentActivity items={summary.recent} period={summary.period} filtersQuery={filtersQuery} />
      break
    default:
      return null
  }
  return <div className={cn("min-w-0", widgetSpan[type])}>{content}</div>
}

function percentDelta(current: number, previous: number | undefined, mode: "up-is-good" | "up-is-bad" | "neutral"): KpiDelta | null {
  if (previous === undefined || previous === 0) return null
  const change = (current - previous) / Math.abs(previous)
  const direction = change > 0 ? "up" : change < 0 ? "down" : "flat"
  const tone =
    mode === "neutral" || direction === "flat"
      ? "neutral"
      : (direction === "up") === (mode === "up-is-good")
        ? "good"
        : "bad"
  return { text: formatPercent(Math.abs(change)), direction, tone }
}

/** Balance can cross zero, so its change is shown in R$ rather than as a percentage. */
function absoluteDelta(current: number, previous: number): KpiDelta {
  const diff = current - previous
  const direction = diff > 0 ? "up" : diff < 0 ? "down" : "flat"
  return {
    text: formatCurrency(Math.abs(diff)),
    direction,
    tone: direction === "up" ? "good" : direction === "down" ? "bad" : "neutral",
  }
}

function fallback(previous: Kpis | null, previousValue: number | undefined) {
  if (!previous) return "Sem transações no período anterior"
  return previousValue === 0 ? "Sem base de comparação no período anterior" : ""
}

function PeriodCaption({ summary }: { summary: DashboardSummary }) {
  const [today] = useState(() => toIsoDate(new Date()))
  const ongoing = summary.period.to >= today
  return (
    <p className="text-xs text-muted-foreground">
      Período: <span className="tabular font-medium text-foreground">{formatDate(summary.period.from)}</span> a{" "}
      <span className="tabular font-medium text-foreground">{formatDate(summary.period.to)}</span>
      <span className="hidden sm:inline">
        {" "}
        · comparado com {formatDate(summary.previousPeriod.from)} a {formatDate(summary.previousPeriod.to)}
      </span>
      {" "}· valores calculados a partir das transações cadastradas
      {ongoing && "; o período inclui o mês atual, ainda em andamento"}.
    </p>
  )
}

function RecentActivity({ items, period, filtersQuery }: { items: Transaction[]; period: { from: string; to: string }; filtersQuery: string }) {
  const params = new URLSearchParams(filtersQuery.replace(/^\?/, ""))
  params.set("periodo", "custom")
  params.set("de", period.from)
  params.set("ate", period.to)

  return (
    <Card className="gap-3">
      <CardHeader>
        <CardTitle className="text-base">Atividade recente</CardTitle>
        <CardDescription>Últimas transações do período</CardDescription>
        <CardAction>
          <Link to={`/transacoes?${params.toString()}`} className={cn(buttonVariants({ variant: "ghost", size: "sm" }), "text-muted-foreground")}>
            Ver todas
            <ArrowRight data-icon="inline-end" />
          </Link>
        </CardAction>
      </CardHeader>
      <CardContent className="px-0">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead className="pl-6">Data</TableHead>
              <TableHead>Descrição</TableHead>
              <TableHead className="hidden sm:table-cell">Categoria</TableHead>
              <TableHead className="hidden md:table-cell">Tipo</TableHead>
              <TableHead className="pr-6 text-right">Valor</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((t) => (
              <TableRow key={t.id}>
                <TableCell className="tabular pl-6 text-muted-foreground">{formatDate(t.date)}</TableCell>
                <TableCell className="max-w-56 truncate font-medium" title={t.description}>
                  {t.description}
                </TableCell>
                <TableCell className="hidden text-muted-foreground sm:table-cell">{t.categoryName}</TableCell>
                <TableCell className="hidden md:table-cell">
                  <TypeBadge type={t.type} />
                </TableCell>
                <TableCell className="pr-6 text-right">
                  <Amount transaction={t} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  )
}

function DashboardSkeleton() {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4" aria-busy="true" aria-label="Carregando indicadores">
      {Array.from({ length: 4 }, (_, i) => (
        <Skeleton key={i} className="h-32 rounded-xl" />
      ))}
      {Array.from({ length: 2 }, (_, i) => (
        <Skeleton key={i} className="h-80 rounded-xl sm:col-span-2" />
      ))}
    </div>
  )
}

import type { LucideIcon } from "lucide-react"
import { AreaChart, ArrowDownLeft, ArrowUpRight, BarChart3, BarChartHorizontal, Hash, LineChart, ListOrdered, Scale } from "lucide-react"
import type { WidgetType } from "./api"

export interface WidgetInfo {
  label: string
  description: string
  icon: LucideIcon
  size: "Indicador" | "Gráfico" | "Tabela"
}

/** Display names for the fixed widget catalog (the backend owns the list of valid types). */
export const widgetCatalog: Record<WidgetType, WidgetInfo> = {
  "kpi-income": { label: "Receitas", description: "Total de receitas no período, comparado ao período anterior.", icon: ArrowUpRight, size: "Indicador" },
  "kpi-expense": { label: "Despesas", description: "Total de despesas no período, comparado ao período anterior.", icon: ArrowDownLeft, size: "Indicador" },
  "kpi-balance": { label: "Saldo do período", description: "Receitas menos despesas cadastradas no período.", icon: Scale, size: "Indicador" },
  "kpi-count": { label: "Transações", description: "Quantidade de transações no período.", icon: Hash, size: "Indicador" },
  "chart-monthly": { label: "Evolução mensal", description: "Linhas de receitas e despesas mês a mês.", icon: LineChart, size: "Gráfico" },
  "chart-income-vs-expense": { label: "Receitas x despesas", description: "Colunas comparando receitas e despesas de cada mês.", icon: BarChart3, size: "Gráfico" },
  "chart-balance-history": { label: "Saldo acumulado", description: "Como o saldo evolui ao longo do período.", icon: AreaChart, size: "Gráfico" },
  "chart-expense-by-category": { label: "Despesas por categoria", description: "Onde o dinheiro está sendo gasto.", icon: BarChartHorizontal, size: "Gráfico" },
  "table-recent": { label: "Atividade recente", description: "As últimas transações do período.", icon: ListOrdered, size: "Tabela" },
}

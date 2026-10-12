import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { api } from "@/lib/api"
import type { PeriodPreset } from "@/lib/periods"
import { toQueryString, type Transaction, type TransactionType } from "@/features/transactions/api"

export interface Kpis {
  income: number
  expense: number
  balance: number
  transactionCount: number
}

export interface MonthlyPoint {
  month: string // yyyy-MM
  income: number
  expense: number
  balance: number
  cumulativeBalance: number
}

export interface CategoryTotal {
  categoryId: string
  categoryName: string
  total: number
  share: number
}

export interface DashboardSummary {
  period: { from: string; to: string }
  previousPeriod: { from: string; to: string }
  current: Kpis
  previous: Kpis | null
  monthly: MonthlyPoint[]
  expensesByCategory: CategoryTotal[]
  recent: Transaction[]
}

export type WidgetType =
  | "kpi-income"
  | "kpi-expense"
  | "kpi-balance"
  | "kpi-count"
  | "chart-monthly"
  | "chart-expense-by-category"
  | "chart-income-vs-expense"
  | "chart-balance-history"
  | "table-recent"

export interface DashboardConfig {
  id: string
  name: string
  defaultPeriod: Exclude<PeriodPreset, "custom" | "all">
  widgets: { type: WidgetType; position: number; isEnabled: boolean }[]
}

export interface SummaryParams {
  from: string
  to: string
  categoryId?: string
  type?: TransactionType
}

export function useDashboardSummary(params: SummaryParams | null) {
  return useQuery({
    queryKey: ["dashboard", "summary", params],
    queryFn: ({ signal }) => api.get<DashboardSummary>(`/dashboard/summary${toQueryString(params!)}`, signal),
    enabled: params !== null,
    // Refetch keeps the frame: charts hold the previous render while new data loads.
    placeholderData: keepPreviousData,
  })
}

export function useDashboardConfig() {
  return useQuery({
    queryKey: ["dashboard", "config"],
    queryFn: ({ signal }) => api.get<DashboardConfig>("/dashboard/config", signal),
    staleTime: 5 * 60 * 1000,
  })
}

export interface DashboardConfigInput {
  defaultPeriod: DashboardConfig["defaultPeriod"]
  widgets: { type: WidgetType; isEnabled: boolean }[]
}

export function useSaveDashboardConfig() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: DashboardConfigInput) => api.put<DashboardConfig>("/dashboard/config", input),
    onSuccess: (config) => queryClient.setQueryData(["dashboard", "config"], config),
  })
}

export function useResetDashboardConfig() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<DashboardConfig>("/dashboard/config/reset"),
    onSuccess: (config) => queryClient.setQueryData(["dashboard", "config"], config),
  })
}

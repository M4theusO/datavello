import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { api } from "@/lib/api"

export type TransactionType = "Income" | "Expense"

export interface Transaction {
  id: string
  date: string // yyyy-MM-dd
  description: string
  type: TransactionType
  amount: number
  categoryId: string
  categoryName: string
  notes: string | null
  imported: boolean
  createdAt: string
  updatedAt: string
}

export interface TransactionInput {
  date: string
  description: string
  type: TransactionType
  amount: number
  categoryId: string
  notes: string | null
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface TransactionFilters {
  from?: string
  to?: string
  categoryId?: string
  type?: TransactionType
  search?: string
  sortBy: "Date" | "Amount"
  sortDir: "Desc" | "Asc"
  page: number
  pageSize: number
}

export const transactionTypeLabels: Record<TransactionType, string> = {
  Income: "Receita",
  Expense: "Despesa",
}

export function toQueryString(params: object): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") search.set(key, String(value))
  }
  const text = search.toString()
  return text ? `?${text}` : ""
}

export function useTransactions(filters: TransactionFilters) {
  return useQuery({
    queryKey: ["transactions", "list", filters],
    queryFn: ({ signal }) => api.get<PagedResult<Transaction>>(`/transactions${toQueryString(filters)}`, signal),
    // Keep showing the current page while the next one loads (no flash, no layout jump).
    placeholderData: keepPreviousData,
  })
}

/** Transactions feed the dashboard and the per-category counts. */
function useInvalidateTransactionData() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ["transactions"] }),
      queryClient.invalidateQueries({ queryKey: ["dashboard"] }),
      queryClient.invalidateQueries({ queryKey: ["categories"] }),
    ])
}

export function useSaveTransaction() {
  const invalidate = useInvalidateTransactionData()
  return useMutation({
    mutationFn: ({ id, input }: { id?: string; input: TransactionInput }) =>
      id ? api.put<Transaction>(`/transactions/${id}`, input) : api.post<Transaction>("/transactions", input),
    onSuccess: invalidate,
  })
}

export function useDeleteTransaction() {
  const invalidate = useInvalidateTransactionData()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/transactions/${id}`),
    onSuccess: invalidate,
  })
}

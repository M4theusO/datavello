import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { api } from "@/lib/api"

export type CategoryKind = "Income" | "Expense" | "Both"

export interface Category {
  id: string
  name: string
  kind: CategoryKind
  isDefault: boolean
  transactionCount: number
}

export interface CategoryInput {
  name: string
  kind: CategoryKind
}

export const categoryKindLabels: Record<CategoryKind, string> = {
  Income: "Receitas",
  Expense: "Despesas",
  Both: "Receitas e despesas",
}

export const categoriesKey = ["categories"] as const

export function useCategories() {
  return useQuery({
    queryKey: categoriesKey,
    queryFn: ({ signal }) => api.get<Category[]>("/categories", signal),
  })
}

/** Category changes affect transaction lists (names) and the dashboard too. */
function useInvalidateCategoryData() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: categoriesKey }),
      queryClient.invalidateQueries({ queryKey: ["transactions"] }),
      queryClient.invalidateQueries({ queryKey: ["dashboard"] }),
    ])
}

export function useSaveCategory() {
  const invalidate = useInvalidateCategoryData()
  return useMutation({
    mutationFn: ({ id, input }: { id?: string; input: CategoryInput }) =>
      id ? api.put<Category>(`/categories/${id}`, input) : api.post<Category>("/categories", input),
    onSuccess: invalidate,
  })
}

export function useDeleteCategory() {
  const invalidate = useInvalidateCategoryData()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/categories/${id}`),
    onSuccess: invalidate,
  })
}

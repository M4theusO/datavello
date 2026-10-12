import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { api } from "@/lib/api"
import type { PagedResult, TransactionType } from "@/features/transactions/api"

export type DateFormatOption = "DayMonthYear" | "MonthDayYear" | "YearMonthDay"
export type DecimalSeparatorOption = "Comma" | "Dot"
export type TypeConvention = "Column" | "Sign" | "AllIncome" | "AllExpense"
export type ImportStatus = "Pending" | "Completed" | "CompletedWithErrors" | "Failed"

export interface ColumnMapping {
  date: number | null
  description: number | null
  amount: number | null
  type: number | null
  category: number | null
}

export interface ImportOptions {
  mapping: ColumnMapping
  dateFormat: DateFormatOption
  decimalSeparator: DecimalSeparatorOption
  typeConvention: TypeConvention
  defaultIncomeCategoryId: string | null
  defaultExpenseCategoryId: string | null
  createMissingCategories: boolean
  skipDuplicates: boolean
}

export interface ImportPreview {
  id: string
  fileName: string
  fileType: "csv" | "xlsx"
  headers: string[]
  previewRows: { rowNumber: number; cells: string[] }[]
  totalRows: number
  emptyRows: number
  suggestedMapping: ColumnMapping
  suggestedTypeConvention: TypeConvention
}

export interface RowError {
  rowNumber: number
  field: string | null
  message: string
  rawValue: string | null
}

export interface ImportValidation {
  totalRows: number
  validRows: number
  invalidRows: number
  duplicateRows: number
  emptyRows: number
  incomeTotal: number
  expenseTotal: number
  newCategories: string[]
  sample: {
    rowNumber: number
    date: string
    description: string
    type: TransactionType
    amount: number
    categoryName: string
    newCategory: boolean
    possibleDuplicate: boolean
  }[]
  errors: RowError[]
  totalErrors: number
}

export interface ImportJob {
  id: string
  fileName: string
  fileType: string
  status: ImportStatus
  userName: string
  totalRows: number
  importedRows: number
  rejectedRows: number
  message: string | null
  createdAt: string
  completedAt: string | null
}

export interface ImportJobDetail {
  job: ImportJob
  errors: RowError[]
  totalErrors: number
}

export const importStatusLabels: Record<ImportStatus, string> = {
  Pending: "Pendente",
  Completed: "Concluída",
  CompletedWithErrors: "Concluída com erros",
  Failed: "Sem linhas importadas",
}

/** Mirrors the backend defaults (Import:MaxFileSizeBytes); the server always re-checks. */
export const MAX_FILE_SIZE = 5 * 1024 * 1024

export function useUploadImport() {
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData()
      form.append("file", file)
      return api.post<ImportPreview>("/imports", form)
    },
  })
}

export function useValidateImport() {
  return useMutation({
    mutationFn: ({ id, options }: { id: string; options: ImportOptions }) => api.post<ImportValidation>(`/imports/${id}/validate`, options),
  })
}

export function useCommitImport() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, options }: { id: string; options: ImportOptions }) => api.post<ImportJobDetail>(`/imports/${id}/commit`, options),
    onSuccess: () =>
      Promise.all(
        [["imports"], ["transactions"], ["dashboard"], ["categories"]].map((queryKey) => queryClient.invalidateQueries({ queryKey })),
      ),
  })
}

export function useCancelImport() {
  return useMutation({ mutationFn: (id: string) => api.delete(`/imports/${id}`) })
}

export function useImportHistory(page: number, pageSize = 20) {
  return useQuery({
    queryKey: ["imports", "list", page, pageSize],
    queryFn: ({ signal }) => api.get<PagedResult<ImportJob>>(`/imports?page=${page}&pageSize=${pageSize}`, signal),
    placeholderData: keepPreviousData,
  })
}

export function useImportDetail(id: string | undefined) {
  return useQuery({
    queryKey: ["imports", "detail", id],
    queryFn: ({ signal }) => api.get<ImportJobDetail>(`/imports/${id}`, signal),
    enabled: !!id,
  })
}

/** Initial options for an uploaded file: the server suggestions plus safe defaults. */
export function defaultOptions(preview: ImportPreview): ImportOptions {
  return {
    mapping: preview.suggestedMapping,
    dateFormat: "DayMonthYear",
    decimalSeparator: "Comma",
    typeConvention: preview.suggestedTypeConvention,
    defaultIncomeCategoryId: null,
    defaultExpenseCategoryId: null,
    createMissingCategories: false,
    skipDuplicates: true,
  }
}

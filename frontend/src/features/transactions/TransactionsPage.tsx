import { useEffect, useMemo, useState } from "react"
import { ArrowDown, ArrowLeftRight, ArrowUp, ChevronLeft, ChevronRight, FileUp, Pencil, Plus, Search, Trash2, X } from "lucide-react"
import { Link, useSearchParams } from "react-router"
import { toast } from "sonner"
import { PeriodFilter, type PeriodValue } from "@/components/filters/PeriodFilter"
import { SimpleSelect } from "@/components/SimpleSelect"
import { EmptyState, ErrorState, Spinner } from "@/components/feedback"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { Badge } from "@/components/ui/badge"
import { Button, buttonVariants } from "@/components/ui/button"
import { Card } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { errorMessage } from "@/lib/api"
import { formatCurrency, formatDate, formatInteger } from "@/lib/format"
import { isPeriodPreset, type PeriodPreset, rangeForPreset } from "@/lib/periods"
import { cn } from "@/lib/utils"
import { useCategories } from "@/features/categories/api"
import { type Transaction, type TransactionFilters, useDeleteTransaction, useTransactions } from "./api"
import { Amount, TypeBadge } from "./TransactionBits"
import { TransactionFormDialog } from "./TransactionFormDialog"

const periodPresets: PeriodPreset[] = ["all", "ThisMonth", "LastMonth", "Last3Months", "Last6Months", "Last12Months", "ThisYear", "custom"]
const pageSizes = [10, 20, 50, 100]

/** Filters live in the URL so they survive reloads and can be shared. */
function useUrlFilters() {
  const [params, setParams] = useSearchParams()

  const period: PeriodValue = {
    preset: isPeriodPreset(params.get("periodo")) ? (params.get("periodo") as PeriodPreset) : "all",
    custom: { from: params.get("de") ?? undefined, to: params.get("ate") ?? undefined },
  }
  const range = period.preset === "custom" ? period.custom : rangeForPreset(period.preset)
  const type = params.get("tipo")
  const pageSize = Number(params.get("porPagina"))

  const filters: TransactionFilters = {
    from: range?.from,
    to: range?.to,
    categoryId: params.get("categoria") ?? undefined,
    type: type === "Income" || type === "Expense" ? type : undefined,
    search: params.get("busca") ?? undefined,
    sortBy: params.get("ordenar") === "Amount" ? "Amount" : "Date",
    sortDir: params.get("direcao") === "Asc" ? "Asc" : "Desc",
    page: Math.max(1, Number(params.get("pagina")) || 1),
    pageSize: pageSizes.includes(pageSize) ? pageSize : 20,
  }

  /** Updates some params; any filter change goes back to page 1. */
  function update(changes: Record<string, string | undefined>, keepPage = false) {
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        for (const [key, value] of Object.entries(changes)) {
          if (value) next.set(key, value)
          else next.delete(key)
        }
        if (!keepPage) next.delete("pagina")
        return next
      },
      { replace: true },
    )
  }

  const hasFilters = ["periodo", "categoria", "tipo", "busca"].some((k) => params.has(k))
  const clear = () => setParams(new URLSearchParams(), { replace: true })

  return { filters, period, update, hasFilters, clear }
}

export function TransactionsPage() {
  const { filters, period, update, hasFilters, clear } = useUrlFilters()
  const transactions = useTransactions(filters)
  const categories = useCategories()
  const remove = useDeleteTransaction()

  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<Transaction | undefined>()
  const [deleting, setDeleting] = useState<Transaction | null>(null)
  const [searchText, setSearchText] = useState(filters.search ?? "")

  // Debounced search: query the API 300 ms after the user stops typing.
  useEffect(() => {
    const handle = setTimeout(() => {
      if ((filters.search ?? "") !== searchText.trim()) update({ busca: searchText.trim() || undefined })
    }, 300)
    return () => clearTimeout(handle)
    // eslint-disable-next-line react-hooks/exhaustive-deps -- only react to typing
  }, [searchText])

  const categoryOptions = useMemo(
    () => [{ value: "all", label: "Todas as categorias" }, ...(categories.data ?? []).map((c) => ({ value: c.id, label: c.name }))],
    [categories.data],
  )

  function openForm(transaction?: Transaction) {
    setEditing(transaction)
    setFormOpen(true)
  }

  function toggleSort(field: "Date" | "Amount") {
    const dir = filters.sortBy === field && filters.sortDir === "Desc" ? "Asc" : "Desc"
    update({ ordenar: field === "Date" ? undefined : field, direcao: dir === "Desc" ? undefined : dir }, true)
  }

  async function confirmDelete() {
    if (!deleting) return
    try {
      await remove.mutateAsync(deleting.id)
      toast.success("Transação excluída.")
    } catch (error) {
      toast.error(errorMessage(error))
    } finally {
      setDeleting(null)
    }
  }

  const data = transactions.data
  const firstItem = data && data.totalCount > 0 ? (data.page - 1) * data.pageSize + 1 : 0
  const lastItem = data ? Math.min(data.page * data.pageSize, data.totalCount) : 0

  return (
    <div className="mx-auto max-w-7xl space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-muted-foreground">Consulte, cadastre e corrija as receitas e despesas da empresa.</p>
        <Button onClick={() => openForm()} className="self-start sm:self-auto">
          <Plus data-icon="inline-start" />
          Nova transação
        </Button>
      </div>

      {/* Filters: one row above the content they scope. */}
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative w-full sm:w-64">
          <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
          <Input
            type="search"
            placeholder="Buscar na descrição"
            aria-label="Buscar na descrição"
            className="h-9 bg-card pl-8"
            value={searchText}
            maxLength={100}
            onChange={(e) => setSearchText(e.target.value)}
          />
        </div>
        <PeriodFilter
          value={period}
          presets={periodPresets}
          onChange={(value) =>
            update({
              periodo: value.preset === "all" ? undefined : value.preset,
              de: value.preset === "custom" ? value.custom.from : undefined,
              ate: value.preset === "custom" ? value.custom.to : undefined,
            })
          }
        />
        <SimpleSelect
          aria-label="Categoria"
          value={filters.categoryId ?? "all"}
          onChange={(v) => update({ categoria: v === "all" ? undefined : v })}
          options={categoryOptions}
          className="h-9 w-full bg-card sm:w-52"
        />
        <SimpleSelect
          aria-label="Tipo"
          value={filters.type ?? "all"}
          onChange={(v) => update({ tipo: v === "all" ? undefined : v })}
          options={[
            { value: "all", label: "Receitas e despesas" },
            { value: "Income", label: "Somente receitas" },
            { value: "Expense", label: "Somente despesas" },
          ]}
          className="h-9 w-full bg-card sm:w-48"
        />
        {hasFilters && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setSearchText("")
              clear()
            }}
          >
            <X data-icon="inline-start" />
            Limpar filtros
          </Button>
        )}
      </div>

      <Card className="gap-0 overflow-hidden py-0">
        {transactions.isPending ? (
          <div className="space-y-3 p-5">
            {Array.from({ length: 8 }, (_, i) => (
              <Skeleton key={i} className="h-9 w-full" />
            ))}
          </div>
        ) : transactions.isError ? (
          <ErrorState error={transactions.error} onRetry={() => transactions.refetch()} className="mx-auto py-12" />
        ) : data!.totalCount === 0 ? (
          hasFilters ? (
            <EmptyState
              icon={<Search />}
              title="Nenhuma transação encontrada"
              description="Nenhum registro corresponde aos filtros selecionados."
              action={
                <Button
                  variant="outline"
                  onClick={() => {
                    setSearchText("")
                    clear()
                  }}
                >
                  Limpar filtros
                </Button>
              }
            />
          ) : (
            <EmptyState
              icon={<ArrowLeftRight />}
              title="Nenhuma transação cadastrada"
              description="Cadastre receitas e despesas manualmente ou importe uma planilha."
              action={
                <div className="flex flex-wrap justify-center gap-2">
                  <Button onClick={() => openForm()}>
                    <Plus data-icon="inline-start" />
                    Nova transação
                  </Button>
                  <Link to="/importar" className={buttonVariants({ variant: "outline" })}>
                    <FileUp data-icon="inline-start" />
                    Importar planilha
                  </Link>
                </div>
              }
            />
          )
        ) : (
          <div className={cn("transition-opacity", transactions.isPlaceholderData && "opacity-60")}>
            {/* Desktop / tablet: table */}
            <Table className="hidden md:table">
              <TableHeader>
                <TableRow className="bg-muted/40 hover:bg-muted/40">
                  <TableHead className="w-32 pl-5">
                    <SortButton label="Data" active={filters.sortBy === "Date"} dir={filters.sortDir} onClick={() => toggleSort("Date")} />
                  </TableHead>
                  <TableHead>Descrição</TableHead>
                  <TableHead>Categoria</TableHead>
                  <TableHead>Tipo</TableHead>
                  <TableHead className="text-right">
                    <SortButton label="Valor" active={filters.sortBy === "Amount"} dir={filters.sortDir} onClick={() => toggleSort("Amount")} align="right" />
                  </TableHead>
                  <TableHead className="w-24 pr-5">
                    <span className="sr-only">Ações</span>
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data!.items.map((t) => (
                  <TableRow key={t.id}>
                    <TableCell className="tabular pl-5 text-muted-foreground">{formatDate(t.date)}</TableCell>
                    <TableCell className="max-w-80">
                      <div className="flex items-center gap-2">
                        <span className="truncate font-medium" title={t.description}>
                          {t.description}
                        </span>
                        {t.imported && (
                          <Badge variant="outline" className="shrink-0 text-muted-foreground">
                            Importada
                          </Badge>
                        )}
                      </div>
                      {t.notes && (
                        <p className="truncate text-xs text-muted-foreground" title={t.notes}>
                          {t.notes}
                        </p>
                      )}
                    </TableCell>
                    <TableCell className="text-muted-foreground">{t.categoryName}</TableCell>
                    <TableCell>
                      <TypeBadge type={t.type} />
                    </TableCell>
                    <TableCell className="text-right">
                      <Amount transaction={t} />
                    </TableCell>
                    <TableCell className="pr-5">
                      <RowActions onEdit={() => openForm(t)} onDelete={() => setDeleting(t)} label={t.description} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            {/* Phone: stacked list */}
            <ul className="divide-y md:hidden">
              {data!.items.map((t) => (
                <li key={t.id} className="flex items-start gap-3 px-4 py-3">
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">{t.description}</p>
                    <p className="truncate text-xs text-muted-foreground">
                      {formatDate(t.date)} · {t.categoryName}
                      {t.imported && " · Importada"}
                    </p>
                  </div>
                  <div className="flex flex-col items-end gap-1">
                    <Amount transaction={t} />
                    <RowActions onEdit={() => openForm(t)} onDelete={() => setDeleting(t)} label={t.description} />
                  </div>
                </li>
              ))}
            </ul>

            <div className="flex flex-col gap-3 border-t px-4 py-3 text-sm sm:flex-row sm:items-center sm:justify-between md:px-5">
              <p className="text-muted-foreground">
                {formatInteger(firstItem)}–{formatInteger(lastItem)} de {formatInteger(data!.totalCount)} transações
              </p>
              <div className="flex items-center gap-2">
                <span className="hidden text-muted-foreground sm:inline">Por página</span>
                <SimpleSelect
                  aria-label="Itens por página"
                  value={String(filters.pageSize)}
                  onChange={(v) => update({ porPagina: v === "20" ? undefined : v })}
                  options={pageSizes.map((s) => ({ value: String(s), label: String(s) }))}
                  className="h-8 w-20"
                />
                <Button
                  variant="outline"
                  size="icon-sm"
                  aria-label="Página anterior"
                  disabled={data!.page <= 1}
                  onClick={() => update({ pagina: String(data!.page - 1) }, true)}
                >
                  <ChevronLeft />
                </Button>
                <span className="tabular min-w-16 text-center">
                  {data!.page} de {Math.max(1, data!.totalPages)}
                </span>
                <Button
                  variant="outline"
                  size="icon-sm"
                  aria-label="Próxima página"
                  disabled={data!.page >= data!.totalPages}
                  onClick={() => update({ pagina: String(data!.page + 1) }, true)}
                >
                  <ChevronRight />
                </Button>
              </div>
            </div>
          </div>
        )}
      </Card>

      <TransactionFormDialog open={formOpen} onOpenChange={setFormOpen} transaction={editing} />

      <AlertDialog open={deleting !== null} onOpenChange={(open) => !open && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Excluir transação?</AlertDialogTitle>
            <AlertDialogDescription>
              {deleting && (
                <>
                  <strong className="text-foreground">{deleting.description}</strong> de {formatDate(deleting.date)}, no valor de{" "}
                  {formatCurrency(deleting.amount)}, será excluída permanentemente.
                </>
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={confirmDelete} disabled={remove.isPending}>
              {remove.isPending && <Spinner className="size-4" />}
              Excluir
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

function SortButton({
  label,
  active,
  dir,
  onClick,
  align = "left",
}: {
  label: string
  active: boolean
  dir: "Asc" | "Desc"
  onClick: () => void
  align?: "left" | "right"
}) {
  const Icon = dir === "Asc" ? ArrowUp : ArrowDown
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "inline-flex items-center gap-1 rounded-sm font-medium outline-none hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring",
        align === "right" && "flex-row-reverse",
        active ? "text-foreground" : "text-muted-foreground",
      )}
      aria-label={`Ordenar por ${label.toLowerCase()}`}
    >
      {label}
      <Icon className={cn("size-3.5", !active && "opacity-0")} aria-hidden="true" />
    </button>
  )
}

function RowActions({ onEdit, onDelete, label }: { onEdit: () => void; onDelete: () => void; label: string }) {
  return (
    <div className="flex justify-end gap-1">
      <Button variant="ghost" size="icon-sm" onClick={onEdit} aria-label={`Editar ${label}`}>
        <Pencil />
      </Button>
      <Button variant="ghost" size="icon-sm" className="text-muted-foreground hover:text-destructive" onClick={onDelete} aria-label={`Excluir ${label}`}>
        <Trash2 />
      </Button>
    </div>
  )
}

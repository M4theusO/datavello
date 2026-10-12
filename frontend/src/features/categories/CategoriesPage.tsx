import { useMemo, useState } from "react"
import { Pencil, Plus, Tags, Trash2 } from "lucide-react"
import { toast } from "sonner"
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
import { Button } from "@/components/ui/button"
import { Card } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip"
import { errorMessage } from "@/lib/api"
import { formatInteger } from "@/lib/format"
import { type Category, useCategories, useDeleteCategory } from "./api"
import { CategoryKindBadge } from "./CategoryKindBadge"
import { CategoryFormDialog } from "./CategoryFormDialog"

type Filter = "all" | "Income" | "Expense"

export function CategoriesPage() {
  const categories = useCategories()
  const remove = useDeleteCategory()
  const [filter, setFilter] = useState<Filter>("all")
  const [editing, setEditing] = useState<Category | undefined>()
  const [formOpen, setFormOpen] = useState(false)
  const [deleting, setDeleting] = useState<Category | null>(null)

  const visible = useMemo(
    () => (categories.data ?? []).filter((c) => filter === "all" || c.kind === filter || c.kind === "Both"),
    [categories.data, filter],
  )

  function openForm(category?: Category) {
    setEditing(category)
    setFormOpen(true)
  }

  async function confirmDelete() {
    if (!deleting) return
    try {
      await remove.mutateAsync(deleting.id)
      toast.success(`Categoria "${deleting.name}" excluída.`)
    } catch (error) {
      toast.error(errorMessage(error))
    } finally {
      setDeleting(null)
    }
  }

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-sm text-muted-foreground">Organize suas receitas e despesas. Cada empresa tem as próprias categorias.</p>
        </div>
        <Button onClick={() => openForm()} className="self-start sm:self-auto">
          <Plus data-icon="inline-start" />
          Nova categoria
        </Button>
      </div>

      <Tabs value={filter} onValueChange={(v) => setFilter(v as Filter)}>
        <TabsList>
          <TabsTrigger value="all">Todas</TabsTrigger>
          <TabsTrigger value="Income">Receitas</TabsTrigger>
          <TabsTrigger value="Expense">Despesas</TabsTrigger>
        </TabsList>
      </Tabs>

      <Card className="gap-0 overflow-hidden py-0">
        {categories.isPending ? (
          <div className="space-y-3 p-5">
            {Array.from({ length: 6 }, (_, i) => (
              <Skeleton key={i} className="h-8 w-full" />
            ))}
          </div>
        ) : categories.isError ? (
          <ErrorState error={categories.error} onRetry={() => categories.refetch()} className="mx-auto py-12" />
        ) : visible.length === 0 ? (
          <EmptyState
            icon={<Tags />}
            title="Nenhuma categoria aqui"
            description="Crie categorias para classificar suas transações."
            action={
              <Button variant="outline" onClick={() => openForm()}>
                <Plus data-icon="inline-start" />
                Nova categoria
              </Button>
            }
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow className="bg-muted/40 hover:bg-muted/40">
                <TableHead className="pl-5">Nome</TableHead>
                <TableHead>Usada em</TableHead>
                <TableHead className="text-right">Transações</TableHead>
                <TableHead className="w-24 pr-5 text-right">
                  <span className="sr-only">Ações</span>
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {visible.map((category) => (
                <TableRow key={category.id}>
                  <TableCell className="pl-5 font-medium">
                    <span className="flex items-center gap-2">
                      {category.name}
                      {category.isDefault && <span className="text-xs font-normal text-muted-foreground">padrão</span>}
                    </span>
                  </TableCell>
                  <TableCell>
                    <CategoryKindBadge kind={category.kind} />
                  </TableCell>
                  <TableCell className="tabular text-right text-muted-foreground">{formatInteger(category.transactionCount)}</TableCell>
                  <TableCell className="pr-5">
                    <div className="flex justify-end gap-1">
                      <Button variant="ghost" size="icon-sm" onClick={() => openForm(category)} aria-label={`Editar ${category.name}`}>
                        <Pencil />
                      </Button>
                      {category.transactionCount > 0 ? (
                        <Tooltip>
                          <TooltipTrigger
                            render={
                              <span tabIndex={0} className="inline-flex rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring" />
                            }
                          >
                            <Button variant="ghost" size="icon-sm" disabled aria-label={`Excluir ${category.name} (indisponível)`}>
                              <Trash2 />
                            </Button>
                          </TooltipTrigger>
                          <TooltipContent>Possui transações vinculadas. Reclassifique-as antes de excluir.</TooltipContent>
                        </Tooltip>
                      ) : (
                        <Button
                          variant="ghost"
                          size="icon-sm"
                          className="text-muted-foreground hover:text-destructive"
                          onClick={() => setDeleting(category)}
                          aria-label={`Excluir ${category.name}`}
                        >
                          <Trash2 />
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Card>

      <CategoryFormDialog open={formOpen} onOpenChange={setFormOpen} category={editing} />

      <AlertDialog open={deleting !== null} onOpenChange={(open) => !open && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Excluir categoria?</AlertDialogTitle>
            <AlertDialogDescription>
              A categoria <strong className="text-foreground">{deleting?.name}</strong> será excluída permanentemente.
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

import { ChevronLeft, ChevronRight, FileSpreadsheet, History } from "lucide-react"
import { Link, useSearchParams } from "react-router"
import { EmptyState, ErrorState } from "@/components/feedback"
import { Button, buttonVariants } from "@/components/ui/button"
import { Card } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { formatInteger } from "@/lib/format"
import { cn } from "@/lib/utils"
import { useImportHistory } from "./api"
import { ImportStatusBadge, ImportTabs } from "./components"

const dateTime = new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" })

export function ImportHistoryPage() {
  const [params, setParams] = useSearchParams()
  const page = Math.max(1, Number(params.get("pagina")) || 1)
  const history = useImportHistory(page)
  const data = history.data

  return (
    <div className="mx-auto max-w-6xl space-y-5">
      <ImportTabs />

      <Card className="gap-0 overflow-hidden py-0">
        {history.isPending ? (
          <div className="space-y-3 p-5">
            {Array.from({ length: 5 }, (_, i) => (
              <Skeleton key={i} className="h-9 w-full" />
            ))}
          </div>
        ) : history.isError ? (
          <ErrorState error={history.error} onRetry={() => history.refetch()} className="mx-auto py-12" />
        ) : data!.totalCount === 0 ? (
          <EmptyState
            icon={<History />}
            title="Nenhuma importação ainda"
            description="As planilhas importadas aparecem aqui, com o resultado de cada linha."
            action={
              <Link to="/importar" className={buttonVariants()}>
                <FileSpreadsheet data-icon="inline-start" />
                Importar planilha
              </Link>
            }
          />
        ) : (
          <div className={cn("transition-opacity", history.isPlaceholderData && "opacity-60")}>
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/40 hover:bg-muted/40">
                    <TableHead className="pl-5">Arquivo</TableHead>
                    <TableHead>Data e hora</TableHead>
                    <TableHead className="hidden md:table-cell">Responsável</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Analisadas</TableHead>
                    <TableHead className="text-right">Importadas</TableHead>
                    <TableHead className="pr-5 text-right">Rejeitadas</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data!.items.map((job) => (
                    <TableRow key={job.id}>
                      <TableCell className="max-w-64 pl-5">
                        <Link
                          to={`/importar/historico/${job.id}`}
                          className="block truncate font-medium outline-none hover:text-primary hover:underline focus-visible:underline"
                          title={job.fileName}
                        >
                          {job.fileName}
                        </Link>
                      </TableCell>
                      <TableCell className="tabular text-muted-foreground">{dateTime.format(new Date(job.createdAt))}</TableCell>
                      <TableCell className="hidden text-muted-foreground md:table-cell">{job.userName}</TableCell>
                      <TableCell>
                        <ImportStatusBadge status={job.status} />
                      </TableCell>
                      <TableCell className="tabular text-right">{formatInteger(job.totalRows)}</TableCell>
                      <TableCell className="tabular text-right">{formatInteger(job.importedRows)}</TableCell>
                      <TableCell className={cn("tabular pr-5 text-right", job.rejectedRows > 0 && "font-medium text-destructive")}>
                        {formatInteger(job.rejectedRows)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
            {data!.totalPages > 1 && (
              <div className="flex items-center justify-end gap-2 border-t px-5 py-3 text-sm">
                <Button variant="outline" size="icon-sm" aria-label="Página anterior" disabled={page <= 1} onClick={() => setParams({ pagina: String(page - 1) })}>
                  <ChevronLeft />
                </Button>
                <span className="tabular">
                  {page} de {data!.totalPages}
                </span>
                <Button
                  variant="outline"
                  size="icon-sm"
                  aria-label="Próxima página"
                  disabled={page >= data!.totalPages}
                  onClick={() => setParams({ pagina: String(page + 1) })}
                >
                  <ChevronRight />
                </Button>
              </div>
            )}
          </div>
        )}
      </Card>
    </div>
  )
}

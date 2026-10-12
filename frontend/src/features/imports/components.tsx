import { NavLink } from "react-router"
import { Badge } from "@/components/ui/badge"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { formatInteger } from "@/lib/format"
import { cn } from "@/lib/utils"
import { type ImportStatus, importStatusLabels, type RowError } from "./api"

/** Tabs shared by the import wizard and the history pages. */
export function ImportTabs() {
  const tab = ({ isActive }: { isActive: boolean }) =>
    cn(
      "rounded-md px-3 py-1.5 text-sm font-medium whitespace-nowrap transition-colors outline-none focus-visible:ring-2 focus-visible:ring-ring",
      isActive ? "bg-card text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground",
    )
  return (
    <nav className="inline-flex rounded-lg bg-muted p-1" aria-label="Importação">
      <NavLink to="/importar" end className={tab}>
        Nova importação
      </NavLink>
      <NavLink to="/importar/historico" className={tab}>
        Histórico
      </NavLink>
    </nav>
  )
}

const statusStyles: Record<ImportStatus, string> = {
  Pending: "bg-muted text-muted-foreground",
  Completed: "bg-success-soft text-success",
  CompletedWithErrors: "bg-amber-50 text-amber-800",
  Failed: "bg-destructive-soft text-destructive",
}

export function ImportStatusBadge({ status }: { status: ImportStatus }) {
  return (
    <Badge variant="secondary" className={statusStyles[status]}>
      {importStatusLabels[status]}
    </Badge>
  )
}

/** Problems per spreadsheet line, so the user can find and fix each one. */
export function RowErrorsTable({ errors, total }: { errors: RowError[]; total: number }) {
  return (
    <div className="space-y-2">
      <div className="max-h-96 overflow-auto rounded-lg border">
        <Table>
          <TableHeader className="sticky top-0 bg-card">
            <TableRow className="hover:bg-transparent">
              <TableHead className="w-20 pl-4">Linha</TableHead>
              <TableHead className="w-32">Campo</TableHead>
              <TableHead>Problema</TableHead>
              <TableHead className="pr-4">Valor na planilha</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {errors.map((e, i) => (
              <TableRow key={`${e.rowNumber}-${e.field}-${i}`}>
                <TableCell className="tabular pl-4 font-medium">{e.rowNumber}</TableCell>
                <TableCell className="text-muted-foreground">{e.field ?? "—"}</TableCell>
                <TableCell className="whitespace-normal">{e.message}</TableCell>
                <TableCell className="max-w-56 truncate pr-4 font-mono text-xs text-muted-foreground" title={e.rawValue ?? undefined}>
                  {e.rawValue ?? <span className="font-sans italic">vazio</span>}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      {total > errors.length && (
        <p className="text-xs text-muted-foreground">
          Mostrando {formatInteger(errors.length)} de {formatInteger(total)} problemas.
        </p>
      )}
    </div>
  )
}

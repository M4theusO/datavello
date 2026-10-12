import { ArrowLeftRight, CheckCircle2, FileWarning, History, RotateCcw, XCircle } from "lucide-react"
import { Link } from "react-router"
import { Button, buttonVariants } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { formatInteger } from "@/lib/format"
import type { ImportJobDetail } from "../api"
import { RowErrorsTable } from "../components"

export function ResultStep({ result, onNew }: { result: ImportJobDetail; onNew: () => void }) {
  const { job } = result
  const { icon, title, tone } =
    job.status === "Completed"
      ? { icon: <CheckCircle2 />, title: "Importação concluída", tone: "bg-success-soft text-success" }
      : job.status === "CompletedWithErrors"
        ? { icon: <FileWarning />, title: "Importação concluída com problemas", tone: "bg-amber-50 text-amber-700" }
        : { icon: <XCircle />, title: "Nenhuma linha foi importada", tone: "bg-destructive-soft text-destructive" }

  return (
    <div className="space-y-5">
      <Card>
        <CardContent className="flex flex-col items-center gap-4 py-6 text-center">
          <div className={`flex size-14 items-center justify-center rounded-full [&_svg]:size-7 ${tone}`} aria-hidden="true">
            {icon}
          </div>
          <div className="space-y-1">
            <h2 className="text-lg font-semibold">{title}</h2>
            <p className="text-sm text-muted-foreground">
              <strong className="text-foreground">{formatInteger(job.importedRows)}</strong> transação(ões) importada(s) e{" "}
              <strong className="text-foreground">{formatInteger(job.rejectedRows)}</strong> linha(s) rejeitada(s) de {job.fileName}.
            </p>
            {job.message && <p className="text-xs text-muted-foreground">{job.message}</p>}
          </div>
          <div className="flex flex-wrap justify-center gap-2">
            {job.importedRows > 0 && (
              <Link to="/transacoes" className={buttonVariants()}>
                <ArrowLeftRight data-icon="inline-start" />
                Ver transações
              </Link>
            )}
            <Button variant="outline" onClick={onNew}>
              <RotateCcw data-icon="inline-start" />
              Nova importação
            </Button>
            <Link to={`/importar/historico/${job.id}`} className={buttonVariants({ variant: "ghost" })}>
              <History data-icon="inline-start" />
              Ver no histórico
            </Link>
          </div>
        </CardContent>
      </Card>

      {result.totalErrors > 0 && (
        <Card className="gap-4">
          <CardHeader>
            <CardTitle className="text-base">Linhas rejeitadas</CardTitle>
            <CardDescription>Corrija estas linhas na planilha e importe apenas elas em um novo arquivo.</CardDescription>
          </CardHeader>
          <CardContent>
            <RowErrorsTable errors={result.errors} total={result.totalErrors} />
          </CardContent>
        </Card>
      )}
    </div>
  )
}

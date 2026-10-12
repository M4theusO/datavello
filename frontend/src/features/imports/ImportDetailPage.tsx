import { ArrowLeft, CheckCircle2 } from "lucide-react"
import { Link, useParams } from "react-router"
import { ErrorState } from "@/components/feedback"
import { buttonVariants } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { ApiError } from "@/lib/api"
import { formatInteger } from "@/lib/format"
import { NotFoundPage } from "@/app/pages/StatusPages"
import { useImportDetail } from "./api"
import { ImportStatusBadge, RowErrorsTable } from "./components"

const dateTime = new Intl.DateTimeFormat("pt-BR", { dateStyle: "long", timeStyle: "short" })

export function ImportDetailPage() {
  const { id } = useParams()
  const detail = useImportDetail(id)

  if (detail.isError && detail.error instanceof ApiError && detail.error.status === 404) return <NotFoundPage />

  return (
    <div className="mx-auto max-w-6xl space-y-5">
      <Link to="/importar/historico" className={buttonVariants({ variant: "ghost", size: "sm" })}>
        <ArrowLeft data-icon="inline-start" />
        Histórico de importações
      </Link>

      {detail.isPending ? (
        <Skeleton className="h-48 w-full rounded-xl" />
      ) : detail.isError ? (
        <Card>
          <ErrorState error={detail.error} onRetry={() => detail.refetch()} className="mx-auto py-12" />
        </Card>
      ) : (
        <>
          <Card className="gap-4">
            <CardHeader>
              <CardTitle className="flex flex-wrap items-center gap-2 text-base">
                {detail.data.job.fileName}
                <ImportStatusBadge status={detail.data.job.status} />
              </CardTitle>
              <CardDescription>
                Importado por {detail.data.job.userName} em {dateTime.format(new Date(detail.data.job.createdAt))}
              </CardDescription>
            </CardHeader>
            <CardContent>
              <dl className="grid grid-cols-3 gap-4 text-sm sm:max-w-md">
                {[
                  ["Analisadas", detail.data.job.totalRows],
                  ["Importadas", detail.data.job.importedRows],
                  ["Rejeitadas", detail.data.job.rejectedRows],
                ].map(([label, value]) => (
                  <div key={label}>
                    <dt className="text-muted-foreground">{label}</dt>
                    <dd className="text-xl font-semibold">{formatInteger(value as number)}</dd>
                  </div>
                ))}
              </dl>
              {detail.data.job.message && <p className="mt-3 text-xs text-muted-foreground">{detail.data.job.message}</p>}
            </CardContent>
          </Card>

          <Card className="gap-4">
            <CardHeader>
              <CardTitle className="text-base">Linhas rejeitadas</CardTitle>
            </CardHeader>
            <CardContent>
              {detail.data.totalErrors === 0 ? (
                <p className="flex items-center gap-2 text-sm text-muted-foreground">
                  <CheckCircle2 className="size-4 text-success" aria-hidden="true" />
                  Todas as linhas com dados foram importadas.
                </p>
              ) : (
                <RowErrorsTable errors={detail.data.errors} total={detail.data.totalErrors} />
              )}
            </CardContent>
          </Card>
        </>
      )}
    </div>
  )
}

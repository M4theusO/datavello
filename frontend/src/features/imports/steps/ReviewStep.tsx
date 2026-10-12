import { AlertCircle, ArrowLeft, CheckCircle2, Copy, FileWarning, MinusCircle, Tags, Upload } from "lucide-react"
import type { ReactNode } from "react"
import { Spinner } from "@/components/feedback"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { errorMessage } from "@/lib/api"
import { formatCurrency, formatDate, formatInteger } from "@/lib/format"
import { cn } from "@/lib/utils"
import { TypeBadge } from "@/features/transactions/TransactionBits"
import { type ImportJobDetail, type ImportOptions, type ImportPreview, type ImportValidation, useCommitImport } from "../api"
import { RowErrorsTable } from "../components"

export function ReviewStep({
  preview,
  options,
  validation,
  onBack,
  onCommitted,
}: {
  preview: ImportPreview
  options: ImportOptions
  validation: ImportValidation
  onBack: () => void
  onCommitted: (result: ImportJobDetail) => void
}) {
  const commit = useCommitImport()

  async function submit() {
    try {
      onCommitted(await commit.mutateAsync({ id: preview.id, options }))
    } catch {
      // Shown below.
    }
  }

  const nothingToImport = validation.validRows === 0

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat icon={<CheckCircle2 />} tone="good" label="Prontas para importar" value={validation.validRows} />
        <Stat icon={<FileWarning />} tone={validation.invalidRows > 0 ? "bad" : "neutral"} label="Com problemas" value={validation.invalidRows} />
        <Stat
          icon={<Copy />}
          tone="neutral"
          label={options.skipDuplicates ? "Possíveis duplicidades (ignoradas)" : "Possíveis duplicidades (serão importadas)"}
          value={validation.duplicateRows}
        />
        <Stat icon={<MinusCircle />} tone="neutral" label="Linhas vazias (ignoradas)" value={validation.emptyRows} />
      </div>

      {!nothingToImport && (
        <Card className="gap-4">
          <CardHeader>
            <CardTitle className="text-base">O que será importado</CardTitle>
            <CardDescription>
              Receitas de <strong className="tabular text-foreground">{formatCurrency(validation.incomeTotal)}</strong> e despesas de{" "}
              <strong className="tabular text-foreground">{formatCurrency(validation.expenseTotal)}</strong>
              {validation.sample.length < validation.validRows && ` · exemplo das primeiras ${validation.sample.length} linhas`}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {validation.newCategories.length > 0 && (
              <div className="flex flex-wrap items-center gap-2 text-sm">
                <Tags className="size-4 text-muted-foreground" aria-hidden="true" />
                <span className="text-muted-foreground">Categorias que serão criadas:</span>
                {validation.newCategories.map((name) => (
                  <Badge key={name} variant="outline">
                    {name}
                  </Badge>
                ))}
              </div>
            )}
            <div className="overflow-x-auto rounded-lg border">
              <Table>
                <TableHeader>
                  <TableRow className="hover:bg-transparent">
                    <TableHead className="pl-4">Linha</TableHead>
                    <TableHead>Data</TableHead>
                    <TableHead>Descrição</TableHead>
                    <TableHead>Categoria</TableHead>
                    <TableHead>Tipo</TableHead>
                    <TableHead className="pr-4 text-right">Valor</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {validation.sample.map((row) => (
                    <TableRow key={row.rowNumber}>
                      <TableCell className="tabular pl-4 text-muted-foreground">{row.rowNumber}</TableCell>
                      <TableCell className="tabular">{formatDate(row.date)}</TableCell>
                      <TableCell className="max-w-64 truncate" title={row.description}>
                        {row.description}
                        {row.possibleDuplicate && (
                          <Badge variant="outline" className="ml-2 text-muted-foreground">
                            possível duplicidade
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell>
                        {row.categoryName}
                        {row.newCategory && <span className="ml-1 text-xs text-muted-foreground">(nova)</span>}
                      </TableCell>
                      <TableCell>
                        <TypeBadge type={row.type} />
                      </TableCell>
                      <TableCell className="tabular pr-4 text-right">{formatCurrency(row.amount)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      )}

      {validation.totalErrors > 0 && (
        <Card className="gap-4">
          <CardHeader>
            <CardTitle className="text-base">Linhas com problemas</CardTitle>
            <CardDescription>
              Estas linhas não serão importadas. Corrija a planilha e envie novamente, ou ajuste o mapeamento e as opções.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <RowErrorsTable errors={validation.errors} total={validation.totalErrors} />
          </CardContent>
        </Card>
      )}

      {nothingToImport ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertTitle>Nenhuma linha pode ser importada</AlertTitle>
          <AlertDescription>Revise o mapeamento e as opções de leitura, ou corrija a planilha.</AlertDescription>
        </Alert>
      ) : (
        validation.invalidRows > 0 && (
          <Alert>
            <AlertCircle />
            <AlertTitle>Importação parcial</AlertTitle>
            <AlertDescription>
              Serão importadas {formatInteger(validation.validRows)} linha(s). As {formatInteger(validation.invalidRows)} linha(s) com problemas
              ficarão registradas no histórico, sem gerar transações.
            </AlertDescription>
          </Alert>
        )
      )}

      {commit.isError && (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{errorMessage(commit.error)}</AlertDescription>
        </Alert>
      )}

      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-between">
        <Button variant="ghost" onClick={onBack} disabled={commit.isPending}>
          <ArrowLeft data-icon="inline-start" />
          Voltar ao mapeamento
        </Button>
        <Button onClick={submit} disabled={nothingToImport || commit.isPending}>
          {commit.isPending ? <Spinner className="size-4 text-primary-foreground" /> : <Upload data-icon="inline-start" />}
          Importar {formatInteger(validation.validRows)} transação(ões)
        </Button>
      </div>
    </div>
  )
}

function Stat({ icon, label, value, tone }: { icon: ReactNode; label: string; value: number; tone: "good" | "bad" | "neutral" }) {
  return (
    <Card className="gap-0 py-4">
      <CardContent className="flex items-center gap-3 px-4">
        <span
          className={cn(
            "flex size-9 shrink-0 items-center justify-center rounded-lg [&_svg]:size-4",
            tone === "good" && "bg-success-soft text-success",
            tone === "bad" && "bg-destructive-soft text-destructive",
            tone === "neutral" && "bg-muted text-muted-foreground",
          )}
          aria-hidden="true"
        >
          {icon}
        </span>
        <div className="min-w-0">
          <p className="text-xl font-semibold">{formatInteger(value)}</p>
          <p className="text-xs text-muted-foreground">{label}</p>
        </div>
      </CardContent>
    </Card>
  )
}

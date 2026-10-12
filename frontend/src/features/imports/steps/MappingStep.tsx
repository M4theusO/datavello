import { useMemo, useState } from "react"
import { AlertCircle, ArrowRight, X } from "lucide-react"
import { SimpleSelect } from "@/components/SimpleSelect"
import { Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import { Label } from "@/components/ui/label"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { errorMessage } from "@/lib/api"
import { formatInteger } from "@/lib/format"
import { cn } from "@/lib/utils"
import { useCategories } from "@/features/categories/api"
import { type ColumnMapping, type ImportOptions, type ImportPreview, type ImportValidation, useValidateImport } from "../api"

type Field = keyof ColumnMapping

const fields: { key: Field; label: string; required?: boolean; hint: string }[] = [
  { key: "date", label: "Data", required: true, hint: "Data do lançamento" },
  { key: "description", label: "Descrição", required: true, hint: "Texto que identifica o lançamento" },
  { key: "amount", label: "Valor", required: true, hint: "Valor em reais" },
  { key: "type", label: "Tipo", hint: "Receita/Despesa, Entrada/Saída, C/D" },
  { key: "category", label: "Categoria", hint: "Nome da categoria" },
]

const columnLetter = (index: number) => {
  let name = ""
  for (let n = index + 1; n > 0; n = Math.floor((n - 1) / 26)) name = String.fromCharCode(65 + ((n - 1) % 26)) + name
  return name
}

export function MappingStep({
  preview,
  initial,
  suggestCategoryDefaults,
  onCancel,
  onValidated,
}: {
  preview: ImportPreview
  initial: ImportOptions
  /** True on the first visit; false when coming back from the review with choices already made. */
  suggestCategoryDefaults: boolean
  onCancel: () => void
  onValidated: (options: ImportOptions, validation: ImportValidation) => void
}) {
  const [edited, setOptions] = useState<ImportOptions>(initial)
  const [defaultsTouched, setDefaultsTouched] = useState(!suggestCategoryDefaults)
  const validate = useValidateImport()
  const categories = useCategories()

  // Until the user picks them, the default categories are "Outras receitas" / "Outras despesas" (when they exist).
  const options: ImportOptions = defaultsTouched
    ? edited
    : {
        ...edited,
        defaultIncomeCategoryId: categories.data?.find((c) => c.name === "Outras receitas" && c.kind !== "Expense")?.id ?? null,
        defaultExpenseCategoryId: categories.data?.find((c) => c.name === "Outras despesas" && c.kind !== "Income")?.id ?? null,
      }

  const columnOptions = useMemo(
    () => [{ value: "none", label: "— Não usar —" }, ...preview.headers.map((h, i) => ({ value: String(i), label: `${columnLetter(i)} · ${h}` }))],
    [preview.headers],
  )

  const categoryOptions = (kind: "Income" | "Expense") => [
    { value: "none", label: "Nenhuma (linha sem categoria é rejeitada)" },
    ...(categories.data ?? []).filter((c) => c.kind === kind || c.kind === "Both").map((c) => ({ value: c.id, label: c.name })),
  ]

  const mapped = new Map<number, string>()
  for (const f of fields) {
    const column = options.mapping[f.key]
    if (column !== null) mapped.set(column, f.label)
  }

  const problems: string[] = []
  for (const f of fields.filter((f) => f.required)) {
    if (options.mapping[f.key] === null) problems.push(`Selecione a coluna de ${f.label.toLowerCase()}.`)
  }
  if (options.typeConvention === "Column" && options.mapping.type === null) {
    problems.push("Selecione a coluna de tipo ou escolha outra forma de identificar receitas e despesas.")
  }
  const used = Object.values(options.mapping).filter((v) => v !== null)
  if (new Set(used).size !== used.length) problems.push("Uma mesma coluna foi escolhida para mais de um campo.")

  function setMapping(field: Field, value: string) {
    setOptions((o) => ({ ...o, mapping: { ...o.mapping, [field]: value === "none" ? null : Number(value) } }))
  }

  async function submit() {
    try {
      onValidated(options, await validate.mutateAsync({ id: preview.id, options }))
    } catch {
      // Shown below.
    }
  }

  return (
    <div className="space-y-5">
      <Card className="gap-4">
        <CardHeader>
          <CardTitle className="text-base">Prévia de {preview.fileName}</CardTitle>
          <CardDescription>
            {formatInteger(preview.totalRows)} linha(s) com dados
            {preview.emptyRows > 0 && ` · ${formatInteger(preview.emptyRows)} linha(s) vazia(s) serão ignoradas`}
            {preview.previewRows.length < preview.totalRows && ` · mostrando as primeiras ${preview.previewRows.length}`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="max-h-80 overflow-auto rounded-lg border">
            <Table>
              <TableHeader className="sticky top-0 z-10 bg-card">
                <TableRow className="hover:bg-transparent">
                  <TableHead className="w-14 pl-3 text-xs">Linha</TableHead>
                  {preview.headers.map((h, i) => (
                    <TableHead key={i} className={cn("min-w-32", mapped.has(i) && "bg-brand-soft")}>
                      <div className="flex flex-col gap-0.5 py-1">
                        <span className="text-xs text-muted-foreground">{columnLetter(i)}</span>
                        <span className="truncate">{h}</span>
                        {mapped.has(i) && (
                          <Badge variant="secondary" className="w-fit bg-primary text-primary-foreground">
                            {mapped.get(i)}
                          </Badge>
                        )}
                      </div>
                    </TableHead>
                  ))}
                </TableRow>
              </TableHeader>
              <TableBody>
                {preview.previewRows.map((row) => (
                  <TableRow key={row.rowNumber}>
                    <TableCell className="tabular pl-3 text-xs text-muted-foreground">{row.rowNumber}</TableCell>
                    {row.cells.map((cell, i) => (
                      <TableCell key={i} className={cn("max-w-56 truncate", mapped.has(i) && "bg-brand-soft/50")} title={cell}>
                        {cell}
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-5 lg:grid-cols-2">
        <Card className="gap-4">
          <CardHeader>
            <CardTitle className="text-base">Colunas</CardTitle>
            <CardDescription>Confira qual coluna da planilha corresponde a cada campo. Sugerimos pelos nomes dos cabeçalhos.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {fields.map((f) => (
              <div key={f.key} className="grid gap-1.5 sm:grid-cols-[9rem_1fr] sm:items-center">
                <Label htmlFor={`map-${f.key}`} className="flex flex-col items-start gap-0">
                  <span>
                    {f.label}
                    {f.required && <span className="text-destructive"> *</span>}
                  </span>
                  <span className="text-xs font-normal text-muted-foreground">{f.hint}</span>
                </Label>
                <SimpleSelect
                  id={`map-${f.key}`}
                  value={options.mapping[f.key] === null ? "none" : String(options.mapping[f.key])}
                  onChange={(v) => setMapping(f.key, v)}
                  options={columnOptions}
                />
              </div>
            ))}
          </CardContent>
        </Card>

        <Card className="gap-4">
          <CardHeader>
            <CardTitle className="text-base">Como ler os valores</CardTitle>
            <CardDescription>Ajuste conforme o padrão da sua planilha.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <OptionRow label="Formato da data" id="opt-date">
              <SimpleSelect
                id="opt-date"
                value={options.dateFormat}
                onChange={(v) => setOptions((o) => ({ ...o, dateFormat: v as ImportOptions["dateFormat"] }))}
                options={[
                  { value: "DayMonthYear", label: "DD/MM/AAAA (31/12/2026)" },
                  { value: "MonthDayYear", label: "MM/DD/AAAA (12/31/2026)" },
                  { value: "YearMonthDay", label: "AAAA-MM-DD (2026-12-31)" },
                ]}
              />
            </OptionRow>
            <OptionRow label="Separador decimal" id="opt-decimal">
              <SimpleSelect
                id="opt-decimal"
                value={options.decimalSeparator}
                onChange={(v) => setOptions((o) => ({ ...o, decimalSeparator: v as ImportOptions["decimalSeparator"] }))}
                options={[
                  { value: "Comma", label: "Vírgula (1.234,56)" },
                  { value: "Dot", label: "Ponto (1,234.56)" },
                ]}
              />
            </OptionRow>
            <OptionRow label="Receita ou despesa" id="opt-type">
              <SimpleSelect
                id="opt-type"
                value={options.typeConvention}
                onChange={(v) => setOptions((o) => ({ ...o, typeConvention: v as ImportOptions["typeConvention"] }))}
                options={[
                  { value: "Column", label: "Pela coluna de tipo" },
                  { value: "Sign", label: "Pelo sinal do valor (negativo = despesa)" },
                  { value: "AllIncome", label: "Todas as linhas são receitas" },
                  { value: "AllExpense", label: "Todas as linhas são despesas" },
                ]}
              />
            </OptionRow>
            <OptionRow label="Categoria padrão (receitas)" id="opt-income-cat">
              <SimpleSelect
                id="opt-income-cat"
                value={options.defaultIncomeCategoryId ?? "none"}
                onChange={(v) => {
                  setOptions({ ...options, defaultIncomeCategoryId: v === "none" ? null : v })
                  setDefaultsTouched(true)
                }}
                options={categoryOptions("Income")}
              />
            </OptionRow>
            <OptionRow label="Categoria padrão (despesas)" id="opt-expense-cat">
              <SimpleSelect
                id="opt-expense-cat"
                value={options.defaultExpenseCategoryId ?? "none"}
                onChange={(v) => {
                  setOptions({ ...options, defaultExpenseCategoryId: v === "none" ? null : v })
                  setDefaultsTouched(true)
                }}
                options={categoryOptions("Expense")}
              />
            </OptionRow>
            <p className="text-xs text-muted-foreground">A categoria padrão é usada quando a linha não tem categoria ou a coluna não foi mapeada.</p>

            <div className="space-y-2.5 border-t pt-3">
              <CheckOption
                id="opt-create"
                checked={options.createMissingCategories}
                onChange={(checked) => setOptions((o) => ({ ...o, createMissingCategories: checked }))}
                label="Criar categorias que ainda não existem"
                description="Sem esta opção, linhas com categoria desconhecida são rejeitadas."
              />
              <CheckOption
                id="opt-duplicates"
                checked={options.skipDuplicates}
                onChange={(checked) => setOptions((o) => ({ ...o, skipDuplicates: checked }))}
                label="Ignorar possíveis duplicidades"
                description="Mesma data, tipo, valor e descrição de uma transação já cadastrada ou de outra linha do arquivo."
              />
            </div>
          </CardContent>
        </Card>
      </div>

      {(problems.length > 0 || validate.isError) && (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>
            {validate.isError ? errorMessage(validate.error) : problems.map((p) => <p key={p}>{p}</p>)}
          </AlertDescription>
        </Alert>
      )}

      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-between">
        <Button variant="ghost" onClick={onCancel}>
          <X data-icon="inline-start" />
          Cancelar importação
        </Button>
        <Button onClick={submit} disabled={problems.length > 0 || validate.isPending}>
          {validate.isPending ? <Spinner className="size-4 text-primary-foreground" /> : null}
          Validar {formatInteger(preview.totalRows)} linha(s)
          {!validate.isPending && <ArrowRight data-icon="inline-end" />}
        </Button>
      </div>
    </div>
  )
}

function OptionRow({ label, id, children }: { label: string; id: string; children: React.ReactNode }) {
  return (
    <div className="grid gap-1.5 sm:grid-cols-[11rem_1fr] sm:items-center">
      <Label htmlFor={id}>{label}</Label>
      {children}
    </div>
  )
}

function CheckOption({
  id,
  checked,
  onChange,
  label,
  description,
}: {
  id: string
  checked: boolean
  onChange: (checked: boolean) => void
  label: string
  description: string
}) {
  return (
    <div className="flex items-start gap-2.5">
      <Checkbox id={id} checked={checked} onCheckedChange={(v) => onChange(v === true)} className="mt-0.5" />
      <Label htmlFor={id} className="flex flex-col items-start gap-0.5 font-normal">
        <span className="font-medium">{label}</span>
        <span className="text-xs text-muted-foreground">{description}</span>
      </Label>
    </div>
  )
}

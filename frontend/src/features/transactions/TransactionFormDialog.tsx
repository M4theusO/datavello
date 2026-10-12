import { useEffect, useMemo } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { AlertCircle, ArrowDownLeft, ArrowUpRight } from "lucide-react"
import { Link } from "react-router"
import { Controller, useForm, useWatch } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"
import { SimpleSelect } from "@/components/SimpleSelect"
import { Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group"
import { ApiError, errorMessage } from "@/lib/api"
import { toIsoDate } from "@/lib/format"
import { formatMoneyInput, parseMoneyInput } from "@/lib/money"
import { cn } from "@/lib/utils"
import { useCategories } from "@/features/categories/api"
import { type Transaction, type TransactionType, useSaveTransaction } from "./api"

// Stricter than the API limit (numeric(18,2)) so the value stays exact as a JS number.
const MAX_AMOUNT = 999_999_999_999.99

const schema = z.object({
  date: z
    .string()
    .min(1, "Informe a data.")
    .refine((v) => v >= "1900-01-01" && v <= "2100-12-31", "Data fora do intervalo permitido (1900 a 2100)."),
  description: z.string().trim().min(2, "A descrição deve ter entre 2 e 200 caracteres.").max(200, "A descrição deve ter entre 2 e 200 caracteres."),
  type: z.enum(["Income", "Expense"]),
  amount: z
    .string()
    .min(1, "Informe o valor.")
    .refine((v) => parseMoneyInput(v) !== null, "Valor inválido. Use o formato 1.234,56.")
    .refine((v) => (parseMoneyInput(v) ?? 0) > 0, "O valor deve ser maior que zero.")
    .refine((v) => (parseMoneyInput(v) ?? 0) <= MAX_AMOUNT, "O valor é muito alto."),
  categoryId: z.string().min(1, "Informe a categoria."),
  notes: z.string().max(1000, "A observação deve ter no máximo 1000 caracteres."),
})

type FormValues = z.infer<typeof schema>

function emptyValues(): FormValues {
  return { date: toIsoDate(new Date()), description: "", type: "Expense", amount: "", categoryId: "", notes: "" }
}

export function TransactionFormDialog({
  open,
  onOpenChange,
  transaction,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Transaction being edited; undefined creates a new one. */
  transaction?: Transaction
}) {
  const save = useSaveTransaction()
  const categories = useCategories()
  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: emptyValues() })
  const { errors } = form.formState
  const type = useWatch({ control: form.control, name: "type" })

  useEffect(() => {
    if (open) {
      form.reset(
        transaction
          ? {
              date: transaction.date,
              description: transaction.description,
              type: transaction.type,
              amount: formatMoneyInput(transaction.amount),
              categoryId: transaction.categoryId,
              notes: transaction.notes ?? "",
            }
          : emptyValues(),
      )
      save.reset()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when the dialog opens
  }, [open, transaction])

  const categoryOptions = useMemo(
    () =>
      (categories.data ?? [])
        .filter((c) => c.kind === "Both" || c.kind === type)
        .map((c) => ({ value: c.id, label: c.name })),
    [categories.data, type],
  )

  // Switching between income/expense clears a category that no longer applies.
  useEffect(() => {
    const current = form.getValues("categoryId")
    if (current && categories.data && !categoryOptions.some((o) => o.value === current)) {
      form.setValue("categoryId", "")
    }
  }, [categoryOptions, categories.data, form])

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await save.mutateAsync({
        id: transaction?.id,
        input: {
          date: values.date,
          description: values.description.trim(),
          type: values.type,
          amount: parseMoneyInput(values.amount)!,
          categoryId: values.categoryId,
          notes: values.notes.trim() || null,
        },
      })
      toast.success(transaction ? "Transação atualizada." : "Transação cadastrada.")
      onOpenChange(false)
    } catch (error) {
      if (error instanceof ApiError) {
        for (const field of ["date", "description", "amount", "categoryId", "notes"] as const) {
          const [message] = error.fieldErrors(field)
          if (message) form.setError(field, { message })
        }
      }
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[calc(100svh-2rem)] overflow-y-auto sm:max-w-lg">
        <form onSubmit={onSubmit} noValidate className="space-y-5">
          <DialogHeader>
            <DialogTitle>{transaction ? "Editar transação" : "Nova transação"}</DialogTitle>
            <DialogDescription>Registre uma receita ou despesa da empresa.</DialogDescription>
          </DialogHeader>

          {save.isError && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{errorMessage(save.error)}</AlertDescription>
            </Alert>
          )}

          <FieldGroup>
            <Field>
              <FieldLabel>Tipo</FieldLabel>
              <Controller
                control={form.control}
                name="type"
                render={({ field }) => (
                  <ToggleGroup
                    value={[field.value]}
                    onValueChange={(value: string[]) => value[0] && field.onChange(value[0] as TransactionType)}
                    variant="outline"
                    className="grid w-full grid-cols-2"
                    aria-label="Tipo da transação"
                  >
                    <ToggleGroupItem
                      value="Income"
                      className={cn("h-9 w-full", field.value === "Income" && "border-success/40 bg-success-soft! text-success!")}
                    >
                      <ArrowUpRight />
                      Receita
                    </ToggleGroupItem>
                    <ToggleGroupItem
                      value="Expense"
                      className={cn("h-9 w-full", field.value === "Expense" && "border-destructive/40 bg-destructive-soft! text-destructive!")}
                    >
                      <ArrowDownLeft />
                      Despesa
                    </ToggleGroupItem>
                  </ToggleGroup>
                )}
              />
            </Field>

            <div className="grid gap-4 sm:grid-cols-2">
              <Field data-invalid={!!errors.date}>
                <FieldLabel htmlFor="tx-date">Data</FieldLabel>
                <Input id="tx-date" type="date" aria-invalid={!!errors.date} {...form.register("date")} />
                <FieldError errors={[errors.date]} />
              </Field>

              <Field data-invalid={!!errors.amount}>
                <FieldLabel htmlFor="tx-amount">Valor (R$)</FieldLabel>
                <Input
                  id="tx-amount"
                  inputMode="decimal"
                  placeholder="0,00"
                  className="tabular text-right"
                  aria-invalid={!!errors.amount}
                  {...form.register("amount", {
                    onBlur: (e) => {
                      const parsed = parseMoneyInput(e.target.value)
                      if (parsed !== null) form.setValue("amount", formatMoneyInput(parsed))
                    },
                  })}
                />
                <FieldError errors={[errors.amount]} />
              </Field>
            </div>

            <Field data-invalid={!!errors.description}>
              <FieldLabel htmlFor="tx-description">Descrição</FieldLabel>
              <Input id="tx-description" placeholder="Ex.: Venda balcão, Conta de luz" aria-invalid={!!errors.description} {...form.register("description")} />
              <FieldError errors={[errors.description]} />
            </Field>

            <Field data-invalid={!!errors.categoryId}>
              <FieldLabel htmlFor="tx-category">Categoria</FieldLabel>
              <Controller
                control={form.control}
                name="categoryId"
                render={({ field }) => (
                  <SimpleSelect
                    id="tx-category"
                    value={field.value}
                    onChange={field.onChange}
                    options={categoryOptions}
                    placeholder={categories.isPending ? "Carregando…" : "Selecione a categoria"}
                    invalid={!!errors.categoryId}
                    disabled={categories.isPending}
                  />
                )}
              />
              {categories.isSuccess && categoryOptions.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  Nenhuma categoria de {type === "Income" ? "receita" : "despesa"}.{" "}
                  <Link to="/categorias" className="font-medium text-primary hover:underline">
                    Criar categoria
                  </Link>
                </p>
              ) : (
                <FieldError errors={[errors.categoryId]} />
              )}
            </Field>

            <Field data-invalid={!!errors.notes}>
              <FieldLabel htmlFor="tx-notes">
                Observação <span className="font-normal text-muted-foreground">(opcional)</span>
              </FieldLabel>
              <Textarea id="tx-notes" rows={2} aria-invalid={!!errors.notes} {...form.register("notes")} />
              <FieldError errors={[errors.notes]} />
            </Field>
          </FieldGroup>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Spinner className="size-4 text-primary-foreground" />}
              {transaction ? "Salvar alterações" : "Cadastrar"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

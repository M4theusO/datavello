import { useEffect } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { AlertCircle } from "lucide-react"
import { Controller, useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"
import { SimpleSelect } from "@/components/SimpleSelect"
import { Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { ApiError, errorMessage } from "@/lib/api"
import { type Category, type CategoryKind, categoryKindLabels, useSaveCategory } from "./api"

const schema = z.object({
  name: z.string().trim().min(2, "O nome deve ter entre 2 e 80 caracteres.").max(80, "O nome deve ter entre 2 e 80 caracteres."),
  kind: z.enum(["Income", "Expense", "Both"], { error: "Informe o tipo da categoria." }),
})

type FormValues = z.infer<typeof schema>

const kindOptions = (Object.keys(categoryKindLabels) as CategoryKind[]).map((value) => ({ value, label: categoryKindLabels[value] }))

export function CategoryFormDialog({
  open,
  onOpenChange,
  category,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Category being edited; undefined creates a new one. */
  category?: Category
}) {
  const save = useSaveCategory()
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", kind: "Expense" },
  })
  const { errors } = form.formState

  useEffect(() => {
    if (open) {
      form.reset(category ? { name: category.name, kind: category.kind } : { name: "", kind: "Expense" })
      save.reset()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when the dialog opens
  }, [open, category])

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await save.mutateAsync({ id: category?.id, input: values })
      toast.success(category ? "Categoria atualizada." : "Categoria criada.")
      onOpenChange(false)
    } catch (error) {
      if (error instanceof ApiError && error.status === 409 && error.message.includes("Já existe")) {
        form.setError("name", { message: error.message })
      }
    }
  })

  const showGeneralError = save.isError && !form.formState.errors.name

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={onSubmit} noValidate className="space-y-5">
          <DialogHeader>
            <DialogTitle>{category ? "Editar categoria" : "Nova categoria"}</DialogTitle>
            <DialogDescription>Categorias organizam suas receitas e despesas nos relatórios.</DialogDescription>
          </DialogHeader>

          {showGeneralError && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{errorMessage(save.error)}</AlertDescription>
            </Alert>
          )}

          <FieldGroup>
            <Field data-invalid={!!errors.name}>
              <FieldLabel htmlFor="category-name">Nome</FieldLabel>
              <Input id="category-name" autoFocus aria-invalid={!!errors.name} {...form.register("name")} />
              <FieldError errors={[errors.name]} />
            </Field>

            <Field data-invalid={!!errors.kind}>
              <FieldLabel htmlFor="category-kind">Usada em</FieldLabel>
              <Controller
                control={form.control}
                name="kind"
                render={({ field }) => (
                  <SimpleSelect id="category-kind" value={field.value} onChange={field.onChange} options={kindOptions} invalid={!!errors.kind} />
                )}
              />
              {errors.kind ? (
                <FieldError errors={[errors.kind]} />
              ) : (
                <FieldDescription>Define em quais transações a categoria pode ser escolhida.</FieldDescription>
              )}
            </Field>
          </FieldGroup>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Spinner className="size-4 text-primary-foreground" />}
              {category ? "Salvar alterações" : "Criar categoria"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

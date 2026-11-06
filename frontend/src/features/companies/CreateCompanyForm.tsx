import { zodResolver } from "@hookform/resolvers/zod"
import { AlertCircle, Building2 } from "lucide-react"
import { useForm } from "react-hook-form"
import { useNavigate } from "react-router"
import { toast } from "sonner"
import { z } from "zod"
import { Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { useCreateCompany } from "@/features/auth/auth"
import { ApiError, errorMessage } from "@/lib/api"

const schema = z.object({
  name: z.string().trim().min(2, "O nome da empresa deve ter entre 2 e 150 caracteres.").max(150, "O nome da empresa deve ter entre 2 e 150 caracteres."),
})

/** Creates a company owned by the user (with default categories and dashboard) and switches to it. */
export function CreateCompanyForm({ onCreated, onCancel }: { onCreated?: () => void; onCancel?: () => void }) {
  const create = useCreateCompany()
  const form = useForm<z.infer<typeof schema>>({ resolver: zodResolver(schema), defaultValues: { name: "" } })
  const { errors } = form.formState

  const onSubmit = form.handleSubmit(async ({ name }) => {
    try {
      const me = await create.mutateAsync({ name })
      toast.success(`Empresa ${me.activeCompany?.name ?? name} criada.`)
      onCreated?.()
    } catch (error) {
      const [message] = error instanceof ApiError ? error.fieldErrors("name") : []
      if (message) form.setError("name", { message })
    }
  })

  const showGeneralError = create.isError && !(create.error instanceof ApiError && create.error.fieldErrors("name").length > 0)

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-5">
      {showGeneralError && (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{errorMessage(create.error)}</AlertDescription>
        </Alert>
      )}
      <Field data-invalid={!!errors.name}>
        <FieldLabel htmlFor="new-company-name">Nome da empresa</FieldLabel>
        <Input id="new-company-name" autoComplete="organization" aria-invalid={!!errors.name} {...form.register("name")} />
        {errors.name ? (
          <FieldError errors={[errors.name]} />
        ) : (
          <FieldDescription>Ela começa com categorias e um dashboard prontos, e seus dados ficam separados das outras empresas.</FieldDescription>
        )}
      </Field>
      <div className="flex justify-end gap-2">
        {onCancel && (
          <Button type="button" variant="outline" onClick={onCancel}>
            Cancelar
          </Button>
        )}
        <Button type="submit" disabled={create.isPending}>
          {create.isPending ? <Spinner className="size-4 text-primary-foreground" /> : <Building2 data-icon="inline-start" />}
          Criar empresa
        </Button>
      </div>
    </form>
  )
}

export function CreateCompanyDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const navigate = useNavigate()
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Nova empresa</DialogTitle>
          <DialogDescription>Você será o proprietário e poderá convidar outras pessoas.</DialogDescription>
        </DialogHeader>
        {/* Remounted on each open, so the form starts empty. */}
        {open && (
          <CreateCompanyForm
            onCreated={() => {
              onOpenChange(false)
              navigate("/")
            }}
            onCancel={() => onOpenChange(false)}
          />
        )}
      </DialogContent>
    </Dialog>
  )
}

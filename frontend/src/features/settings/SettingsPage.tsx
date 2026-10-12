import { useEffect } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { Building2, Info, Save } from "lucide-react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"
import { ErrorState, Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { ApiError, errorMessage } from "@/lib/api"
import { useCompany, useUpdateCompany } from "./api"

const schema = z.object({
  name: z.string().trim().min(2, "O nome da empresa deve ter entre 2 e 150 caracteres.").max(150, "O nome da empresa deve ter entre 2 e 150 caracteres."),
  document: z
    .string()
    .trim()
    .refine((v) => v === "" || [11, 14].includes(v.replace(/\D/g, "").length), "Informe um CPF (11 dígitos) ou CNPJ (14 dígitos)."),
})

type FormValues = z.infer<typeof schema>

const dateFormatter = new Intl.DateTimeFormat("pt-BR", { dateStyle: "long" })

export function SettingsPage() {
  const company = useCompany()
  const update = useUpdateCompany()
  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { name: "", document: "" } })
  const { errors, isDirty } = form.formState
  const isOwner = company.data?.role === "Owner"

  useEffect(() => {
    if (company.data) form.reset({ name: company.data.name, document: company.data.document ?? "" })
  }, [company.data, form])

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await update.mutateAsync({ name: values.name, document: values.document || null })
      toast.success("Dados da empresa atualizados.")
    } catch (error) {
      if (error instanceof ApiError && error.message.includes("CPF ou CNPJ")) form.setError("document", { message: error.message })
      else toast.error(errorMessage(error))
    }
  })

  if (company.isPending) {
    return (
      <div className="mx-auto max-w-3xl space-y-4">
        <Skeleton className="h-72 w-full rounded-xl" />
      </div>
    )
  }

  if (company.isError) {
    return (
      <Card className="mx-auto max-w-3xl">
        <ErrorState error={company.error} onRetry={() => company.refetch()} className="mx-auto py-12" />
      </Card>
    )
  }

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <Card>
        <form onSubmit={onSubmit} noValidate>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Building2 className="size-4 text-muted-foreground" aria-hidden="true" />
              Dados da empresa
            </CardTitle>
            <CardDescription>Informações usadas em todo o Datavello. Os dados de cada empresa ficam separados.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-5 pt-5">
            {!isOwner && (
              <Alert>
                <Info />
                <AlertDescription>Apenas o proprietário da empresa pode alterar estes dados.</AlertDescription>
              </Alert>
            )}
            <FieldGroup>
              <Field data-invalid={!!errors.name}>
                <FieldLabel htmlFor="company-name">Nome da empresa</FieldLabel>
                <Input id="company-name" disabled={!isOwner} aria-invalid={!!errors.name} {...form.register("name")} />
                <FieldError errors={[errors.name]} />
              </Field>
              <Field data-invalid={!!errors.document}>
                <FieldLabel htmlFor="company-document">
                  CPF ou CNPJ <span className="font-normal text-muted-foreground">(opcional)</span>
                </FieldLabel>
                <Input
                  id="company-document"
                  inputMode="numeric"
                  placeholder="00.000.000/0000-00"
                  disabled={!isOwner}
                  aria-invalid={!!errors.document}
                  {...form.register("document")}
                />
                {errors.document ? <FieldError errors={[errors.document]} /> : <FieldDescription>Pode ser digitado com ou sem pontuação.</FieldDescription>}
              </Field>
              <Field>
                <FieldLabel htmlFor="company-currency">Moeda</FieldLabel>
                <Input id="company-currency" value="Real brasileiro (BRL)" disabled readOnly />
                <FieldDescription>Fixa em reais nesta versão do Datavello.</FieldDescription>
              </Field>
            </FieldGroup>
          </CardContent>
          {isOwner && (
            <CardFooter className="mt-5 justify-end gap-2 border-t pt-4">
              <Button type="submit" disabled={!isDirty || update.isPending}>
                {update.isPending ? <Spinner className="size-4 text-primary-foreground" /> : <Save data-icon="inline-start" />}
                Salvar alterações
              </Button>
            </CardFooter>
          )}
        </form>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Sua participação</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-4 text-sm sm:grid-cols-3">
            <div>
              <dt className="text-muted-foreground">Seu papel</dt>
              <dd className="mt-1">
                <Badge variant="secondary" className="bg-brand-soft text-primary">
                  {isOwner ? "Proprietário" : "Membro"}
                </Badge>
              </dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Usuários com acesso</dt>
              <dd className="mt-1 font-medium">{company.data.memberCount}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Empresa criada em</dt>
              <dd className="mt-1 font-medium">{dateFormatter.format(new Date(company.data.createdAt))}</dd>
            </div>
          </dl>
          <p className="mt-4 text-xs text-muted-foreground">Convites para outros usuários e permissões por papel serão disponibilizados em uma versão futura.</p>
        </CardContent>
      </Card>
    </div>
  )
}

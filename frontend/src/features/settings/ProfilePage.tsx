import { useEffect } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { KeyRound, Save, UserRound } from "lucide-react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"
import { ErrorState, Spinner } from "@/components/feedback"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { ApiError, errorMessage } from "@/lib/api"
import { useChangePassword, useProfile, useUpdateProfile } from "./api"

const profileSchema = z.object({
  fullName: z.string().trim().min(2, "O nome deve ter entre 2 e 150 caracteres.").max(150, "O nome deve ter entre 2 e 150 caracteres."),
})

const passwordSchema = z
  .object({
    currentPassword: z.string().min(1, "Informe a senha atual."),
    newPassword: z
      .string()
      .min(8, "A senha deve ter pelo menos 8 caracteres.")
      .max(128, "A senha deve ter no máximo 128 caracteres.")
      .regex(/\d/, "A senha deve conter pelo menos um número."),
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, { path: ["confirmPassword"], message: "As senhas não coincidem." })
  .refine((v) => v.newPassword !== v.currentPassword, { path: ["newPassword"], message: "A nova senha deve ser diferente da atual." })

export function ProfilePage() {
  const profile = useProfile()

  if (profile.isPending) {
    return (
      <div className="mx-auto max-w-3xl space-y-4">
        <Skeleton className="h-56 w-full rounded-xl" />
        <Skeleton className="h-72 w-full rounded-xl" />
      </div>
    )
  }

  if (profile.isError) {
    return (
      <Card className="mx-auto max-w-3xl">
        <ErrorState error={profile.error} onRetry={() => profile.refetch()} className="mx-auto py-12" />
      </Card>
    )
  }

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <PersonalDataCard fullName={profile.data.fullName} email={profile.data.email} />
      <ChangePasswordCard />
    </div>
  )
}

function PersonalDataCard({ fullName, email }: { fullName: string; email: string }) {
  const update = useUpdateProfile()
  const form = useForm<z.infer<typeof profileSchema>>({ resolver: zodResolver(profileSchema), defaultValues: { fullName } })
  const { errors, isDirty } = form.formState

  useEffect(() => form.reset({ fullName }), [fullName, form])

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await update.mutateAsync(values)
      toast.success("Perfil atualizado.")
    } catch (error) {
      toast.error(errorMessage(error))
    }
  })

  return (
    <Card>
      <form onSubmit={onSubmit} noValidate>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <UserRound className="size-4 text-muted-foreground" aria-hidden="true" />
            Dados pessoais
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-5">
          <FieldGroup>
            <Field data-invalid={!!errors.fullName}>
              <FieldLabel htmlFor="profile-name">Nome</FieldLabel>
              <Input id="profile-name" autoComplete="name" aria-invalid={!!errors.fullName} {...form.register("fullName")} />
              <FieldError errors={[errors.fullName]} />
            </Field>
            <Field>
              <FieldLabel htmlFor="profile-email">E-mail</FieldLabel>
              <Input id="profile-email" type="email" value={email} disabled readOnly />
              <FieldDescription>Usado para entrar no Datavello. A troca de e-mail estará disponível em uma versão futura.</FieldDescription>
            </Field>
          </FieldGroup>
        </CardContent>
        <CardFooter className="mt-5 justify-end border-t pt-4">
          <Button type="submit" disabled={!isDirty || update.isPending}>
            {update.isPending ? <Spinner className="size-4 text-primary-foreground" /> : <Save data-icon="inline-start" />}
            Salvar
          </Button>
        </CardFooter>
      </form>
    </Card>
  )
}

function ChangePasswordCard() {
  const change = useChangePassword()
  const form = useForm<z.infer<typeof passwordSchema>>({
    resolver: zodResolver(passwordSchema),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  })
  const { errors } = form.formState

  const onSubmit = form.handleSubmit(async ({ currentPassword, newPassword }) => {
    try {
      await change.mutateAsync({ currentPassword, newPassword })
      form.reset()
      toast.success("Senha alterada. Sessões abertas em outros dispositivos foram encerradas.")
    } catch (error) {
      if (error instanceof ApiError && error.message.includes("Senha atual incorreta")) {
        form.setError("currentPassword", { message: "Senha atual incorreta." })
      } else if (error instanceof ApiError && error.status === 400) {
        form.setError("newPassword", { message: error.message })
      } else {
        toast.error(errorMessage(error))
      }
    }
  })

  return (
    <Card>
      <form onSubmit={onSubmit} noValidate>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <KeyRound className="size-4 text-muted-foreground" aria-hidden="true" />
            Alterar senha
          </CardTitle>
          <CardDescription>Ao trocar a senha, você continua conectado aqui e as outras sessões são encerradas.</CardDescription>
        </CardHeader>
        <CardContent className="pt-5">
          <FieldGroup>
            <Field data-invalid={!!errors.currentPassword}>
              <FieldLabel htmlFor="current-password">Senha atual</FieldLabel>
              <Input id="current-password" type="password" autoComplete="current-password" aria-invalid={!!errors.currentPassword} {...form.register("currentPassword")} />
              <FieldError errors={[errors.currentPassword]} />
            </Field>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field data-invalid={!!errors.newPassword}>
                <FieldLabel htmlFor="new-password">Nova senha</FieldLabel>
                <Input id="new-password" type="password" autoComplete="new-password" aria-invalid={!!errors.newPassword} {...form.register("newPassword")} />
                {errors.newPassword ? <FieldError errors={[errors.newPassword]} /> : <FieldDescription>Mínimo de 8 caracteres, com um número.</FieldDescription>}
              </Field>
              <Field data-invalid={!!errors.confirmPassword}>
                <FieldLabel htmlFor="confirm-password">Confirme a nova senha</FieldLabel>
                <Input id="confirm-password" type="password" autoComplete="new-password" aria-invalid={!!errors.confirmPassword} {...form.register("confirmPassword")} />
                <FieldError errors={[errors.confirmPassword]} />
              </Field>
            </div>
          </FieldGroup>
        </CardContent>
        <CardFooter className="mt-5 justify-end border-t pt-4">
          <Button type="submit" disabled={change.isPending}>
            {change.isPending ? <Spinner className="size-4 text-primary-foreground" /> : <KeyRound data-icon="inline-start" />}
            Alterar senha
          </Button>
        </CardFooter>
      </form>
    </Card>
  )
}

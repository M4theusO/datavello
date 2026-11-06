import { useEffect } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { AlertCircle, ArrowLeft, KeyRound, MailCheck, Send } from "lucide-react"
import { useForm } from "react-hook-form"
import { Link, useNavigate, useSearchParams } from "react-router"
import { toast } from "sonner"
import { z } from "zod"
import { Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button, buttonVariants } from "@/components/ui/button"
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { ApiError, errorMessage } from "@/lib/api"
import { useForgotPassword, useResetPassword } from "./auth"
import { AuthLayout } from "./AuthLayout"

const backToLogin = (
  <Link to="/login" className="inline-flex items-center gap-1.5 text-sm font-medium text-primary underline-offset-4 hover:underline">
    <ArrowLeft className="size-4" aria-hidden="true" />
    Voltar para o login
  </Link>
)

const forgotSchema = z.object({
  email: z.string().trim().min(1, "Informe o e-mail.").max(256, "E-mail muito longo.").pipe(z.email("E-mail inválido.")),
})

/** /esqueci-senha — asks for the e-mail; the answer is the same whether or not it has an account. */
export function ForgotPasswordPage() {
  const forgot = useForgotPassword()
  const form = useForm<z.infer<typeof forgotSchema>>({ resolver: zodResolver(forgotSchema), defaultValues: { email: "" } })
  const { errors } = form.formState

  useEffect(() => {
    document.title = "Esqueci minha senha · Datavello"
  }, [])

  const onSubmit = form.handleSubmit(async ({ email }) => {
    try {
      await forgot.mutateAsync({ email })
    } catch {
      // Shown in the alert below.
    }
  })

  if (forgot.isSuccess) {
    return (
      <AuthLayout title="Confira seu e-mail" description={`Se houver uma conta para ${forgot.variables.email}, enviamos um link para criar uma nova senha.`}>
        <div className="space-y-6">
          <Alert>
            <MailCheck />
            <AlertDescription>
              O link vale por 1 hora e só pode ser usado uma vez. Não chegou? Confira a caixa de spam ou peça novamente em alguns minutos.
            </AlertDescription>
          </Alert>
          <Button variant="outline" className="w-full" onClick={() => forgot.reset()}>
            Usar outro e-mail
          </Button>
          <p className="text-center">{backToLogin}</p>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title="Esqueci minha senha" description="Informe o e-mail da sua conta e enviaremos um link para você criar uma nova senha.">
      <form onSubmit={onSubmit} noValidate className="space-y-6">
        {forgot.isError && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{errorMessage(forgot.error)}</AlertDescription>
          </Alert>
        )}
        <Field data-invalid={!!errors.email}>
          <FieldLabel htmlFor="email">E-mail</FieldLabel>
          <Input id="email" type="email" autoComplete="email" placeholder="voce@empresa.com.br" className="h-10" aria-invalid={!!errors.email} {...form.register("email")} />
          <FieldError errors={[errors.email]} />
        </Field>
        <Button type="submit" size="lg" className="h-10 w-full" disabled={forgot.isPending}>
          {forgot.isPending ? <Spinner className="text-primary-foreground" /> : <Send data-icon="inline-start" />}
          Enviar link
        </Button>
        <p className="text-center">{backToLogin}</p>
      </form>
    </AuthLayout>
  )
}

const resetSchema = z
  .object({
    newPassword: z
      .string()
      .min(8, "A senha deve ter pelo menos 8 caracteres.")
      .max(128, "A senha deve ter no máximo 128 caracteres.")
      .regex(/\d/, "A senha deve conter pelo menos um número."),
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, { path: ["confirmPassword"], message: "As senhas não coincidem." })

/** /redefinir-senha?uid=…&token=… — the link from the e-mail. */
export function ResetPasswordPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const reset = useResetPassword()
  const userId = params.get("uid")
  const token = params.get("token")
  const form = useForm<z.infer<typeof resetSchema>>({ resolver: zodResolver(resetSchema), defaultValues: { newPassword: "", confirmPassword: "" } })
  const { errors } = form.formState

  useEffect(() => {
    document.title = "Nova senha · Datavello"
  }, [])

  const invalidLink = !userId || !token || (reset.error instanceof ApiError && reset.error.message.includes("inválido ou expirou"))

  if (invalidLink) {
    return (
      <AuthLayout title="Link inválido ou expirado" description="Os links de redefinição valem por 1 hora e só podem ser usados uma vez. Peça um novo para continuar.">
        <div className="space-y-6">
          <Link to="/esqueci-senha" className={buttonVariants({ size: "lg", className: "h-10 w-full" })}>
            Pedir novo link
          </Link>
          <p className="text-center">{backToLogin}</p>
        </div>
      </AuthLayout>
    )
  }

  const onSubmit = form.handleSubmit(async ({ newPassword }) => {
    try {
      await reset.mutateAsync({ userId, token, newPassword })
      toast.success("Senha alterada. Entre com a nova senha.")
      navigate("/login", { replace: true })
    } catch (error) {
      const [message] = error instanceof ApiError ? error.fieldErrors("newPassword") : []
      if (message) form.setError("newPassword", { message })
    }
  })

  const generalError = reset.isError && !(reset.error instanceof ApiError && reset.error.fieldErrors("newPassword").length > 0)

  return (
    <AuthLayout title="Criar nova senha" description="Escolha uma nova senha para sua conta. Por segurança, sessões abertas em outros dispositivos serão encerradas.">
      <form onSubmit={onSubmit} noValidate className="space-y-6">
        {generalError && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{errorMessage(reset.error)}</AlertDescription>
          </Alert>
        )}
        <FieldGroup>
          <Field data-invalid={!!errors.newPassword}>
            <FieldLabel htmlFor="newPassword">Nova senha</FieldLabel>
            <Input id="newPassword" type="password" autoComplete="new-password" className="h-10" aria-invalid={!!errors.newPassword} {...form.register("newPassword")} />
            {errors.newPassword ? <FieldError errors={[errors.newPassword]} /> : <FieldDescription>Mínimo de 8 caracteres, com pelo menos um número.</FieldDescription>}
          </Field>
          <Field data-invalid={!!errors.confirmPassword}>
            <FieldLabel htmlFor="confirmPassword">Confirme a nova senha</FieldLabel>
            <Input id="confirmPassword" type="password" autoComplete="new-password" className="h-10" aria-invalid={!!errors.confirmPassword} {...form.register("confirmPassword")} />
            <FieldError errors={[errors.confirmPassword]} />
          </Field>
        </FieldGroup>
        <Button type="submit" size="lg" className="h-10 w-full" disabled={reset.isPending}>
          {reset.isPending ? <Spinner className="text-primary-foreground" /> : <KeyRound data-icon="inline-start" />}
          Salvar nova senha
        </Button>
        <p className="text-center">{backToLogin}</p>
      </form>
    </AuthLayout>
  )
}

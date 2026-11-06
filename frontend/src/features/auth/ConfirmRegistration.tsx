import { useEffect, useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { AlertCircle, CheckCircle2, MailCheck, RotateCw } from "lucide-react"
import { useForm } from "react-hook-form"
import { Link, useNavigate, useSearchParams } from "react-router"
import { toast } from "sonner"
import { z } from "zod"
import { Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button, buttonVariants } from "@/components/ui/button"
import { Field, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { ApiError, errorMessage } from "@/lib/api"
import { useConfirmEmail, useResendConfirmation } from "./auth"
import { AuthLayout } from "./AuthLayout"

/** Resends the confirmation e-mail for a known address (after registration or a refused login). */
export function ResendConfirmationButton({ email, variant = "outline" }: { email: string; variant?: "outline" | "default" }) {
  const resend = useResendConfirmation()
  const [sent, setSent] = useState(false)

  async function handleClick() {
    try {
      await resend.mutateAsync({ email })
      setSent(true)
      toast.success("Enviamos um novo link de confirmação.")
    } catch (error) {
      toast.error(errorMessage(error))
    }
  }

  return (
    <Button type="button" variant={variant} className="w-full" onClick={handleClick} disabled={resend.isPending || sent}>
      {resend.isPending ? <Spinner className="size-4" /> : sent ? <CheckCircle2 data-icon="inline-start" /> : <RotateCw data-icon="inline-start" />}
      {sent ? "Novo link enviado" : "Reenviar e-mail de confirmação"}
    </Button>
  )
}

/** Shown right after registration: the account exists but needs the e-mail confirmed to sign in. */
export function CheckYourEmail({ email, companyName }: { email: string; companyName?: string }) {
  return (
    <AuthLayout
      title="Confirme seu e-mail"
      description={`Enviamos um link de confirmação para ${email}. Abra o e-mail e confirme para entrar${companyName ? ` em ${companyName}` : ""}.`}
    >
      <div className="space-y-6">
        <Alert>
          <MailCheck />
          <AlertDescription>O link vale por 1 hora. Não chegou? Confira a caixa de spam ou peça um novo link abaixo.</AlertDescription>
        </Alert>
        <ResendConfirmationButton email={email} />
        <p className="text-center text-sm text-muted-foreground">
          Já confirmou?{" "}
          <Link to="/login" className="font-medium text-primary underline-offset-4 hover:underline">
            Entrar
          </Link>
        </p>
      </div>
    </AuthLayout>
  )
}

const resendSchema = z.object({
  email: z.string().trim().min(1, "Informe o e-mail.").max(256, "E-mail muito longo.").pipe(z.email("E-mail inválido.")),
})

/**
 * /confirmar-cadastro?uid=…&token=… — the link from the registration e-mail.
 * Confirms on an explicit click (never on load: e-mail scanners open links automatically) and signs in.
 */
export function ConfirmRegistrationPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const confirm = useConfirmEmail()
  const resend = useResendConfirmation()
  const userId = params.get("uid")
  const token = params.get("token")
  const form = useForm<z.infer<typeof resendSchema>>({ resolver: zodResolver(resendSchema), defaultValues: { email: "" } })

  useEffect(() => {
    document.title = "Confirmar e-mail · Datavello"
  }, [])

  async function handleConfirm() {
    if (!userId || !token) return
    try {
      const me = await confirm.mutateAsync({ userId, token })
      toast.success(`E-mail confirmado. Bem-vindo(a), ${me.user.fullName}!`)
      navigate("/", { replace: true })
    } catch {
      // Handled by the states below.
    }
  }

  const alreadyConfirmed = confirm.error instanceof ApiError && confirm.error.status === 409
  const invalidLink = !userId || !token || (confirm.error instanceof ApiError && confirm.error.status === 400)

  if (alreadyConfirmed) {
    return (
      <AuthLayout title="E-mail já confirmado" description="Sua conta já está ativa. Entre com seu e-mail e senha.">
        <Link to="/login" className={buttonVariants({ size: "lg", className: "h-10 w-full" })}>
          Entrar
        </Link>
      </AuthLayout>
    )
  }

  if (invalidLink) {
    const onResend = form.handleSubmit(async ({ email }) => {
      try {
        await resend.mutateAsync({ email })
      } catch {
        // Shown below.
      }
    })

    return (
      <AuthLayout
        title="Link inválido ou expirado"
        description="Os links de confirmação valem por 1 hora. Informe seu e-mail para receber um novo link."
      >
        {resend.isSuccess ? (
          <div className="space-y-6">
            <Alert>
              <MailCheck />
              <AlertDescription>Se houver uma conta aguardando confirmação para {resend.variables.email}, enviamos um novo link.</AlertDescription>
            </Alert>
            <Link to="/login" className={buttonVariants({ variant: "outline", className: "w-full" })}>
              Voltar para o login
            </Link>
          </div>
        ) : (
          <form onSubmit={onResend} noValidate className="space-y-6">
            {resend.isError && (
              <Alert variant="destructive">
                <AlertCircle />
                <AlertDescription>{errorMessage(resend.error)}</AlertDescription>
              </Alert>
            )}
            <Field data-invalid={!!form.formState.errors.email}>
              <FieldLabel htmlFor="email">E-mail</FieldLabel>
              <Input id="email" type="email" autoComplete="email" className="h-10" aria-invalid={!!form.formState.errors.email} {...form.register("email")} />
              <FieldError errors={[form.formState.errors.email]} />
            </Field>
            <Button type="submit" size="lg" className="h-10 w-full" disabled={resend.isPending}>
              {resend.isPending ? <Spinner className="text-primary-foreground" /> : <RotateCw data-icon="inline-start" />}
              Enviar novo link
            </Button>
          </form>
        )}
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title="Confirmar e-mail" description="Falta só um passo: confirme seu e-mail para ativar a conta e entrar no Datavello.">
      <div className="space-y-6">
        {confirm.isError && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{errorMessage(confirm.error)}</AlertDescription>
          </Alert>
        )}
        <Button size="lg" className="h-10 w-full" onClick={handleConfirm} disabled={confirm.isPending}>
          {confirm.isPending ? <Spinner className="text-primary-foreground" /> : <MailCheck data-icon="inline-start" />}
          Confirmar e entrar
        </Button>
      </div>
    </AuthLayout>
  )
}

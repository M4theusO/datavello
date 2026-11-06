import { useEffect } from "react"
import { AlertCircle, CheckCircle2, MailCheck } from "lucide-react"
import { Link, useSearchParams } from "react-router"
import { Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button, buttonVariants } from "@/components/ui/button"
import { ApiError, errorMessage } from "@/lib/api"
import { useConfirmEmailChange } from "./auth"
import { AuthLayout } from "./AuthLayout"

/**
 * /confirmar-email?uid=…&email=…&token=… — the link sent to the new address.
 * The change happens on an explicit click, never on page load: e-mail security scanners
 * open links automatically and must not trigger it.
 */
export function ConfirmEmailPage() {
  const [params] = useSearchParams()
  const confirm = useConfirmEmailChange()
  const userId = params.get("uid")
  const newEmail = params.get("email")
  const token = params.get("token")

  useEffect(() => {
    document.title = "Confirmar e-mail · Datavello"
  }, [])

  const invalidLink = !userId || !newEmail || !token || (confirm.error instanceof ApiError && confirm.error.message.includes("inválido ou expirou"))

  if (invalidLink) {
    return (
      <AuthLayout
        title="Link inválido ou expirado"
        description="Os links de confirmação valem por 1 hora e só podem ser usados uma vez. Peça a troca de e-mail novamente na página do seu perfil."
      >
        <Link to="/perfil" className={buttonVariants({ variant: "outline", size: "lg", className: "h-10 w-full" })}>
          Ir para o perfil
        </Link>
      </AuthLayout>
    )
  }

  if (confirm.isSuccess) {
    return (
      <AuthLayout title="E-mail alterado" description={`A partir de agora, entre no Datavello com ${confirm.data.email}.`}>
        <div className="space-y-6">
          <Alert>
            <CheckCircle2 />
            <AlertDescription>Por segurança, todas as sessões foram encerradas. Avisamos também o e-mail anterior sobre a troca.</AlertDescription>
          </Alert>
          <Link to="/login" className={buttonVariants({ size: "lg", className: "h-10 w-full" })}>
            Entrar com o novo e-mail
          </Link>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title="Confirmar novo e-mail" description={`Confirme para usar ${newEmail} como e-mail de acesso da sua conta.`}>
      <div className="space-y-6">
        {confirm.isError && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{errorMessage(confirm.error)}</AlertDescription>
          </Alert>
        )}
        <Button size="lg" className="h-10 w-full" disabled={confirm.isPending} onClick={() => confirm.mutate({ userId, newEmail, token })}>
          {confirm.isPending ? <Spinner className="text-primary-foreground" /> : <MailCheck data-icon="inline-start" />}
          Confirmar novo e-mail
        </Button>
        <p className="text-center text-sm text-muted-foreground">Depois da confirmação, você precisará entrar novamente.</p>
      </div>
    </AuthLayout>
  )
}

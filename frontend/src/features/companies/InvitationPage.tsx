import { useEffect } from "react"
import { AlertCircle, Building2, Check, LogIn, LogOut, UserPlus } from "lucide-react"
import { Link, useNavigate, useParams } from "react-router"
import { toast } from "sonner"
import { FullPageLoader, Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button, buttonVariants } from "@/components/ui/button"
import { AuthLayout } from "@/features/auth/AuthLayout"
import { useAcceptInvitation, useInvitationPreview, useLogout, useMe } from "@/features/auth/auth"
import { ApiError, errorMessage } from "@/lib/api"

const shortDate = new Intl.DateTimeFormat("pt-BR")

/** /convite/:token — public page where the invitee reviews and accepts an invitation. */
export function InvitationPage() {
  const { token = "" } = useParams()
  const me = useMe()
  const preview = useInvitationPreview(token)
  const accept = useAcceptInvitation()
  const logout = useLogout()
  const navigate = useNavigate()

  useEffect(() => {
    document.title = "Convite · Datavello"
  }, [])

  if (me.isPending || preview.isPending) return <FullPageLoader />

  if (preview.isError) {
    const unavailable = preview.error instanceof ApiError && [404, 410].includes(preview.error.status)
    return (
      <AuthLayout title={unavailable ? "Convite indisponível" : "Não foi possível abrir o convite"} description={errorMessage(preview.error)}>
        <Link to={me.data ? "/" : "/login"} className={buttonVariants({ variant: "outline", className: "w-full" })}>
          {me.data ? "Ir para o Datavello" : "Ir para o login"}
        </Link>
      </AuthLayout>
    )
  }

  const invitation = preview.data
  const user = me.data?.user
  const sameEmail = !!user && user.email.toLowerCase() === invitation.email.toLowerCase()
  const here = `/convite/${encodeURIComponent(token)}`

  async function handleAccept() {
    try {
      await accept.mutateAsync(token)
      toast.success(`Você agora faz parte de ${invitation.companyName}.`)
      navigate("/", { replace: true })
    } catch {
      // Shown in the alert below.
    }
  }

  return (
    <AuthLayout
      title={`Convite para ${invitation.companyName}`}
      description={`${invitation.invitedBy} convidou ${invitation.email} para acessar os dados desta empresa no Datavello.`}
    >
      <div className="space-y-6">
        <div className="flex items-center gap-3 rounded-lg border p-3">
          <div className="flex size-10 shrink-0 items-center justify-center rounded-md bg-brand-soft text-primary">
            <Building2 className="size-5" aria-hidden="true" />
          </div>
          <div className="min-w-0">
            <p className="truncate font-medium">{invitation.companyName}</p>
            <p className="text-xs text-muted-foreground">Válido até {shortDate.format(new Date(invitation.expiresAt))} · acesso como membro</p>
          </div>
        </div>

        {accept.isError && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{errorMessage(accept.error)}</AlertDescription>
          </Alert>
        )}

        {!user ? (
          <div className="space-y-3">
            <Link to={`/cadastro?convite=${encodeURIComponent(token)}`} className={buttonVariants({ size: "lg", className: "h-10 w-full" })}>
              <UserPlus data-icon="inline-start" />
              Criar conta e entrar
            </Link>
            <Link to={`/login?redirect=${encodeURIComponent(here)}`} className={buttonVariants({ variant: "outline", size: "lg", className: "h-10 w-full" })}>
              <LogIn data-icon="inline-start" />
              Já tenho conta
            </Link>
          </div>
        ) : sameEmail ? (
          <Button size="lg" className="h-10 w-full" onClick={handleAccept} disabled={accept.isPending}>
            {accept.isPending ? <Spinner className="text-primary-foreground" /> : <Check data-icon="inline-start" />}
            Aceitar convite
          </Button>
        ) : (
          <div className="space-y-3">
            <Alert>
              <AlertCircle />
              <AlertDescription>
                Você está conectado como <strong>{user.email}</strong>. Este convite é para <strong>{invitation.email}</strong>: saia e entre com essa conta (ou crie
                uma) para aceitá-lo.
              </AlertDescription>
            </Alert>
            <Button
              variant="outline"
              size="lg"
              className="h-10 w-full"
              disabled={logout.isPending}
              onClick={() => logout.mutateAsync().catch(() => undefined)}
            >
              <LogOut data-icon="inline-start" />
              Sair e usar outra conta
            </Button>
          </div>
        )}
      </div>
    </AuthLayout>
  )
}

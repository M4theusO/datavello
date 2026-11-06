import { useEffect } from "react"
import { LogOut } from "lucide-react"
import { useNavigate } from "react-router"
import { Button } from "@/components/ui/button"
import { AuthLayout } from "@/features/auth/AuthLayout"
import { useLogout } from "@/features/auth/auth"
import { CreateCompanyForm } from "./CreateCompanyForm"

/** Signed-in user without any company (left it or was removed): create one or wait for an invitation. */
export function NoCompanyPage() {
  const logout = useLogout()
  const navigate = useNavigate()

  useEffect(() => {
    document.title = "Nenhuma empresa · Datavello"
  }, [])

  return (
    <AuthLayout
      title="Você não participa de nenhuma empresa"
      description="Crie uma empresa para começar ou abra o link de convite que recebeu de outra empresa."
    >
      <div className="space-y-6">
        <CreateCompanyForm onCreated={() => navigate("/", { replace: true })} />
        <Button
          variant="ghost"
          className="w-full"
          disabled={logout.isPending}
          onClick={async () => {
            await logout.mutateAsync().catch(() => undefined)
            navigate("/login", { replace: true })
          }}
        >
          <LogOut data-icon="inline-start" />
          Sair da conta
        </Button>
      </div>
    </AuthLayout>
  )
}

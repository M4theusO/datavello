import type { ReactNode } from "react"
import { FileQuestion, ShieldX } from "lucide-react"
import { Link } from "react-router"
import { buttonVariants } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"

function StatusCard({ icon, title, description, action }: { icon: ReactNode; title: string; description: string; action?: ReactNode }) {
  return (
    <div className="flex justify-center py-10">
      <Card className="w-full max-w-lg">
        <CardContent className="flex flex-col items-center gap-4 py-8 text-center">
          <div className="flex size-12 items-center justify-center rounded-full bg-brand-soft text-primary [&_svg]:size-6">{icon}</div>
          <div className="space-y-1.5">
            <h2 className="text-lg font-semibold">{title}</h2>
            <p className="text-sm text-muted-foreground">{description}</p>
          </div>
          {action}
        </CardContent>
      </Card>
    </div>
  )
}

const backHome = (
  <Link to="/" className={buttonVariants({ variant: "outline" })}>
    Voltar ao dashboard
  </Link>
)

export function NotFoundPage() {
  return (
    <StatusCard
      icon={<FileQuestion />}
      title="Página não encontrada"
      description="O endereço acessado não existe ou foi movido."
      action={backHome}
    />
  )
}

export function AccessDeniedPage() {
  return (
    <StatusCard
      icon={<ShieldX />}
      title="Acesso negado"
      description="Você não tem permissão para acessar este recurso ou ele pertence a outra empresa."
      action={backHome}
    />
  )
}

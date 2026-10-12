import type { LucideIcon } from "lucide-react"
import {
  ArrowLeftRight,
  FileUp,
  LayoutDashboard,
  SlidersHorizontal,
  Settings,
  Tags,
  UserRound,
} from "lucide-react"

export interface NavItem {
  label: string
  to: string
  icon: LucideIcon
}

export interface NavGroup {
  label: string
  items: NavItem[]
}

export const navigation: NavGroup[] = [
  {
    label: "Análise",
    items: [
      { label: "Dashboard", to: "/", icon: LayoutDashboard },
      { label: "Personalizar dashboard", to: "/personalizar", icon: SlidersHorizontal },
    ],
  },
  {
    label: "Dados",
    items: [
      { label: "Transações", to: "/transacoes", icon: ArrowLeftRight },
      { label: "Categorias", to: "/categorias", icon: Tags },
      { label: "Importar dados", to: "/importar", icon: FileUp },
    ],
  },
  {
    label: "Conta",
    items: [
      { label: "Configurações", to: "/configuracoes", icon: Settings },
      { label: "Perfil do usuário", to: "/perfil", icon: UserRound },
    ],
  },
]

/** Per-route metadata, read by the layout through React Router's `handle`. */
export interface RouteHandle {
  title: string
}

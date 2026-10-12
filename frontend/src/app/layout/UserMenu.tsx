import { LogOut, Settings, UserRound } from "lucide-react"
import { useNavigate } from "react-router"
import { toast } from "sonner"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { useLogout, useMe } from "@/features/auth/auth"
import { initials } from "@/lib/format"

export function UserMenu() {
  const me = useMe()
  const logout = useLogout()
  const navigate = useNavigate()
  const user = me.data?.user

  if (!user) return null

  async function handleLogout() {
    try {
      await logout.mutateAsync()
    } catch {
      // Local session state is cleared either way (see useLogout); nothing else to do.
    }
    toast.success("Você saiu da sua conta.")
    navigate("/login", { replace: true })
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        className="flex items-center gap-2 rounded-full p-0.5 outline-none focus-visible:ring-2 focus-visible:ring-ring sm:rounded-lg sm:py-1 sm:pr-2 sm:pl-1 sm:hover:bg-muted"
        aria-label="Menu do usuário"
      >
        <Avatar className="size-8">
          <AvatarFallback className="bg-primary text-xs font-medium text-primary-foreground">{initials(user.fullName)}</AvatarFallback>
        </Avatar>
        <span className="hidden max-w-40 truncate text-sm font-medium sm:block">{user.fullName}</span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-60">
        <DropdownMenuGroup>
          <DropdownMenuLabel className="font-normal">
            <p className="truncate text-sm font-medium text-foreground">{user.fullName}</p>
            <p className="truncate text-xs text-muted-foreground">{user.email}</p>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuGroup>
          <DropdownMenuItem onClick={() => navigate("/perfil")}>
            <UserRound />
            Perfil
          </DropdownMenuItem>
          <DropdownMenuItem onClick={() => navigate("/configuracoes")}>
            <Settings />
            Configurações
          </DropdownMenuItem>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem variant="destructive" onClick={handleLogout} disabled={logout.isPending}>
          <LogOut />
          Sair
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

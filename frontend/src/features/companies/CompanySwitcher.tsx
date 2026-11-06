import { useState } from "react"
import { Building2, Check, ChevronsUpDown, Plus } from "lucide-react"
import { useNavigate } from "react-router"
import { toast } from "sonner"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { SidebarMenu, SidebarMenuButton, SidebarMenuItem, useSidebar } from "@/components/ui/sidebar"
import { type CompanySummary, useMe, useSwitchCompany } from "@/features/auth/auth"
import { CreateCompanyDialog } from "./CreateCompanyForm"

const roleLabel = (role: CompanySummary["role"]) => (role === "Owner" ? "Proprietário" : "Membro")

/** Sidebar footer: shows the active company and switches between the user's companies. */
export function CompanySwitcher() {
  const me = useMe()
  const switchCompany = useSwitchCompany()
  const navigate = useNavigate()
  const { isMobile, setOpenMobile } = useSidebar()
  const [creating, setCreating] = useState(false)
  const active = me.data?.activeCompany
  const companies = me.data?.companies ?? []

  if (!active) return null

  async function choose(company: CompanySummary) {
    if (company.id === active?.id) return
    if (isMobile) setOpenMobile(false)
    navigate("/")
    await switchCompany(company.id)
    toast.success(`Agora você está em ${company.name}.`)
  }

  return (
    <>
      <SidebarMenu>
        <SidebarMenuItem>
          <DropdownMenu>
            <DropdownMenuTrigger
              render={
                <SidebarMenuButton
                  size="lg"
                  className="border bg-background/60 group-data-[collapsible=icon]:border-0 group-data-[collapsible=icon]:bg-transparent"
                  tooltip={active.name}
                  aria-label={`Empresa ativa: ${active.name}. Trocar de empresa`}
                />
              }
            >
              <div className="flex size-8 shrink-0 items-center justify-center rounded-md bg-brand-soft text-primary">
                <Building2 className="size-4" aria-hidden="true" />
              </div>
              <div className="grid min-w-0 flex-1 text-left leading-tight">
                <span className="truncate text-sm font-medium">{active.name}</span>
                <span className="text-xs text-muted-foreground">{roleLabel(active.role)}</span>
              </div>
              <ChevronsUpDown className="ml-auto size-4 text-muted-foreground" aria-hidden="true" />
            </DropdownMenuTrigger>
            <DropdownMenuContent side={isMobile ? "top" : "right"} align="end" className="w-64">
              <DropdownMenuGroup>
                <DropdownMenuLabel className="text-xs text-muted-foreground">Suas empresas</DropdownMenuLabel>
                {companies.map((company) => (
                  <DropdownMenuItem key={company.id} onClick={() => choose(company)}>
                    <Building2 />
                    <span className="min-w-0 flex-1">
                      <span className="block truncate">{company.name}</span>
                      <span className="block text-xs text-muted-foreground">{roleLabel(company.role)}</span>
                    </span>
                    {company.id === active.id && <Check className="text-primary" aria-label="Empresa ativa" />}
                  </DropdownMenuItem>
                ))}
              </DropdownMenuGroup>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={() => setCreating(true)}>
                <Plus />
                Nova empresa
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </SidebarMenuItem>
      </SidebarMenu>
      <CreateCompanyDialog
        open={creating}
        onOpenChange={(open) => {
          setCreating(open)
          if (!open && isMobile) setOpenMobile(false)
        }}
      />
    </>
  )
}

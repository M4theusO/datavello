import { Building2 } from "lucide-react"
import { NavLink, useLocation } from "react-router"
import { Logo, LogoMark } from "@/components/brand/Logo"
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
  useSidebar,
} from "@/components/ui/sidebar"
import { navigation } from "@/app/navigation"
import { useMe } from "@/features/auth/auth"

function isActive(pathname: string, to: string) {
  return to === "/" ? pathname === "/" : pathname === to || pathname.startsWith(`${to}/`)
}

export function AppSidebar() {
  const { pathname } = useLocation()
  const { isMobile, setOpenMobile } = useSidebar()
  const me = useMe()
  const company = me.data?.activeCompany

  return (
    <Sidebar collapsible="icon">
      <SidebarHeader className="h-16 justify-center px-3 group-data-[collapsible=icon]:px-2">
        <NavLink to="/" aria-label="Datavello — início" className="flex items-center rounded-md outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring">
          <Logo className="group-data-[collapsible=icon]:hidden" />
          <LogoMark className="hidden group-data-[collapsible=icon]:block" />
        </NavLink>
      </SidebarHeader>

      <SidebarContent>
        {navigation.map((group) => (
          <SidebarGroup key={group.label}>
            <SidebarGroupLabel>{group.label}</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                {group.items.map((item) => (
                  <SidebarMenuItem key={item.to}>
                    <SidebarMenuButton
                      isActive={isActive(pathname, item.to)}
                      tooltip={item.label}
                      render={<NavLink to={item.to} end={item.to === "/"} onClick={() => isMobile && setOpenMobile(false)} />}
                    >
                      <item.icon />
                      <span>{item.label}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                ))}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        ))}
      </SidebarContent>

      {company && (
        <SidebarFooter className="p-3 group-data-[collapsible=icon]:p-2">
          <div
            className="flex items-center gap-2.5 rounded-lg border bg-background/60 p-2 group-data-[collapsible=icon]:justify-center group-data-[collapsible=icon]:border-0 group-data-[collapsible=icon]:bg-transparent group-data-[collapsible=icon]:p-0"
            title={company.name}
          >
            <div className="flex size-8 shrink-0 items-center justify-center rounded-md bg-brand-soft text-primary">
              <Building2 className="size-4" aria-hidden="true" />
            </div>
            <div className="min-w-0 group-data-[collapsible=icon]:hidden">
              <p className="truncate text-sm font-medium">{company.name}</p>
              <p className="text-xs text-muted-foreground">{company.role === "Owner" ? "Proprietário" : "Membro"}</p>
            </div>
          </div>
        </SidebarFooter>
      )}

      <SidebarRail />
    </Sidebar>
  )
}

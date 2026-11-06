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
import { CompanySwitcher } from "@/features/companies/CompanySwitcher"

function isActive(pathname: string, to: string) {
  return to === "/" ? pathname === "/" : pathname === to || pathname.startsWith(`${to}/`)
}

export function AppSidebar() {
  const { pathname } = useLocation()
  const { isMobile, setOpenMobile } = useSidebar()

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

      <SidebarFooter className="p-3 group-data-[collapsible=icon]:p-2">
        <CompanySwitcher />
      </SidebarFooter>

      <SidebarRail />
    </Sidebar>
  )
}

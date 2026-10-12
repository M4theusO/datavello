import { useEffect } from "react"
import { Outlet, useMatches } from "react-router"
import { Separator } from "@/components/ui/separator"
import { SidebarInset, SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar"
import type { RouteHandle } from "@/app/navigation"
import { AppSidebar } from "./AppSidebar"
import { UserMenu } from "./UserMenu"

/** The sidebar component stores its expanded/collapsed state in this cookie; restore it on load. */
function sidebarStartsOpen(): boolean {
  return !document.cookie.split("; ").includes("sidebar_state=false")
}

function usePageTitle(): string {
  const matches = useMatches()
  const handle = [...matches].reverse().find((m) => (m.handle as RouteHandle | undefined)?.title)?.handle as RouteHandle | undefined
  return handle?.title ?? "Datavello"
}

export function AppLayout() {
  const title = usePageTitle()

  useEffect(() => {
    document.title = title === "Datavello" ? "Datavello" : `${title} · Datavello`
  }, [title])

  return (
    <SidebarProvider defaultOpen={sidebarStartsOpen()}>
      <AppSidebar />
      <SidebarInset className="min-w-0">
        <header className="sticky top-0 z-20 flex h-16 shrink-0 items-center gap-3 border-b bg-background/85 px-4 backdrop-blur supports-[backdrop-filter]:bg-background/70 md:px-6">
          <SidebarTrigger className="-ml-1" aria-label="Expandir ou recolher menu" />
          <Separator orientation="vertical" className="h-5" />
          <h1 className="min-w-0 flex-1 truncate text-base font-semibold tracking-tight md:text-lg">{title}</h1>
          <UserMenu />
        </header>
        <main className="flex-1 px-4 py-6 md:px-6 lg:px-8">
          <Outlet />
        </main>
      </SidebarInset>
    </SidebarProvider>
  )
}

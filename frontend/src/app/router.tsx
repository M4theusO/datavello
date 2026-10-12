import { createBrowserRouter, Outlet } from "react-router"
import { FullPageLoader } from "@/components/feedback"
import { AppLayout } from "@/app/layout/AppLayout"
import type { RouteHandle } from "@/app/navigation"
import { AccessDeniedPage, ComingSoonPage, NotFoundPage } from "@/app/pages/StatusPages"
import { GuestOnly, RequireAuth } from "@/features/auth/guards"
import { LoginPage } from "@/features/auth/LoginPage"
import { RegisterPage } from "@/features/auth/RegisterPage"

const handle = (title: string): RouteHandle => ({ title })

export const router = createBrowserRouter([
  {
    // Shown while a lazily loaded page is fetched on the first load.
    element: <Outlet />,
    hydrateFallbackElement: <FullPageLoader />,
    children: [
      {
        element: <GuestOnly />,
        children: [
          { path: "/login", element: <LoginPage /> },
          { path: "/cadastro", element: <RegisterPage /> },
        ],
      },
      {
        element: <RequireAuth />,
        children: [
          {
            element: <AppLayout />,
            children: [
              {
                index: true,
                handle: handle("Dashboard"),
                lazy: async () => ({ Component: (await import("@/features/dashboard/DashboardPage")).DashboardPage }),
              },
              {
                path: "transacoes",
                handle: handle("Transações"),
                lazy: async () => ({ Component: (await import("@/features/transactions/TransactionsPage")).TransactionsPage }),
              },
              {
                path: "categorias",
                handle: handle("Categorias"),
                lazy: async () => ({ Component: (await import("@/features/categories/CategoriesPage")).CategoriesPage }),
              },
              {
                path: "importar",
                handle: handle("Importar dados"),
                element: <ComingSoonPage stage="Etapa 4" description="Importação de planilhas Excel (.xlsx) e CSV, com histórico." />,
              },
              {
                path: "personalizar",
                handle: handle("Personalizar dashboard"),
                element: <ComingSoonPage stage="Etapa 4" description="Escolha, ordene e ative os widgets do seu dashboard." />,
              },
              {
                path: "configuracoes",
                handle: handle("Configurações"),
                element: <ComingSoonPage stage="Etapa 5" description="Dados e preferências da empresa." />,
              },
              {
                path: "perfil",
                handle: handle("Perfil do usuário"),
                element: <ComingSoonPage stage="Etapa 5" description="Seus dados pessoais e troca de senha." />,
              },
              { path: "acesso-negado", handle: handle("Acesso negado"), element: <AccessDeniedPage /> },
              { path: "*", handle: handle("Página não encontrada"), element: <NotFoundPage /> },
            ],
          },
        ],
      },
    ],
  },
])

import { MutationCache, QueryCache, QueryClient } from "@tanstack/react-query"
import { toast } from "sonner"
import { ApiError } from "@/lib/api"
import { setActiveCompanyId } from "@/lib/activeCompany"
import { meQueryKey } from "@/features/auth/auth"

function handleSessionErrors(error: unknown) {
  if (!(error instanceof ApiError)) return

  // A 401 on any call means the session ended: clear it so the route guard sends the user to /login.
  if (error.status === 401) {
    queryClient.setQueryData(meQueryKey, null)
    return
  }

  // The selected company is no longer accessible (e.g. the user was removed from it):
  // drop the choice, reload the session (server default company) and everything on screen.
  if (error.isCompanyAccessDenied) {
    setActiveCompanyId(null)
    toast.info("Você não tem mais acesso a essa empresa.", { id: "company-access-denied" })
    void queryClient.resetQueries()
  }
}

export const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: handleSessionErrors }),
  mutationCache: new MutationCache({ onError: handleSessionErrors }),
  defaultOptions: {
    queries: {
      staleTime: 30 * 1000,
      refetchOnWindowFocus: false,
      // Do not retry client errors (4xx); retry transient failures once.
      retry: (failureCount, error) => !(error instanceof ApiError && error.status >= 400 && error.status < 500) && failureCount < 1,
    },
  },
})

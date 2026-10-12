import { MutationCache, QueryCache, QueryClient } from "@tanstack/react-query"
import { ApiError } from "@/lib/api"
import { meQueryKey } from "@/features/auth/auth"

/** A 401 on any call means the session ended: clear it so the route guard sends the user to /login. */
function handleSessionExpiry(error: unknown) {
  if (error instanceof ApiError && error.status === 401) {
    queryClient.setQueryData(meQueryKey, null)
  }
}

export const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: handleSessionExpiry }),
  mutationCache: new MutationCache({ onError: handleSessionExpiry }),
  defaultOptions: {
    queries: {
      staleTime: 30 * 1000,
      refetchOnWindowFocus: false,
      // Do not retry client errors (4xx); retry transient failures once.
      retry: (failureCount, error) => !(error instanceof ApiError && error.status >= 400 && error.status < 500) && failureCount < 1,
    },
  },
})

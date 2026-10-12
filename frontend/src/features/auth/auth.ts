import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { api, ApiError } from "@/lib/api"

export type CompanyRole = "Owner" | "Member"

export interface CompanySummary {
  id: string
  name: string
  role: CompanyRole
}

export interface Me {
  user: { id: string; fullName: string; email: string }
  activeCompany: CompanySummary | null
  companies: CompanySummary[]
}

export interface LoginInput {
  email: string
  password: string
  rememberMe: boolean
}

export interface RegisterInput {
  fullName: string
  email: string
  password: string
  companyName: string
}

export const meQueryKey = ["auth", "me"] as const

/** Current session. Resolves to null (not an error) when the user is not signed in. */
export function useMe() {
  return useQuery({
    queryKey: meQueryKey,
    queryFn: async ({ signal }) => {
      try {
        return await api.get<Me>("/auth/me", signal)
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null
        throw error
      }
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  })
}

export function useLogin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: LoginInput) => api.post<Me>("/auth/login", input),
    onSuccess: (me) => queryClient.setQueryData(meQueryKey, me),
  })
}

export function useRegister() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: RegisterInput) => api.post<Me>("/auth/register", input),
    onSuccess: (me) => queryClient.setQueryData(meQueryKey, me),
  })
}

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<void>("/auth/logout"),
    onSettled: () => {
      // Drop every cached query so no data from this session survives the logout.
      queryClient.clear()
      queryClient.setQueryData(meQueryKey, null)
    },
  })
}

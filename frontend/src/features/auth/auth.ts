import { useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query"
import { api, ApiError } from "@/lib/api"
import { chooseCompany, rememberCompany, setActiveCompanyId } from "@/lib/activeCompany"

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
  /** Required unless joining through an invitation. */
  companyName?: string
  inviteToken?: string
}

export const meQueryKey = ["auth", "me"] as const

/** Applies this tab's company choice to a /auth/me response and starts sending it to the API. */
export function resolveMe(me: Me): Me {
  const active = chooseCompany(me.user.id, me.companies, me.activeCompany)
  setActiveCompanyId(active?.id ?? null)
  return { ...me, activeCompany: active }
}

/** Makes `companyId` the active company of this tab (and the remembered default) and returns the updated session. */
export function selectCompany(me: Me, companyId: string): Me {
  setActiveCompanyId(companyId)
  rememberCompany(me.user.id, companyId)
  return resolveMe(me)
}

/**
 * Drops every cached query except the session, then refetches what is on screen,
 * so nothing from the previous company is shown after a switch.
 */
export async function resetCompanyData(queryClient: QueryClient) {
  const notSession = { predicate: (q: { queryKey: readonly unknown[] }) => q.queryKey[0] !== meQueryKey[0] }
  await queryClient.cancelQueries(notSession)
  queryClient.removeQueries({ ...notSession, type: "inactive" })
  await queryClient.resetQueries(notSession)
}

/** Current session. Resolves to null (not an error) when the user is not signed in. */
export function useMe() {
  return useQuery({
    queryKey: meQueryKey,
    queryFn: async ({ signal }) => {
      try {
        return resolveMe(await api.get<Me>("/auth/me", signal))
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null
        if (error instanceof ApiError && error.isCompanyAccessDenied) {
          // The selected company is no longer accessible: fall back to the server default.
          setActiveCompanyId(null)
          return resolveMe(await api.get<Me>("/auth/me", signal))
        }
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
    onSuccess: (me) => queryClient.setQueryData(meQueryKey, resolveMe(me)),
  })
}

export function useRegister() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: RegisterInput) => api.post<Me>("/auth/register", input),
    onSuccess: (me) => queryClient.setQueryData(meQueryKey, me.activeCompany ? selectCompany(me, me.activeCompany.id) : resolveMe(me)),
  })
}

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<void>("/auth/logout"),
    onSettled: () => {
      // Drop every cached query so no data from this session survives the logout.
      setActiveCompanyId(null)
      queryClient.clear()
      queryClient.setQueryData(meQueryKey, null)
    },
  })
}

/** Switches the active company of this tab. */
export function useSwitchCompany() {
  const queryClient = useQueryClient()
  return async (companyId: string) => {
    const me = queryClient.getQueryData<Me | null>(meQueryKey)
    if (!me || me.activeCompany?.id === companyId) return
    queryClient.setQueryData(meQueryKey, selectCompany(me, companyId))
    await resetCompanyData(queryClient)
  }
}

/** Creates another company owned by the user and switches to it. */
export function useCreateCompany() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { name: string }) => api.post<Me>("/companies", input),
    onSuccess: async (me) => {
      queryClient.setQueryData(meQueryKey, selectCompany(me, me.activeCompany!.id))
      await resetCompanyData(queryClient)
    },
  })
}

export interface InvitationPreview {
  companyName: string
  email: string
  invitedBy: string
  expiresAt: string
}

/** Public data of an invitation link. 404 = unknown/cancelled, 410 = used or expired. */
export function useInvitationPreview(token: string | null) {
  return useQuery({
    queryKey: ["invitation-preview", token],
    queryFn: () => api.post<InvitationPreview>("/invitations/preview", { token }),
    enabled: !!token,
    retry: false,
    staleTime: Infinity,
  })
}

/** Accepts an invitation with the signed-in account and switches to the company. */
export function useAcceptInvitation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (token: string) => api.post<Me>("/invitations/accept", { token }),
    onSuccess: async (me) => {
      queryClient.setQueryData(meQueryKey, selectCompany(me, me.activeCompany!.id))
      await resetCompanyData(queryClient)
    },
  })
}

/** Always succeeds (204) for a valid e-mail, whether or not it has an account. */
export function useForgotPassword() {
  return useMutation({ mutationFn: (input: { email: string }) => api.post<void>("/auth/forgot-password", input) })
}

/** Sets a new password from the e-mailed link. The user then signs in again. */
export function useResetPassword() {
  return useMutation({
    mutationFn: (input: { userId: string; token: string; newPassword: string }) => api.post<void>("/auth/reset-password", input),
  })
}

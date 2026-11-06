import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { api } from "@/lib/api"
import { type CompanyRole, meQueryKey, resetCompanyData } from "@/features/auth/auth"
import { setActiveCompanyId } from "@/lib/activeCompany"

export interface Company {
  id: string
  name: string
  document: string | null
  currency: string
  role: CompanyRole
  memberCount: number
  createdAt: string
}

export interface Profile {
  id: string
  fullName: string
  email: string
  createdAt: string
}

export function useCompany() {
  return useQuery({ queryKey: ["company"], queryFn: ({ signal }) => api.get<Company>("/company", signal) })
}

export function useUpdateCompany() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { name: string; document: string | null }) => api.put<Company>("/company", input),
    onSuccess: (company) => {
      queryClient.setQueryData(["company"], company)
      // The company name is shown in the sidebar (from /auth/me).
      return queryClient.invalidateQueries({ queryKey: meQueryKey })
    },
  })
}

export function useProfile() {
  return useQuery({ queryKey: ["profile"], queryFn: ({ signal }) => api.get<Profile>("/profile", signal) })
}

export function useUpdateProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { fullName: string }) => api.put<Profile>("/profile", input),
    onSuccess: (profile) => {
      queryClient.setQueryData(["profile"], profile)
      return queryClient.invalidateQueries({ queryKey: meQueryKey })
    },
  })
}

export function useChangePassword() {
  return useMutation({
    mutationFn: (input: { currentPassword: string; newPassword: string }) => api.post<void>("/profile/password", input),
  })
}

// ---- Team (members and invitations of the active company) ----

export interface Member {
  userId: string
  fullName: string
  email: string
  role: CompanyRole
  joinedAt: string
}

export interface Invitation {
  id: string
  email: string
  role: CompanyRole
  invitedBy: string
  createdAt: string
  expiresAt: string
  isExpired: boolean
}

export interface CreatedInvitation {
  invitation: Invitation
  /** Shown only once: the server stores just its hash. */
  token: string
}

const membersKey = ["company", "members"] as const
const invitationsKey = ["company", "invitations"] as const

/** Link the invitee opens; built from the token returned at creation. */
export function invitationLink(token: string): string {
  return `${window.location.origin}/convite/${encodeURIComponent(token)}`
}

export function useMembers() {
  return useQuery({ queryKey: membersKey, queryFn: ({ signal }) => api.get<Member[]>("/company/members", signal) })
}

export function useInvitations(enabled: boolean) {
  return useQuery({ queryKey: invitationsKey, queryFn: ({ signal }) => api.get<Invitation[]>("/company/invitations", signal), enabled })
}

export function useCreateInvitation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { email: string }) => api.post<CreatedInvitation>("/company/invitations", input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: invitationsKey }),
  })
}

export function useRevokeInvitation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/company/invitations/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: invitationsKey }),
  })
}

export function useRemoveMember() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (userId: string) => api.delete(`/company/members/${userId}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["company"] }),
  })
}

/** Leaves the active company; the session falls back to another company (or none). */
export function useLeaveCompany() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<void>("/company/leave"),
    onSuccess: async () => {
      setActiveCompanyId(null)
      await resetCompanyData(queryClient)
      await queryClient.invalidateQueries({ queryKey: meQueryKey })
    },
  })
}

/** Asks to change the sign-in e-mail; a confirmation link goes to the new address (202, nothing changes yet). */
export function useRequestEmailChange() {
  return useMutation({
    mutationFn: (input: { newEmail: string; currentPassword: string }) => api.post<void>("/profile/email", input),
  })
}

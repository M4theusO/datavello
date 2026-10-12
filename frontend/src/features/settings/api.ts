import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { api } from "@/lib/api"
import { type CompanyRole, meQueryKey } from "@/features/auth/auth"

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

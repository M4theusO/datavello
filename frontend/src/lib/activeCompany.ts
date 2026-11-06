/**
 * The company this browser tab works on, sent to the API as X-Company-Id.
 * - Kept in memory per tab, so switching in one tab never redirects writes made in another.
 * - The last choice per user is remembered in localStorage only as a preference for new tabs/sessions.
 * The backend validates the header against the user's memberships; this is a selection, not an authorization.
 */

export const COMPANY_HEADER = "X-Company-Id"

/** ProblemDetails.type returned when the selected company is no longer accessible. */
export const COMPANY_ACCESS_DENIED = "urn:datavello:company-access-denied"

let activeCompanyId: string | null = null

const storageKey = (userId: string) => `datavello.activeCompany.${userId}`

export function getActiveCompanyId(): string | null {
  return activeCompanyId
}

export function setActiveCompanyId(companyId: string | null): void {
  activeCompanyId = companyId
}

export function rememberCompany(userId: string, companyId: string): void {
  try {
    localStorage.setItem(storageKey(userId), companyId)
  } catch {
    // Storage unavailable (private mode, blocked): the choice simply is not remembered.
  }
}

export function preferredCompany(userId: string): string | null {
  try {
    return localStorage.getItem(storageKey(userId))
  } catch {
    return null
  }
}

/**
 * Picks the company to use: the tab's current choice, else the remembered preference,
 * else the server default — always one the user still belongs to.
 */
export function chooseCompany<T extends { id: string }>(userId: string, companies: T[], serverDefault: T | null): T | null {
  const byId = (id: string | null) => (id ? companies.find((c) => c.id === id) : undefined)
  return byId(activeCompanyId) ?? byId(preferredCompany(userId)) ?? serverDefault ?? companies[0] ?? null
}

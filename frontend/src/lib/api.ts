/**
 * Thin fetch wrapper for the Datavello API.
 * - Same-origin requests (Vite proxies /api in development), so the HttpOnly auth cookie is sent automatically.
 * - Every request carries the X-Requested-With header the backend requires on state-changing calls.
 * - Errors are surfaced as ApiError built from the server's ProblemDetails, never replaced with fake data.
 */

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail || firstValidationMessage(problem) || problem.title || defaultMessage(status))
    this.name = "ApiError"
    this.status = status
    this.problem = problem
  }

  /** Validation messages for a field, matched case-insensitively (server keys may be PascalCase). */
  fieldErrors(field: string): string[] {
    const entry = Object.entries(this.problem.errors ?? {}).find(([key]) => key.toLowerCase() === field.toLowerCase())
    return entry?.[1] ?? []
  }
}

function firstValidationMessage(problem: ProblemDetails): string | undefined {
  const all = Object.values(problem.errors ?? {}).flat()
  return all.length > 0 ? all[0] : undefined
}

function defaultMessage(status: number): string {
  if (status === 0) return "Não foi possível conectar ao servidor. Verifique sua conexão."
  if (status === 401) return "Sua sessão expirou. Faça login novamente."
  if (status === 403) return "Você não tem acesso a este recurso."
  if (status === 404) return "Recurso não encontrado."
  if (status >= 500) return "Ocorreu um erro no servidor. Tente novamente em instantes."
  return "Não foi possível concluir a operação."
}

type Method = "GET" | "POST" | "PUT" | "PATCH" | "DELETE"

async function request<T>(method: Method, path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const headers: Record<string, string> = {
    Accept: "application/json",
    "X-Requested-With": "datavello",
  }

  let payload: BodyInit | undefined
  if (body instanceof FormData) {
    payload = body
  } else if (body !== undefined) {
    headers["Content-Type"] = "application/json"
    payload = JSON.stringify(body)
  }

  let response: Response
  try {
    response = await fetch(`/api${path}`, { method, headers, body: payload, credentials: "same-origin", signal })
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") throw error
    throw new ApiError(0, {})
  }

  if (response.status === 204) {
    return undefined as T
  }

  const isJson = response.headers.get("content-type")?.includes("json") ?? false
  const data: unknown = isJson ? await response.json().catch(() => undefined) : undefined

  if (!response.ok) {
    throw new ApiError(response.status, (data as ProblemDetails | undefined) ?? {})
  }

  return data as T
}

export const api = {
  get: <T>(path: string, signal?: AbortSignal) => request<T>("GET", path, undefined, signal),
  post: <T>(path: string, body?: unknown) => request<T>("POST", path, body),
  put: <T>(path: string, body?: unknown) => request<T>("PUT", path, body),
  patch: <T>(path: string, body?: unknown) => request<T>("PATCH", path, body),
  delete: <T = void>(path: string) => request<T>("DELETE", path),
}

/** Message suitable for a toast or inline alert. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  return "Ocorreu um erro inesperado. Tente novamente."
}

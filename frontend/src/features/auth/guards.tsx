import { Navigate, Outlet, useLocation, useSearchParams } from "react-router"
import { FullPageError, FullPageLoader } from "@/components/feedback"
import { safeRedirect } from "@/lib/redirect"
import { useMe } from "./auth"

/** Renders child routes only for signed-in users; otherwise redirects to /login keeping the target. */
export function RequireAuth() {
  const me = useMe()
  const location = useLocation()

  if (me.isPending) return <FullPageLoader />
  if (me.isError) return <FullPageError error={me.error} onRetry={() => me.refetch()} />

  if (!me.data) {
    const redirect = location.pathname + location.search
    return <Navigate to={redirect === "/" ? "/login" : `/login?redirect=${encodeURIComponent(redirect)}`} replace />
  }

  return <Outlet />
}

/** Login/registration pages: signed-in users go straight to the app. */
export function GuestOnly() {
  const me = useMe()
  const [params] = useSearchParams()

  if (me.isPending) return <FullPageLoader />
  if (me.data) return <Navigate to={safeRedirect(params.get("redirect"))} replace />

  return <Outlet />
}


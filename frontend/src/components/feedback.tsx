import type { ReactNode } from "react"
import { AlertTriangle, Loader2, RefreshCw } from "lucide-react"
import { Button } from "@/components/ui/button"
import { LogoMark } from "@/components/brand/Logo"
import { errorMessage } from "@/lib/api"
import { cn } from "@/lib/utils"

export function Spinner({ className }: { className?: string }) {
  return <Loader2 className={cn("size-5 animate-spin text-muted-foreground", className)} aria-hidden="true" />
}

export function FullPageLoader() {
  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-4" role="status" aria-live="polite">
      <LogoMark className="size-10" />
      <Spinner />
      <span className="sr-only">Carregando…</span>
    </div>
  )
}

export function FullPageError({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div className="flex min-h-svh items-center justify-center p-6">
      <ErrorState error={error} onRetry={onRetry} />
    </div>
  )
}

/** Inline error block for a failed request, with an optional retry. */
export function ErrorState({ error, onRetry, className }: { error: unknown; onRetry?: () => void; className?: string }) {
  return (
    <div className={cn("flex max-w-md flex-col items-center gap-3 text-center", className)} role="alert">
      <div className="flex size-11 items-center justify-center rounded-full bg-destructive-soft text-destructive">
        <AlertTriangle className="size-5" aria-hidden="true" />
      </div>
      <div className="space-y-1">
        <p className="font-medium">Não foi possível carregar os dados</p>
        <p className="text-sm text-muted-foreground">{errorMessage(error)}</p>
      </div>
      {onRetry && (
        <Button variant="outline" size="sm" onClick={onRetry}>
          <RefreshCw data-icon="inline-start" />
          Tentar novamente
        </Button>
      )}
    </div>
  )
}

/** Friendly empty state: what is missing and what to do next. */
export function EmptyState({
  icon,
  title,
  description,
  action,
  className,
}: {
  icon: ReactNode
  title: string
  description?: string
  action?: ReactNode
  className?: string
}) {
  return (
    <div className={cn("flex flex-col items-center gap-3 px-6 py-12 text-center", className)}>
      <div className="flex size-12 items-center justify-center rounded-full bg-brand-soft text-primary [&_svg]:size-5">{icon}</div>
      <div className="max-w-sm space-y-1">
        <p className="font-medium">{title}</p>
        {description && <p className="text-sm text-muted-foreground">{description}</p>}
      </div>
      {action}
    </div>
  )
}

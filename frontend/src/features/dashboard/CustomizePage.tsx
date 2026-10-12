import { useState } from "react"
import { ArrowDown, ArrowUp, LayoutDashboard, RotateCcw, Save } from "lucide-react"
import { Link } from "react-router"
import { toast } from "sonner"
import { SimpleSelect } from "@/components/SimpleSelect"
import { ErrorState, Spinner } from "@/components/feedback"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { Badge } from "@/components/ui/badge"
import { Button, buttonVariants } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { Switch } from "@/components/ui/switch"
import { errorMessage } from "@/lib/api"
import { dashboardPresets, periodLabels } from "@/lib/periods"
import { cn } from "@/lib/utils"
import { type DashboardConfig, type DashboardConfigInput, useDashboardConfig, useResetDashboardConfig, useSaveDashboardConfig } from "./api"
import { widgetCatalog } from "./widgetCatalog"

const periodOptions = dashboardPresets.filter((p) => p !== "custom").map((p) => ({ value: p, label: periodLabels[p] }))

function toInput(config: DashboardConfig): DashboardConfigInput {
  return {
    defaultPeriod: config.defaultPeriod,
    widgets: [...config.widgets].sort((a, b) => a.position - b.position).map((w) => ({ type: w.type, isEnabled: w.isEnabled })),
  }
}

export function CustomizePage() {
  const config = useDashboardConfig()
  const save = useSaveDashboardConfig()
  const reset = useResetDashboardConfig()
  // Local edits only; while null, the saved configuration is shown.
  const [edits, setEdits] = useState<DashboardConfigInput | null>(null)
  const [confirmReset, setConfirmReset] = useState(false)

  const saved = config.data ? toInput(config.data) : null
  const draft = edits ?? saved
  const dirty = edits !== null && JSON.stringify(edits) !== JSON.stringify(saved)

  function setDraft(update: DashboardConfigInput | ((current: DashboardConfigInput | null) => DashboardConfigInput | null)) {
    setEdits((current) => (typeof update === "function" ? update(current ?? saved) : update))
  }
  const enabledCount = draft?.widgets.filter((w) => w.isEnabled).length ?? 0

  function move(index: number, delta: -1 | 1) {
    setDraft((d) => {
      if (!d) return d
      const widgets = [...d.widgets]
      const target = index + delta
      if (target < 0 || target >= widgets.length) return d
      ;[widgets[index], widgets[target]] = [widgets[target], widgets[index]]
      return { ...d, widgets }
    })
  }

  function toggle(index: number, enabled: boolean) {
    setDraft((d) => d && { ...d, widgets: d.widgets.map((w, i) => (i === index ? { ...w, isEnabled: enabled } : w)) })
  }

  async function handleSave() {
    if (!draft) return
    try {
      await save.mutateAsync(draft)
      setEdits(null)
      toast.success("Preferências do dashboard salvas.")
    } catch (error) {
      toast.error(errorMessage(error))
    }
  }

  async function handleReset() {
    try {
      await reset.mutateAsync()
      setEdits(null)
      toast.success("Layout padrão restaurado.")
    } catch (error) {
      toast.error(errorMessage(error))
    } finally {
      setConfirmReset(false)
    }
  }

  if (config.isError) {
    return (
      <Card className="mx-auto max-w-3xl">
        <ErrorState error={config.error} onRetry={() => config.refetch()} className="mx-auto py-12" />
      </Card>
    )
  }

  if (!draft) {
    return (
      <div className="mx-auto max-w-3xl space-y-4">
        <Skeleton className="h-28 w-full rounded-xl" />
        <Skeleton className="h-96 w-full rounded-xl" />
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <p className="text-sm text-muted-foreground">
        Escolha quais indicadores e gráficos aparecem no dashboard e em que ordem. As preferências valem para toda a empresa.
      </p>

      <Card className="gap-4">
        <CardHeader>
          <CardTitle className="text-base">Período padrão</CardTitle>
          <CardDescription>Período exibido ao abrir o dashboard. Você ainda pode trocá-lo nos filtros.</CardDescription>
        </CardHeader>
        <CardContent>
          <Label htmlFor="default-period" className="sr-only">
            Período padrão
          </Label>
          <SimpleSelect
            id="default-period"
            value={draft.defaultPeriod}
            onChange={(v) => setDraft({ ...draft, defaultPeriod: v as DashboardConfigInput["defaultPeriod"] })}
            options={periodOptions}
            className="sm:w-72"
          />
        </CardContent>
      </Card>

      <Card className="gap-4">
        <CardHeader>
          <CardTitle className="text-base">Widgets</CardTitle>
          <CardDescription>
            {enabledCount} de {draft.widgets.length} ativos. Use as setas para mudar a ordem: indicadores ocupam um quarto da linha, gráficos metade e a
            tabela a linha inteira.
          </CardDescription>
        </CardHeader>
        <CardContent className="px-0">
          <ol className="divide-y border-y">
            {draft.widgets.map((widget, index) => {
              const info = widgetCatalog[widget.type]
              const Icon = info.icon
              return (
                <li key={widget.type} className={cn("flex items-center gap-3 px-4 py-3 sm:px-6", !widget.isEnabled && "bg-muted/30")}>
                  <span className="tabular w-5 text-center text-xs text-muted-foreground">{index + 1}</span>
                  <span
                    className={cn(
                      "flex size-9 shrink-0 items-center justify-center rounded-lg [&_svg]:size-4",
                      widget.isEnabled ? "bg-brand-soft text-primary" : "bg-muted text-muted-foreground",
                    )}
                    aria-hidden="true"
                  >
                    <Icon />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className={cn("flex flex-wrap items-center gap-2 font-medium", !widget.isEnabled && "text-muted-foreground")}>
                      {info.label}
                      <Badge variant="outline" className="font-normal text-muted-foreground">
                        {info.size}
                      </Badge>
                    </p>
                    <p className="truncate text-xs text-muted-foreground sm:whitespace-normal">{info.description}</p>
                  </div>
                  <div className="flex items-center gap-1">
                    <Button variant="ghost" size="icon-sm" onClick={() => move(index, -1)} disabled={index === 0} aria-label={`Mover ${info.label} para cima`}>
                      <ArrowUp />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      onClick={() => move(index, 1)}
                      disabled={index === draft.widgets.length - 1}
                      aria-label={`Mover ${info.label} para baixo`}
                    >
                      <ArrowDown />
                    </Button>
                    <Switch
                      className="ml-2"
                      checked={widget.isEnabled}
                      onCheckedChange={(checked) => toggle(index, checked)}
                      aria-label={`${widget.isEnabled ? "Desativar" : "Ativar"} ${info.label}`}
                    />
                  </div>
                </li>
              )
            })}
          </ol>
        </CardContent>
      </Card>

      <div className="sticky bottom-0 z-10 -mx-4 flex flex-col-reverse gap-2 border-t bg-background/90 px-4 py-3 backdrop-blur sm:mx-0 sm:flex-row sm:items-center sm:justify-between sm:rounded-xl sm:border">
        <Button variant="ghost" onClick={() => setConfirmReset(true)} disabled={reset.isPending}>
          <RotateCcw data-icon="inline-start" />
          Restaurar padrão
        </Button>
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:items-center">
          {dirty && <span className="text-center text-xs text-muted-foreground sm:text-left">Alterações não salvas</span>}
          <Link to="/" className={buttonVariants({ variant: "outline" })}>
            <LayoutDashboard data-icon="inline-start" />
            Ver dashboard
          </Link>
          <Button onClick={handleSave} disabled={!dirty || save.isPending}>
            {save.isPending ? <Spinner className="size-4 text-primary-foreground" /> : <Save data-icon="inline-start" />}
            Salvar alterações
          </Button>
        </div>
      </div>

      <AlertDialog open={confirmReset} onOpenChange={setConfirmReset}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Restaurar o layout padrão?</AlertDialogTitle>
            <AlertDialogDescription>
              Todos os widgets voltam a ficar ativos, na ordem original, e o período padrão volta ao valor inicial. Suas transações não são afetadas.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction onClick={handleReset} disabled={reset.isPending}>
              {reset.isPending && <Spinner className="size-4 text-primary-foreground" />}
              Restaurar
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

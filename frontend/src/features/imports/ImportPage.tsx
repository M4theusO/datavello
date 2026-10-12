import { useState } from "react"
import { Check } from "lucide-react"
import { toast } from "sonner"
import { cn } from "@/lib/utils"
import { defaultOptions, type ImportJobDetail, type ImportOptions, type ImportPreview, type ImportValidation, useCancelImport } from "./api"
import { ImportTabs } from "./components"
import { MappingStep } from "./steps/MappingStep"
import { ResultStep } from "./steps/ResultStep"
import { ReviewStep } from "./steps/ReviewStep"
import { UploadStep } from "./steps/UploadStep"

type Step =
  | { name: "upload" }
  | { name: "mapping"; preview: ImportPreview; options: ImportOptions; returning: boolean }
  | { name: "review"; preview: ImportPreview; options: ImportOptions; validation: ImportValidation }
  | { name: "done"; result: ImportJobDetail }

const steps = [
  { key: "upload", label: "Arquivo" },
  { key: "mapping", label: "Colunas" },
  { key: "review", label: "Revisão" },
  { key: "done", label: "Resultado" },
] as const

export function ImportPage() {
  const [step, setStepState] = useState<Step>({ name: "upload" })
  const cancel = useCancelImport()

  /** Each step starts at the top of the page (the previous one may have been scrolled down). */
  function setStep(next: Step) {
    setStepState(next)
    window.scrollTo({ top: 0, behavior: "smooth" })
  }
  const currentIndex = steps.findIndex((s) => s.key === step.name)

  async function cancelImport(preview: ImportPreview) {
    try {
      await cancel.mutateAsync(preview.id)
    } catch {
      // The pending import expires on its own; leaving the wizard is what matters here.
    }
    toast.info("Importação cancelada. Nenhum dado foi gravado.")
    setStep({ name: "upload" })
  }

  return (
    <div className="mx-auto max-w-6xl space-y-5">
      <div className="flex flex-col gap-3 xl:flex-row xl:items-center xl:justify-between">
        <ImportTabs />
        <ol className="flex flex-wrap items-center gap-2 text-sm" aria-label="Etapas da importação">
          {steps.map((s, i) => (
            <li key={s.key} className="flex items-center gap-2" aria-current={i === currentIndex ? "step" : undefined}>
              <span
                className={cn(
                  "flex size-6 items-center justify-center rounded-full text-xs font-medium",
                  i < currentIndex && "bg-primary text-primary-foreground",
                  i === currentIndex && "bg-brand-soft text-primary ring-2 ring-primary",
                  i > currentIndex && "bg-muted text-muted-foreground",
                )}
              >
                {i < currentIndex ? <Check className="size-3.5" aria-hidden="true" /> : i + 1}
              </span>
              <span className={cn("hidden sm:inline", i === currentIndex ? "font-medium" : "text-muted-foreground")}>{s.label}</span>
              {i < steps.length - 1 && <span className="h-px w-4 bg-border sm:w-6" aria-hidden="true" />}
            </li>
          ))}
        </ol>
      </div>

      {step.name === "upload" && (
        <UploadStep onUploaded={(preview) => setStep({ name: "mapping", preview, options: defaultOptions(preview), returning: false })} />
      )}

      {step.name === "mapping" && (
        <MappingStep
          key={step.preview.id}
          preview={step.preview}
          initial={step.options}
          suggestCategoryDefaults={!step.returning}
          onCancel={() => cancelImport(step.preview)}
          onValidated={(options, validation) => setStep({ name: "review", preview: step.preview, options, validation })}
        />
      )}

      {step.name === "review" && (
        <ReviewStep
          preview={step.preview}
          options={step.options}
          validation={step.validation}
          onBack={() => setStep({ name: "mapping", preview: step.preview, options: step.options, returning: true })}
          onCommitted={(result) => setStep({ name: "done", result })}
        />
      )}

      {step.name === "done" && <ResultStep result={step.result} onNew={() => setStep({ name: "upload" })} />}
    </div>
  )
}

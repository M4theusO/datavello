import { useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { AlertCircle, MailCheck, Send } from "lucide-react"
import { useForm } from "react-hook-form"
import { z } from "zod"
import { Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { ApiError, errorMessage } from "@/lib/api"
import { useRequestEmailChange } from "./api"

const schema = z.object({
  newEmail: z.string().trim().min(1, "Informe o novo e-mail.").max(256, "E-mail muito longo.").pipe(z.email("E-mail inválido.")),
  currentPassword: z.string().min(1, "Informe a senha atual."),
})

type FormValues = z.infer<typeof schema>

/** Requests an e-mail change: the confirmation link goes to the new address; nothing changes until it is used. */
export function ChangeEmailDialog({ open, onOpenChange, currentEmail }: { open: boolean; onOpenChange: (open: boolean) => void; currentEmail: string }) {
  const request = useRequestEmailChange()
  const [sentTo, setSentTo] = useState<string | null>(null)
  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { newEmail: "", currentPassword: "" } })
  const { errors } = form.formState

  function handleOpenChange(next: boolean) {
    if (!next) {
      setSentTo(null)
      form.reset()
      request.reset()
    }
    onOpenChange(next)
  }

  const onSubmit = form.handleSubmit(async ({ newEmail, currentPassword }) => {
    if (newEmail.toLowerCase() === currentEmail.toLowerCase()) {
      form.setError("newEmail", { message: "Informe um e-mail diferente do atual." })
      return
    }
    try {
      await request.mutateAsync({ newEmail, currentPassword })
      setSentTo(newEmail)
    } catch (error) {
      if (!(error instanceof ApiError)) return
      if (error.message.includes("Senha atual incorreta")) form.setError("currentPassword", { message: "Senha atual incorreta." })
      else if (error.status === 409 || error.message.includes("diferente do atual")) form.setError("newEmail", { message: error.message })
      form.setValue("currentPassword", "")
    }
  })

  const fieldError =
    request.error instanceof ApiError &&
    (request.error.status === 409 || ["Senha atual incorreta", "diferente do atual"].some((t) => request.error!.message.includes(t)))
  const showGeneralError = request.isError && !fieldError

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        {sentTo ? (
          <div className="space-y-5">
            <DialogHeader>
              <DialogTitle>Confirme no novo e-mail</DialogTitle>
              <DialogDescription>
                Enviamos um link de confirmação para <strong className="text-foreground">{sentTo}</strong>. A troca só acontece quando você abrir o link e
                confirmar.
              </DialogDescription>
            </DialogHeader>
            <Alert>
              <MailCheck />
              <AlertDescription>
                O link vale por 1 hora. Até lá, continue entrando com {currentEmail}. Depois da confirmação, todas as sessões são encerradas e você entra com o
                novo e-mail.
              </AlertDescription>
            </Alert>
            <DialogFooter>
              <Button type="button" onClick={() => handleOpenChange(false)}>
                Concluir
              </Button>
            </DialogFooter>
          </div>
        ) : (
          <form onSubmit={onSubmit} noValidate className="space-y-5">
            <DialogHeader>
              <DialogTitle>Alterar e-mail</DialogTitle>
              <DialogDescription>O e-mail é usado para entrar no Datavello. Enviaremos um link de confirmação para o novo endereço.</DialogDescription>
            </DialogHeader>
            {showGeneralError && (
              <Alert variant="destructive">
                <AlertCircle />
                <AlertDescription>{errorMessage(request.error)}</AlertDescription>
              </Alert>
            )}
            <FieldGroup>
              <Field data-invalid={!!errors.newEmail}>
                <FieldLabel htmlFor="new-email">Novo e-mail</FieldLabel>
                <Input id="new-email" type="email" autoComplete="email" placeholder="voce@empresa.com.br" aria-invalid={!!errors.newEmail} {...form.register("newEmail")} />
                <FieldError errors={[errors.newEmail]} />
              </Field>
              <Field data-invalid={!!errors.currentPassword}>
                <FieldLabel htmlFor="email-current-password">Senha atual</FieldLabel>
                <Input
                  id="email-current-password"
                  type="password"
                  autoComplete="current-password"
                  aria-invalid={!!errors.currentPassword}
                  {...form.register("currentPassword")}
                />
                {errors.currentPassword ? <FieldError errors={[errors.currentPassword]} /> : <FieldDescription>Para confirmar que é você.</FieldDescription>}
              </Field>
            </FieldGroup>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
                Cancelar
              </Button>
              <Button type="submit" disabled={request.isPending}>
                {request.isPending ? <Spinner className="size-4 text-primary-foreground" /> : <Send data-icon="inline-start" />}
                Enviar confirmação
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}

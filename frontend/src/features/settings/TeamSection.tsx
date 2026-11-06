import { useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { AlertCircle, Check, Copy, LogOut, MailPlus, Trash2, UserMinus, Users } from "lucide-react"
import { useForm } from "react-hook-form"
import { useNavigate } from "react-router"
import { toast } from "sonner"
import { z } from "zod"
import { EmptyState, ErrorState, Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
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
import { Button } from "@/components/ui/button"
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { useMe } from "@/features/auth/auth"
import { ApiError, errorMessage } from "@/lib/api"
import {
  type CreatedInvitation,
  type Invitation,
  type Member,
  invitationLink,
  useCreateInvitation,
  useInvitations,
  useLeaveCompany,
  useMembers,
  useRemoveMember,
  useRevokeInvitation,
} from "./api"

const shortDate = new Intl.DateTimeFormat("pt-BR")
const formatDay = (iso: string) => shortDate.format(new Date(iso))

/** Members of the active company and, for the owner, invitations by link. */
export function TeamSection({ isOwner, companyName }: { isOwner: boolean; companyName: string }) {
  const me = useMe()
  const members = useMembers()
  const invitations = useInvitations(isOwner)
  const [inviteOpen, setInviteOpen] = useState(false)
  const [removing, setRemoving] = useState<Member | null>(null)
  const [revoking, setRevoking] = useState<Invitation | null>(null)
  const [leaving, setLeaving] = useState(false)
  const removeMember = useRemoveMember()
  const revoke = useRevokeInvitation()
  const leave = useLeaveCompany()
  const navigate = useNavigate()

  async function confirmRemove() {
    if (!removing) return
    try {
      await removeMember.mutateAsync(removing.userId)
      toast.success(`${removing.fullName} não tem mais acesso à empresa.`)
    } catch (error) {
      toast.error(errorMessage(error))
    }
    setRemoving(null)
  }

  async function confirmRevoke() {
    if (!revoking) return
    try {
      await revoke.mutateAsync(revoking.id)
      toast.success("Convite cancelado. O link deixou de funcionar.")
    } catch (error) {
      toast.error(errorMessage(error))
    }
    setRevoking(null)
  }

  async function confirmLeave() {
    try {
      await leave.mutateAsync()
      toast.success(`Você saiu de ${companyName}.`)
      navigate("/", { replace: true })
    } catch (error) {
      toast.error(errorMessage(error))
      setLeaving(false)
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Users className="size-4 text-muted-foreground" aria-hidden="true" />
          Equipe
        </CardTitle>
        <CardDescription>Pessoas com acesso aos dados desta empresa. Todos os membros podem ver e lançar dados.</CardDescription>
        {isOwner && (
          <CardAction>
            <Button size="sm" onClick={() => setInviteOpen(true)}>
              <MailPlus data-icon="inline-start" />
              Convidar
            </Button>
          </CardAction>
        )}
      </CardHeader>
      <CardContent className="space-y-6">
        {members.isPending ? (
          <Skeleton className="h-24 w-full" />
        ) : members.isError ? (
          <ErrorState error={members.error} onRetry={() => members.refetch()} className="mx-auto py-6" />
        ) : (
          <ul className="divide-y rounded-lg border">
            {members.data.map((member) => {
              const isSelf = member.userId === me.data?.user.id
              return (
                <li key={member.userId} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2.5">
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">
                      {member.fullName}
                      {isSelf && <span className="font-normal text-muted-foreground"> (você)</span>}
                    </p>
                    <p className="truncate text-xs text-muted-foreground">{member.email}</p>
                  </div>
                  <Badge variant="secondary" className={member.role === "Owner" ? "bg-brand-soft text-primary" : undefined}>
                    {member.role === "Owner" ? "Proprietário" : "Membro"}
                  </Badge>
                  {isOwner && member.role !== "Owner" && (
                    <Button variant="ghost" size="icon-sm" aria-label={`Remover ${member.fullName}`} onClick={() => setRemoving(member)}>
                      <UserMinus />
                    </Button>
                  )}
                </li>
              )
            })}
          </ul>
        )}

        {isOwner && (
          <section className="space-y-2" aria-labelledby="pending-invitations">
            <h3 id="pending-invitations" className="text-sm font-medium">
              Convites pendentes
            </h3>
            {invitations.isPending ? (
              <Skeleton className="h-12 w-full" />
            ) : invitations.isError ? (
              <ErrorState error={invitations.error} onRetry={() => invitations.refetch()} className="mx-auto py-6" />
            ) : invitations.data.length === 0 ? (
              <EmptyState
                icon={<MailPlus />}
                title="Nenhum convite pendente"
                description="Convide alguém pelo e-mail e envie o link gerado pelo canal que preferir."
                className="rounded-lg border border-dashed py-6"
              />
            ) : (
              <ul className="divide-y rounded-lg border">
                {invitations.data.map((invitation) => (
                  <li key={invitation.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2.5">
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">{invitation.email}</p>
                      <p className="text-xs text-muted-foreground">
                        {invitation.isExpired ? `Expirou em ${formatDay(invitation.expiresAt)}` : `Válido até ${formatDay(invitation.expiresAt)}`}
                      </p>
                    </div>
                    {invitation.isExpired ? (
                      <Badge variant="secondary" className="bg-destructive-soft text-destructive">
                        Expirado
                      </Badge>
                    ) : (
                      <Badge variant="secondary">Aguardando</Badge>
                    )}
                    <Button variant="ghost" size="icon-sm" aria-label={`Cancelar convite para ${invitation.email}`} onClick={() => setRevoking(invitation)}>
                      <Trash2 />
                    </Button>
                  </li>
                ))}
              </ul>
            )}
            <p className="text-xs text-muted-foreground">
              Para reenviar, convide o mesmo e-mail novamente: um novo link é gerado e o anterior deixa de funcionar.
            </p>
          </section>
        )}

        {!isOwner && (
          <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-4">
            <p className="text-sm text-muted-foreground">Ao sair, você perde o acesso aos dados desta empresa.</p>
            <Button variant="outline" size="sm" onClick={() => setLeaving(true)}>
              <LogOut data-icon="inline-start" />
              Sair da empresa
            </Button>
          </div>
        )}
      </CardContent>

      {isOwner && <InviteDialog open={inviteOpen} onOpenChange={setInviteOpen} companyName={companyName} />}

      <AlertDialog open={removing !== null} onOpenChange={(open) => !open && setRemoving(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remover membro?</AlertDialogTitle>
            <AlertDialogDescription>
              <strong className="text-foreground">{removing?.fullName}</strong> perderá o acesso a {companyName} imediatamente. Os dados lançados por essa pessoa
              continuam na empresa.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={confirmRemove} disabled={removeMember.isPending}>
              {removeMember.isPending && <Spinner className="size-4" />}
              Remover
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={revoking !== null} onOpenChange={(open) => !open && setRevoking(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Cancelar convite?</AlertDialogTitle>
            <AlertDialogDescription>
              O link enviado para <strong className="text-foreground">{revoking?.email}</strong> deixará de funcionar.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Voltar</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={confirmRevoke} disabled={revoke.isPending}>
              {revoke.isPending && <Spinner className="size-4" />}
              Cancelar convite
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={leaving} onOpenChange={setLeaving}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Sair de {companyName}?</AlertDialogTitle>
            <AlertDialogDescription>Você perderá o acesso aos dados desta empresa. Para voltar, será preciso um novo convite do proprietário.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={confirmLeave} disabled={leave.isPending}>
              {leave.isPending && <Spinner className="size-4" />}
              Sair da empresa
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Card>
  )
}

const inviteSchema = z.object({
  email: z.string().trim().min(1, "Informe o e-mail.").max(256, "E-mail muito longo.").pipe(z.email("E-mail inválido.")),
})

function InviteDialog({ open, onOpenChange, companyName }: { open: boolean; onOpenChange: (open: boolean) => void; companyName: string }) {
  const create = useCreateInvitation()
  const [created, setCreated] = useState<CreatedInvitation | null>(null)
  const [copied, setCopied] = useState(false)
  const form = useForm<z.infer<typeof inviteSchema>>({ resolver: zodResolver(inviteSchema), defaultValues: { email: "" } })
  const { errors } = form.formState

  function handleOpenChange(next: boolean) {
    if (!next) {
      // The link cannot be shown again once closed; start fresh next time.
      setCreated(null)
      setCopied(false)
      form.reset()
      create.reset()
    }
    onOpenChange(next)
  }

  const onSubmit = form.handleSubmit(async ({ email }) => {
    try {
      setCreated(await create.mutateAsync({ email }))
    } catch (error) {
      if (error instanceof ApiError && (error.status === 409 || error.fieldErrors("email").length > 0)) {
        form.setError("email", { message: error.fieldErrors("email")[0] ?? error.message })
      }
    }
  })

  async function copyLink() {
    if (!created) return
    try {
      await navigator.clipboard.writeText(invitationLink(created.token))
      setCopied(true)
      toast.success("Link copiado.")
    } catch {
      toast.error("Não foi possível copiar automaticamente. Selecione o link e copie manualmente.")
    }
  }

  const showGeneralError = create.isError && !(create.error instanceof ApiError && (create.error.status === 409 || create.error.fieldErrors("email").length > 0))

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        {created ? (
          <div className="space-y-5">
            <DialogHeader>
              <DialogTitle>Convite criado</DialogTitle>
              <DialogDescription>
                Envie este link para <strong className="text-foreground">{created.invitation.email}</strong> (por e-mail, WhatsApp ou outro canal). Ele vale
                até {formatDay(created.invitation.expiresAt)} e só pode ser usado uma vez, com esse e-mail.
              </DialogDescription>
            </DialogHeader>
            <Field>
              <FieldLabel htmlFor="invite-link">Link do convite</FieldLabel>
              <div className="flex gap-2">
                <Input id="invite-link" readOnly value={invitationLink(created.token)} onFocus={(e) => e.currentTarget.select()} className="font-mono text-xs" />
                <Button type="button" variant="outline" onClick={copyLink} aria-label="Copiar link">
                  {copied ? <Check /> : <Copy />}
                </Button>
              </div>
              <FieldDescription>Por segurança, o link não será exibido novamente. Se perdê-lo, convide o mesmo e-mail outra vez.</FieldDescription>
            </Field>
            <DialogFooter>
              <Button type="button" onClick={() => handleOpenChange(false)}>
                Concluir
              </Button>
            </DialogFooter>
          </div>
        ) : (
          <form onSubmit={onSubmit} noValidate className="space-y-5">
            <DialogHeader>
              <DialogTitle>Convidar para {companyName}</DialogTitle>
              <DialogDescription>A pessoa entra como membro: pode ver o dashboard e lançar, importar e editar dados. Só você altera a empresa e a equipe.</DialogDescription>
            </DialogHeader>
            {showGeneralError && (
              <Alert variant="destructive">
                <AlertCircle />
                <AlertDescription>{errorMessage(create.error)}</AlertDescription>
              </Alert>
            )}
            <Field data-invalid={!!errors.email}>
              <FieldLabel htmlFor="invite-email">E-mail da pessoa</FieldLabel>
              <Input id="invite-email" type="email" autoComplete="off" placeholder="pessoa@empresa.com.br" aria-invalid={!!errors.email} {...form.register("email")} />
              {errors.email ? <FieldError errors={[errors.email]} /> : <FieldDescription>Somente uma conta com este e-mail poderá aceitar o convite.</FieldDescription>}
            </Field>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
                Cancelar
              </Button>
              <Button type="submit" disabled={create.isPending}>
                {create.isPending ? <Spinner className="size-4 text-primary-foreground" /> : <MailPlus data-icon="inline-start" />}
                Gerar link
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}

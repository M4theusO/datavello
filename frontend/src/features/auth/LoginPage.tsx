import { useEffect } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { AlertCircle, LogIn } from "lucide-react"
import { useForm } from "react-hook-form"
import { Link, useNavigate, useSearchParams } from "react-router"
import { z } from "zod"
import { Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { errorMessage } from "@/lib/api"
import { safeRedirect } from "@/lib/redirect"
import { useLogin } from "./auth"
import { AuthLayout } from "./AuthLayout"

const schema = z.object({
  email: z.string().trim().min(1, "Informe o e-mail.").pipe(z.email("E-mail inválido.")),
  password: z.string().min(1, "Informe a senha."),
  rememberMe: z.boolean(),
})

type FormValues = z.infer<typeof schema>

export function LoginPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const login = useLogin()

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: "", password: "", rememberMe: false },
  })
  const { errors } = form.formState

  useEffect(() => {
    document.title = "Entrar · Datavello"
  }, [])

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await login.mutateAsync(values)
      navigate(safeRedirect(params.get("redirect")), { replace: true })
    } catch {
      form.setValue("password", "")
      form.setFocus("password")
    }
  })

  return (
    <AuthLayout title="Entrar na sua conta" description="Acesse o painel da sua empresa.">
      <form onSubmit={onSubmit} noValidate className="space-y-6">
        {login.isError && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{errorMessage(login.error)}</AlertDescription>
          </Alert>
        )}

        <FieldGroup>
          <Field data-invalid={!!errors.email}>
            <FieldLabel htmlFor="email">E-mail</FieldLabel>
            <Input
              id="email"
              type="email"
              autoComplete="email"
              placeholder="voce@empresa.com.br"
              className="h-10"
              aria-invalid={!!errors.email}
              {...form.register("email")}
            />
            <FieldError errors={[errors.email]} />
          </Field>

          <Field data-invalid={!!errors.password}>
            <FieldLabel htmlFor="password">Senha</FieldLabel>
            <Input
              id="password"
              type="password"
              autoComplete="current-password"
              className="h-10"
              aria-invalid={!!errors.password}
              {...form.register("password")}
            />
            <FieldError errors={[errors.password]} />
          </Field>

          <label className="flex items-center gap-2 text-sm text-muted-foreground select-none">
            <input type="checkbox" className="size-4 rounded border-input accent-primary" {...form.register("rememberMe")} />
            Manter conectado neste dispositivo
          </label>
        </FieldGroup>

        <Button type="submit" size="lg" className="h-10 w-full" disabled={login.isPending}>
          {login.isPending ? <Spinner className="text-primary-foreground" /> : <LogIn data-icon="inline-start" />}
          Entrar
        </Button>

        <p className="text-center text-sm text-muted-foreground">
          Ainda não tem conta?{" "}
          <Link to="/cadastro" className="font-medium text-primary underline-offset-4 hover:underline">
            Criar conta grátis
          </Link>
        </p>
      </form>
    </AuthLayout>
  )
}

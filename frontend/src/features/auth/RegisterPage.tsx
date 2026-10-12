import { useEffect } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { AlertCircle, UserPlus } from "lucide-react"
import { useForm, type Path } from "react-hook-form"
import { Link, useNavigate } from "react-router"
import { toast } from "sonner"
import { z } from "zod"
import { Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { ApiError, errorMessage } from "@/lib/api"
import { useRegister } from "./auth"
import { AuthLayout } from "./AuthLayout"

// Mirrors the backend rules (RegisterRequest + Identity password options).
const schema = z
  .object({
    fullName: z.string().trim().min(2, "Informe seu nome.").max(150, "Nome muito longo."),
    companyName: z.string().trim().min(2, "Informe o nome da empresa.").max(150, "Nome muito longo."),
    email: z.string().trim().min(1, "Informe o e-mail.").pipe(z.email("E-mail inválido.")),
    password: z
      .string()
      .min(8, "A senha deve ter pelo menos 8 caracteres.")
      .max(128, "A senha deve ter no máximo 128 caracteres.")
      .regex(/\d/, "A senha deve conter pelo menos um número."),
    confirmPassword: z.string(),
  })
  .refine((v) => v.password === v.confirmPassword, {
    path: ["confirmPassword"],
    message: "As senhas não coincidem.",
  })

type FormValues = z.infer<typeof schema>
const serverFields: Path<FormValues>[] = ["fullName", "companyName", "email", "password"]

export function RegisterPage() {
  const navigate = useNavigate()
  const register = useRegister()

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { fullName: "", companyName: "", email: "", password: "", confirmPassword: "" },
  })
  const { errors } = form.formState

  useEffect(() => {
    document.title = "Criar conta · Datavello"
  }, [])

  const onSubmit = form.handleSubmit(async ({ fullName, companyName, email, password }) => {
    try {
      await register.mutateAsync({ fullName, companyName, email, password })
      toast.success("Conta criada! Bem-vindo(a) ao Datavello.")
      navigate("/", { replace: true })
    } catch (error) {
      if (error instanceof ApiError) {
        // Show field-level validation messages from the server next to the matching inputs.
        for (const field of serverFields) {
          const [message] = error.fieldErrors(field)
          if (message) form.setError(field, { message })
        }
        if (error.status === 409) form.setError("email", { message: error.message })
      }
    }
  })

  const generalError = register.isError && !(register.error instanceof ApiError && register.error.status === 409)

  return (
    <AuthLayout title="Crie sua conta" description="Leva menos de um minuto. Sua empresa já começa com categorias e um dashboard prontos.">
      <form onSubmit={onSubmit} noValidate className="space-y-6">
        {generalError && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{errorMessage(register.error)}</AlertDescription>
          </Alert>
        )}

        <FieldGroup>
          <Field data-invalid={!!errors.fullName}>
            <FieldLabel htmlFor="fullName">Seu nome</FieldLabel>
            <Input id="fullName" autoComplete="name" className="h-10" aria-invalid={!!errors.fullName} {...form.register("fullName")} />
            <FieldError errors={[errors.fullName]} />
          </Field>

          <Field data-invalid={!!errors.companyName}>
            <FieldLabel htmlFor="companyName">Nome da empresa</FieldLabel>
            <Input id="companyName" autoComplete="organization" className="h-10" aria-invalid={!!errors.companyName} {...form.register("companyName")} />
            <FieldError errors={[errors.companyName]} />
          </Field>

          <Field data-invalid={!!errors.email}>
            <FieldLabel htmlFor="email">E-mail</FieldLabel>
            <Input id="email" type="email" autoComplete="email" placeholder="voce@empresa.com.br" className="h-10" aria-invalid={!!errors.email} {...form.register("email")} />
            <FieldError errors={[errors.email]} />
          </Field>

          <Field data-invalid={!!errors.password}>
            <FieldLabel htmlFor="password">Senha</FieldLabel>
            <Input id="password" type="password" autoComplete="new-password" className="h-10" aria-invalid={!!errors.password} {...form.register("password")} />
            {errors.password ? <FieldError errors={[errors.password]} /> : <FieldDescription>Mínimo de 8 caracteres, com pelo menos um número.</FieldDescription>}
          </Field>

          <Field data-invalid={!!errors.confirmPassword}>
            <FieldLabel htmlFor="confirmPassword">Confirme a senha</FieldLabel>
            <Input id="confirmPassword" type="password" autoComplete="new-password" className="h-10" aria-invalid={!!errors.confirmPassword} {...form.register("confirmPassword")} />
            <FieldError errors={[errors.confirmPassword]} />
          </Field>
        </FieldGroup>

        <Button type="submit" size="lg" className="h-10 w-full" disabled={register.isPending}>
          {register.isPending ? <Spinner className="text-primary-foreground" /> : <UserPlus data-icon="inline-start" />}
          Criar conta
        </Button>

        <p className="text-center text-sm text-muted-foreground">
          Já tem conta?{" "}
          <Link to="/login" className="font-medium text-primary underline-offset-4 hover:underline">
            Entrar
          </Link>
        </p>
      </form>
    </AuthLayout>
  )
}

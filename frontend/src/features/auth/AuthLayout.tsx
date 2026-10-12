import type { ReactNode } from "react"
import { BarChart3, FileSpreadsheet, ShieldCheck } from "lucide-react"
import { Logo } from "@/components/brand/Logo"

const currentYear = new Date().getFullYear()

const highlights = [
  { icon: BarChart3, text: "Receitas, despesas e saldo do período em um só lugar." },
  { icon: FileSpreadsheet, text: "Importe suas planilhas Excel ou CSV sem complicação." },
  { icon: ShieldCheck, text: "Os dados de cada empresa ficam isolados e protegidos." },
]

export function AuthLayout({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return (
    <div className="grid min-h-svh lg:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
      <aside className="relative hidden overflow-hidden bg-[#0f2f66] text-white lg:flex lg:flex-col lg:justify-between lg:p-12">
        {/* Subtle grid that echoes the chart motif of the logo. */}
        <svg className="pointer-events-none absolute inset-0 size-full opacity-[0.07]" aria-hidden="true">
          <defs>
            <pattern id="auth-grid" width="40" height="40" patternUnits="userSpaceOnUse">
              <path d="M40 0H0V40" fill="none" stroke="white" strokeWidth="1" />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#auth-grid)" />
        </svg>

        <Logo tone="inverted" className="relative text-white" />

        <div className="relative max-w-md space-y-8">
          <div className="space-y-3">
            <p className="text-3xl leading-tight font-semibold tracking-tight">Dados claros. Decisões melhores.</p>
            <p className="text-base text-white/70">
              Acompanhe os números do seu negócio sem planilhas complicadas nem ferramentas difíceis.
            </p>
          </div>
          <ul className="space-y-4">
            {highlights.map(({ icon: Icon, text }) => (
              <li key={text} className="flex items-start gap-3 text-sm text-white/85">
                <span className="mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-md bg-white/10">
                  <Icon className="size-4" aria-hidden="true" />
                </span>
                {text}
              </li>
            ))}
          </ul>
        </div>

        <p className="relative text-xs text-white/50">© {currentYear} Datavello</p>
      </aside>

      <main className="flex items-center justify-center px-4 py-10 sm:px-8">
        <div className="w-full max-w-sm space-y-8">
          <Logo className="lg:hidden" />
          <div className="space-y-1.5">
            <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
            <p className="text-sm text-muted-foreground">{description}</p>
          </div>
          {children}
        </div>
      </main>
    </div>
  )
}

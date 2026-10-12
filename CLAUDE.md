# Datavello — contexto do projeto

SaaS de BI e dashboards para pequenas empresas. Slogan: **Dados claros. Decisões melhores.**
Especificação completa (fonte de verdade do escopo): [docs/product-spec.md](docs/product-spec.md).

## Essencial

- UI em **pt-BR**, moeda **BRL**, datas **DD/MM/AAAA**. Código (classes, variáveis, funções) em **inglês**.
- MVP **real**: persistência, autenticação e funcionalidades integradas ao backend. Nada de dados fictícios hardcoded em componentes, nem dados falsos escondendo erros de API.
- **Multi-tenant** desde o início: todo dado empresarial pertence a uma Company/Tenant. A empresa ativa é resolvida no backend a partir do usuário autenticado e de sua associação validada — nunca confiar em `tenantId` vindo do frontend. Autorização em todo CRUD; testes de isolamento entre empresas são obrigatórios.
- Valores monetários com `decimal` (C#) / `numeric` (PostgreSQL). Nunca ponto flutuante binário.
- Indicador “**saldo do período**” — não chamar de lucro nem de fluxo de caixa.
- Monólito modular, frontend e backend separados. Sem microserviços.

## Stack

- **Frontend:** React + TypeScript + Vite, Tailwind CSS, shadcn/ui, Recharts, React Router, TanStack Query, React Hook Form + Zod, Lucide React.
- **Backend:** C# ASP.NET Core Web API, EF Core, PostgreSQL, Swagger/OpenAPI, ASP.NET Core Identity.
- **Dev:** PostgreSQL 18 nativo no Windows (Docker Compose opcional), `.env` local + `.env.example` sem segredos, migrations EF Core, `dotnet-ef` fixado em `backend/dotnet-tools.json`. Ambiente: Windows.
- **Versões em uso:** .NET 10, EF Core/Npgsql 10, React 19, Vite 8, Tailwind 4, shadcn/ui (estilo base-nova, Base UI), Recharts 3, React Router 7, TanStack Query 5, Zod 4, Vitest 5.

## Estrutura

```text
datavello/
├── frontend/
├── backend/
├── docs/   (product-spec.md, architecture.md, implementation-plan.md, data-import.md)
├── scripts/ (setup.ps1, dev.ps1)
├── docker-compose.yml
├── .env.example
├── .gitignore
└── README.md
```

## Identidade visual

SaaS B2B limpo: fundo claro, neutros, **azul** como cor principal; verde = positivo, vermelho = alerta (com moderação). Cards com borda discreta e sombra sutil. Sem excesso de gradientes. Logotipo original em SVG local (funciona em fundo claro e escuro). Sidebar recolhível (Dashboard, Transações, Categorias, Importar dados, Personalizar dashboard, Configurações, Perfil) + topbar com título, seletor de período e menu do usuário. Responsivo; estados de loading, erro, vazio e sucesso.

## Etapas (todas concluídas — detalhes em `docs/implementation-plan.md`)

1. ✅ Inspeção e planejamento → `docs/architecture.md`, `docs/implementation-plan.md`
2. ✅ Fundação (estrutura, PostgreSQL, migrations, UI base, auth, modelo multiempresa)
3. ✅ Funcionalidades centrais (transações, categorias, indicadores, dashboard, filtros, paginação)
4. ✅ Importação CSV/XLSX (mapeamento, prévia, validação, histórico) e personalização de widgets — regras em `docs/data-import.md`
5. ✅ Testes e acabamento (configurações, perfil, revisão de segurança, responsividade, Vitest)
6. ✅ Execução local no Windows (README passo a passo, `scripts/setup.ps1`, `scripts/dev.ps1`)

Fora do escopo do MVP: cobrança, planos, integrações ERP/bancárias, IA, app mobile, editor avançado de dashboards, marketplace, permissões complexas, banco por cliente.

## Estado atual (12/10/2026)

**MVP completo e verificado.** Repositório: https://github.com/matheusodev/datavello (privado, branch `main`, um commit por etapa; último: `b00f372` Stage 6). Testes: 153 backend (xUnit, PostgreSQL real) + 31 frontend (Vitest); `npm audit` e NuGet sem vulnerabilidades. Instalação do zero conferida seguindo o README.

**Rodar:** `powershell -ExecutionPolicy Bypass -File scripts\dev.ps1` → http://localhost:5173 (API em :5080, Swagger em /swagger). Login demo (somente local): `demo@datavello.local` / `Demo12345`. Recriar demo: `dotnet run --project src/Datavello.Api -- seed-demo --reset` (em `backend/`).

**Próximos passos possíveis (backlog, nada iniciado):**
- Convites de usuários para a empresa e troca de empresa ativa na UI (o modelo `CompanyMember` e o cabeçalho `X-Company-Id` já suportam várias empresas por usuário).
- Troca de e-mail com confirmação; recuperação de senha por e-mail.
- Publicação em produção: CSP no servidor do frontend, `ForwardedHeaders` atrás de proxy, segredos em cofre, CORS restrito (ver `docs/architecture.md` §7).
- Processamento de importações grandes em segundo plano (hoje síncrono, limite de 5.000 linhas).
- Novos módulos previstos na especificação: vendas, estoque, serviços, metas.

## Convenções técnicas estabelecidas

- **Backend por feature:** `backend/src/Datavello.Api/Features/<Módulo>/` (Controller enxuto + Service + DTOs); infraestrutura em `Infrastructure/` (Data, Tenancy, Security, Errors, Seed).
- **Isolamento:** entidades de empresa implementam `ITenantOwned`; o `AppDbContext` aplica filtro global por empresa e bloqueia escrita cruzada; services chamam `tenant.RequireCompanyId()`. Recurso de outra empresa responde 404. Todo recurso novo precisa de teste de isolamento entre empresas.
- **Segurança:** cookie HttpOnly `SameSite=Strict`; requisições que alteram estado exigem o cabeçalho `X-Requested-With: datavello`; `EndpointSecurityTests` falha se um endpoint novo ficar acessível sem login (públicos só health/register/login/logout); erros em `ProblemDetails` com mensagens pt-BR via `AppException`.
- **Testes de integração:** usam `datavello_test` (schema recriado a cada execução; recusa apontar para o banco principal), via `DatavelloApiFactory` + `TestDatabase` na collection "Database".
- **Frontend por feature:** `frontend/src/features/<módulo>/` (api.ts com hooks TanStack Query + páginas); rotas lazy em `src/app/router.tsx`; filtros de listas guardados na URL; componentes shadcn adicionados com `npx shadcn@4.21.4 add` (o CLI não é dependência; CSS base em `src/styles/shadcn.css`).
- **Gráficos:** cores validadas (receitas `#2a78d6`, despesas `#eb6834`), sempre com alternância "Tabela"; verde/vermelho só para variações.
- **Testes visuais no navegador:** dados de teste criados durante a verificação devem ser removidos ao final (ou `seed-demo --reset`).

## Regras de trabalho

- Etapas pequenas e verificáveis; nada de uma alteração gigantesca.
- Mudança arquitetural relevante: explicar necessidade e alternativas antes.
- Decisões técnicas simples: escolher a mais razoável e documentar, sem pedir confirmação.
- Nunca afirmar que testes/comandos funcionaram sem executá-los.
- Não adicionar dependências sem necessidade; verificar versões instaladas antes de configurar.
- Segredos nunca no repositório; logs sem senhas/tokens.
- Toda ação visível na UI deve funcionar ou estar marcada como indisponível no MVP.
- Seed de demo idempotente, claramente fictício, sem apagar dados reais.

Ao fim de cada etapa, reportar: (1) o que foi implementado, (2) arquivos criados/alterados, (3) comandos executados, (4) testes executados e resultados, (5) como executar/testar, (6) limitações e pendências.

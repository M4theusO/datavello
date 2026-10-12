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
- **Dev:** Docker Compose (PostgreSQL), `.env` local + `.env.example` sem segredos, migrations EF Core. Ambiente: Windows.

## Estrutura

```text
datavello/
├── frontend/
├── backend/
├── docs/   (product-spec.md, architecture.md, implementation-plan.md, data-import.md)
├── docker-compose.yml
├── .env.example
├── .gitignore
└── README.md
```

## Identidade visual

SaaS B2B limpo: fundo claro, neutros, **azul** como cor principal; verde = positivo, vermelho = alerta (com moderação). Cards com borda discreta e sombra sutil. Sem excesso de gradientes. Logotipo original em SVG local (funciona em fundo claro e escuro). Sidebar recolhível (Dashboard, Transações, Categorias, Importar dados, Personalizar dashboard, Configurações, Perfil) + topbar com título, seletor de período e menu do usuário. Responsivo; estados de loading, erro, vazio e sucesso.

## Etapas

1. Inspeção e planejamento → `docs/architecture.md`, `docs/implementation-plan.md`
2. Fundação (estrutura, Docker/PostgreSQL, migrations, UI base, auth, modelo multiempresa)
3. Funcionalidades centrais (transações, categorias, indicadores, dashboard, filtros, paginação)
4. Importação CSV/XLSX (mapeamento, prévia, validação, histórico) e personalização de widgets
5. Testes e acabamento (segurança, isolamento, responsividade, docs)
6. Execução local no Windows (comandos exatos no README, credenciais demo apenas locais)

Fora do escopo: cobrança, planos, integrações ERP/bancárias, IA, app mobile, editor avançado de dashboards, marketplace, permissões complexas, banco por cliente.

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

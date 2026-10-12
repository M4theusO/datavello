# Datavello — Plano de implementação

Cada etapa termina com build verde, testes executados e relatório (o que foi feito, arquivos, comandos, testes, como testar, pendências).

## Etapa 1 — Inspeção e planejamento ✅

- [x] Inspecionar ambiente e ferramentas (ver `architecture.md` §1)
- [x] `docs/architecture.md` com decisões e riscos
- [x] `docs/implementation-plan.md`

## Etapa 2 — Fundação ✅

**2a. Esqueleto do repositório** ✅
- [x] `git init`, `.gitignore`, `.env.example`, `docker-compose.yml`, README inicial
- [x] Solução .NET (`Datavello.Api` + `Datavello.Tests`) compilando, Swagger e endpoint `/api/health`
- [x] Frontend Vite + React + TS + Tailwind + shadcn compilando, proxy `/api`

**2b. Banco e modelo** ✅
- [x] Criar usuário/banco `datavello` no PostgreSQL local (passo manual documentado)
- [x] `AppDbContext`, entidades, configuração EF, filtro global por empresa
- [x] Migration inicial aplicada; seed de `DashboardTemplate`

**2c. Autenticação e multiempresa** ✅
- [x] Identity com cookie, register/login/logout/me
- [x] `ICurrentTenant`, cadastro cria empresa + categorias + dashboard padrão
- [x] Tratamento central de erros (ProblemDetails), proteção CSRF por cabeçalho
- [x] Testes de integração: cadastro, login, acesso não autenticado, isolamento básico

**2d. Layout base do frontend** ✅
- [x] Logotipo SVG, tema (tokens de cor), sidebar recolhível, topbar, menu do usuário
- [x] Telas de login e cadastro integradas; rotas protegidas; página de acesso negado/404

## Etapa 3 — Funcionalidades centrais ✅

- [x] API de categorias (CRUD, contagem de transações, exclusão bloqueada se houver vínculos)
- [x] API de transações (CRUD, busca, filtros, ordenação, paginação)
- [x] API de resumo do dashboard (cards, comparação, séries, categorias, saldo, recentes)
- [x] Telas: Categorias, Transações (tabela + formulário criar/editar + confirmação de exclusão)
- [x] Dashboard com cards, gráficos Recharts, tabela recente e filtros globais
- [x] Testes: cálculo de receitas/despesas/saldo, filtros por período, validação, isolamento

## Etapa 4 — Importação e personalização ✅

- [x] Parsers CSV (CsvHelper, detecção de delimitador `;`/`,`, UTF-8/Latin-1) e XLSX (ClosedXML)
- [x] Upload → prévia → mapeamento → validação → confirmação transacional
- [x] Detecção de duplicidade + `docs/data-import.md`
- [x] Histórico de importações com erros por linha
- [x] Personalização: ativar/desativar, reordenar, período padrão, salvar, restaurar padrão
- [x] Testes: CSV, XLSX, linhas inválidas, duplicidade, persistência dos widgets

## Etapa 5 — Testes e acabamento ✅

- [x] Configurações da empresa e perfil do usuário (incluindo troca de senha)
- [x] Revisão de segurança e isolamento (todos os endpoints)
- [x] Responsividade (desktop/tablet/celular), estados vazios, mensagens de erro
- [x] Execução completa dos testes e correções
- [x] Testes unitários do frontend (Vitest) e auditoria de dependências (0 vulnerabilidades)

## Etapa 6 — Execução local ✅

- [x] Seed de demonstração idempotente (`seed-demo`, antecipado na Etapa 3 para validar o dashboard)
- [x] README com comandos exatos para Windows (PowerShell): banco, dependências, migrations, seed, backend, frontend, testes
- [x] Verificação ponta a ponta seguindo o README do zero
- [x] `dotnet-ef` fixado no projeto (`backend/dotnet-tools.json`) e scripts `scripts/setup.ps1` / `scripts/dev.ps1`
- [x] Verificação a partir de uma cópia limpa do repositório e de um banco vazio: setup, migrations, seed, aplicação no ar, login de demonstração, dashboard e 153 + 31 testes aprovados

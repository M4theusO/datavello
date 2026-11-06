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

## Etapa 7 — Equipe e várias empresas ✅

- [x] Convites por link (token de uso único, 7 dias, só o hash no banco, vinculado ao e-mail), cancelamento e reenvio
- [x] Cadastro pelo convite (entra na empresa sem criar uma própria) e aceite com conta existente
- [x] Lista de membros, remoção pelo proprietário e saída voluntária do membro
- [x] Criação de outras empresas pelo usuário (`POST /api/companies`)
- [x] Seletor de empresa ativa na sidebar (escolha por aba, enviada em `X-Company-Id`; preferência lembrada por usuário)
- [x] Tela para usuário sem empresa; volta automática à empresa padrão quando o acesso é revogado
- [x] Testes: 11 de integração (convites, papéis, isolamento entre empresas, cabeçalho recusado) + 6 Vitest; fluxo completo verificado no navegador

## Etapa 8 — E-mail, recuperação de senha e troca de e-mail ✅

**8a. Envio de e-mail e recuperação de senha** ✅
- [x] Infraestrutura de e-mail: fila em memória + envio em segundo plano via SMTP; Mailpit em desenvolvimento (`scripts/dev.ps1` o inicia)
- [x] `App:PublicUrl` para os links (validado na inicialização)
- [x] "Esqueci minha senha" (resposta neutra) e redefinição pelo link (1 hora, uso único, encerra outras sessões, desbloqueia a conta)
- [x] Telas `/esqueci-senha` e `/redefinir-senha`; link na tela de login
- [x] Testes: 6 de integração (fluxo completo, e-mail inexistente, token adulterado/de outro usuário, senha fraca, desbloqueio, HTML escapado); fluxo verificado no navegador com o Mailpit

**8b. Convite por e-mail e troca de e-mail** ✅
- [x] Convite enviado por e-mail (o link continua visível para o proprietário compartilhar)
- [x] Troca de e-mail: senha atual + link no novo endereço, confirmação por clique, aviso ao endereço antigo, sessões encerradas
- [x] Testes: 5 de integração (fluxo completo, senha/e-mail inválidos, link adulterado ou de outra conta, e-mail ocupado antes da confirmação, e-mail do convite); fluxo verificado no navegador com o Mailpit

## Etapa 9 — Confirmação de e-mail no cadastro e limite de empresas ✅

- [x] Cadastro sem sessão até confirmar o e-mail (link de 1 hora; confirmação por clique já inicia a sessão; link reutilizado não cria sessão)
- [x] Login de conta não confirmada: aviso só para quem acerta a senha, com reenvio do link; bloqueio por tentativas respeitado
- [x] Reenvio de confirmação com resposta neutra; redefinição de senha também confirma o e-mail
- [x] Migration que marca as contas existentes como confirmadas
- [x] Limite de 3 empresas próprias por usuário; "Nova empresa" desabilitada no limite (`canCreateCompany` em `/api/auth/me`)
- [x] Testes: 8 novos de integração e ajuste dos existentes para o cadastro confirmado; fluxo verificado no navegador com o Mailpit

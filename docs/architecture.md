# Datavello — Arquitetura

> Escopo e requisitos: [product-spec.md](product-spec.md). Plano de execução: [implementation-plan.md](implementation-plan.md).

## 1. Ambiente inspecionado (12/10/2026)

| Ferramenta | Versão encontrada | Observação |
| --- | --- | --- |
| Windows | 10 Pro 19045 | Máquina de desenvolvimento |
| Node.js | 22.21.0 | LTS, atende Vite 8 |
| npm | 10.9.4 | Gerenciador de pacotes do frontend |
| .NET SDK | 10.0.201 | .NET 10 (LTS) |
| dotnet-ef | 10.0.5 | Já instalado como ferramenta global |
| PostgreSQL | 18.3 (nativo, serviço `postgresql-x64-18`, porta 5432) | Autenticação `scram-sha-256` |
| Docker | **não instalado** | WSL também não está instalado |
| Git | 2.51.1 | Diretório ainda não era repositório |

O diretório do projeto continha apenas `CLAUDE.md` e `docs/product-spec.md`.

## 2. Visão geral

Monólito modular com duas aplicações independentes:

```text
┌──────────────────────┐   /api (proxy do Vite em dev)   ┌───────────────────────────┐
│ frontend (React SPA) │ ───────────────────────────────▶ │ backend (ASP.NET Core 10) │
│ Vite · TS · Tailwind │   cookie HttpOnly de sessão      │ Controllers → Services    │
└──────────────────────┘                                  │ EF Core → PostgreSQL      │
                                                          └───────────────────────────┘
```

- **Sem microserviços.** Um único processo de API, dividido internamente por módulos (features).
- Em desenvolvimento o Vite faz proxy de `/api` para o backend, então navegador e API ficam na mesma origem — o cookie de autenticação funciona sem CORS. CORS fica configurado por lista explícita de origens (`Cors:AllowedOrigins`) para cenários em domínios distintos.

## 3. Stack e versões-alvo

**Backend** (.NET 10): ASP.NET Core Web API (controllers), EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x, ASP.NET Core Identity, Swashbuckle (Swagger UI), `CsvHelper` (CSV), `ClosedXML` (XLSX), xUnit + `Microsoft.AspNetCore.Mvc.Testing` (testes).

**Frontend**: React 19, TypeScript, Vite 8, Tailwind CSS 4, shadcn/ui, Recharts 3, React Router 7, TanStack Query 5, React Hook Form 7 + Zod 4, Lucide React.

Versões exatas serão fixadas nos arquivos de projeto/lockfiles no momento da instalação.

## 4. Organização do código

```text
datavello/
├── backend/
│   ├── Datavello.slnx
│   ├── src/Datavello.Api/
│   │   ├── Program.cs
│   │   ├── Domain/             # Entidades e enums (sem dependência de EF/HTTP)
│   │   ├── Infrastructure/     # DbContext, configurações EF, migrations, tenant, seed, erros
│   │   └── Features/           # Um módulo por recurso
│   │       ├── Auth/           #   Controller + Service + DTOs
│   │       ├── Companies/
│   │       ├── Categories/
│   │       ├── Transactions/
│   │       ├── Dashboard/      #   Indicadores + configuração de widgets
│   │       └── Imports/        #   Parsers CSV/XLSX, mapeamento, validação, histórico
│   └── tests/Datavello.Tests/  # Unitários + integração (API real contra PostgreSQL de teste)
├── frontend/
│   └── src/
│       ├── app/                # Rotas, providers, layout (sidebar/topbar)
│       ├── components/         # ui/ (shadcn) + componentes compartilhados
│       ├── features/           # auth, dashboard, transactions, categories, imports, settings
│       └── lib/                # cliente HTTP, formatação BRL/datas, utilitários
├── docs/
├── docker-compose.yml
├── .env.example
└── README.md
```

**Decisão:** um único projeto de API organizado por features, em vez de vários projetos (Domain/Application/Infrastructure). Para o tamanho do MVP, a separação por pastas mantém as camadas claras (controllers enxutos → services → EF) sem a cerimônia de múltiplos assemblies. Se um módulo crescer, ele pode ser extraído para um projeto próprio sem mudar contratos.

## 5. Modelo de dados

Todas as chaves primárias são `Guid` (UUID v7, ordenável), o que evita enumeração de IDs.

| Entidade | Escopo | Campos principais |
| --- | --- | --- |
| `User` (Identity) | Global | Email, FullName, hash de senha (Identity) |
| `Company` | Global | Name, Document (opcional), Currency = BRL, CreatedAt |
| `CompanyMember` | Global | CompanyId, UserId, Role (`Owner`, `Member`), CreatedAt — único (CompanyId, UserId) |
| `Category` | Empresa | Name, Kind (`Income`, `Expense`, `Both`), IsDefault — único (CompanyId, Name) |
| `Transaction` | Empresa | Date (`date`), Description, Type (`Income`/`Expense`), Amount (`numeric(18,2)`, sempre positivo), CategoryId, Notes, ImportJobId?, CreatedAt, UpdatedAt |
| `ImportJob` | Empresa | FileName, FileType, Status, UserId, TotalRows, ImportedRows, RejectedRows, staging (jsonb), mapeamento/opções (jsonb), CreatedAt, CompletedAt |
| `ImportRowError` | Empresa | ImportJobId, RowNumber, Field, Message, RawValue |
| `DashboardTemplate` | Global | Key, Name, DefaultPeriod, widgets padrão em ordem (`text[]`) |
| `Dashboard` | Empresa | Name, TemplateId, DefaultPeriod, IsDefault |
| `WidgetConfig` | Empresa | DashboardId, WidgetType, Position, IsEnabled, Settings (jsonb) |

- **Valores monetários:** `decimal` em C#, `numeric(18,2)` no PostgreSQL, `string`/`number` formatado só na borda do frontend. Cálculos agregados (somas, saldo) são feitos no banco via `SUM` em `numeric`.
- **Tipo da transação** é explícito; o valor é sempre positivo. Saldo = Σ receitas − Σ despesas.
- **Índices:** `(CompanyId, Date)`, `(CompanyId, CategoryId)`, `(CompanyId, Type, Date)` em `Transaction`; índices de unicidade citados acima.
- **Exclusão de categoria:** bloqueada (409) se houver transações vinculadas; o usuário precisa reclassificá-las antes. FK com `Restrict`.

## 6. Multi-tenancy e isolamento

Modelo: **banco compartilhado, esquema compartilhado, coluna `CompanyId`** em toda entidade empresarial (interface `ITenantOwned`).

Resolução da empresa ativa (`ICurrentTenant`, por requisição):

1. O usuário precisa estar autenticado (cookie de sessão do Identity).
2. O backend carrega as associações `CompanyMember` do usuário.
3. Se o cliente enviar o cabeçalho `X-Company-Id`, ele só é aceito se existir associação válida; caso contrário a resposta é **403**. Sem cabeçalho, usa-se a empresa padrão (primeira associação).
4. Sem nenhuma associação → **403** com orientação para criar a empresa.

Defesa em profundidade:

- **Filtro global de consulta do EF Core** em todas as entidades `ITenantOwned` (`e.CompanyId == currentTenant.CompanyId`). Uma consulta que esqueça o filtro continua isolada.
- **Na gravação**, o `SaveChanges` define `CompanyId` em entidades novas e rejeita alterações em entidades de outra empresa.
- **Referências cruzadas validadas:** ao criar/editar uma transação, a `CategoryId` informada é buscada pelo filtro do tenant; se não existir na empresa ativa → 400/404. O `CompanyId` nunca é lido do corpo da requisição.
- Recursos de outra empresa respondem **404** (não revelam existência).
- Testes de integração criam duas empresas e verificam leitura, edição, exclusão, referências cruzadas e importação entre elas.

Vários usuários por empresa já são suportados pelo modelo (`CompanyMember.Role`); convites e permissões finas ficam fora do MVP.

## 7. Autenticação e segurança

- **ASP.NET Core Identity** (hash PBKDF2 padrão, política de senha mínima de 8 caracteres com letras e números, lockout após tentativas falhas).
- **Sessão por cookie** `HttpOnly`, `Secure` (fora de dev HTTP), `SameSite=Strict`. Não há token em `localStorage`, o que reduz o impacto de XSS. Endpoints da API respondem 401/403 em vez de redirecionar.
- **CSRF:** `SameSite=Strict` impede envio do cookie em requisições cross-site; além disso, métodos que alteram estado exigem o cabeçalho `X-Requested-With: datavello` (formulários HTML cross-site não conseguem enviá-lo sem preflight CORS, que só é aceito para origens permitidas).
- Endpoints: `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me`. O cadastro cria usuário + empresa + associação `Owner` + categorias padrão + dashboard padrão em uma única transação.
- **Erros:** `ProblemDetails` (RFC 9457) via `IExceptionHandler` central; exceções inesperadas retornam mensagem genérica, detalhes só no log.
- **Logs:** sem senhas, cookies ou conteúdo de planilhas.
- **Segredos:** connection string e senhas em `.env` (ignorado pelo Git) ou variáveis de ambiente; `.env.example` só com placeholders. Em desenvolvimento o backend carrega `.env` da raiz com um leitor mínimo próprio (sem dependência extra).
- Swagger exposto apenas em `Development`.

## 8. API (convenções)

- REST por recurso sob `/api`, JSON em camelCase, datas `yyyy-MM-dd` na API (a UI formata `DD/MM/AAAA`), valores monetários como número decimal.
- Listagens paginadas: `?page=1&pageSize=20` (máx. 100) → `{ items, page, pageSize, totalCount, totalPages }`.
- Filtros comuns: `from`, `to`, `categoryId`, `type`, `search`, `sortBy` (`date`|`amount`), `sortDir`.
- Recursos previstos: `/api/auth`, `/api/company`, `/api/profile`, `/api/categories`, `/api/transactions`, `/api/dashboard/summary`, `/api/dashboard/config`, `/api/imports`.

## 9. Dashboard e indicadores

- Um endpoint de resumo (`GET /api/dashboard/summary?from&to&categoryId&type`) devolve cards, série mensal, despesas por categoria, saldo acumulado e atividade recente — tudo calculado no PostgreSQL com o mesmo conjunto de filtros.
- **Comparação com período anterior:** mesma duração imediatamente anterior ao intervalo; só é exibida se o período anterior tiver transações.
- O indicador é rotulado **“Saldo do período”**, com nota de que reflete apenas os dados cadastrados (não é lucro contábil nem fluxo de caixa completo).
- Personalização: catálogo fixo de widgets no código (`kpi-income`, `kpi-expense`, `kpi-balance`, `kpi-count`, `chart-monthly`, `chart-expense-by-category`, `chart-income-vs-expense`, `chart-balance-history`, `table-recent`). O usuário ativa/desativa, reordena e define o período padrão; “Restaurar padrão” recria os `WidgetConfig` a partir do `DashboardTemplate`.

## 10. Importação CSV/XLSX

Fluxo em três chamadas, com estado salvo em `ImportJob`:

1. **Upload** `POST /api/imports` (multipart): valida extensão (`.csv`, `.xlsx`), tamanho (`Import:MaxFileSizeBytes`, padrão 5 MB) e número de linhas (`Import:MaxRows`, padrão 5.000). Lê a primeira planilha/arquivo, guarda as linhas brutas como texto em staging (jsonb) e devolve cabeçalhos, prévia (20 linhas) e sugestão de mapeamento por cabeçalhos comuns (`data`, `descrição`, `histórico`, `valor`, `tipo`, `categoria`…). O arquivo em si não é armazenado.
2. **Validação** `POST /api/imports/{id}/validate` com mapeamento e opções (formato de data, separador decimal, convenção de tipo: coluna de tipo **ou** sinal do valor). Retorna contagem de válidas/inválidas e erros por linha (linha, campo, mensagem, valor original).
3. **Confirmação** `POST /api/imports/{id}/commit`: revalida no servidor e grava **todas as linhas válidas em uma única transação de banco**; linhas inválidas são registradas em `ImportRowError` e o status final é `Completed` ou `CompletedWithErrors`. Se nada for válido, nada é gravado (`Failed`). O resultado mostra exatamente quantas linhas entraram e quantas não.

Segurança: `.xlsm`/`.xls` são rejeitados; ClosedXML só lê valores de células (macros nunca são executadas); fórmulas usam o valor em cache; arquivos malformados geram erro 400 tratado; limites de tamanho/linhas também protegem contra arquivos compactados abusivos. Categoria inexistente: erro por linha (opção de criá-la automaticamente pode ser adicionada depois).

**Duplicidade** (regra detalhada em `docs/data-import.md`): uma linha é considerada duplicada quando já existe, na mesma empresa, uma transação com mesma data, mesmo tipo, mesmo valor e mesma descrição normalizada (minúsculas, espaços colapsados, sem acentos) — seja no banco, seja em linha anterior do mesmo arquivo. Duplicadas são **rejeitadas e reportadas**, nunca excluem registros existentes.

## 11. Dados de demonstração

Comando explícito, apenas em `Development`: `dotnet run --project src/Datavello.Api -- seed-demo` (ou `seed-demo --reset` para recriar). Idempotente: se o usuário `demo@datavello.local` já existe, não faz nada. O `--reset` apaga somente as empresas cujo dono é o usuário de demonstração (e recusa se houver outros membros) e as recria. Cerca de 14 meses de movimentações fictícias de uma padaria, com a observação "Dado fictício de demonstração.". Senha padrão `Demo12345`, configurável por `Demo__Password` no `.env`; apenas para ambiente local.

## 12. Testes

- **Unitários:** parsers CSV/XLSX, conversão de data/valor/tipo, detecção de duplicidade, cálculo de período anterior.
- **Integração:** API real (`WebApplicationFactory`) contra um banco PostgreSQL de teste (`datavello_test`, recriado a cada execução): autenticação, CRUD, filtros, indicadores, importação, persistência de widgets e isolamento entre empresas.
- Como Docker não está disponível, os testes usam o PostgreSQL local via `ConnectionStrings__Test`. Testcontainers pode ser adotado quando Docker estiver instalado.

## 13. Visualização de dados

- Cores das séries validadas com verificador de paleta (daltonismo e contraste): receitas `#2a78d6`, despesas `#eb6834`, sempre as mesmas em todos os gráficos. Verde/vermelho ficam reservados para variações (deltas) e tipo da transação, sempre com ícone/sinal além da cor.
- Formas: linhas para evolução mensal, colunas agrupadas para o comparativo, área para saldo acumulado e barras horizontais ordenadas para despesas por categoria (máx. 6 barras; o restante vira "Outras").
- Todo gráfico tem tooltip e uma alternância "Tabela", para que nenhum valor dependa só do hover ou da cor.
- Filtros (período, categoria, tipo) ficam numa única linha acima do conteúdo, valem para todos os blocos e são guardados na URL.
- Variação do saldo é mostrada em R$ (não em %), pois o saldo pode mudar de sinal.

## 14. Decisões e riscos

| # | Decisão / risco | Tratamento |
| --- | --- | --- |
| D1 | **Docker não instalado** | `docker-compose.yml` é entregue conforme pedido (PostgreSQL 18), mas a execução local usa o PostgreSQL 18 nativo já em execução. Ambos funcionam com a mesma connection string. Instalar Docker Desktop é opcional. |
| D2 | Cookie em vez de JWT | Mais seguro para SPA na mesma origem; JWT pode ser adicionado para clientes externos no futuro. |
| D3 | Um projeto de API com features | Simplicidade; extração posterior possível. |
| D4 | Validação com DataAnnotations + regras nos services | Evita dependência extra (FluentValidation) no MVP. |
| D5 | Staging de importação em jsonb | Evita armazenar arquivos em disco e simplifica a limpeza; limite de linhas mantém o tamanho controlado. Jobs pendentes com mais de 24 h podem ser limpos. |
| R1 | Senha do usuário `postgres` local é desconhecida | Necessária uma única vez para criar o usuário/banco `datavello` (passo manual documentado no README). |
| R2 | Formatos variados de planilhas brasileiras (`1.234,56`, `DD/MM/AAAA`, valores negativos) | Opções explícitas no mapeamento + testes com amostras reais. |
| R3 | Vazamento entre empresas por consulta esquecida | Filtro global do EF + validação de referências + testes de isolamento. |
| R4 | Versões muito novas (Vite 8, Tailwind 4, React Router 7) | Fixar versões no lockfile e validar build a cada etapa. |

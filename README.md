# Datavello

**Dados claros. Decisões melhores.**

Plataforma SaaS de BI e dashboards para pequenas empresas: cadastre receitas e despesas, importe planilhas Excel/CSV e acompanhe indicadores e gráficos da sua empresa. Frontend em React + Vite, backend em ASP.NET Core 10 e PostgreSQL.

> **Status:** MVP completo (Etapas 1 a 6).
> Documentação: [arquitetura](docs/architecture.md) · [plano de implementação](docs/implementation-plan.md) · [regras de importação](docs/data-import.md) · [especificação do produto](docs/product-spec.md)

## Funcionalidades

- Conta de usuário com empresa própria; dados de cada empresa totalmente isolados.
- Transações (receitas e despesas) com busca, filtros, ordenação e paginação.
- Categorias por empresa, com categorias padrão já criadas no cadastro.
- Dashboard com receitas, despesas, saldo do período, comparação com o período anterior, gráficos e atividade recente.
- Importação de planilhas `.csv` e `.xlsx` com mapeamento de colunas, prévia, validação por linha, detecção de duplicidades e histórico.
- Personalização do dashboard (widgets, ordem e período padrão), configurações da empresa e perfil com troca de senha.

---

## Guia de execução no Windows

Todos os comandos são para o **PowerShell**, executados a partir da pasta raiz do projeto (`datavello`), salvo indicação.

### 1. Pré-requisitos

| Ferramenta | Versão | Download |
| --- | --- | --- |
| .NET SDK | 10.0 | <https://dotnet.microsoft.com/download/dotnet/10.0> |
| Node.js (inclui npm) | 22 LTS (mínimo 20) | <https://nodejs.org> |
| PostgreSQL | 16 ou superior (testado com 18) | <https://www.postgresql.org/download/windows/> — **ou** Docker Desktop |
| Git | qualquer recente | <https://git-scm.com/download/win> |

Confira as versões:

```powershell
dotnet --version   # 10.x
node --version     # v22.x
```

A ferramenta de migrations (`dotnet-ef`) não precisa ser instalada globalmente: ela está fixada no projeto (`backend/dotnet-tools.json`) e é instalada pelo `dotnet tool restore`.

### 2. Obter o código

```powershell
git clone https://github.com/matheusodev/datavello.git
cd datavello
```

### 3. Banco de dados

O Datavello usa um usuário próprio no PostgreSQL (`datavello`), em vez do administrador `postgres`. Escolha a senha desse usuário agora; ela será usada no passo 4.

#### Opção A — PostgreSQL instalado no Windows

Execute os dois comandos SQL abaixo **separadamente** (o `CREATE DATABASE` não pode rodar junto com outro comando), trocando `SUA_SENHA`:

```sql
CREATE ROLE datavello LOGIN PASSWORD 'SUA_SENHA' CREATEDB;
```

```sql
CREATE DATABASE datavello OWNER datavello;
```

Onde executar:

- **pgAdmin:** clique com o botão direito no servidor → *Query Tool* → cole um comando e pressione **F5**; repita com o segundo.
- **psql:** (pedirá a senha do usuário `postgres` definida na instalação)

  ```powershell
  & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost -c "CREATE ROLE datavello LOGIN PASSWORD 'SUA_SENHA' CREATEDB;"
  & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost -c "CREATE DATABASE datavello OWNER datavello;"
  ```

A permissão `CREATEDB` permite que os testes automatizados criem o banco separado `datavello_test`.

#### Opção B — Docker

Faça primeiro o passo 4 (o `docker-compose.yml` lê a senha do `.env`) e depois:

```powershell
docker compose up -d
```

O contêiner já cria o usuário e o banco `datavello`. Se já existir um PostgreSQL instalado usando a porta 5432, defina `POSTGRES_PORT=5433` no `.env` e use `Port=5433` nas connection strings.

### 4. Configuração (`.env`)

```powershell
Copy-Item .env.example .env
Copy-Item frontend\.env.example frontend\.env
notepad .env
```

No `.env`, troque **todas** as ocorrências de `change-me` pela senha escolhida no passo 3 e salve. O arquivo `.env` contém senhas e **não é enviado ao Git**.

### 5. Instalar, preparar o banco e criar os dados de demonstração

**Opção rápida (script):**

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup.ps1
```

O script confere as versões das ferramentas, restaura os pacotes do backend e do frontend, aplica as migrations e cria os dados de demonstração. Use `-SkipDemo` para não criar os dados fictícios. O `-ExecutionPolicy Bypass` vale apenas para essa execução e não altera configurações do Windows.

**Opção manual (comandos equivalentes):**

```powershell
cd backend
dotnet tool restore                                       # instala o dotnet-ef do projeto
dotnet restore
dotnet ef database update --project src/Datavello.Api     # cria as tabelas (migrations)
dotnet run --project src/Datavello.Api -- seed-demo       # dados de demonstração (opcional)
cd ..\frontend
npm ci
cd ..
```

### 6. Iniciar a aplicação

**Opção rápida:** abre o backend e o frontend em duas janelas.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\dev.ps1
```

**Opção manual:** em dois terminais separados.

```powershell
# Terminal 1 — backend
cd backend
dotnet run --project src/Datavello.Api
```

```powershell
# Terminal 2 — frontend
cd frontend
npm run dev
```

Para parar, pressione `Ctrl+C` em cada terminal (ou feche as janelas).

### 7. Acessar

| O quê | Endereço |
| --- | --- |
| **Aplicação** | <http://localhost:5173> |
| Documentação da API (Swagger, só em desenvolvimento) | <http://localhost:5080/swagger> |
| Verificação da API | <http://localhost:5080/api/health> |

Entre com a conta de demonstração ou crie a sua em **Criar conta grátis**:

| Ambiente | E-mail | Senha |
| --- | --- | --- |
| **Somente local** | `demo@datavello.local` | `Demo12345` |

A conta de demonstração tem a empresa fictícia **Padaria Pão Dourado (dados fictícios)** com cerca de 14 meses de movimentações. **Nunca use essas credenciais fora do ambiente local**; o comando de demonstração se recusa a rodar fora do ambiente de desenvolvimento. Para alterar a senha de demonstração, defina `Demo__Password` no `.env` antes de criar os dados.

Para recriar os dados de demonstração do zero (apaga **apenas** a empresa e o usuário de demonstração):

```powershell
cd backend
dotnet run --project src/Datavello.Api -- seed-demo --reset
```

### 8. Executar os testes

```powershell
cd backend
dotnet test                 # testes unitários e de integração da API (PostgreSQL real)
cd ..\frontend
npm test                    # testes unitários do frontend (Vitest)
npm run lint                # análise estática
npm run build               # checagem de tipos e build de produção
```

Os testes de integração usam o banco **`datavello_test`** (`ConnectionStrings__Test` no `.env`). Ele é criado automaticamente e **todas as suas tabelas são apagadas e recriadas** a cada execução; por isso os testes se recusam a rodar se essa variável apontar para o mesmo banco da aplicação.

---

## Solução de problemas

| Sintoma | Causa provável e solução |
| --- | --- |
| `password authentication failed for user "datavello"` | A senha no `.env` não é a mesma do `CREATE ROLE`. Corrija o `.env` (todas as ocorrências) ou redefina a senha no pgAdmin: `ALTER ROLE datavello PASSWORD 'NOVA_SENHA';` |
| `ConnectionStrings__Default is not set` / `Connection string 'Default' is not configured` | O arquivo `.env` não existe na raiz do projeto. Refaça o passo 4. |
| `database "datavello" does not exist` | Falta o segundo comando do passo 3 (`CREATE DATABASE`). |
| Esqueci a senha do usuário `postgres` | Use o pgAdmin, se ele já estiver conectado ao servidor, para executar os comandos do passo 3. |
| `A porta 5080 (ou 5173) já está em uso` / `Port 5173 is in use` | Já existe um backend/frontend aberto. Feche a janela antiga ou finalize-o: `Get-NetTCPConnection -LocalPort 5080 -State Listen \| ForEach-Object { Stop-Process -Id $_.OwningProcess }` |
| A aplicação abre, mas mostra *"Não foi possível conectar ao servidor"* | O backend não está rodando (passo 6, terminal 1). |
| Em desenvolvimento, a página recarrega sozinha na primeira visita a uma tela | Comportamento normal do Vite ao otimizar dependências pela primeira vez; acontece uma vez e não existe no build de produção. |
| `scripts\setup.ps1 cannot be loaded because running scripts is disabled` | Execute com `powershell -ExecutionPolicy Bypass -File ...`, como indicado, ou use a opção manual. |
| `dotnet ef` não encontrado | Rode `dotnet tool restore` dentro da pasta `backend`. |
| `MSB3021` / `The process cannot access the file ... Datavello.Api.exe` ao compilar ou testar | O backend está rodando e o Windows bloqueia o arquivo. Pare o backend (`Ctrl+C` na janela dele) e rode o comando de novo. |

---

## Referência

### Configurações opcionais

Podem ser definidas no `.env` (desenvolvimento) ou como variáveis de ambiente:

| Variável | Padrão | Uso |
| --- | --- | --- |
| `Import__MaxFileSizeBytes` | 5242880 (5 MB) | Tamanho máximo de planilha |
| `Import__MaxRows` | 5000 | Linhas de dados por arquivo |
| `RateLimiting__AuthPermitPerMinute` | 10 | Tentativas de login/cadastro por IP por minuto |
| `Auth__SecurityStampValidationSeconds` | 60 | Tempo máximo até outras sessões caírem após troca de senha |
| `Cors__AllowedOrigins__0` | — | Origem do frontend, se servido em outro domínio |
| `Demo__Password` | Demo12345 | Senha do usuário de demonstração (somente local) |

### Estrutura do projeto

```text
datavello/
├── backend/            # API ASP.NET Core 10 (src/Datavello.Api) e testes (tests/Datavello.Tests)
├── frontend/           # SPA React + TypeScript + Vite
├── docs/               # arquitetura, plano, regras de importação e especificação
├── scripts/            # setup.ps1 e dev.ps1 (Windows)
├── docker-compose.yml  # PostgreSQL em contêiner (opcional)
└── .env.example        # modelo de configuração local (sem segredos)
```

### Desenvolvimento

Nova migration após alterar o modelo de dados:

```powershell
cd backend
dotnet ef migrations add NomeDaMudanca --project src/Datavello.Api --output-dir Infrastructure/Data/Migrations
```

Novo componente de interface ([shadcn/ui](https://ui.shadcn.com); os componentes ficam em `frontend/src/components/ui` e fazem parte do código):

```powershell
cd frontend
npx shadcn@4.21.4 add <componente>
```

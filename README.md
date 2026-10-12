# Datavello

**Dados claros. Decisões melhores.**

Plataforma SaaS de BI e dashboards para pequenas empresas. Frontend em React + Vite, backend em ASP.NET Core 10 e PostgreSQL.

> Status: **Etapa 5 concluída.** MVP completo: login, multiempresa, transações, categorias, dashboard, importação CSV/XLSX, personalização, configurações e perfil. Próxima: roteiro final de execução local no Windows (Etapa 6).
> Arquitetura: [docs/architecture.md](docs/architecture.md) · Plano: [docs/implementation-plan.md](docs/implementation-plan.md) · Importação: [docs/data-import.md](docs/data-import.md)

## Pré-requisitos

| Ferramenta | Versão testada |
| --- | --- |
| .NET SDK | 10.0 |
| Node.js / npm | 22.x / 10.x |
| PostgreSQL | 18 (nativo **ou** via Docker) |
| dotnet-ef | 10.x (`dotnet tool install --global dotnet-ef`) |

Todos os comandos abaixo são para **PowerShell**, a partir da raiz do repositório.

## 1. Configuração

```powershell
Copy-Item .env.example .env
Copy-Item frontend\.env.example frontend\.env
```

Edite `.env` e troque `change-me` por uma senha forte (a mesma em `POSTGRES_PASSWORD` e nas connection strings).

## 2. Banco de dados

### Opção A — PostgreSQL nativo do Windows (já instalado)

Crie o usuário e os bancos uma única vez. O `psql` pedirá a senha do usuário `postgres` definida na instalação do PostgreSQL:

```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost -c "CREATE ROLE datavello LOGIN PASSWORD 'SUA_SENHA' CREATEDB;"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost -c "CREATE DATABASE datavello OWNER datavello;"
```

Use em `SUA_SENHA` o mesmo valor colocado no `.env`. A permissão `CREATEDB` é usada pelos testes de integração para criar e recriar o banco `datavello_test`.

### Opção B — Docker

```powershell
docker compose up -d
```

Se já houver um PostgreSQL nativo na porta 5432, defina `POSTGRES_PORT=5433` no `.env` e ajuste a porta nas connection strings.

## 3. Backend

```powershell
cd backend
dotnet restore
dotnet ef database update --project src/Datavello.Api   # aplica as migrations
dotnet run --project src/Datavello.Api
```

Para criar uma nova migration após alterar o modelo:

```powershell
dotnet ef migrations add NomeDaMudanca --project src/Datavello.Api --output-dir Infrastructure/Data/Migrations
```

- API: <http://localhost:5080>
- Swagger (apenas em desenvolvimento): <http://localhost:5080/swagger>
- Health check: <http://localhost:5080/api/health>

### Dados de demonstração (opcional, só em desenvolvimento)

```powershell
dotnet run --project src/Datavello.Api -- seed-demo          # cria (não faz nada se já existir)
dotnet run --project src/Datavello.Api -- seed-demo --reset  # apaga e recria apenas os dados de demonstração
```

Cria a empresa fictícia **Padaria Pão Dourado (dados fictícios)** com ~14 meses de movimentações.

| Ambiente | E-mail | Senha |
| --- | --- | --- |
| Somente local | `demo@datavello.local` | `Demo12345` (altere com `Demo__Password` no `.env`) |

Nunca use essas credenciais fora do ambiente local. O comando recusa rodar fora de `Development`.

## 4. Frontend

Em outro terminal:

```powershell
cd frontend
npm install
npm run dev
```

Acesse <http://localhost:5173>. As chamadas a `/api` são encaminhadas para o backend pelo proxy do Vite.

## 5. Testes

```powershell
cd backend
dotnet test
```

Os testes de integração usam o banco `datavello_test` (connection string `ConnectionStrings__Test` no `.env`), que é **apagado e recriado** a cada execução. Nunca aponte essa variável para o banco principal.

```powershell
cd frontend
npm test        # testes unitários (Vitest)
npm run lint
npm run build
```

## 6. Configurações opcionais

Todas podem ser definidas no `.env` (desenvolvimento) ou como variáveis de ambiente:

| Variável | Padrão | Uso |
| --- | --- | --- |
| `Import__MaxFileSizeBytes` | 5242880 (5 MB) | Tamanho máximo de planilha |
| `Import__MaxRows` | 5000 | Linhas de dados por arquivo |
| `RateLimiting__AuthPermitPerMinute` | 10 | Tentativas de login/cadastro por IP por minuto |
| `Auth__SecurityStampValidationSeconds` | 60 | Tempo máximo até outras sessões caírem após troca de senha |
| `Cors__AllowedOrigins__0` | — | Origem do frontend, se servido em outro domínio |
| `Demo__Password` | Demo12345 | Senha do usuário de demonstração (somente local) |

## 7. Componentes de interface

Os componentes em `frontend/src/components/ui` vêm do [shadcn/ui](https://ui.shadcn.com) e fazem parte do código do projeto. Para adicionar um novo, sem instalar o CLI como dependência:

```powershell
cd frontend
npx shadcn@4.21.4 add <componente>
```

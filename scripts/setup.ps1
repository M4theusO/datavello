<#
.SYNOPSIS
    Prepara o Datavello para rodar localmente: confere ferramentas, cria os arquivos .env,
    restaura dependências, aplica as migrations e (opcionalmente) cria os dados de demonstração.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\setup.ps1
    powershell -ExecutionPolicy Bypass -File scripts\setup.ps1 -SkipDemo
#>
param(
    # Não criar a empresa/usuário de demonstração.
    [switch]$SkipDemo
)

# Native tools (dotnet, npm) write warnings to stderr; in Windows PowerShell 5.1 'Stop' would abort on them.
# Failures are detected through exit codes instead (see Run).
$root = Split-Path -Parent $PSScriptRoot

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }
function Fail($text) { Write-Host "`nERRO: $text" -ForegroundColor Red; exit 1 }

function Require($command, $hint) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) { Fail "'$command' não encontrado. $hint" }
}

function Run($description, [scriptblock]$block) {
    & $block
    if ($LASTEXITCODE -ne 0) { Fail "$description falhou (código $LASTEXITCODE). Veja as mensagens acima." }
}

Step 'Conferindo ferramentas'
Require 'dotnet' 'Instale o .NET SDK 10: https://dotnet.microsoft.com/download'
Require 'node' 'Instale o Node.js 22 LTS: https://nodejs.org'
Require 'npm' 'O npm acompanha o Node.js.'
$dotnetVersion = (dotnet --version)
$nodeVersion = (node --version)
Write-Host "  .NET SDK $dotnetVersion | Node.js $nodeVersion"
if (-not $dotnetVersion.StartsWith('10.')) { Fail "É necessário o .NET SDK 10 (encontrado $dotnetVersion)." }
if ([int]($nodeVersion.TrimStart('v').Split('.')[0]) -lt 20) { Fail "É necessário o Node.js 20 ou superior (encontrado $nodeVersion)." }

Step 'Arquivos de configuração (.env)'
$envFile = Join-Path $root '.env'
if (-not (Test-Path $envFile)) {
    Copy-Item (Join-Path $root '.env.example') $envFile -ErrorAction Stop
    Write-Host "  Criado .env a partir de .env.example."
}
$frontendEnv = Join-Path $root 'frontend\.env'
if (-not (Test-Path $frontendEnv)) {
    Copy-Item (Join-Path $root 'frontend\.env.example') $frontendEnv -ErrorAction Stop
    Write-Host "  Criado frontend\.env a partir de frontend\.env.example."
}
if (Select-String -Path $envFile -Pattern 'change-me' -Quiet) {
    Fail "Edite o arquivo .env e troque 'change-me' pela senha do usuário 'datavello' do PostgreSQL (veja o README, passo 3). Depois rode este script de novo."
}

Step 'Backend: ferramentas e pacotes'
Push-Location (Join-Path $root 'backend')
try {
    Run 'dotnet tool restore' { dotnet tool restore }
    Run 'dotnet restore' { dotnet restore }

    Step 'Banco de dados: aplicando migrations'
    Run 'Aplicar migrations' { dotnet ef database update --project src/Datavello.Api }

    if (-not $SkipDemo) {
        Step 'Dados de demonstração'
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        Run 'Criar dados de demonstração' { dotnet run --project src/Datavello.Api -- seed-demo }
    }
}
finally {
    Pop-Location
}

Step 'Frontend: pacotes'
Push-Location (Join-Path $root 'frontend')
try {
    Run 'npm ci' { npm ci --no-audit --no-fund }
}
finally {
    Pop-Location
}

Write-Host "`nPronto! Para iniciar: powershell -ExecutionPolicy Bypass -File scripts\dev.ps1" -ForegroundColor Green
if (-not $SkipDemo) { Write-Host "Login de demonstração (somente local): demo@datavello.local / Demo12345" -ForegroundColor Green }

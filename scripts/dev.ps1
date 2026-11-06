<#
.SYNOPSIS
    Abre o backend (http://localhost:5080), o frontend (http://localhost:5173) e o Mailpit
    (e-mails de teste em http://localhost:8025) em janelas separadas do PowerShell.
    Feche as janelas (ou pressione Ctrl+C em cada uma) para parar.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\dev.ps1
#>

$root = Split-Path -Parent $PSScriptRoot

foreach ($port in 5080, 5173) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
        Write-Host "A porta $port já está em uso. Feche a janela do Datavello aberta anteriormente e tente de novo." -ForegroundColor Red
        exit 1
    }
}

$backend = Join-Path $root 'backend'
$frontend = Join-Path $root 'frontend'

# Mailpit captura os e-mails enviados em desenvolvimento (nada sai para a internet).
$mailpitRunning = Get-NetTCPConnection -LocalPort 1025 -State Listen -ErrorAction SilentlyContinue
if ($mailpitRunning) {
    Write-Host 'Mailpit já está em execução (porta 1025).' -ForegroundColor DarkGray
} elseif (Get-Command mailpit -ErrorAction SilentlyContinue) {
    Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$host.UI.RawUI.WindowTitle = 'Datavello - e-mails (Mailpit)'; mailpit --smtp 127.0.0.1:1025 --listen 127.0.0.1:8025"
} else {
    Write-Host 'Mailpit não encontrado: os e-mails (ex.: redefinição de senha) não serão entregues.' -ForegroundColor Yellow
    Write-Host '  Instale com: winget install --id axllent.mailpit -e   (depois abra um novo PowerShell)' -ForegroundColor Yellow
}

Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$host.UI.RawUI.WindowTitle = 'Datavello - backend'; Set-Location '$backend'; dotnet run --project src/Datavello.Api"
Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$host.UI.RawUI.WindowTitle = 'Datavello - frontend'; Set-Location '$frontend'; npm run dev"

Write-Host 'Iniciando em novas janelas...' -ForegroundColor Cyan
Write-Host '  Aplicação:   http://localhost:5173'
Write-Host '  API/Swagger: http://localhost:5080/swagger'
Write-Host '  E-mails:     http://localhost:8025 (Mailpit)'

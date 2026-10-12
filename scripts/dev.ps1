<#
.SYNOPSIS
    Abre o backend (http://localhost:5080) e o frontend (http://localhost:5173) em duas janelas do PowerShell.
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

Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$host.UI.RawUI.WindowTitle = 'Datavello - backend'; Set-Location '$backend'; dotnet run --project src/Datavello.Api"
Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$host.UI.RawUI.WindowTitle = 'Datavello - frontend'; Set-Location '$frontend'; npm run dev"

Write-Host 'Iniciando backend e frontend em novas janelas...' -ForegroundColor Cyan
Write-Host '  Aplicação:  http://localhost:5173'
Write-Host '  API/Swagger: http://localhost:5080/swagger'

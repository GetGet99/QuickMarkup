param(
    [ValidateSet('framework-dependent', 'self-contained')]
    [string]$Mode = 'self-contained',
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release'
)

$root = Split-Path -Parent $PSScriptRoot
$serverProject = Join-Path $root 'QuickMarkup.LanguageServer\QuickMarkup.LanguageServer.csproj'
$serverOut = Join-Path $PSScriptRoot 'server'

Write-Host "Publishing Language Server (mode=$Mode, runtime=$Runtime)..." -ForegroundColor Green

if ($Mode -eq 'self-contained') {
    & dotnet publish $serverProject -c $Configuration --self-contained true -r $Runtime -o $serverOut
} else {
    & dotnet publish $serverProject -c $Configuration -o $serverOut
}

if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish failed'
}

$dll = Get-ChildItem -LiteralPath $serverOut -Filter 'QuickMarkup.LanguageServer.dll' -Recurse | Select-Object -First 1
if (-not $dll) {
    throw 'Published DLL not found'
}

Write-Host "Server published to: $serverOut" -ForegroundColor Green

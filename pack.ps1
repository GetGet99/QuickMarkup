$root = $PSScriptRoot

function Pack($project) {
    $path = Join-Path $root $project
    Write-Host "Packing $project..." -ForegroundColor Green
    dotnet pack $path --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for $project" }
}

switch ($args[0]) {
    'all' {
        Pack 'QuickMarkup.Infra'
        Pack 'Frameworks/QuickMarkup.WinUI'
        Pack 'Frameworks/QuickMarkup.UWP'
    }
    'infra' {
        Pack 'QuickMarkup.Infra'
    }
    default {
        Write-Host "Usage: pack.ps1 {all | infra}" -ForegroundColor Yellow
    }
}

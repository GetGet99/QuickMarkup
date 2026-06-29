$root = $PSScriptRoot

$branchName = $null
$autoConfirm = $false

foreach ($arg in $args) {
    if ($arg -eq '-y') { $autoConfirm = $true }
    else { $branchName = $arg }
}

Set-Location $root

if (-not $branchName) {
    $status = git submodule status Parser 2>&1
    if ($LASTEXITCODE -ne 0 -or $status -match '^-') {
        Write-Host "Fetching Parser submodule..." -ForegroundColor Green
        git submodule update --init Parser
    } else {
        Write-Host "Parser submodule is already fetched." -ForegroundColor Green
    }
    exit 0
}

if (-not $autoConfirm) {
    $response = Read-Host "This will wipe all unsaved and uncommitted work. Continue? (y/N)"
    if ($response -ne 'y') {
        Write-Host "Aborted." -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "Fetching origin/master..." -ForegroundColor Green
git fetch origin master
if ($LASTEXITCODE -ne 0) { Write-Host "Fetch failed." -ForegroundColor Red; exit 1 }

Write-Host "Creating branch '$branchName' from origin/master..." -ForegroundColor Green
git checkout --no-track -b $branchName origin/master
if ($LASTEXITCODE -ne 0) { Write-Host "Failed to create branch." -ForegroundColor Red; exit 1 }

Write-Host "Updating Parser submodule..." -ForegroundColor Green
git submodule update --init --recursive
if ($LASTEXITCODE -ne 0) { Write-Host "Failed to update submodule." -ForegroundColor Red; exit 1 }

Write-Host "Done. Switched to branch '$branchName'." -ForegroundColor Green

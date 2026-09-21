# Build the mod and deploy it to the game's Mods folder
# Usage in VS Code terminal:  .\build.ps1
#   or:  .\build.ps1 -GameDir "D:\path\to\Saiko no sutoka"
# You can also set the SAIKO_GAME_DIR environment variable once.

param(
    [string]$GameDir = $env:SAIKO_GAME_DIR,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$project = $PSScriptRoot

if (-not $GameDir) { $GameDir = "E:\steam\steamapps\common\Saiko no sutoka" }

$dll = "$project\bin\$Configuration\SaikoTrainer.dll"
$mods = "$GameDir\Mods"

Write-Host "== Build ==" -ForegroundColor Cyan
Write-Host "GameDir: $GameDir" -ForegroundColor DarkGray
dotnet build -c $Configuration "$project\SaikoTrainer.csproj" -p:GameDir="$GameDir"

if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }

$proc = Get-Process -Name "Saiko no sutoka" -ErrorAction SilentlyContinue
if ($proc) { Write-Host "Closing game first..." -ForegroundColor Yellow; Stop-Process -Id $proc.Id -Force; Start-Sleep 2 }

if (-not (Test-Path -LiteralPath $mods)) { New-Item -ItemType Directory -Path $mods | Out-Null }
Copy-Item $dll $mods -Force
Write-Host ""
Write-Host "Deployed -> $mods" -ForegroundColor Green
Write-Host "Now run the game via Steam and press Insert in-game." -ForegroundColor Green

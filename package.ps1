param(
    [string]$Version = "",
    [string]$GameDir = $env:SAIKO_GAME_DIR,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$project = $PSScriptRoot
if (-not $GameDir) { $GameDir = "E:\steam\steamapps\common\Saiko no sutoka" }

if (-not $Version) {
    $main = Get-Content -LiteralPath "$project\Main.cs" -Raw
    $m = [regex]::Match($main, '"Saiko Trainer",\s*"([0-9]+\.[0-9]+\.[0-9]+)"')
    if ($m.Success) { $Version = $m.Groups[1].Value } else { $Version = "0.0.0" }
}

Write-Host "== Build v$Version ==" -ForegroundColor Cyan
dotnet build -c $Configuration "$project\SaikoTrainer.csproj" -p:GameDir="$GameDir"
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }

$dist = "$project\dist"
$stage = "$dist\SaikoTrainer_v$Version"
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Path "$stage\Mods" -Force | Out-Null

Copy-Item "$project\bin\$Configuration\SaikoTrainer.dll" "$stage\Mods\" -Force
Copy-Item "$project\INSTALL.txt" $stage -Force
Copy-Item "$project\README.md" $stage -Force
Copy-Item "$project\LICENSE" $stage -Force

$zip = "$dist\SaikoTrainer_v$Version.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zip

Write-Host ""
Write-Host "Release package -> $zip" -ForegroundColor Green
Write-Host "Upload this zip to a GitHub Release." -ForegroundColor Green

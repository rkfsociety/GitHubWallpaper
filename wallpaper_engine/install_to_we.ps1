# Установка GitHub Wallpaper в Wallpaper Engine (myprojects).
# После копирования обои появятся в WE с preview и title из project.json.

param(
    [string]$WeRoot = ""
)

$ErrorActionPreference = "Stop"

$source = $PSScriptRoot
$projectName = "GitHubWallpaper"

function Find-WallpaperEngineRoot {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Steam\steamapps\common\wallpaper_engine",
        "$env:ProgramFiles\Steam\steamapps\common\wallpaper_engine",
        "F:\SteamLibrary\steamapps\common\wallpaper_engine",
        "D:\Steam\steamapps\common\wallpaper_engine"
    )
    foreach ($path in $candidates) {
        if (Test-Path (Join-Path $path "wallpaper64.exe")) {
            return $path
        }
    }
    throw "Wallpaper Engine не найден. Укажите путь: .\install_to_we.ps1 -WeRoot 'C:\...\wallpaper_engine'"
}

if (-not $WeRoot) {
    $WeRoot = Find-WallpaperEngineRoot
}

$dest = Join-Path $WeRoot "projects\myprojects\$projectName"
New-Item -ItemType Directory -Path $dest -Force | Out-Null

$exclude = @("install_to_we.ps1")
Get-ChildItem $source -File | Where-Object { $exclude -notcontains $_.Name } | ForEach-Object {
    Copy-Item $_.FullName -Destination (Join-Path $dest $_.Name) -Force
}

$indexPath = Join-Path $dest "index.html"
Write-Host ""
Write-Host "Готово: $dest"
Write-Host ""
Write-Host "Дальше в Wallpaper Engine:"
Write-Host "  1. Удалите старую запись из истории (кнопка «Удалить из истории»)."
Write-Host "  2. Open Wallpaper -> Open from file ->"
Write-Host "     $indexPath"
Write-Host "  3. Либо: Create Wallpaper -> перетащите index.html из папки выше."
Write-Host ""
Write-Host "Должны появиться название «GitHub Wallpaper (Wallpaper Engine)» и иконка preview.jpg."

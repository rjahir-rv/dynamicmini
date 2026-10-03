param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$projectPath = Join-Path $PSScriptRoot "..\src\DynamicMiniPlayer\DynamicMiniPlayer.csproj"
$outputPath = Join-Path $PSScriptRoot "..\artifacts\publish\win-x64"

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $outputPath `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

Write-Host "Publicación creada en: $outputPath"

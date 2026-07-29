[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src\WallpaperChanger.App\WallpaperChanger.App.csproj"
$publishDirectory = Join-Path $repositoryRoot "artifacts\publish\win-x64"
$installerScript = Join-Path $repositoryRoot "installer\WallpaperChanger.iss"
$compilerPath = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"

if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:LocalAppData "Programs\Inno Setup 6\ISCC.exe"
}

if (-not (Test-Path -LiteralPath $compilerPath)) {
    throw "Inno Setup 6 was not found. Install it with: winget install --id JRSoftware.InnoSetup --exact"
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory | Out-Null

& dotnet publish $projectPath --configuration $Configuration --output $publishDirectory -p:DebugSymbols=false -p:DebugType=None
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

& $compilerPath $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

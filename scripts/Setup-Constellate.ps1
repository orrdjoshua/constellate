Param(
    [string]$Framework = "net10.0"
)

$ErrorActionPreference = "Stop"

# Resolve repo root relative to this script (scripts/..)
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $RepoRoot

Write-Host "== CONSTELLATE Setup =="$([Environment]::NewLine)
Write-Host "Repo Root: $RepoRoot"
Write-Host "Target Framework: $Framework"
Write-Host ""

# Ensure src directory
$src = Join-Path $RepoRoot "src"
if (-not (Test-Path $src)) {
    New-Item -ItemType Directory -Path $src | Out-Null
}

# Install Avalonia templates (idempotent)
Write-Host "Installing Avalonia templates (safe to re-run)..."
dotnet new install Avalonia.Templates

# Create solution if missing
$sln = Join-Path $RepoRoot "Constellate.sln"
if (-not (Test-Path $sln)) {
    Write-Host "Creating solution Constellate.sln..."
    dotnet new sln -n Constellate
} else {
    Write-Host "Solution already exists; skipping creation."
}

# Project specs
$projects = @(
    @{ Name = "Constellate.App"; Template = "avalonia.app" },
    @{ Name = "Constellate.Core"; Template = "classlib" },
    @{ Name = "Constellate.Renderer.OpenTK"; Template = "classlib" },
    @{ Name = "Constellate.SDK"; Template = "classlib" }
)

# Scaffold projects if missing
foreach ($p in $projects) {
    $projDir = Join-Path $src $($p.Name)
    $csproj  = Join-Path $projDir "$($p.Name).csproj"

    if (-not (Test-Path $projDir)) {
        New-Item -ItemType Directory -Path $projDir | Out-Null
    }

    if (-not (Test-Path $csproj)) {
        Write-Host "Scaffolding $($p.Name) (template: $($p.Template))..."
        dotnet new $($p.Template) -o $projDir --framework $Framework
    } else {
        Write-Host "$($p.Name) already exists; skipping scaffold."
    }
}

# Add to solution (safe; duplicates will be caught)
Write-Host "Adding projects to solution..."
$projPaths = @(
  "src/Constellate.App/Constellate.App.csproj",
  "src/Constellate.Core/Constellate.Core.csproj",
  "src/Constellate.Renderer.OpenTK/Constellate.Renderer.OpenTK.csproj",
  "src/Constellate.SDK/Constellate.SDK.csproj"
)
foreach ($pp in $projPaths) {
    try {
        dotnet sln Constellate.sln add $pp
    } catch {
        Write-Host "Note: $pp may already be in the solution; continuing."
    }
}

# Add references to the app
Write-Host "Adding project references to Constellate.App..."
try {
    dotnet add "src/Constellate.App" reference `
        "src/Constellate.Core" `
        "src/Constellate.Renderer.OpenTK" `
        "src/Constellate.SDK"
} catch {
    Write-Host "Note: references may already exist; continuing."
}

# Restore and build
Write-Host "Restoring and building the solution..."
dotnet restore "Constellate.sln"
dotnet build "Constellate.sln" -c Debug /consoleloggerparameters:Summary

Write-Host ""
Write-Host "== Done =="
Write-Host "To run with hot reload:"
Write-Host "  dotnet watch --project src/Constellate.App"

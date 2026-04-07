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

# Detect or create solution (prefer .slnx in .NET 10+)
$slnx = Join-Path $RepoRoot "Constellate.slnx"
$sln  = Join-Path $RepoRoot "Constellate.sln"
$solutionPath = $null

if (Test-Path $slnx) {
    $solutionPath = $slnx
    Write-Host "Detected existing solution: $solutionPath"
} elseif (Test-Path $sln) {
    $solutionPath = $sln
    Write-Host "Detected existing solution: $solutionPath"
} else {
    Write-Host "Creating solution 'Constellate' (will be .slnx on newer SDKs)..."
    # Use --force to avoid conflicts with placeholder files (e.g., .slnx stub)
    dotnet new sln -n Constellate --force
    if (Test-Path $slnx) {
        $solutionPath = $slnx
    } elseif (Test-Path $sln) {
        $solutionPath = $sln
    } else {
        throw "Failed to create a solution (.slnx or .sln) at $RepoRoot"
    }
}
Write-Host "Solution path: $solutionPath"

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
Set-Location $RepoRoot

$projPaths = @(
  "src/Constellate.App/Constellate.App.csproj",
  "src/Constellate.Core/Constellate.Core.csproj",
  "src/Constellate.Renderer.OpenTK/Constellate.Renderer.OpenTK.csproj",
  "src/Constellate.SDK/Constellate.SDK.csproj"
)

foreach ($pp in $projPaths) {
    $ppFull = Join-Path $RepoRoot $pp
    try {
        dotnet sln "$solutionPath" add "$ppFull"
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

# Restore and build (prefer solution; fall back to repo root if needed)
Write-Host "Restoring and building the solution..."
$restoreOk = $true
try {
    dotnet restore "$solutionPath"
} catch {
    Write-Host "Restore on solution failed; attempting restore at repo root..."
    $restoreOk = $false
    dotnet restore "$RepoRoot"
}

try {
    if ($restoreOk) {
        dotnet build "$solutionPath" -c Debug /consoleloggerparameters:Summary
    } else {
        dotnet build "$RepoRoot" -c Debug /consoleloggerparameters:Summary
    }
} catch {
    Write-Host "Build failed; you can also try:"
    Write-Host "  dotnet build `"$RepoRoot`" -c Debug"
    throw
}

Write-Host ""
Write-Host "== Done =="
Write-Host "To run with hot reload:"
Write-Host "  dotnet watch --project src/Constellate.App"

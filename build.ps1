param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $repoRoot "PcNinja.SmartOfficeInstaller.csproj"
$artifacts = Join-Path $repoRoot "artifacts"

dotnet build $project -c $Configuration

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

$projectText = Get-Content -LiteralPath $project -Raw
$assemblyName = [regex]::Match($projectText, '<AssemblyName>([^<]+)</AssemblyName>').Groups[1].Value
if (-not $assemblyName) {
    throw "AssemblyName was not found in $project"
}

$exeName = "$assemblyName.exe"
$exe = Join-Path $repoRoot "bin\$Configuration\net48\$exeName"
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Build output was not found: $exe"
}

Copy-Item -LiteralPath $exe -Destination (Join-Path $artifacts $exeName) -Force
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $artifacts $exeName)


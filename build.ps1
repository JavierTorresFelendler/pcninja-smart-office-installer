param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $repoRoot "PcNinja.SmartOfficeInstaller.csproj"
$artifacts = Join-Path $repoRoot "artifacts"

dotnet build $project -c $Configuration

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

$exe = Join-Path $repoRoot "bin\$Configuration\net48\OfficeSmart-v3.3.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Build output was not found: $exe"
}

Copy-Item -LiteralPath $exe -Destination (Join-Path $artifacts "OfficeSmart-v3.3.exe") -Force
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $artifacts "OfficeSmart-v3.3.exe")


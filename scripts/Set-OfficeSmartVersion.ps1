param(
    [Parameter(Mandatory = $true)]
    [string]$AssemblyVersion,

    [Parameter(Mandatory = $true)]
    [string]$FileVersion,

    [string]$ExeMinorName = ""
)

$ErrorActionPreference = "Stop"

if ($AssemblyVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "AssemblyVersion must use four numeric parts, for example 3.4.0.0."
}

if ($FileVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "FileVersion must use four numeric parts, for example 3.4.0.0."
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$assemblyInfo = Join-Path $repoRoot "Properties\AssemblyInfo.cs"
$project = Join-Path $repoRoot "PcNinja.SmartOfficeInstaller.csproj"

$assemblyText = Get-Content -LiteralPath $assemblyInfo -Raw
$assemblyText = [regex]::Replace($assemblyText, 'AssemblyVersion\("[^"]+"\)', "AssemblyVersion(`"$AssemblyVersion`")")
$assemblyText = [regex]::Replace($assemblyText, 'AssemblyFileVersion\("[^"]+"\)', "AssemblyFileVersion(`"$FileVersion`")")
Set-Content -LiteralPath $assemblyInfo -Value $assemblyText -Encoding UTF8

if ($ExeMinorName) {
    if ($ExeMinorName -notmatch '^\d+\.\d+$') {
        throw "ExeMinorName must look like 3.4."
    }

    $projectText = Get-Content -LiteralPath $project -Raw
    $projectText = [regex]::Replace($projectText, '<AssemblyName>OfficeSmart-v[^<]+</AssemblyName>', "<AssemblyName>OfficeSmart-v$ExeMinorName</AssemblyName>")
    Set-Content -LiteralPath $project -Value $projectText -Encoding UTF8
}

Write-Host "Updated AssemblyVersion=$AssemblyVersion AssemblyFileVersion=$FileVersion"


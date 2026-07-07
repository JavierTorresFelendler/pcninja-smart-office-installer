param(
    [string]$Configuration = "Release",
    [string]$ExpectedReleaseSha256 = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$project = Join-Path $repoRoot "PcNinja.SmartOfficeInstaller.csproj"
$assemblyInfo = Join-Path $repoRoot "Properties\AssemblyInfo.cs"
$releaseAsset = Join-Path $repoRoot "release-assets\OfficeSmart-v3.3.exe"

function Fail($message) {
    throw "[public-ready] $message"
}

if (-not (Test-Path -LiteralPath $project)) {
    Fail "Project file not found: $project"
}

if (-not (Test-Path -LiteralPath $assemblyInfo)) {
    Fail "AssemblyInfo.cs not found: $assemblyInfo"
}

dotnet build $project -c $Configuration

$offlineLayoutCheck = Join-Path $repoRoot "scripts\Test-OfflinePackageLayout.ps1"
if (Test-Path -LiteralPath $offlineLayoutCheck) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $offlineLayoutCheck
}

$builtExe = Join-Path $repoRoot "bin\$Configuration\net48\OfficeSmart-v3.3.exe"
if (-not (Test-Path -LiteralPath $builtExe)) {
    Fail "Build output not found: $builtExe"
}

$assemblyText = Get-Content -LiteralPath $assemblyInfo -Raw
$assemblyVersion = [regex]::Match($assemblyText, 'AssemblyVersion\("([^"]+)"\)').Groups[1].Value
$fileVersion = [regex]::Match($assemblyText, 'AssemblyFileVersion\("([^"]+)"\)').Groups[1].Value

if (-not $assemblyVersion) {
    Fail "AssemblyVersion was not found."
}

if (-not $fileVersion) {
    Fail "AssemblyFileVersion was not found."
}

$info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($builtExe)
if ($info.FileVersion -ne $fileVersion) {
    Fail "Built FileVersion '$($info.FileVersion)' does not match AssemblyFileVersion '$fileVersion'."
}

function Join-Parts([string[]]$parts) {
    return [string]::Concat($parts)
}

$scanPatterns = @(
    (Join-Parts @("tech", "nion")),
    (Join-Parts @("OneDrive - ", "Tech", "nion")),
    (Join-Parts @("Users\\", "Jav", "ier")),
    (Join-Parts @("D:\\", "One", "Drive")),
    (Join-Parts @("C:\\Users\\", "Jav", "ier")),
    (Join-Parts @("@", "tech", "nion")),
    (Join-Parts @("pid", "key")),
    (Join-Parts @("product", " key")),
    (Join-Parts @("license", " key")),
    (Join-Parts @("pass", "word")),
    (Join-Parts @("pass", "wd")),
    (Join-Parts @("sec", "ret")),
    (Join-Parts @("api", "_key")),
    (Join-Parts @("api", "key")),
    (Join-Parts @("to", "ken")),
    (Join-Parts @("k", "ms"))
)

$excludedPathFragments = @(
    "\.git\",
    "\bin\",
    "\obj\",
    "\artifacts\"
)

$findings = New-Object System.Collections.Generic.List[object]
$currentScript = $MyInvocation.MyCommand.Path
$files = Get-ChildItem -LiteralPath $repoRoot -Recurse -Force -File |
    Where-Object {
        $fullName = $_.FullName
        if ($fullName -eq $currentScript) { return $false }
        foreach ($fragment in $excludedPathFragments) {
            if ($fullName -like "*$fragment*") { return $false }
        }
        if ($_.FullName -like "*\release-assets\*.exe") { return $false }
        return $true
    }

foreach ($file in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    if ($bytes.Length -gt 0 -and ($bytes[0..([Math]::Min($bytes.Length - 1, 255))] -contains 0)) {
        continue
    }

    $text = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($pattern in $scanPatterns) {
        if ($text -match [regex]::Escape($pattern)) {
            $findings.Add([pscustomobject]@{ Path = $file.FullName.Substring($repoRoot.Length + 1); Pattern = $pattern }) | Out-Null
        }
    }
}

if ($findings.Count -gt 0) {
    $findings | Format-Table -AutoSize
    Fail "Sensitive/public-risk strings were found."
}

if ($ExpectedReleaseSha256) {
    if (-not (Test-Path -LiteralPath $releaseAsset)) {
        Fail "Expected release asset not found: $releaseAsset"
    }

    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $releaseAsset).Hash
    if ($hash -ne $ExpectedReleaseSha256.ToUpperInvariant()) {
        Fail "Release asset SHA256 '$hash' does not match expected '$ExpectedReleaseSha256'."
    }
}

[pscustomobject]@{
    Status = "OK"
    AssemblyVersion = $assemblyVersion
    FileVersion = $fileVersion
    BuiltExe = $builtExe
    BuiltSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $builtExe).Hash
}

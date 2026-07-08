param()

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$manifestPath = Join-Path $repoRoot "public-release\update-manifest.json"
$sourcePath = Join-Path $repoRoot "OfficeSmart\InstallerForm.cs"

function Fail($message) {
    throw "[update-manifest] $message"
}

function Test-HttpsUrl([string]$Url) {
    if ([string]::IsNullOrWhiteSpace($Url)) {
        return $false
    }

    $uri = $null
    if (-not [System.Uri]::TryCreate($Url, [System.UriKind]::Absolute, [ref]$uri)) {
        return $false
    }

    return $uri.Scheme -eq [System.Uri]::UriSchemeHttps
}

if (-not (Test-Path -LiteralPath $manifestPath)) {
    Fail "Manifest not found: $manifestPath"
}

if (-not (Test-Path -LiteralPath $sourcePath)) {
    Fail "InstallerForm.cs not found: $sourcePath"
}

$manifestText = Get-Content -LiteralPath $manifestPath -Raw
$manifest = $manifestText | ConvertFrom-Json

foreach ($name in @("channel", "publicLabel", "version", "releaseNotesUrl", "portable", "signing")) {
    if ($null -eq $manifest.$name) {
        Fail "Missing manifest field: $name"
    }
}

if ($manifest.channel -ne "stable") {
    Fail "Manifest channel must be stable."
}

try {
    [void][version]$manifest.version
}
catch {
    Fail "Manifest version is not a valid Version value."
}

if (-not (Test-HttpsUrl $manifest.releaseNotesUrl)) {
    Fail "releaseNotesUrl must be HTTPS."
}

foreach ($name in @("fileName", "url", "sha256")) {
    if ($null -eq $manifest.portable.$name -or [string]::IsNullOrWhiteSpace([string]$manifest.portable.$name)) {
        Fail "Missing manifest portable field: $name"
    }
}

if ($manifest.portable.fileName -notmatch '^OfficeSmart-v[0-9A-Za-z.\-]+\.exe$') {
    Fail "portable.fileName must use the OfficeSmart public EXE naming pattern."
}

if (-not (Test-HttpsUrl $manifest.portable.url)) {
    Fail "portable.url must be HTTPS."
}

if ($manifest.portable.sha256 -notmatch '^[A-Fa-f0-9]{64}$') {
    Fail "portable.sha256 must be a 64-character SHA256 value."
}

if ($null -eq $manifest.signing.required) {
    Fail "signing.required is required."
}

$source = Get-Content -LiteralPath $sourcePath -Raw
$requiredSourceSnippets = @(
    "UPDATE_MANIFEST_URL",
    "public-release/update-manifest.json?ref=main",
    "DownloadStringTaskAsync",
    "DataContractJsonSerializer",
    "FileVersionInfo.GetVersionInfo",
    "Update available",
    "Check updates",
    "Up to date",
    "SetUpdateButtonAvailable",
    "OnUpdateButtonClick"
)

foreach ($snippet in $requiredSourceSnippets) {
    if ($source -notlike "*$snippet*") {
        Fail "InstallerForm.cs does not contain required updater snippet: $snippet"
    }
}

[pscustomobject]@{
    Status = "OK"
    Manifest = $manifestPath
    Version = $manifest.version
    FileName = $manifest.portable.fileName
}

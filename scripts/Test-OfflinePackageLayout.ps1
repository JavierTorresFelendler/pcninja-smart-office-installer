$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$source = Join-Path $repoRoot "OfficeSmart\InstallerForm.cs"

if (-not (Test-Path -LiteralPath $source)) {
    throw "InstallerForm.cs not found: $source"
}

$text = Get-Content -LiteralPath $source -Raw
$offlineStart = $text.IndexOf("private void RunOfflineDownload")
$offlineEnd = $text.IndexOf("private string BuildXML", $offlineStart)
if ($offlineStart -lt 0 -or $offlineEnd -lt $offlineStart) {
    throw "RunOfflineDownload block not found."
}
$offlineText = $text.Substring($offlineStart, $offlineEnd - $offlineStart)

function Assert-Contains($pattern, $message) {
    if ($text -notmatch $pattern) {
        throw $message
    }
}

function Assert-NotContains($pattern, $message) {
    if ($text -match $pattern) {
        throw $message
    }
}

function Assert-OfflineNotContains($pattern, $message) {
    if ($offlineText -match $pattern) {
        throw $message
    }
}

Assert-Contains 'private\s+const\s+string\s+OFFLINE_PACKAGE_FOLDER\s*=\s*"OFFICE-OFFLINE"' "Offline package folder must be named OFFICE-OFFLINE."
Assert-Contains 'Text\s*=\s*"Smart Office Installer"' "The public app title should use Smart Office Installer."
Assert-Contains 'Auto-activate volume license during installation' "Auto-activation wording should be clear for online and package installs."
Assert-Contains 'Create offline package only - do not install on this PC' "Offline mode must clearly say it does not install on this PC."
Assert-Contains 'Create Package' "Offline package mode should use a package-oriented final action label."
Assert-Contains 'Package' "Offline package mode should use package-oriented step wording."
Assert-Contains 'string\s+selectedPath\s*=\s*\(offPath\s*\?\?\s*""\)\.Trim\(\)' "Offline download should use the already selected parent folder."
Assert-Contains 'Path\.GetFullPath\(selectedPath\)' "Offline package parent destination should be normalized before use."
Assert-Contains 'string\s+packagePath\s*=\s*ResolveOfflinePackagePath\(selectedPath\)' "Offline package files should go into OFFICE-OFFLINE under the selected folder."
Assert-Contains 'Path\.Combine\(selectedPath,\s*OFFLINE_PACKAGE_FOLDER\)' "Offline package resolver should create OFFICE-OFFLINE under the selected folder."
Assert-Contains 'StringComparison\.OrdinalIgnoreCase' "Offline package resolver should avoid nesting OFFICE-OFFLINE inside an existing OFFICE-OFFLINE folder."
Assert-Contains 'string\s+stagingRoot\s*=\s*Path\.Combine\(packagePath,\s*"_OfficeSmartDownload"\)' "Offline download should use an internal staging folder under the selected package folder."
Assert-Contains 'BuildXML\(stagingRoot\)' "Offline download XML must point ODT at the staging source folder."
Assert-Contains 'WorkingDirectory\s*=\s*stagingRoot' "ODT download should run in the staging folder."
Assert-Contains 'Path\.Combine\(stagingRoot,\s*"Office"\)' "Offline package should validate that the Office source folder exists under staging."
Assert-Contains 'TryDelDir\(Path\.Combine\(packagePath,\s*"Data"\)\)' "Offline package should replace stale Data contents before copying the new package."
Assert-Contains 'CopyDirectoryContents\(text2,\s*packagePath\)' "Downloaded Office source files should be moved into the selected package folder."
Assert-Contains 'Path\.Combine\(packagePath,\s*"setup\.exe"\)' "Offline package setup.exe should be copied to the selected package root."
Assert-Contains 'Path\.Combine\(packagePath,\s*"Office_Config\.xml"\)' "Offline package config XML should be copied to the selected package root."
Assert-Contains 'Path\.Combine\(packagePath,\s*"Install-Office\.bat"\)' "Offline install batch file should be written to the selected package root."
Assert-Contains 'TryDelDir\(stagingRoot\)' "Offline staging folder should be cleaned after package creation."
Assert-NotContains 'File\.WriteAllText\(Path\.Combine\(text2,\s*"Install-Office\.bat"\)' "Install-Office.bat must not be written inside the Office source data folder."
Assert-NotContains 'BuildXML\(offPath\)' "Offline download should not use the final package folder as the ODT staging source."
Assert-NotContains 'WorkingDirectory\s*=\s*offPath' "ODT download should not run directly in the final package folder."
Assert-NotContains 'Path\.Combine\(offPath,\s*"Office"\)' "Offline package should not leave Office data one level below a separate destination."
Assert-NotContains 'Ultimate\s+Office\s+Installer' "The public app title should no longer use the old product name."
Assert-NotContains 'Offline\s*/\s*Download\s+mode' "Offline mode label should not look like an additive download option."
Assert-OfflineNotContains 'FolderBrowserDialog' "RunOfflineDownload must not open a second destination picker."
Assert-OfflineNotContains 'MessageBox\.Show\("Please select' "RunOfflineDownload must use the folder already selected by the user."

[pscustomobject]@{
    Status = "OK"
    Check = "Offline package layout"
}

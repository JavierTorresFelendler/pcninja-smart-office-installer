$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$source = Join-Path $repoRoot "OfficeSmart\InstallerForm.cs"

if (-not (Test-Path -LiteralPath $source)) {
    throw "InstallerForm.cs not found: $source"
}

$text = Get-Content -LiteralPath $source -Raw

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

Assert-Contains 'BuildXML\(offPath\)' "Offline download XML must include SourcePath for the selected destination."
Assert-Contains 'WorkingDirectory\s*=\s*offPath' "ODT download should run with the selected destination as the working directory."
Assert-Contains 'Path\.Combine\(offPath,\s*"setup\.exe"\)' "Offline package setup.exe should be copied to the selected destination root."
Assert-Contains 'Path\.Combine\(offPath,\s*"Office_Config\.xml"\)' "Offline package config XML should be copied to the selected destination root."
Assert-Contains 'Path\.Combine\(offPath,\s*"Install-Office\.bat"\)' "Offline install batch file should be written to the selected destination root."
Assert-Contains 'Path\.Combine\(offPath,\s*"Office"\)' "Offline package should validate that the Office source folder exists under the selected destination."
Assert-NotContains 'File\.WriteAllText\(Path\.Combine\(text2,\s*"Install-Office\.bat"\)' "Install-Office.bat must not be written inside the Office source data folder."

[pscustomobject]@{
    Status = "OK"
    Check = "Offline package layout"
}

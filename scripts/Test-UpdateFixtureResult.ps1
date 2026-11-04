$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UpdateFixtureResult.ps1')
$newHash = 'A' * 64
$oldHash = 'B' * 64
$cases = @(
    @{ Status = 'Updated to 3.3.13.0; previous copy: C:\fixture\OLD-Smart Office Installer.exe'; Expected = $true },
    @{ Status = 'Recovered new GUI startup; previous copy: C:\fixture\OLD-Smart Office Installer.exe'; Expected = $true },
    @{ Status = 'Recovered: previous application remains current.'; Expected = $false },
    @{ Status = 'Update incomplete: archive failed'; Expected = $false },
    @{ Status = ''; Expected = $false },
    @{ Status = 'Updated to 3.3.13.0'; Expected = $false }
)
foreach ($case in $cases) {
    $actual = Test-CompletedAppUpdate $case.Status $newHash $newHash $oldHash $oldHash $false
    if ($actual -ne $case.Expected) { throw "Unexpected fixture verdict: $($case.Status)" }
}
$recovered = $cases[1].Status
if (Test-CompletedAppUpdate $recovered $newHash $newHash $oldHash $oldHash $true) { throw 'Pending update accepted.' }
if (Test-CompletedAppUpdate $recovered $oldHash $newHash $oldHash $oldHash $false) { throw 'Old current executable accepted.' }
if (Test-CompletedAppUpdate $recovered $newHash $newHash $newHash $oldHash $false) { throw 'Incorrect archive accepted.' }
if (Test-CompletedAppUpdate $recovered '' '' '' '' $false) { throw 'Missing hashes accepted.' }
Write-Host 'PASS: Helper and recovered-GUI completion accepted only with exact current/archive hashes and no pending operation.'

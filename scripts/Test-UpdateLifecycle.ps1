$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UpdateFixtureResult.ps1')
function Wait-FixtureGuiExit([string]$Folder) {
    # Confirmed update completion can precede the disposable GUI's normal close.
    # Wait for that exact successor before starting another single-instance case.
    $record = [IO.File]::ReadAllText((Join-Path $Folder 'fixture-gui-process.txt'))
    if ($record -notmatch '^(\d+),(\d+)$') { throw 'Invalid fixture GUI process record.' }
    $fixtureGuiProcessId = [int]$Matches[1]
    $fixtureGuiStartTicks = [long]$Matches[2]
    $gui = Get-Process -Id $fixtureGuiProcessId -ErrorAction SilentlyContinue
    if ($null -eq $gui) { return }
    try {
        if ($gui.HasExited) { return }
        if ($gui.StartTime.ToUniversalTime().Ticks -ne $fixtureGuiStartTicks) { return }
        if (-not $gui.WaitForExit(10000)) { throw "Disposable GUI did not close: $Folder" }
    } finally { $gui.Dispose() }
}
function Assert-CleanFixture([string]$Folder, [string]$CurrentHash, [bool]$Archive) {
    $statePath = Join-Path $Folder '.pcninja-office-updates'
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while ((Test-Path -LiteralPath $statePath) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
    if (Test-Path -LiteralPath $statePath) { throw "Update work directory remained: $Folder" }
    Remove-Item -LiteralPath (Join-Path $Folder 'fixture-gui-process.txt')
    $expected = @('Smart Office Installer.exe')
    if ($Archive) { $expected += 'Smart Office Installer-v3.3.12.0.exe' }
    $entries = @(Get-ChildItem -LiteralPath $Folder -Force)
    if ($entries.Count -ne $expected.Count -or @($entries | Where-Object { $_.PSIsContainer -or $_.Name -notin $expected }).Count -ne 0) {
        throw "Unexpected update leftovers: $Folder"
    }
    if ((Get-FileHash -LiteralPath (Join-Path $Folder 'Smart Office Installer.exe')).Hash -ne $CurrentHash) { throw 'Cleanup changed the current executable.' }
    Write-Host "PASS: No work directory or extra files remain at $Folder"
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host 'SKIP: Elevated fixture required for production staging ACL and parent/helper handoff.'
    exit 0
}
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'tests\UpdateLifecycle\UpdateLifecycle.csproj'
$oldBuild = Join-Path $root 'artifacts\lifecycle-old'
$newBuild = Join-Path $root 'artifacts\lifecycle-new'
dotnet build $project -c Release -p:FileVersion=3.3.12.0 -o $oldBuild -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Old fixture build failed.' }
dotnet build $project -c Release -p:FileVersion=3.3.13.0 -o $newBuild -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'New fixture build failed.' }
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('PcNinja-OfficeLifecycle-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$old = Join-Path $fixture 'Smart Office Installer.exe'
$new = Join-Path $newBuild 'LifecycleFixture.exe'
Copy-Item -LiteralPath (Join-Path $oldBuild 'LifecycleFixture.exe') -Destination $old
$oldHash = (Get-FileHash -LiteralPath $old).Hash
$newHash = (Get-FileHash -LiteralPath $new).Hash
$process = Start-Process -FilePath $old -ArgumentList ('"' + $new + '"') -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit(40000)) { throw "Fixture parent did not finish. Inspect $fixture" }
if ($process.ExitCode -ne 0) { throw "Fixture parent failed. Inspect $fixture" }
$deadline = [DateTime]::UtcNow.AddSeconds(45)
$result = $null
$complete = $false
$status = ''
$state = Join-Path $fixture '.pcninja-office-updates'
do {
    $result = @(Get-ChildItem -LiteralPath $state -Filter result.txt -Recurse -ErrorAction SilentlyContinue)
    try {
            $status = if ($result.Count -gt 0) { [IO.File]::ReadAllText($result[0].FullName) } else { '' }
            $currentHash = (Get-FileHash -LiteralPath $old).Hash
            $previousHash = (Get-FileHash -LiteralPath (Join-Path $fixture 'Smart Office Installer-v3.3.12.0.exe')).Hash
            $complete = ($currentHash -eq $newHash -and $previousHash -eq $oldHash -and -not (Test-Path -LiteralPath $state)) -or
                (Test-CompletedAppUpdate $status $currentHash $newHash $previousHash $oldHash (Test-Path -LiteralPath (Join-Path $state 'active-operation')))
        } catch [IO.IOException] {
            # The helper may still be writing its result or publishing the archive.
        } catch [System.Management.Automation.ItemNotFoundException] {
            # Archive publication has not finished yet.
        }
    if ($complete) { break }
    Start-Sleep -Milliseconds 200
} while ([DateTime]::UtcNow -lt $deadline)
if (-not $complete) {
    Write-Host $status
    throw "Helper did not report confirmed success. Inspect $fixture"
}
if ((Get-FileHash -LiteralPath $old).Hash -ne $newHash -or
    (Get-FileHash -LiteralPath (Join-Path $fixture 'Smart Office Installer-v3.3.12.0.exe')).Hash -ne $oldHash) {
    throw 'Same-name replacement or exact predecessor archive mismatch.'
}
Write-Host "PASS: Elevated single-instance parent/helper same-name replacement, successor guard acquisition, new GUI readiness and exact OLD archive. Disposable fixture retained at $fixture"
Wait-FixtureGuiExit $fixture
Assert-CleanFixture $fixture $newHash $true
foreach ($phase in @('before-publication', 'after-publication', 'after-archive', 'missing-canonical', 'legacy-completed')) {
    $phaseFolder = Join-Path $fixture $phase
    New-Item -ItemType Directory -Path $phaseFolder | Out-Null
    $current = Join-Path $phaseFolder 'Smart Office Installer.exe'
    Copy-Item -LiteralPath (Join-Path $oldBuild 'LifecycleFixture.exe') -Destination $current
    $stageMode = if ($phase -eq 'legacy-completed') { '--stage-legacy' } else { '--stage-recovery' }
    $stager = Start-Process -FilePath $current -ArgumentList (('"' + $new + '"'), $stageMode) -PassThru -WindowStyle Hidden
    if (-not $stager.WaitForExit(20000) -or $stager.ExitCode -ne 0) { throw "Recovery fixture staging failed: $phaseFolder" }
    $state = Join-Path $phaseFolder '.pcninja-office-updates'
    $id = Get-Content -LiteralPath (Join-Path $state 'active-operation') -Raw
    if ($id -notmatch '^[a-f0-9]{32}$') { throw 'Invalid fixture operation.' }
    $operation = Join-Path $state $id
    if ($phase -ne 'before-publication' -and $phase -ne 'missing-canonical') {
        [IO.File]::Replace((Join-Path $operation 'candidate.exe'), $current, (Join-Path $operation 'displaced.exe'))
    }
    if ($phase -eq 'after-archive') {
        [IO.File]::Move((Join-Path $operation 'previous.exe'), (Join-Path $phaseFolder 'Smart Office Installer-v3.3.12.0.exe'))
    }
    if ($phase -eq 'legacy-completed') {
        $legacy = Join-Path $phaseFolder 'OLD.V1-Smart Office Installer.exe'
        [IO.File]::Move((Join-Path $operation 'previous.exe'), $legacy)
        [IO.File]::WriteAllText((Join-Path $operation 'result.txt'), ('Updated to 3.3.13.0; previous copy: ' + $legacy))
        Remove-Item -LiteralPath (Join-Path $state 'active-operation')
    }
    $launch = $current
    if ($phase -eq 'missing-canonical') {
        [IO.File]::Move($current, (Join-Path $operation 'displaced.exe'))
        $launch = Join-Path $operation 'helper.exe'
    }
    $recovered = Start-Process -FilePath $launch -PassThru -WindowStyle Normal
    if (-not $recovered.WaitForExit(20000) -or $recovered.ExitCode -ne 0) { throw "Recovery GUI failed: $phaseFolder" }
    $recoveryDeadline = [DateTime]::UtcNow.AddSeconds(20)
    while ((Test-Path -LiteralPath (Join-Path $state 'active-operation')) -and [DateTime]::UtcNow -lt $recoveryDeadline) { Start-Sleep -Milliseconds 100 }
    if (Test-Path -LiteralPath (Join-Path $state 'active-operation')) { throw "Pending recovery was not cleared: $phaseFolder" }
    if ($phase -eq 'missing-canonical' -and (Get-FileHash -LiteralPath $current).Hash -ne $oldHash) { throw 'Missing canonical recovery did not restore predecessor.' }
    if ($phase -ne 'before-publication' -and $phase -ne 'missing-canonical' -and $phase -ne 'legacy-completed' -and (Get-FileHash -LiteralPath (Join-Path $phaseFolder 'Smart Office Installer-v3.3.12.0.exe')).Hash -ne $oldHash) {
        throw 'Recovery archive bytes differ.'
    }
    Wait-FixtureGuiExit $phaseFolder
    $hasArchive = $phase -ne 'before-publication' -and $phase -ne 'missing-canonical'
    $expectedCurrentHash = if ($hasArchive) { $newHash } else { $oldHash }
    Assert-CleanFixture $phaseFolder $expectedCurrentHash $hasArchive
    if ($hasArchive -and (Get-FileHash -LiteralPath (Join-Path $phaseFolder 'Smart Office Installer-v3.3.12.0.exe')).Hash -ne $oldHash) { throw 'Versioned predecessor hash differs.' }
    Write-Host "PASS: Normal GUI startup recovered interruption at $phase."
}

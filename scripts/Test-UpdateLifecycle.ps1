$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UpdateFixtureResult.ps1')
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
    if ($result.Count -gt 0) {
        try {
            $status = [IO.File]::ReadAllText($result[0].FullName)
            $currentHash = (Get-FileHash -LiteralPath $old).Hash
            $previousHash = (Get-FileHash -LiteralPath (Join-Path $fixture 'OLD-Smart Office Installer.exe')).Hash
            $complete = Test-CompletedAppUpdate $status $currentHash $newHash $previousHash $oldHash (Test-Path -LiteralPath (Join-Path $state 'active-operation'))
        } catch [IO.IOException] {
            # The helper may still be writing its result or publishing the archive.
        } catch [System.Management.Automation.ItemNotFoundException] {
            # Archive publication has not finished yet.
        }
    }
    if ($complete) { break }
    Start-Sleep -Milliseconds 200
} while ([DateTime]::UtcNow -lt $deadline)
if (-not $complete) {
    Write-Host $status
    throw "Helper did not report confirmed success. Inspect $fixture"
}
if ((Get-FileHash -LiteralPath $old).Hash -ne $newHash -or
    (Get-FileHash -LiteralPath (Join-Path $fixture 'OLD-Smart Office Installer.exe')).Hash -ne $oldHash) {
    throw 'Same-name replacement or exact predecessor archive mismatch.'
}
Write-Host "PASS: Elevated parent/helper same-name replacement, new GUI readiness and exact OLD archive. Disposable fixture retained at $fixture"
foreach ($phase in @('before-publication', 'after-publication', 'after-archive', 'missing-canonical')) {
    $phaseFolder = Join-Path $fixture $phase
    New-Item -ItemType Directory -Path $phaseFolder | Out-Null
    $current = Join-Path $phaseFolder 'Smart Office Installer.exe'
    Copy-Item -LiteralPath (Join-Path $oldBuild 'LifecycleFixture.exe') -Destination $current
    $stager = Start-Process -FilePath $current -ArgumentList (('"' + $new + '"'), '--stage-recovery') -PassThru -WindowStyle Hidden
    if (-not $stager.WaitForExit(20000) -or $stager.ExitCode -ne 0) { throw "Recovery fixture staging failed: $phaseFolder" }
    $state = Join-Path $phaseFolder '.pcninja-office-updates'
    $id = Get-Content -LiteralPath (Join-Path $state 'active-operation') -Raw
    if ($id -notmatch '^[a-f0-9]{32}$') { throw 'Invalid fixture operation.' }
    $operation = Join-Path $state $id
    if ($phase -ne 'before-publication' -and $phase -ne 'missing-canonical') {
        [IO.File]::Replace((Join-Path $operation 'candidate.exe'), $current, (Join-Path $operation 'displaced.exe'))
    }
    if ($phase -eq 'after-archive') {
        [IO.File]::Move((Join-Path $operation 'previous.exe'), (Join-Path $phaseFolder 'OLD-Smart Office Installer.exe'))
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
    if ($phase -ne 'before-publication' -and $phase -ne 'missing-canonical' -and (Get-FileHash -LiteralPath (Join-Path $phaseFolder 'OLD-Smart Office Installer.exe')).Hash -ne $oldHash) {
        throw 'Recovery archive bytes differ.'
    }
    Write-Host "PASS: Normal GUI startup recovered interruption at $phase."
}

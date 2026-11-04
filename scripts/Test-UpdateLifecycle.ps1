$ErrorActionPreference = 'Stop'
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
do {
    $result = @(Get-ChildItem -LiteralPath (Join-Path $fixture '.pcninja-office-updates') -Filter result.txt -Recurse -ErrorAction SilentlyContinue)
    if ($result.Count -gt 0) { break }
    Start-Sleep -Milliseconds 200
} while ([DateTime]::UtcNow -lt $deadline)
if (-not $result -or (Get-Content -LiteralPath $result[0].FullName -Raw) -notlike 'Updated to 3.3.13.0*') {
    throw "Helper did not report confirmed success. Inspect $fixture"
}
if ((Get-FileHash -LiteralPath $old).Hash -ne $newHash -or
    (Get-FileHash -LiteralPath (Join-Path $fixture 'OLD-Smart Office Installer.exe')).Hash -ne $oldHash) {
    throw 'Same-name replacement or exact predecessor archive mismatch.'
}
Write-Host "PASS: Elevated parent/helper same-name replacement, new GUI readiness and exact OLD archive. Disposable fixture retained at $fixture"

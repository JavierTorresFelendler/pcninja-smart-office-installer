$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Core
$root = Split-Path -Parent $PSScriptRoot
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'bin\Release\net48\Smart Office Installer.exe'))
$type = $assembly.GetType('OfficeSmart.InstallerForm', $true)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$form = [Activator]::CreateInstance($type)
function Field($name) { $type.GetField($name, $flags).GetValue($form) }
function Set-Field($name, $value) { $type.GetField($name, $flags).SetValue($form, $value) }
function Call($name, [object[]]$values) { $type.GetMethod($name, $flags).Invoke($form, $values) | Out-Null }
try {
    Set-Field 'updateCheckInProgress' $true
    $form.Show()
    $manifestType = $type.GetNestedType('UpdateManifest', [Reflection.BindingFlags]::NonPublic)
    $packageType = $type.GetNestedType('UpdatePackage', [Reflection.BindingFlags]::NonPublic)
    $manifest = [Activator]::CreateInstance($manifestType, $true)
    $package = [Activator]::CreateInstance($packageType, $true)
    $package.FileName = 'Smart Office Installer.exe'
    $package.Url = 'https://github.com/JavierTorresFelendler/pcninja-smart-office-installer/releases/latest/download/SmartOfficeInstaller.exe'
    $package.Sha256 = 'A' * 64
    $manifest.Channel = 'stable'
    $manifest.Version = '99.0.0.0'
    $manifest.MinimumSupportedVersion = '3.3.4.0'
    $manifest.ReleaseNotesUrl = 'https://github.com/JavierTorresFelendler/pcninja-smart-office-installer/releases/tag/v99.0.0'
    $manifest.Portable = $package
    Set-Field 'availableUpdateManifest' $manifest
    Set-Field 'availableUpdateVersion' ([version]'99.0.0.0')
    $errors = New-Object 'System.Collections.Generic.List[string]'
    Set-Field 'reportAppUpdateFailure' ([Action[string]]{ param($message) $errors.Add($message) })
    $prepare = $type.GetField('prepareAppUpdate', $flags)
    $parameters = @($prepare.FieldType.GetMethod('Invoke').GetParameters() | ForEach-Object {
        [Linq.Expressions.Expression]::Parameter($_.ParameterType, $_.Name)
    })
    foreach ($mode in @('decline', 'cancel', 'failure')) {
        $exception = if ($mode -eq 'cancel') { New-Object OperationCanceledException } else { New-Object IO.IOException('Fixture transfer failure') }
        $task = [Threading.Tasks.Task]::FromException($exception)
        $body = [Linq.Expressions.Expression]::Constant($task, [Threading.Tasks.Task])
        $callback = [Linq.Expressions.Expression]::Lambda($prepare.FieldType, $body, [Linq.Expressions.ParameterExpression[]]$parameters).Compile()
        Set-Field 'prepareAppUpdate' $callback
        if ($mode -eq 'decline') { Set-Field 'confirmAppUpdate' ([Func[string,bool]]{ param($message) $false }) }
        else { Set-Field 'confirmAppUpdate' ([Func[string,bool]]{ param($message) $true }) }
        Call 'OnUpdateButtonClick' @()
        if (-not $form.Visible -or (Field 'appUpdateInProgress')) { throw "The current app did not recover after $mode." }
    }
    if ($errors.Count -ne 1 -or $errors[0] -notlike '*Fixture transfer failure*') { throw 'Unexpected update error handling.' }
    Set-Field 'appUpdateInProgress' $true
    Call 'OnUpdateButtonClick' @()
    if ($errors.Count -ne 1) { throw 'A busy update started another operation.' }
    Set-Field 'appUpdateInProgress' $false
    Write-Host 'PASS: Decline, cancellation, transfer failure and busy update preserve the current GUI. No HTTP/helper/Office operations.'
}
finally { $form.Dispose() }

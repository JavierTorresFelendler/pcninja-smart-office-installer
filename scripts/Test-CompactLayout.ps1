param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "bin\$Configuration\net48\Smart Office Installer.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the application before running this check.' }
$assembly = [System.Reflection.Assembly]::LoadFrom($exe)
$type = $assembly.GetType('OfficeSmart.InstallerForm', $true)
$flags = [System.Reflection.BindingFlags]'Instance,NonPublic'
$form = [Activator]::CreateInstance($type)
function Field($name) { $type.GetField($name, $flags).GetValue($form) }
function Set-Field($name, $value) { $type.GetField($name, $flags).SetValue($form, $value) }
function Call($name, [object[]]$parameters = @()) { $type.GetMethod($name, $flags).Invoke($form, $parameters) | Out-Null }
try {
    # Suppress the startup update check. No Office operations are invoked.
    Set-Field 'updateCheckInProgress' $true
    $form.Show()
    [System.Windows.Forms.Application]::DoEvents()
    $logo = Field 'familyLogo'
    $site = Field 'familyWebsite'
    $footer = Field 'pFooter'
    $next = Field 'btnNext'
    $back = Field 'btnBack'
    $content = Field 'pContent'
    $form.ClientSize = New-Object System.Drawing.Size(740, 800)
    Call 'Render'
    if (-not $logo.Visible) { throw 'Logo should remain visible at the standard size.' }
    $fullTop = $site.Top
    foreach ($size in @((New-Object System.Drawing.Size(720, 500)), (New-Object System.Drawing.Size(500, 480)))) {
        $form.ClientSize = $size
        Set-Field 'step' 0
        Call 'Render'
        if ($logo.Visible -or $site.Top -ge $fullTop -or -not $site.Visible) {
            throw "Compact family view: logo=$($logo.Visible), site=$($site.Visible), top=$($site.Top), full=$fullTop, step=$(Field 'step')."
        }
        foreach ($familyName in @('LTSC', 'M365', 'Remove')) {
            $enumType = $type.Assembly.GetType('OfficeSmart.OfficeFamily', $true)
            Set-Field 'family' ([Enum]::Parse($enumType, $familyName))
            $last = if ($familyName -eq 'Remove') { 1 } else { 4 }
            foreach ($step in 0..$last) {
                Set-Field 'step' $step
                Call 'Render'
                if ($next.Left -lt 0 -or $next.Right -gt $footer.ClientSize.Width -or
                    $next.Top -lt 0 -or $next.Bottom -gt $footer.ClientSize.Height) {
                    throw "Next is outside the footer for $familyName step $step at $size."
                }
                if ($step -gt 0 -and ($back.Left -lt 0 -or $back.Right -gt $next.Left)) {
                    throw "Back is outside the footer or overlaps Next for $familyName step $step."
                }
                $active = @($content.Controls | Where-Object { $_.Visible })
                if ($active.Count -ne 1 -or $active[0].Height -ne $content.Height -or -not $active[0].AutoScroll) {
                    throw 'The active step did not retain a correctly sized scrollable view.'
                }
            }
        }
        Set-Field 'family' ([Enum]::Parse($enumType, 'LTSC'))
        Set-Field 'offline' $true
        Set-Field 'step' 4
        Call 'Render'
        if ($next.Text -ne 'Create Package' -or $next.Right -gt $footer.ClientSize.Width) {
            throw 'Offline package action is clipped or missing.'
        }
        Set-Field 'offline' $false
    }
    $done = New-Object System.ComponentModel.RunWorkerCompletedEventArgs($null, $null, $false)
    Call 'OnDone' @($null, $done.PSObject.BaseObject)
    if ($next.Right -gt $footer.ClientSize.Width -or $back.Right -gt $next.Left) {
        throw 'Done navigation buttons are clipped or overlap.'
    }
    $form.ClientSize = New-Object System.Drawing.Size(740, 800)
    Set-Field 'step' 0
    Call 'Render'
    if (-not $logo.Visible -or $site.Top -ne $fullTop) { throw 'The standard branding did not return.' }
    Write-Host 'PASS: Compact branding, family flows, footer navigation, scrolling and done state. No network or Office actions.'
}
finally { $form.Dispose() }

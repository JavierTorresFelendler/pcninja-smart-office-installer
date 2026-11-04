param([string]$Configuration = 'Release', [string]$ScreenshotDirectory = '', [string]$ExePath = '')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$exe = if ($ExePath) { (Resolve-Path -LiteralPath $ExePath).Path } else { Join-Path $root "bin\$Configuration\net48\Smart Office Installer.exe" }
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the application before running this check.' }
$assembly = [System.Reflection.Assembly]::LoadFrom($exe)
$type = $assembly.GetType('OfficeSmart.InstallerForm', $true)
$flags = [System.Reflection.BindingFlags]'Instance,NonPublic'
$form = [Activator]::CreateInstance($type)
function Field($name) { $type.GetField($name, $flags).GetValue($form) }
function Set-Field($name, $value) { $type.GetField($name, $flags).SetValue($form, $value) }
function Call($name, [object[]]$parameters = @()) { $type.GetMethod($name, $flags).Invoke($form, $parameters) | Out-Null }
function Check-FamilyFits {
    [System.Windows.Forms.Application]::DoEvents()
    $page = Field 'pFamily'
    if (-not $content.ClientRectangle.Contains($page.Bounds)) { throw "Family page $($page.Bounds) is outside viewport $($content.ClientRectangle) at $($form.ClientSize)." }
    if ($page.HorizontalScroll.Visible -or $page.VerticalScroll.Visible -or
        $content.HorizontalScroll.Visible -or $content.VerticalScroll.Visible) {
        throw 'The family screen must fit without scrollbars.'
    }
    foreach ($child in $page.Controls) {
        if ($child.Visible -and -not $page.ClientRectangle.Contains($child.Bounds)) {
            throw "Family control is outside the viewport: $($child.GetType().Name) $($child.Text) $($child.Bounds)."
        }
    }
    if (-not $form.ClientRectangle.Contains($footer.Bounds)) { throw 'Footer is outside the form.' }
}
function Save-Preview($name) {
    if (-not $ScreenshotDirectory) { return }
    [IO.Directory]::CreateDirectory($ScreenshotDirectory) | Out-Null
    $bitmap = New-Object System.Drawing.Bitmap($form.Width, $form.Height)
    try {
        $form.DrawToBitmap($bitmap, (New-Object System.Drawing.Rectangle([System.Drawing.Point]::Empty, $form.Size)))
        $bitmap.Save((Join-Path $ScreenshotDirectory $name))
    }
    finally { $bitmap.Dispose() }
}
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
    Check-FamilyFits
    Save-Preview 'Standard.png'
    $fullTop = $site.Top
    foreach ($size in @((New-Object System.Drawing.Size(740, 600)), (New-Object System.Drawing.Size(740, 420)), (New-Object System.Drawing.Size(720, 500)), (New-Object System.Drawing.Size(500, 480)))) {
        $form.ClientSize = $size
        Set-Field 'step' 0
        Call 'Render'
        if ($logo.Visible -or $site.Top -ge $fullTop -or -not $site.Visible) {
            throw "Compact family view: logo=$($logo.Visible), site=$($site.Visible), top=$($site.Top), full=$fullTop, step=$(Field 'step')."
        }
        Check-FamilyFits
        Save-Preview "Compact-$($size.Width)x$($size.Height).png"
        foreach ($familyName in @('LTSC', 'M365', 'Remove')) {
            $enumType = $type.Assembly.GetType('OfficeSmart.OfficeFamily', $true)
            Call 'SelectFamily' @([Enum]::Parse($enumType, $familyName))
            # Only advance off the initial page; never click a final operation.
            $next.PerformClick()
            if ((Field 'step') -ne 1) { throw 'Next failed to advance from Family.' }
            $back.PerformClick()
            if ((Field 'step') -ne 0) { throw 'Back failed to return to Family.' }
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
                if ($active.Count -ne 1 -or $active[0].Height -ne $content.Height -or ($step -gt 0 -and -not $active[0].AutoScroll)) {
                    throw 'The active step did not retain a correctly sized scrollable view.'
                }
                if ($step -eq 0) { Check-FamilyFits }
            }
        }
        Set-Field 'family' ([Enum]::Parse($enumType, 'LTSC'))
        Set-Field 'step' 1
        Call 'Render'
        $content.AutoScrollPosition = New-Object System.Drawing.Point(240, 0)
        Set-Field 'step' 0
        Call 'Render'
        Check-FamilyFits
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
    foreach ($cardName in @('fCardLTSC', 'fCard365')) {
        foreach ($label in (Field $cardName).Controls) {
            if (-not $label.Visible) { throw 'Standard card descriptions did not return.' }
        }
    }
    Check-FamilyFits
    Write-Host 'PASS: Family screen fits without scrolling; compact branding, family flows, footer navigation and done state. No network or Office actions.'
}
finally { $form.Dispose() }

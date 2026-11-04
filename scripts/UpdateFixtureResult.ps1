function Test-CompletedAppUpdate {
    param(
        [string]$Status,
        [string]$CurrentHash,
        [string]$ExpectedCurrentHash,
        [string]$PreviousHash,
        [string]$ExpectedPreviousHash,
        [bool]$Pending
    )
    $confirmed = $Status -cmatch '^(Updated to 3\.3\.13\.0; previous copy: |Recovered new GUI startup; previous copy: )[^\r\n]+\z'
    return ($confirmed -and -not $Pending -and
        $ExpectedCurrentHash -match '^[a-fA-F0-9]{64}$' -and
        $ExpectedPreviousHash -match '^[a-fA-F0-9]{64}$' -and
        $CurrentHash -eq $ExpectedCurrentHash -and $PreviousHash -eq $ExpectedPreviousHash)
}

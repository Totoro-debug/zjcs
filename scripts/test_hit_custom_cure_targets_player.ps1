param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet(10029,10072)][int[]]$ItemIds = @(10029,10072),
    [ValidateSet('up','down','left','right')][string[]]$Directions = @('right'),
    [ValidateSet('normal','blocked','shield')][string[]]$Combinations = @('normal','blocked','shield')
)
$ErrorActionPreference = 'Stop'
$previous = @{}
Get-ChildItem Env:ZJCS_* | ForEach-Object { $previous[$_.Name] = $_.Value }
$cases = @()
try {
    foreach ($mode in $Combinations) { foreach ($item in $ItemIds) { foreach ($direction in $Directions) {
        Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
        $env:ZJCS_HIT_CUSTOM_CURE_TARGETS = '1'
        $env:ZJCS_STATUS_HIT_CUSTOM_CURE_ACCEPTANCE = $mode
        $before = @{}
        Get-ChildItem -LiteralPath $Workspace -Directory -Filter "native-$item-$direction-*" |
            ForEach-Object { $before[$_.FullName] = $true }
        & (Join-Path $PSScriptRoot 'test_first_promotion_skill_player.ps1') -Player $Player -Workspace $Workspace -ItemIds $item -Directions $direction
        $runs = @(Get-ChildItem -LiteralPath $Workspace -Directory -Filter "native-$item-$direction-*" |
            Where-Object { -not $before.ContainsKey($_.FullName) })
        if ($runs.Count -ne 1) { throw 'Expected one original absorption Player diagnostic.' }
        $run = $runs[0].FullName
        $evidence = Get-Content -LiteralPath (Join-Path $run 'hit-custom-cure-targets.json') -Raw | ConvertFrom-Json
        $expected = if ($item -eq 10072) { 6 } else { 2 }
        if (-not $evidence.passed -or $evidence.ItemId -ne $item -or $evidence.Combination -ne $mode -or
            $evidence.DamageHits -ne $expected -or $evidence.NativeCures -ne $expected -or
            $evidence.TriggerCount -ne $expected -or $evidence.TargetIds.Count -ne 2 -or
            @($evidence.TargetIds | Sort-Object -Unique).Count -ne 2) {
            throw "Original independent target / self-cure / saved-counter mismatch: $run"
        }
        if (-not (Test-Path -LiteralPath (Join-Path $run "hit-custom-cure-$item-$mode.png")) -or
            -not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Hit custom cure targets] Two original targets')) {
            throw "Original multi-target absorption native observation is incomplete: $run"
        }
        $cases += @{ item=$item; direction=$direction; combination=$mode; run=$run; hits=$expected }
        Write-Output "Original absorption targets passed: $item / $mode / $direction"
    } } }
    @{ passed=$true; cases=$cases } | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 -LiteralPath (
        Join-Path $Workspace ('hit-custom-cure-targets-' + [guid]::NewGuid().ToString('N') + '.json'))
}
finally {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}

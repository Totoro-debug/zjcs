param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet(10030,10048)][int[]]$ItemIds = @(10030,10048),
    [ValidateSet('up','down','left','right')][string[]]$Directions = @('up','down','left','right')
)
$ErrorActionPreference = 'Stop'
$previous = @{}
Get-ChildItem Env:ZJCS_* | ForEach-Object { $previous[$_.Name] = $_.Value }
try {
    foreach ($item in $ItemIds) { foreach ($direction in $Directions) {
        Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
        $env:ZJCS_CHAINED_HIT_ACCEPTANCE = '1'
        $before = @{}
        Get-ChildItem -LiteralPath $Workspace -Directory -Filter "native-$item-$direction-*" |
            ForEach-Object { $before[$_.FullName] = $true }
        & (Join-Path $PSScriptRoot 'test_first_promotion_skill_player.ps1') -Player $Player -Workspace $Workspace -ItemIds $item -Directions $direction
        $runs = @(Get-ChildItem -LiteralPath $Workspace -Directory -Filter "native-$item-$direction-*" |
            Where-Object { -not $before.ContainsKey($_.FullName) })
        if ($runs.Count -ne 1) { throw 'Expected one original chained-hit Player diagnostic.' }
        $run = $runs[0].FullName
        $expected = if ($item -eq 10030) { 3 } else { 4 }
        $snapshot = Get-Content -LiteralPath (Join-Path $run 'chained-hit-checkpoint.json') -Raw | ConvertFrom-Json
        $entity = if ($item -eq 10030) { 10820 } else { 11090 }
        $casts = @($snapshot.checkpoint.Receipts.Event.SkillCasts | Where-Object { $_.ActorId -eq 1 -and $_.SkillEntityId -eq $entity })
        if ($casts.Count -ne 1 -or $casts[0].HitSelections.Count -ne $expected -or
            @($casts[0].HitSelections | Where-Object { -not $_.HasChainedOrigin }).Count -ne 0) {
            throw "Original chained receipts differ: $run"
        }
        if (@(Get-ChildItem -LiteralPath $run -File -Filter 'chained-hit-*.png').Count -ne $expected -or
            -not (Test-Path -LiteralPath (Join-Path $run 'chained-hits-ended.png')) -or
            -not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Chained hits] Original multi-target flight origins, native graphs, disk receipt and retirement passed:')) {
            throw "Original chained native observation is incomplete: $run"
        }
        Write-Output "Original chained Player passed: $item / $direction / $expected hops"
    } }
}
finally {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}

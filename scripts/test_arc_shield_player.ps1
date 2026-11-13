param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet('up','down','left','right')][string[]]$Directions = @('up','down','left','right')
)
$ErrorActionPreference = 'Stop'
$previous = @{}
Get-ChildItem Env:ZJCS_* | ForEach-Object { $previous[$_.Name] = $_.Value }
try {
    foreach ($direction in $Directions) {
        Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
        $env:ZJCS_ARC_SHIELD_LIFECYCLE = '1'
        $before = @{}
        Get-ChildItem -LiteralPath $Workspace -Directory -Filter "native-10024-$direction-*" |
            ForEach-Object { $before[$_.FullName] = $true }
        & (Join-Path $PSScriptRoot 'test_first_promotion_skill_player.ps1') -Player $Player -Workspace $Workspace -ItemIds 10024 -Directions $direction
        $runs = @(Get-ChildItem -LiteralPath $Workspace -Directory -Filter "native-10024-$direction-*" |
            Where-Object { -not $before.ContainsKey($_.FullName) })
        if ($runs.Count -ne 1) { throw 'Expected one original arc-shield Player diagnostic.' }
        $run = $runs[0].FullName
        foreach ($name in @('arc-shield-round-1.png','arc-shield-round-2.png','arc-shield-round-3.png',
            'arc-shield-restored.png','arc-shield-ended.png','arc-shield-checkpoint.json')) {
            if (-not (Test-Path -LiteralPath (Join-Path $run $name))) { throw "Arc-shield lifecycle evidence missing: $name ($run)" }
        }
        $saved = Get-Content -LiteralPath (Join-Path $run 'arc-shield-checkpoint.json') -Raw | ConvertFrom-Json
        $playerUnit = @($saved.checkpoint.Units | Where-Object Id -eq 1)
        $status = @($playerUnit.Statuses | Where-Object ClassId -eq 24030)
        if ($status.Count -ne 1 -or $status[0].DurationRound -ne 3 -or
            $status[0].SourceSkillRank -ne 2 -or $status[0].SourceSkillLevel -ne 20 -or
            $status[0].ApplyingSkillItemId -ne 10024 -or $status[0].ShieldKind -ne 0 -or
            -not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Arc shield] Original 24030 body particles, disk rebind, three owner rounds, property rollback and retirement passed.')) {
            throw "Original arc-shield native assertions or saved state differ: $run"
        }
        Write-Output "Original arc-shield Player passed: 10024 / $direction / three owner rounds"
    }
}
finally {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}

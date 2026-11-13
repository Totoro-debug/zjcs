param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Workspace,
    [ValidateSet('natural','blocked')][string[]]$Modes = @('natural','blocked')
)
$ErrorActionPreference = 'Stop'
$previous = @{}
Get-ChildItem Env:ZJCS_* | ForEach-Object { $previous[$_.Name] = $_.Value }
$cases = @()
try {
    foreach ($mode in $Modes) {
        Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
        $env:ZJCS_SHIELD_EXPIRY = $mode
        $env:ZJCS_STATUS_HIT_CUSTOM_CURE_ACCEPTANCE = 'shield'
        $before = @{}
        Get-ChildItem -LiteralPath $Workspace -Directory -Filter 'native-10029-right-*' |
            ForEach-Object { $before[$_.FullName] = $true }
        & (Join-Path $PSScriptRoot 'test_first_promotion_skill_player.ps1') -Player $Player -Workspace $Workspace -ItemIds 10029 -Directions right
        $runs = @(Get-ChildItem -LiteralPath $Workspace -Directory -Filter 'native-10029-right-*' |
            Where-Object { -not $before.ContainsKey($_.FullName) })
        if ($runs.Count -ne 1) { throw 'Expected one original shield expiry diagnostic.' }
        $run = $runs[0].FullName
        $evidence = Get-Content -LiteralPath (Join-Path $run 'shield-expiry.json') -Raw | ConvertFrom-Json
        if (-not $evidence.passed -or $evidence.Mode -ne $mode -or $evidence.Remaining -le 0 -or
            $evidence.NativeCures -ne 1 -or $evidence.RuleCures -ne 1 -or $evidence.AbsorptionCount -ne 1 -or
            ($mode -eq 'natural' -and $evidence.Healed -le 0) -or ($mode -eq 'blocked' -and $evidence.Healed -ne 0)) {
            throw "Original shield expiry cure count or interception differs: $run"
        }
        foreach ($name in @('shield-before-cold-scene.png','shield-after-cold-scene.png',"shield-expiry-$mode.png")) {
            if (-not (Test-Path -LiteralPath (Join-Path $run $name))) { throw "Missing shield frame: $name ($run)" }
        }
        if ((Get-FileHash -LiteralPath (Join-Path $run 'shield-cold-scene-before.json')).Hash -ne
            (Get-FileHash -LiteralPath (Join-Path $run 'shield-cold-scene-after.json')).Hash -or
            -not (Select-String -LiteralPath (Join-Path $run 'player.log') -SimpleMatch '[Shield expiry] Original 24435 cold scene')) {
            throw "Original shield cold scene persistence or expiry observation did not finish: $run"
        }
        $cases += @{ mode=$mode; run=$run; nativeCures=$evidence.NativeCures; healed=$evidence.Healed }
        Write-Output "Original shield cold scene and expiry passed: $mode / $run"
    }
    @{ passed=$true; cases=$cases } | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 -LiteralPath (
        Join-Path $Workspace ('shield-expiry-player-' + [guid]::NewGuid().ToString('N') + '.json'))
}
finally {
    Get-ChildItem Env:ZJCS_* | ForEach-Object { Remove-Item ('Env:' + $_.Name) }
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}

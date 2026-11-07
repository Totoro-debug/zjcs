. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Get-FightCombatSources)

# Golden positions from Common.GridTransformComponent.CalcLocalToWorldPos and
# Common.MyMath.Rotate, using original Common.Direction rather than local AI indices.
$samples = @(
    @(0,1, 0,0, 0,$false, 4,7),
    @(0,1, 0,0, 1,$false, 4,5),
    @(0,1, 0,0, 2,$false, 3,6),
    @(0,1, 0,0, 3,$false, 5,6),
    @(2,1, .5,.5, 2,$true, 4,5),
    @(2,1, .5,.5, 2,$false, 4,8),
    @(2,1, .5,.5, 0,$true, 3,7),
    @(1,0, .25,.25, 1,$false, 4,6),
    @(2,0, .25,.25, 1,$false, 2,6),
    @(2,1, .5,.5, 99,$false, 6,7)
)
foreach ($sample in $samples) {
    $actual = [Zjcs.Combat.Official.FightGridTransformMath]::CalcLocalToWorldPos(
        [Zjcs.Combat.Official.FightGridPoint]::new($sample[0],$sample[1]),
        [Zjcs.Combat.Official.FightGridPoint]::new(4,6), $sample[2],$sample[3],$sample[4],$sample[5])
    if ($actual.X -ne $sample[6] -or $actual.Y -ne $sample[7]) {
        throw "Original grid transform mismatch: $sample => $($actual.X),$($actual.Y)"
    }
}
Write-Output 'Original grid transform passed: four directions, real centre, flip before rotation, ties-to-even and unchanged unsupported-direction fallback.'

param([switch]$Baseline)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
if (-not $env:ZJCS_VALIDATION_DIR) { throw 'Set ZJCS_VALIDATION_DIR to this session workspace.' }
$sources = @(Get-FightCombatSources)
if ($Baseline) {
    $sources = @($sources | ForEach-Object {
        if ((Split-Path $_ -Leaf) -in @('FightBattleStatistics.cs', 'FightBattleExecutor.cs')) {
            Join-Path $env:ZJCS_VALIDATION_DIR (([IO.Path]::GetFileNameWithoutExtension($_)) + '.before.cs')
        } else { $_ }
    })
}
Add-Type -Path ($sources + (Join-Path $PSScriptRoot 'fixtures/battle_damage_pose_receipts_harness.cs'))
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
function Read-Input($name) { [IO.File]::ReadAllText((Join-Path $inputs $name)) }
$skill = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest((Read-Input 'skill-rules-source-manifest.json'))
$battle = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest((Read-Input 'battle-rules-source-manifest.json'))
$rules = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest((Read-Input 'rules-source-manifest.json'))
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Input 'skill-rules.json'), $skill.OutputSha256, $skill.Manifest,
    (Read-Input 'battle-rules.json'), $battle.OutputSha256, $battle.Manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load((Read-Input 'rules.json'), $rules.OutputSha256, $rules.Manifest)
$formula = $catalog.CreateDamageFormula((Read-Input 'rules.json'), $rules.OutputSha256, $rules.Manifest)
$folder = if ($Baseline) { 'pose-baseline' } else { 'pose-captured' }
[BattleDamagePoseReceiptsHarness]::Run($catalog, $items, $formula, (Join-Path $env:ZJCS_VALIDATION_DIR $folder), [bool]$Baseline)
if (-not $Baseline -and (Test-Path (Join-Path $env:ZJCS_VALIDATION_DIR 'pose-baseline'))) {
    $originals = @(Get-ChildItem (Join-Path $env:ZJCS_VALIDATION_DIR 'pose-baseline') -Filter '*.json')
    if ($originals.Count -ne 7) { throw 'Expected seven actual pre-change checkpoint fixtures.' }
    foreach ($original in $originals) {
        $captured = Join-Path (Join-Path $env:ZJCS_VALIDATION_DIR 'pose-captured') $original.Name
        if ((Get-FileHash -LiteralPath $original.FullName).Hash -ne (Get-FileHash -LiteralPath $captured).Hash) {
            throw "Damage pose metadata changed checkpoint gameplay: $($original.Name)"
        }
    }
    Write-Output 'Seven complete checkpoints match before/after, excluding only new position evidence and captured absolute-cure metadata.'
}
Write-Output "Damage pose receipts $folder passed: pre-displacement point, original body cells, zero damage, full/effective cure and checkpoint compatibility."

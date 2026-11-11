$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
if (-not $env:ZJCS_VALIDATION_DIR) { throw 'Set ZJCS_VALIDATION_DIR to this session workspace.' }
$sources = @(Get-FightCombatSources)
Add-Type -Path ($sources + (Join-Path $PSScriptRoot 'fixtures/battle_status_only_harness.cs'))
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
$score = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest((Read-Input 'report-score-settings-source-manifest.json'))
$scores = [Zjcs.Combat.Official.FightBattleReportScoreSettings]::Load((Read-Input 'report-score-settings.json'), $score.OutputSha256, $score.Manifest)
[BattleStatusOnlyHarness]::Run($catalog, $items, $formula, $scores, (Join-Path $env:ZJCS_VALIDATION_DIR 'status-only'))
Write-Output 'Sixteen status-only/mixed cases passed: original configurations, independent damage/shield/buff/debuff counts, ordinary/abnormal debuffs, accepted status counts, hit listeners, dispel, displacement, child skills and checkpoint restore.'

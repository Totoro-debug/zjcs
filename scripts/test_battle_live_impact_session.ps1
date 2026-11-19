$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root = Split-Path -Parent $PSScriptRoot
$validation = Join-Path $root ('output/validation/live-impact-race-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $validation | Out-Null
$bridgeSource = [IO.File]::ReadAllText((Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleImpactViewBridge.cs'))
$boundary = 'views.QueueSelectedHit(cast, skillId, selection, elapsedSinceCast), out long revision);'
if ($bridgeSource.IndexOf($boundary, [StringComparison]::Ordinal) -lt 0 -or
    $bridgeSource.IndexOf($boundary, [StringComparison]::Ordinal) -ne $bridgeSource.LastIndexOf($boundary, [StringComparison]::Ordinal)) {
    throw 'The native selection handoff boundary changed.'
}
$bridgeSource = $bridgeSource.Replace($boundary, $boundary + "`n            SelectionBoundaryRace.AfterSelection?.Invoke(latch, selected, revision);")
$testBridge = Join-Path $validation 'OfficialBattleImpactViewBridge.cs'
[IO.File]::WriteAllText($testBridge, $bridgeSource, [Text.UTF8Encoding]::new($false))
Add-Type -Path (@(Get-FightCombatSources) + @(
    (Join-Path $PSScriptRoot 'fixtures/battle_live_impact_session_harness.cs'),
    $testBridge,
    (Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleLiveImpactSession.cs')))
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
function Read-Input($name) { [IO.File]::ReadAllText((Join-Path $inputs $name)) }
$skill = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest((Read-Input 'skill-rules-source-manifest.json'))
$battle = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest((Read-Input 'battle-rules-source-manifest.json'))
$rules = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest((Read-Input 'rules-source-manifest.json'))
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load((Read-Input 'skill-rules.json'), $skill.OutputSha256,
    $skill.Manifest, (Read-Input 'battle-rules.json'), $battle.OutputSha256, $battle.Manifest)
$formula = $catalog.CreateDamageFormula((Read-Input 'rules.json'), $rules.OutputSha256, $rules.Manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load((Read-Input 'rules.json'), $rules.OutputSha256, $rules.Manifest)
[Zjcs.Presentation.Combat.BattleLiveImpactSessionHarness]::Run($catalog, $formula, $items)

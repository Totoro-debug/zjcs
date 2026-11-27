$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskInputs = Join-Path $taskRoot 'unity/Assets/Official/Combat'
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_asura_hand_harness.cs') + (Join-Path $PSScriptRoot 'fixtures/battle_charged_burst_harness.cs') + (Join-Path $PSScriptRoot 'fixtures/battle_damage_types_harness.cs'))
$taskSkillSource = Get-Content (Join-Path $taskInputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$taskBattleSource = Get-Content (Join-Path $taskInputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$taskRuleSource = Get-Content (Join-Path $taskInputs 'rules-source-manifest.json') -Raw | ConvertFrom-Json
$taskCatalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $taskInputs 'skill-rules.json')), $taskSkillSource.output_sha256, $taskSkillSource.manifest,
    [IO.File]::ReadAllText((Join-Path $taskInputs 'battle-rules.json')), $taskBattleSource.output_sha256, $taskBattleSource.manifest)
$taskRules = [IO.File]::ReadAllText((Join-Path $taskInputs 'rules.json'))
$taskFormula = $taskCatalog.CreateDamageFormula($taskRules, $taskRuleSource.output_sha256, $taskRuleSource.manifest)
$taskItems = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($taskRules, $taskRuleSource.output_sha256, $taskRuleSource.manifest)
$taskPack = Join-Path $taskRoot 'unity/Assets/Game/Resources/Combat/SkillRules/10065.json'
$taskSource = Get-Content ($taskPack -replace '\.json$', '-source-manifest.json') -Raw | ConvertFrom-Json
$taskCatalog.ApplySkillRulePack([IO.File]::ReadAllText($taskPack), $taskSource.output_sha256, $taskSource.manifest)
$taskPack=Join-Path $taskRoot 'unity/Assets/Game/Resources/Combat/SkillRules/10063.json'
$taskSource=Get-Content ($taskPack -replace '\.json$','-source-manifest.json') -Raw | ConvertFrom-Json
$taskCatalog.ApplySkillRulePack([IO.File]::ReadAllText($taskPack),$taskSource.output_sha256,$taskSource.manifest)
[BattleAsuraHandHarness]::Run($taskCatalog, $taskFormula, $taskItems)

# Compile the official method verbatim, independently of the migrated formula.
$taskOriginal = [IO.File]::ReadAllText((Join-Path $taskRoot '.tools/source-packages/current/911_156995/official-code/common-source/Common/BattleFormulaHandler.cs'))
$taskStart = $taskOriginal.IndexOf('public long CalcTrueDamage(')
if ($taskStart -lt 0) { throw 'Official breaking shield formula is missing.' }
$taskOpen = $taskOriginal.IndexOf('{', $taskStart)
$taskDepth = 1
$taskEnd = $taskOpen + 1
while ($taskDepth -gt 0 -and $taskEnd -lt $taskOriginal.Length) {
    if ($taskOriginal[$taskEnd] -eq '{') { $taskDepth++ }
    if ($taskOriginal[$taskEnd] -eq '}') { $taskDepth-- }
    $taskEnd++
}
if ($taskDepth -ne 0) { throw 'Official breaking shield formula is incomplete.' }
$taskMethod = $taskOriginal.Substring($taskStart, $taskEnd - $taskStart)
Add-Type -TypeDefinition ('using System; public class AsuraOriginalBreakingOracle {' + $taskMethod + '}' +
    'public static class Parsers { public static OracleSettings GameSettings = new OracleSettings(); }' +
    'public class OracleSettings { public OracleBattle Battle = new OracleBattle(); }' +
    'public class OracleBattle { public float TrueDamageAddPerWhenHasShield = 1f; }')
$taskOracle = [AsuraOriginalBreakingOracle]::new()
$taskCases = 0
foreach ($taskDamage in @(0L, 1L, -1L, -2L, -99L, -100L, -101L, -1000000001L)) {
    foreach ($taskShield in @(0L, 1L, 2L, 49L, 50L, 99L, 100L, 101L, 150L, 200L, 1000000000L)) {
        if ($taskFormula.CalcTrueDamage($taskDamage, $taskShield) -ne $taskOracle.CalcTrueDamage($taskDamage, $taskShield)) {
            throw "Official breaking shield formula diverged for damage=$taskDamage shield=$taskShield"
        }
        $taskCases++
    }
}
if ($taskFormula.CalculateTrueDamageTargetScale(-101L, 3) -ne -202L -or
    $taskFormula.CalculateTrueDamageTargetScale(-101L, 1) -ne -101L -or
    $taskFormula.CalculateTrueDamageTargetScale(-101L, 7) -ne -101L) {
    throw 'The configured summon-only breaking target scale was not retained.'
}
Write-Output "Breaking shield formula matched the original implementation in $taskCases boundary cases; summon configuration passed."
[BattleChargedBurstHarness]::Run($taskCatalog, $taskFormula, $taskItems)

$taskOriginalShield = [Func[long,long,long]]{param([long]$damage,[long]$shield) $taskOracle.CalcTrueDamage($damage,$shield)}
[BattleDamageTypesHarness]::Run($taskFormula,$taskOriginalShield)

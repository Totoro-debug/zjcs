$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskInputs = Join-Path $taskRoot 'unity/Assets/Official/Combat'
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_extinction_blade_harness.cs'))
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
$taskPack=Join-Path $taskRoot 'unity/Assets/Game/Resources/Combat/SkillRules/10069.json'
$taskSource=Get-Content ($taskPack -replace '\.json$','-source-manifest.json') -Raw | ConvertFrom-Json
$taskCatalog.ApplySkillRulePack([IO.File]::ReadAllText($taskPack),$taskSource.output_sha256,$taskSource.manifest)
[BattleExtinctionBladeHarness]::Run($taskCatalog, $taskFormula, $taskItems)

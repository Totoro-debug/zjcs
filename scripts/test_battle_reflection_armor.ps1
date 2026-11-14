$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root=Split-Path -Parent $PSScriptRoot
& python -B (Join-Path $PSScriptRoot 'prepare_battle_reflection_armor_sources.py') --verify
if($LASTEXITCODE -ne 0){throw 'Original reflection armor source identity check failed.'}
$inputPath=Join-Path $root 'unity/Assets/Official/Combat'
Add-Type -Path (@(Get-FightCombatSources)+(Join-Path $PSScriptRoot 'fixtures/battle_reflection_armor_harness.cs'))
$sp=Get-Content (Join-Path $inputPath 'skill-rules-source-manifest.json') -Raw|ConvertFrom-Json
$bp=Get-Content (Join-Path $inputPath 'battle-rules-source-manifest.json') -Raw|ConvertFrom-Json
$rp=Get-Content (Join-Path $inputPath 'rules-source-manifest.json') -Raw|ConvertFrom-Json
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputPath 'skill-rules.json')),$sp.output_sha256,$sp.manifest,
    [IO.File]::ReadAllText((Join-Path $inputPath 'battle-rules.json')),$bp.output_sha256,$bp.manifest)
$rules=[IO.File]::ReadAllText((Join-Path $inputPath 'rules.json'))
$formula=$catalog.CreateDamageFormula($rules,$rp.output_sha256,$rp.manifest)
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,$rp.output_sha256,$rp.manifest)
[BattleReflectionArmorHarness]::Run($catalog,$formula,$items)

param([switch]$Record)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$root=Split-Path -Parent $PSScriptRoot
$inputPath=Join-Path $root 'unity/Assets/Official/Combat'
Invoke-ZjcsValidation -Name 'arc-shield-growth-rules' -ScriptBlock {
    param($workspace)
    $argsList=@('-B',(Join-Path $PSScriptRoot 'prepare_battle_arc_growth_reference.py'))
    if($Record){$argsList+='--record'}
    & python @argsList
    if($LASTEXITCODE -ne 0){throw 'Original arc growth preparation failed.'}
    Add-Type -Path (@(Get-FightCombatSources)+(Join-Path $workspace 'arc-growth-reference.cs')+
        (Join-Path $PSScriptRoot 'fixtures/battle_arc_shield_growth_harness.cs'))
    $sp=Get-Content (Join-Path $inputPath 'skill-rules-source-manifest.json') -Raw|ConvertFrom-Json
    $bp=Get-Content (Join-Path $inputPath 'battle-rules-source-manifest.json') -Raw|ConvertFrom-Json
    $rp=Get-Content (Join-Path $inputPath 'rules-source-manifest.json') -Raw|ConvertFrom-Json
    $skill=[IO.File]::ReadAllText((Join-Path $inputPath 'skill-rules.json'))
    $battle=[IO.File]::ReadAllText((Join-Path $inputPath 'battle-rules.json'))
    $rules=[IO.File]::ReadAllText((Join-Path $inputPath 'rules.json'))
    $catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skill,$sp.output_sha256,$sp.manifest,$battle,$bp.output_sha256,$bp.manifest)
    $formula=$catalog.CreateDamageFormula($rules,$rp.output_sha256,$rp.manifest)
    $items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,$rp.output_sha256,$rp.manifest)
    [BattleArcShieldGrowthHarness]::Run([IO.File]::ReadAllText((Join-Path $workspace 'arc-growth-input.json')),$catalog,$formula,$items)
}

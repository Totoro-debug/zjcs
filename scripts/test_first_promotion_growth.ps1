param([switch]$Record)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$root=Split-Path -Parent $PSScriptRoot
Invoke-ZjcsValidation -Name 'first-promotion-growth' -ScriptBlock {
    param($workspace)
    $argsList=@('-B',(Join-Path $PSScriptRoot 'prepare_first_promotion_growth_reference.py'))
    if($Record){$argsList+='--record'}
    & python @argsList
    if($LASTEXITCODE -ne 0){throw 'Original first-promotion growth preparation failed.'}
    Add-Type -Path (@(Get-FightCombatSources)+(Join-Path $workspace 'first-promotion-growth-reference.cs')+
        (Join-Path $PSScriptRoot 'fixtures/first_promotion_growth_harness.cs'))
    $inputPath=Join-Path $root 'unity/Assets/Official/Combat'
    $sp=Get-Content (Join-Path $inputPath 'skill-rules-source-manifest.json') -Raw|ConvertFrom-Json
    $bp=Get-Content (Join-Path $inputPath 'battle-rules-source-manifest.json') -Raw|ConvertFrom-Json
    $catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
        [IO.File]::ReadAllText((Join-Path $inputPath 'skill-rules.json')),$sp.output_sha256,$sp.manifest,
        [IO.File]::ReadAllText((Join-Path $inputPath 'battle-rules.json')),$bp.output_sha256,$bp.manifest)
    [FirstPromotionGrowthHarness]::Run([IO.File]::ReadAllText((Join-Path $workspace 'first-promotion-growth-input.json')),$catalog)
}

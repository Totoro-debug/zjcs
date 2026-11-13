$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
$root = Split-Path -Parent $PSScriptRoot
Invoke-ZjcsValidation -Name 'first-promotion-passive-growth' -ScriptBlock {
    param($workspace)
    & python -B (Join-Path $PSScriptRoot 'prepare_first_promotion_passive_reference.py') --output $workspace
    if ($LASTEXITCODE -ne 0) { throw 'Original passive growth reference preparation failed.' }
    Add-Type -Path ((Get-FightCombatSources) + @(Join-Path $workspace 'passive-growth-reference.cs'))
    function Read-Combat($name) { [IO.File]::ReadAllText((Join-Path $root "unity/Assets/Official/Combat/$name.json")) }
    function Hash-Combat($name) { (Get-FileHash (Join-Path $root "unity/Assets/Official/Combat/$name.json")).Hash.ToLowerInvariant() }
    $catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
        (Read-Combat 'skill-rules'), (Hash-Combat 'skill-rules'), '854_156383',
        (Read-Combat 'battle-rules'), (Hash-Combat 'battle-rules'), '854_156383')
    $count = [SourcePassiveGrowth.Validation]::Run($catalog)
    Write-Output "First-promotion passive growth passed: 47 configured passive contributions; $count original-formula comparisons and independent-result checks. Trigger scheduling, UI, animation and audio are outside this check."
}

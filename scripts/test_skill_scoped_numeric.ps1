$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/skill_scoped_numeric_harness.cs'))
$inputs=Join-Path (Split-Path -Parent $PSScriptRoot) 'unity/Assets/Official/Combat'
$sp=Get-Content (Join-Path $inputs 'skill-rules-source-manifest.json') -Raw | ConvertFrom-Json
$bp=Get-Content (Join-Path $inputs 'battle-rules-source-manifest.json') -Raw | ConvertFrom-Json
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText((Join-Path $inputs 'skill-rules.json')), $sp.output_sha256, $sp.manifest,
    [IO.File]::ReadAllText((Join-Path $inputs 'battle-rules.json')), $bp.output_sha256, $bp.manifest)
$fixture = & python -B (Join-Path $PSScriptRoot 'fixtures/prepare_skill_scoped_numeric_fixture.py') | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Could not generate the isolated numeric regression fixture.' }
[SkillScopedNumericHarness]::Run($catalog, $fixture.pack, $fixture.sentinel, [string[]]$fixture.invalid)

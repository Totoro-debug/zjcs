$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_status_seconds_harness.cs'))
$inputPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'unity/Assets/Official/Combat'
function Read-SecondsInput($name) { [IO.File]::ReadAllText((Join-Path $inputPath "$name.json")) }
$sp = Read-SecondsInput 'skill-rules-source-manifest' | ConvertFrom-Json
$bp = Read-SecondsInput 'battle-rules-source-manifest' | ConvertFrom-Json
$rp = Read-SecondsInput 'rules-source-manifest' | ConvertFrom-Json
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load((Read-SecondsInput 'skill-rules'),$sp.output_sha256,$sp.manifest,(Read-SecondsInput 'battle-rules'),$bp.output_sha256,$bp.manifest)
$formula = $catalog.CreateDamageFormula((Read-SecondsInput 'rules'),$rp.output_sha256,$rp.manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load((Read-SecondsInput 'rules'),$rp.output_sha256,$rp.manifest)
try { [BattleStatusSecondsHarness]::Run($catalog,$formula,$items) }
catch { Write-Output $_.Exception.ToString(); throw }
Write-Output 'Status seconds passed: original airborne/recovery configuration, hit offsets, reset/block/miss/displacement, pause, saved pending hits and remaining time, provenance and 128 frame partitions.'

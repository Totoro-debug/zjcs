$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$root=Split-Path -Parent $PSScriptRoot
Add-Type -Path (@(Get-FightCombatSources)+(Join-Path $PSScriptRoot 'fixtures/battle_round_interval_filter_harness.cs'))
$inputs=Join-Path $root 'unity/Assets/Official/Combat'
function Read-IntervalInput($name){[IO.File]::ReadAllText((Join-Path $inputs ($name+'.json')))}
function Hash-IntervalInput($value){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($value))).ToLowerInvariant()}
$skills=Read-IntervalInput 'skill-rules';$battle=Read-IntervalInput 'battle-rules';$rules=Read-IntervalInput 'rules'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-IntervalInput $skills),'854_156383',$battle,(Hash-IntervalInput $battle),'854_156383')
$formula=$catalog.CreateDamageFormula($rules,(Hash-IntervalInput $rules),'854_156383')
[BattleRoundIntervalFilterHarness]::Run($catalog,$formula)
Write-Output 'Round interval filter passed: original 24387 ApplyEntity character types 0-9, creator independence, actual 20% timeline advance exactly once, deep copy, JSON and legacy counters.'

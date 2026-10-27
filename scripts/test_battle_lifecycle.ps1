$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$sources = @(Get-FightCombatSources)
$sources += Join-Path $PSScriptRoot 'fixtures/battle_lifecycle_harness.cs'
Add-Type -Path $sources
[BattleLifecycleHarness]::Run()
Write-Output 'Battle lifecycle checks passed: original ordered playback/cancellation, preparation ranges/ties, excluded unit types, guard/duel/invalid settlement, checkpoint and actual executor.'

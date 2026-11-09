$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
Add-Type -Path (@(Get-FightCombatSources) + (Join-Path $PSScriptRoot 'fixtures/battle_skill_cast_snapshot_harness.cs'))
[BattleSkillCastSnapshotHarness]::Run()
Write-Output 'Skill cast snapshots passed: ordered status identities, duplicate classes, shared totals, zero removal, independent successive casts, coordinates, explicit facing and current-game checkpoint recovery.'

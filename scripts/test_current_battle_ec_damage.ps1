$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$references = @((Get-ChildItem (Join-Path $PSHOME 'ref/*.dll')).FullName)
Add-Type -ReferencedAssemblies $references -CompilerOptions '/nowarn:0649' -Path @(
    (Join-Path $PSScriptRoot 'fixtures/current_battle_ec_damage_harness.cs'),
    (Join-Path $repository 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables/ECFightDamageView.cs')
)
[CurrentBattleEcDamageHarness]::Run()

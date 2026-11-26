$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$runtime = Join-Path $repository 'unity/Assets/Game/Runtime/Official/BattleStatusPlayables'
$references = @((Get-ChildItem (Join-Path $PSHOME 'ref/*.dll')).FullName)
Add-Type -ReferencedAssemblies $references -CompilerOptions '/nowarn:0649,0067' -Path @(
    (Join-Path $PSScriptRoot 'fixtures/current_status_ec_harness.cs'),
    (Join-Path $runtime 'IFightStatusPlayableContext.cs'),
    (Join-Path $runtime 'StackCountPlayableMap.cs'),
    (Join-Path $runtime 'MyPlayableStackPlay.cs'),
    (Join-Path $runtime 'ECFightStatusPlayableView.cs')
)
[CurrentStatusEcHarness]::Run()

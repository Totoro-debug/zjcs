$ErrorActionPreference = 'Stop'
python (Join-Path $PSScriptRoot 'prepare_battle_native_graph_sources.py')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$root = Split-Path -Parent $PSScriptRoot
$runtime = Join-Path $root 'unity/Assets/Game/Runtime/Presentation/Combat'
$references = @((Get-ChildItem (Join-Path $PSHOME 'ref/*.dll')).FullName)
Add-Type -ReferencedAssemblies $references -Path @(
    (Join-Path $PSScriptRoot 'fixtures/battle_native_graph_sources_harness.cs'),
    (Join-Path $runtime 'OfficialBattleNativeGraphSources.cs'),
    (Join-Path $runtime 'OfficialBattleSkillGraphCatalog.cs'),
    (Join-Path $runtime 'OfficialBattleSkillEntityViewCatalog.cs')
)
[BattleNativeGraphSourcesHarness]::Run()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
Invoke-ZjcsValidation -Name 'map12-stele-npc-rules' -ScriptBlock {
    param($workspace)
    $sources = @(Get-FightCombatSources)
    $sources += Join-Path $PSScriptRoot 'fixtures/map12_stele_npc_harness.cs'
    $assembly = Join-Path $workspace 'Map12SteleNpcRules.dll'
    Add-Type -Path $sources -OutputAssembly $assembly
    [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($assembly)) | Out-Null
    [Map12SteleNpcHarness]::Run($env:ZJCS_REPOSITORY_ROOT)
}

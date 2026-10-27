function Get-FightCombatSources {
    $combatRuntime = Join-Path (Split-Path -Parent $PSScriptRoot) 'unity/Assets/Game/Runtime'
    Get-ChildItem -LiteralPath @((Join-Path $combatRuntime 'Official/Combat'),
        (Join-Path $combatRuntime 'Offline/Combat')) -Filter '*.cs' -File |
        Select-Object -ExpandProperty FullName
}

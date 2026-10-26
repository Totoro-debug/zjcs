$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sources = @(
    'unity/Assets/Game/Runtime/Official/World/OfficialStageStateInfo.cs',
    'unity/Assets/Game/Runtime/Offline/World/OfficialMapPathfinder.cs',
    'unity/Assets/Game/Runtime/Offline/World/Map13WaterDefinition.cs',
    'unity/Assets/Game/Runtime/Offline/World/OfflineMap13WaterSession.cs'
) | ForEach-Object { Join-Path $root $_ }
Add-Type -Path $sources
$options = [System.Text.Json.JsonSerializerOptions]::new()
$options.IncludeFields = $true
$json = [IO.File]::ReadAllText((Join-Path $root 'unity/Assets/Official/World/Map/13/Resources/Map13/water-rules.json'))
$definition = [System.Text.Json.JsonSerializer]::Deserialize($json, [Zjcs.Offline.World.Map13WaterDefinition], $options)
function Assert-Water($condition, $message) { if (-not $condition) { throw $message } }
function Snapshot($session) { [System.Text.Json.JsonSerializer]::Serialize($session.Save(), $options) }
function Cold-Load($session) {
    $saved = [System.Text.Json.JsonSerializer]::Deserialize((Snapshot $session), [Zjcs.Offline.World.Map13WaterState[]], $options)
    [Zjcs.Offline.World.OfflineMap13WaterSession]::new($definition, $saved)
}
$free = [Func[int,Zjcs.Offline.World.MapGridPoint,bool]] { param($id, $position) $true }
$platforms = @($definition.Rules | Where-Object { $_.Routes.Length -gt 0 })
Assert-Water ($platforms.Length -eq 10) 'All ten original floating platforms must be present.'
foreach ($platform in $platforms) {
    $trigger = $definition.Rules | Where-Object { $_.TriggerEntities -contains $platform.PlacementId } | Select-Object -First 1
    Assert-Water ($null -ne $trigger) "No original trigger for platform $($platform.PlacementId)."
    $session = [Zjcs.Offline.World.OfflineMap13WaterSession]::new($definition)
    $actorGrid = $trigger.OriginalGrid + $trigger.InteractiveRange[0]
    for ($step = 0; $step -lt 2 * $platform.Routes.Length; $step++) {
        $passenger = [Zjcs.Offline.World.Map13WaterOccupant]::new()
        $passenger.PlacementId = -3001
        $passenger.EntityClassId = 3001
        $passenger.HasGridMover = $true
        $passenger.Grid = $session.State($platform.PlacementId).Grid
        Assert-Water ($session.Touch($trigger.PlacementId, $actorGrid, @($passenger), $free)) "Original trigger rejected for platform $($platform.PlacementId)."
        $movement = @($session.Movements | Where-Object { $_.PlacementId -eq $platform.PlacementId })
        $carried = @($session.Movements | Where-Object { $_.PlacementId -eq -3001 })
        Assert-Water ($movement.Length -eq 1 -and $carried.Length -eq 1) 'The platform and its passenger must both receive movement paths.'
        Assert-Water ($movement[0].Path.Length -eq $carried[0].Path.Length) 'Carried movement path length changed.'
        for ($point = 0; $point -lt $movement[0].Path.Length; $point++) {
            Assert-Water ($movement[0].Path[$point].Equals($carried[0].Path[$point])) 'The passenger lost its original platform offset.'
        }
        $beforeLoad = Snapshot $session
        $session = Cold-Load $session
        Assert-Water ((Snapshot $session) -eq $beforeLoad) 'Cold load changed water state or route direction.'
    }
    $final = $session.State($platform.PlacementId)
    Assert-Water ($final.Grid.Equals($platform.OriginalGrid) -and $final.NextSegment -eq 0 -and -not $final.Returning) 'A complete forward/backward cycle did not return to the original platform position.'
    $beforeBlock = Snapshot $session
    $blocked = [Func[int,Zjcs.Offline.World.MapGridPoint,bool]] { param($id, $position) $false }
    Assert-Water (-not $session.Touch($trigger.PlacementId, $actorGrid, @(), $blocked)) 'A blocked platform moved.'
    Assert-Water ((Snapshot $session) -eq $beforeBlock -and $session.Movements.Count -eq 0) 'A blocked route changed persisted state or left movement events.'
    $damaged = $session.Save()
    ($damaged | Where-Object PlacementId -eq $platform.PlacementId).Grid = [Zjcs.Offline.World.MapGridPoint]::new(-1, -1)
    $rejected = $false
    try { [Zjcs.Offline.World.OfflineMap13WaterSession]::new($definition, $damaged) | Out-Null }
    catch { $rejected = $true }
    Assert-Water $rejected 'An off-route saved platform was silently accepted.'
}

foreach ($terminal in $definition.Terminals) {
    $session = [Zjcs.Offline.World.OfflineMap13WaterSession]::new($definition)
    $session.OpenPaidTerminalRoads($terminal.PlacementId)
    $roads = Snapshot $session
    $session.OpenPaidTerminalRoads($terminal.PlacementId)
    Assert-Water ((Snapshot $session) -eq $roads) 'Replaying an already paid road-open operation changed state.'
    foreach ($id in $terminal.ExtendRoadLoEntityIds) { Assert-Water ($session.State($id).Spawned) 'An original extension road was not activated.' }
    $session.ResetTerminal($terminal.PlacementId)
    foreach ($id in $terminal.NeedResetLoEntityIds) {
        $rule = $definition.Rules | Where-Object PlacementId -eq $id
        $value = $session.State($id)
        Assert-Water ($value.State -eq $rule.InitialState -and $value.Grid.Equals($rule.OriginalGrid)) 'A reset did not restore the configured original entity state/position.'
        $fresh = [Zjcs.Offline.World.OfflineMap13WaterSession]::new($definition)
        Assert-Water ($value.GroundOperation -eq $fresh.State($id).GroundOperation) 'A reset left a ground override from the previous water state.'
    }
    $session = Cold-Load $session
    foreach ($id in $terminal.ExtendRoadLoEntityIds) { Assert-Water ($session.State($id).Spawned) 'Reset or cold load removed a previously paid extension road.' }
}

# 5328's original DataList intentionally contains 5324 twice. Preserve its bytes
# and the UseLoEntDefaultState semantics rather than deduplicating configuration.
$switchRule = $definition.Rules | Where-Object PlacementId -eq 5328
Assert-Water (@($switchRule.StateUpdates[0].StageDatas | Where-Object LoEntId -eq 5324).Length -eq 2) 'The repeated original state update was dropped.'
$session = [Zjcs.Offline.World.OfflineMap13WaterSession]::new($definition)
$actor = $switchRule.OriginalGrid + $switchRule.InteractiveRange[0]
Assert-Water ($session.Touch(5328, $actor, @(), $free)) 'The original water-level switch could not be used.'
foreach ($update in $switchRule.StateUpdates[1].StageDatas) { Assert-Water ($session.State($update.LoEntId).State -eq $update.State) 'Water-level states were not applied in the configured order.' }
Assert-Water ($session.Touch(5328, $actor, @(), $free)) 'The original water-level switch could not return.'
foreach ($update in $switchRule.StateUpdates[0].StageDatas) {
    $rule = $definition.Rules | Where-Object PlacementId -eq $update.LoEntId
    Assert-Water ($session.State($update.LoEntId).State -eq $rule.InitialState) 'UseLoEntDefaultState used the literal state instead of the original placement state.'
}
$session = Cold-Load $session
foreach ($rule in $definition.Rules | Where-Object { $_.GroundOperations.Length -gt 0 }) {
    $body = $session.Body($rule.PlacementId)
    Assert-Water ($body.Length -gt 0) 'Original water ground coverage was lost.'
}
Write-Output 'Map13 water rules passed: ten floating platforms, original multi-segment round trips, carried paths, blocked-route rollback, five paid-road/reset scopes, ordered/default water states, and JSON cold restoration. This is a rule test; UI input, animations and Player integration are not covered.'

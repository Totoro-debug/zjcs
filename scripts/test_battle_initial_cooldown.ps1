$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
if (-not $env:ZJCS_VALIDATION_DIR) { throw 'Set ZJCS_VALIDATION_DIR to this session workspace.' }
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(Get-FightCombatSources)
$inputs = Join-Path $root 'unity/Assets/Official/Combat'
function Load-Proof($name) {
    Get-Content -LiteralPath (Join-Path $inputs "$name-source-manifest.json") -Raw | ConvertFrom-Json
}
function Original-Text($name) { [IO.File]::ReadAllText((Join-Path $inputs "$name.json")) }
function Require($value, $message) { if (-not $value) { throw $message } }
$skillProof = Load-Proof 'skill-rules'; $battleProof = Load-Proof 'battle-rules'; $rulesProof = Load-Proof 'rules'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Original-Text 'skill-rules'), $skillProof.output_sha256, $skillProof.manifest,
    (Original-Text 'battle-rules'), $battleProof.output_sha256, $battleProof.manifest)
$formula = $catalog.CreateDamageFormula((Original-Text 'rules'), $rulesProof.output_sha256, $rulesProof.manifest)
$items = [Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load(
    (Original-Text 'rules'), $rulesProof.output_sha256, $rulesProof.manifest)
foreach ($itemId in @(10019,10024,10025,10046,10051,10058)) {
    $item = $items.Required($itemId)
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1001; $cp.MaxRound = 10; $cp.RandomSeed = 71
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]@{ Width = 12; Height = 12 }
    foreach ($id in @(1,2)) {
        $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
        $unit.Id = $unit.Side = $id; $unit.CharacterType = 1; $unit.X = 4 + $id; $unit.Y = 5
        $unit.Level = 20; $unit.SubRank = 1; $unit.Hp = $unit.MaxHp = $unit.LastMaxHp = 1000000
        $unit.Turn.Id = $id; $unit.Turn.Hp = $unit.Hp
        $unit.Turn.Speed = $(if ($id -eq 1) { 100 } else { 1 })
        foreach ($prop in @(@(1,100),@(2,100),@(3,1000000))) {
            $unit.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $prop[0]; Value = $prop[1] })
        }
        $cp.Units.Add($unit)
    }
    $actor = $cp.Units[0]
    $props = [Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
    foreach ($prop in $catalog.CalculateSkillProps($item.EntityId,2,20,1).GetEnumerator()) {
        $props.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $prop.Key; Value = $prop.Value })
    }
    $skill = $catalog.CreateSkill($item.EntityId,0,$itemId,2,20,1,$props)
    Require $skill.ResetCooldownAtStart "Original initial-cooldown flag missing: $itemId"
    $actor.Skills.Add($skill)
    $actor.SkillState.Slots.Add($catalog.CreateSkillSlot($skill,4))
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    $runner = [Zjcs.Combat.Official.FightBattleExecutor]::Begin(
        $cp,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    $saved = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
    $step = $runner.Step(0,1).Event
    $casts = @($step.SkillCasts | Where-Object { $_.ActorId -eq 1 -and $_.Slot -eq 0 })
    Require ($actor.SkillState.Round -eq 1 -and $casts.Count -eq 1 -and $casts[0].SkillItemId -eq $itemId) `
        "Original skill was not released in its first personal turn: $itemId"
    $cooldown = [Zjcs.Combat.Official.FightSkillState]::new($actor.SkillState)
    Require ($actor.SkillState.Slots[0].ReleasedTimes -eq 1 -and $cooldown.LeftCooldown(0) -eq 4) `
        "Original skill did not enter its configured cooldown after release: $itemId"
    Require ($runner.Step(0,1).Replayed -and $actor.SkillState.Slots[0].ReleasedTimes -eq 1) `
        "Initial skill replay consumed another release: $itemId"
    $restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
    $cold = [Zjcs.Combat.Official.FightBattleExecutor]::Restore(
        $restored,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
    $null = $cold.Step(0,1)
    Require ([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp) -eq
        [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($restored)) `
        "First-turn skill changed after cold checkpoint restoration: $itemId"
}
Write-Output 'Initial cooldown passed: six original skills cast in their first personal turn, then normal cooldown, one release and identical checkpoint restoration.'

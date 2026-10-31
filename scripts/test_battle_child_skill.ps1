. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$combat = Join-Path $root 'unity/Assets/Game/Runtime/Official/Combat'
$sources = Get-FightCombatSources
Add-Type -Path $sources

function Assert-Child($condition, $message) { if (-not $condition) { throw $message } }
function Read-Text($path) { return [System.IO.File]::ReadAllText((Join-Path $root $path)) }
$skillSource = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules-source-manifest.json'))
$battleSource = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest(
    (Read-Text 'unity/Assets/Official/Combat/battle-rules-source-manifest.json'))
$formulaSource = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::ReadSourceManifest(
    (Read-Text 'unity/Assets/Official/Combat/rules-source-manifest.json'))
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    (Read-Text 'unity/Assets/Official/Combat/skill-rules.json'),
    $skillSource.OutputSha256, $skillSource.Manifest,
    (Read-Text 'unity/Assets/Official/Combat/battle-rules.json'),
    $battleSource.OutputSha256, $battleSource.Manifest)

$officialHit = $catalog.CreateSkill(12730, 0, 0, 1, 1, 1, $null).HitGraphEntities |
    Where-Object { $_.EntityId -eq 12731 } | Select-Object -First 1
Assert-Child ($null -ne $officialHit -and $officialHit.ChildSkillId -eq 12733) 'Official child skill mapping mismatch'
Assert-Child ([Math]::Abs($officialHit.ChildSkillLessThanHpPer - 0.3) -lt 0.0001 -and [Math]::Abs($officialHit.ChildSkillRate - 1) -lt 0.0001) 'Official child skill condition mismatch'

function New-Unit($id, $side, $x, $y, $hp, $maxHp, $speed) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id = $id; $unit.Side = $side; $unit.X = $x; $unit.Y = $y; $unit.Hp = $hp; $unit.MaxHp = $maxHp
    $unit.Turn = [Zjcs.Combat.Official.FightTurnUnit]::new()
    $unit.Turn.Id = $id; $unit.Turn.Hp = $hp; $unit.Turn.Speed = $speed
    return $unit
}

function New-ChildSkill($childId, $rate, $per) {
    $skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
    $skill.Slot = 0; $skill.EntityId = 940; $skill.ItemId = 941; $skill.TargetType = 1
    $skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1, 0))
    $hit = [Zjcs.Combat.Official.FightBattleHitDefinition]::new()
    $hit.EntityId = 942; $hit.DamageId = 943; $hit.DamageType = 8
    $hit.HasCustomDamage = $true; $hit.CustomDamage = 1; $hit.TargetMode = 0
    $hit.ChildSkillId = $childId; $hit.ChildSkillRate = $rate; $hit.ChildSkillLessThanHpPer = $per
    $skill.Hits.Add($hit)
    return $skill
}

function Invoke-ChildBattle($skill, $enemyHp, $enemyMaxHp, $useOfficialDamageFormula = $false, $secondEnemy = $false, $friendlyTarget = $false) {
    $player = New-Unit 1 1 0 0 5000 5000 100
    $player.SkillState = [Zjcs.Combat.Official.FightSkillAgentState]::new()
    $player.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{ Slot = 0; LimitedTimes = -1 })
    $player.Skills.Add($skill)
    $enemy = New-Unit 2 2 1 0 $enemyHp $enemyMaxHp 1
    if ($friendlyTarget) { $enemy.Side = 1 }
    $cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
    $cp.StageId = 1; $cp.MaxRound = 10; $cp.RandomSeed = 29
    $cp.Grid = [Zjcs.Combat.Official.FightBattleGrid]::new(); $cp.Grid.Width = 8; $cp.Grid.Height = 8; $cp.Grid.DefaultGroundType = 2
    $cp.Units.Add($player); $cp.Units.Add($enemy)
    $wave = [Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index = 0; $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
    if ($secondEnemy) {
        $cp.Units.Add((New-Unit 3 2 1 1 $enemyHp $enemyMaxHp 1))
        $wave.UnitIds.Add(3)
    }
    if ($friendlyTarget) {
        $cp.Units.Add((New-Unit 3 2 4 4 5000 5000 1))
        $wave.UnitIds.Add(3)
    }
    $formula = [Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
    if ($useOfficialDamageFormula) {
        $player.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $catalog.PropertyType('Attack'); Value = 100 })
        $player.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $catalog.PropertyType('MaxHp'); Value = $player.MaxHp })
        $player.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $catalog.PropertyType('BaseCureAdd'); Value = 100 })
        $enemy.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $catalog.PropertyType('Attack'); Value = 1000 })
        $enemy.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{ Id = $catalog.PropertyType('BaseBeCureAdd'); Value = 100 })
        $formula = $catalog.CreateDamageFormula((Read-Text 'unity/Assets/Official/Combat/rules.json'),
            $formulaSource.OutputSha256, $formulaSource.Manifest)
    }
    $executor = [Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp, $formula,
        [Zjcs.Combat.Official.FightDamageSettlement]::new(), $catalog)
    return $executor.Step(0, 1)
}

# Child casts preserve the original caster and hit target.
$result = Invoke-ChildBattle (New-ChildSkill 10640 1.0 -1) 5000 5000
$childCasts = @($result.Event.SkillCasts | Where-Object { $_.Slot -eq -1 -and $_.SkillEntityId -eq 10640 -and $_.ActorId -eq 1 })
Assert-Child ($childCasts.Count -eq 1 -and $childCasts[0].TargetIds.Count -eq 1 -and $childCasts[0].TargetIds[0] -eq 2) 'Child skill did not preserve the original caster and hit target'

# HP condition: only below 30% of maximum HP.
$high = Invoke-ChildBattle (New-ChildSkill 10640 1.0 0.3) 4000 5000
Assert-Child (@($high.Event.SkillCasts | Where-Object { $_.Slot -eq -1 }).Count -eq 0) 'Child skill fired above its HP threshold'
$low = Invoke-ChildBattle (New-ChildSkill 10640 1.0 0.3) 100 5000
Assert-Child (@($low.Event.SkillCasts | Where-Object { $_.Slot -eq -1 -and $_.SkillEntityId -eq 10640 }).Count -eq 1) 'Child skill did not fire below its HP threshold'

# Zero rate never triggers, and child skills do not cascade.
$zero = Invoke-ChildBattle (New-ChildSkill 10640 0.0 -1) 5000 5000
Assert-Child (@($zero.Event.SkillCasts | Where-Object { $_.Slot -eq -1 }).Count -eq 0) 'Zero-rate child skill was released'
$nested = Invoke-ChildBattle (New-ChildSkill 12730 1.0 -1) 5000 5000
Assert-Child (@($nested.Event.SkillCasts | Where-Object { $_.Slot -eq -1 -and $_.SkillEntityId -eq 12730 }).Count -eq 1) 'Child skill cascade was not limited to one level'

# Child effects follow the triggering skill's cultivation, not the damaged unit's level.
$cultivated = New-ChildSkill 63434 1.0 -1
$cultivated.Rank = 6; $cultivated.Level = 17; $cultivated.SubRank = 10
$inherited = Invoke-ChildBattle $cultivated 5000 5000
$status = @($inherited.Checkpoint.Units | ForEach-Object { $_.Statuses } |
    Where-Object { $_.ClassId -eq 24658 -and $_.RootSkillId -eq 63434 })
Assert-Child ($status.Count -gt 0) 'Official child skill did not apply its configured status'
Assert-Child ($status[0].SourceSkillRank -eq 6 -and $status[0].SourceSkillLevel -eq 17 -and $status[0].SourceSubRank -eq 10) 'Child skill did not inherit its triggering skill cultivation'
Assert-Child ($status[0].CreatorId -eq 1 -and @($inherited.Checkpoint.Units | Where-Object { $_.Id -eq 1 } | ForEach-Object { $_.Statuses } | Where-Object { $_.ClassId -eq 24658 }).Count -eq 1) 'Self child status was applied to the damaged enemy instead of its caster'
$restored = [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load(
    [Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($inherited.Checkpoint))
$savedStatus = @($restored.Units | ForEach-Object { $_.Statuses } | Where-Object { $_.ClassId -eq 24658 })
Assert-Child ($savedStatus[0].SourceSkillLevel -eq 17) 'Child status cultivation did not survive checkpoint restoration'

# The inherited parameters must change actual official damage, not only status metadata.
$plain = New-ChildSkill 11050 1.0 -1
$plain.Rank = 1; $plain.Level = 1; $plain.SubRank = 1
$boosted = New-ChildSkill 11050 1.0 -1
$boosted.Rank = 6; $boosted.Level = 17; $boosted.SubRank = 10
$plainResult = Invoke-ChildBattle $plain 5000 5000 $true
$boostedResult = Invoke-ChildBattle $boosted 5000 5000 $true
$plainDamage = @($plainResult.Event.DamageEvents | Where-Object { $_.SkillEntityId -eq 11050 })
$boostedDamage = @($boostedResult.Event.DamageEvents | Where-Object { $_.SkillEntityId -eq 11050 })
Assert-Child ($plainDamage.Count -gt 0 -and $boostedDamage.Count -gt 0) 'Official child damage was not executed'
Assert-Child (@($boostedDamage | Where-Object { $_.SourceId -ne 1 -or $_.TargetId -ne 2 }).Count -eq 0) 'Child damage reversed its source and target'
Assert-Child ([Math]::Abs($boostedDamage[0].DamageValue) -gt [Math]::Abs($plainDamage[0].DamageValue)) 'Parent cultivation did not scale actual child damage'

# Two hits in one parent cast must retain their respective targets, rather than both retargeting one enemy.
$area = New-ChildSkill 11050 1.0 -1
$area.Hits[0].TargetMode = 1
$area.Hits[0].Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new(0, 0))
$area.Hits[0].Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new(0, 1))
$multiple = Invoke-ChildBattle $area 5000 5000 $true $true
$multipleCasts = @($multiple.Event.SkillCasts | Where-Object { $_.SkillEntityId -eq 11050 })
$multipleDamage = @($multiple.Event.DamageEvents | Where-Object { $_.SkillEntityId -eq 11050 })
Assert-Child ($multipleCasts.Count -eq 2 -and $multipleDamage.Count -eq 2) 'Area parent did not execute one child per original hit'
Assert-Child ((($multipleDamage.TargetId | Sort-Object) -join ',') -eq '2,3') 'Child damage did not retain each original hit position'
Assert-Child (@($multipleDamage | Where-Object { $_.SourceId -ne 1 }).Count -eq 0) 'Area child lost its original caster'

# The original healing child keeps the caster's side and heals the original friendly hit target.
$healing = New-ChildSkill 12733 1.0 -1
$healing.TargetType = 3
$healed = Invoke-ChildBattle $healing 100 5000 $true $false $true
$cures = @($healed.Event.DamageEvents | Where-Object { $_.SkillEntityId -eq 12733 -and $_.IsCure })
Assert-Child (@($cures | Where-Object { $_.SourceId -eq 1 -and $_.TargetId -eq 2 -and $_.AfterHp -gt $_.BeforeHp }).Count -eq 1) 'Healing child did not heal its original friendly hit target'
Assert-Child (@($cures | Where-Object { $_.TargetId -eq 3 -or $_.SourceId -ne 1 }).Count -eq 0) 'Healing child changed faction or caster'

Write-Output 'Battle child skill checks passed: official mapping, original caster/target, HP condition, zero rate, single-level cascade, inherited cultivation/status persistence, self buff, healing and separate area-hit children.'

. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Get-FightCombatSources)
$root = Split-Path -Parent $PSScriptRoot
$skillPath = Join-Path $root 'unity/Assets/Official/Combat/skill-rules.json'
$battlePath = Join-Path $root 'unity/Assets/Official/Combat/battle-rules.json'
$catalog = [Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load(
    [IO.File]::ReadAllText($skillPath), (Get-FileHash $skillPath -Algorithm SHA256).Hash.ToLowerInvariant(), '854_156383',
    [IO.File]::ReadAllText($battlePath), (Get-FileHash $battlePath -Algorithm SHA256).Hash.ToLowerInvariant(), '854_156383')
function Assert-Logic($condition, $label) { if (-not $condition) { throw $label } }
function New-Unit($id, $side, $x, $y) {
    $unit = [Zjcs.Combat.Official.FightBattleUnitState]::new()
    $unit.Id=$id; $unit.Side=$side; $unit.CharacterType=1; $unit.X=$x; $unit.Y=$y
    $unit.Hp=$unit.MaxHp=$unit.LastMaxHp=1000000
    $unit.Turn.Id=$id; $unit.Turn.Hp=$unit.Hp; $unit.Turn.Speed=100-$id
    return $unit
}
$formula = [Zjcs.Combat.Official.FightDamageFormula]::new(
    [Zjcs.Combat.Official.FightDamagePropertySchema]::new(), [Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$settlement = [Zjcs.Combat.Official.FightDamageSettlement]::new()
$hitExecutor = [Zjcs.Combat.Official.FightBattleHitExecutor]::new($formula,$settlement)
$logicType = [Zjcs.Combat.Official.FightBattleExecutor].Assembly.GetType('Zjcs.Combat.Official.FightSkillLogicRuntime', $true)
$create = $logicType.GetMethod('Create', [Reflection.BindingFlags]'Static,NonPublic')
$transient = $logicType.GetMethod('OnDoHitTransient', [Reflection.BindingFlags]'Instance,NonPublic')
$actor = New-Unit 1 1 4 4
$target = New-Unit 2 2 4 5
$units = [Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
$units.Add($actor); $units.Add($target)
$random = [Zjcs.Combat.Official.FightRandomStream]::new(91)
$skill = $catalog.CreateSkill(13830,0,10197,5,20,1,$null)
$skill.RootEntityId=13100
$runtime = $create.Invoke($null,@($actor,$skill,$units,$catalog,$random,$null))
Assert-Logic ($null -ne $runtime) 'Original 13830 graph was not restored.'
$hit = [Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)[0]
$hit.HasCustomDamage=$true; $hit.CustomDamage=100
$probe = @{Count=0; Hp=$target.Hp}
$callback = [Action[Zjcs.Combat.Official.FightDamageTypeData]] {
    param($type)
    Assert-Logic ($target.Hp -eq $probe.Hp) 'Transient graph ran after HP settlement.'
    $transient.Invoke($runtime,@($target,$hit,$type)) | Out-Null
    $probe.Count++
}
$result = $hitExecutor.Apply($random,1,$actor,$target,$skill,$hit,0,$false,0,$null,$false,1,$null,$callback)
Assert-Logic ($probe.Count -eq 1 -and $target.Hp -lt $probe.Hp) 'Actual hit did not call the pre-HP graph once.'
$charge = $actor.Statuses | Where-Object ClassId -eq 24031
Assert-Logic ($charge.Count -eq 1 -and $charge.StackCount -eq 1 -and $charge.CreatorId -eq 1) 'First hit did not create one original charge.'
Assert-Logic ($charge.RootSkillId -eq 13830 -and $charge.Origin.SkillEntityId -eq 13100 -and $charge.ApplyingSkillItemId -eq 10197 -and $charge.SourceSkillRank -eq 5) 'Created charge lost actual/root/cultivation provenance.'
foreach ($field in @('Dodge','Blinding')) {
    $type = [Zjcs.Combat.Official.FightDamageTypeData]::new(); $type.$field=$true
    $transient.Invoke($runtime,@($target,$hit,$type)) | Out-Null
}
Assert-Logic ($charge.StackCount -eq 1) 'A dodge or blinding result added a charge.'
$type = [Zjcs.Combat.Official.FightDamageTypeData]::new(); $type.Immunity=$true
$transient.Invoke($runtime,@($target,$hit,$type)) | Out-Null
Assert-Logic ($charge.StackCount -eq 2) 'An immune transient hit was incorrectly skipped.'
$shield = [Zjcs.Combat.Official.FightBattleStatusState]::new()
$shield.ClassId=999999; $shield.InstanceId=1; $shield.ShieldKind=[Zjcs.Combat.Official.FightBattleShieldKind]::Persistent
$shield.ShieldInitialized=$true; $shield.ShieldRemainValue=100000; $target.Statuses.Add($shield)
$probe.Hp=$target.Hp
$result = $hitExecutor.Apply($random,2,$actor,$target,$skill,$hit,0,$false,0,$null,$false,1,$null,$callback)
Assert-Logic ($target.Hp -eq $probe.Hp -and $result.ShieldDamage -gt 0 -and $charge.StackCount -eq 3) 'Shield-only settlement did not add exactly one charge.'
$skill.CanTriggerChild=$false
$transient.Invoke($runtime,@($target,$hit,[Zjcs.Combat.Official.FightDamageTypeData]::new())) | Out-Null
Assert-Logic ($charge.StackCount -eq 3) 'Disabled child triggers changed an existing stack.'
$skill.CanTriggerStatusEntityIds.Add(24031)
for ($index=0; $index -lt 30; $index++) {
    $transient.Invoke($runtime,@($target,$hit,[Zjcs.Combat.Official.FightDamageTypeData]::new())) | Out-Null
}
Assert-Logic ($charge.StackCount -eq 20) 'The original 20-layer cap or whitelist was lost.'

$skill2 = $catalog.CreateSkill(13840,0,10198,5,20,1,$null)
$runtime2 = $create.Invoke($null,@($actor,$skill2,$units,$catalog,$random,$null))
$hit2 = [Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill2)[0]
$clean = New-Unit 3 2 5 5; $units.Add($clean)
$transient.Invoke($runtime2,@($clean,$hit2,[Zjcs.Combat.Official.FightDamageTypeData]::new())) | Out-Null
Assert-Logic (-not ($actor.Statuses | Where-Object ClassId -eq 24697)) 'A target without 24266 added a 24697 stack.'
$burn = $catalog.CreateStatus(24266,$target.Id,$actor.Id,2)
$target.Statuses.Add($burn)
for ($index=0; $index -lt 15; $index++) {
    $transient.Invoke($runtime2,@($target,$hit2,[Zjcs.Combat.Official.FightDamageTypeData]::new())) | Out-Null
    $transient.Invoke($runtime2,@($clean,$hit2,[Zjcs.Combat.Official.FightDamageTypeData]::new())) | Out-Null
}
$charge2 = $actor.Statuses | Where-Object ClassId -eq 24697
Assert-Logic ($charge2.Count -eq 1 -and $charge2.StackCount -eq 12) 'Per-target status filter or original 12-layer cap failed.'

$isolated = New-Unit 4 1 3 4; $units.Add($isolated)
$fresh = $create.Invoke($null,@($isolated,$skill,$units,$catalog,$random,$null))
Assert-Logic (-not [object]::ReferenceEquals(
    $logicType.GetField('blackboard',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($runtime),
    $logicType.GetField('blackboard',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($fresh))) 'Separate casts share the same blackboard.'
foreach ($replacement in @(
    @('"FilterCondition":null','"FilterCondition":"UnknownSkillGraphFilter"'),
    @('"TargetEntityKey":{"Id":1,','"TargetEntityKey":{"Id":9999,'),
    @('FightHandleCfgNewIntInBB','UnknownSkillGraphHandler'),
    @('IteratingAgentStatusClassIdFilter','UnknownSkillGraphFilter'))) {
    $invalid=$catalog.CreateSkill(13830,0,10197,5,20,1,$null)
    $invalid.LogicDefinitionJson=$invalid.LogicDefinitionJson.Replace($replacement[0],$replacement[1])
    Assert-Logic ($invalid.LogicDefinitionJson.Contains($replacement[1])) 'Invalid graph fixture did not change the intended field.'
    $invalidRuntime=$create.Invoke($null,@($isolated,$invalid,$units,$catalog,$random,$null))
    $transient.Invoke($invalidRuntime,@($target,$hit,[Zjcs.Combat.Official.FightDamageTypeData]::new())) | Out-Null
    Assert-Logic ($isolated.Statuses.Count -eq 0) 'An unknown handler/filter or unbound entity port created a status.'
}

# Real battle execution: the three original two-cell columns each hit the same
# three-cell target once. The graph must run for every actual segment hit.
$cp = [Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$cp.StageId=1; $cp.MaxRound=10; $cp.RandomSeed=91
$cp.Grid=[Zjcs.Combat.Official.FightBattleGrid]::new(); $cp.Grid.Width=10; $cp.Grid.Height=10; $cp.Grid.DefaultGroundType=2
$player=New-Unit 1 1 4 4; $enemy=New-Unit 2 2 4 5
$enemy.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(-1,0))
$enemy.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,0))
$enemy.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
$active=$catalog.CreateSkill(13830,0,10197,5,20,1,$null)
$active.HasCustomDamage=$true; $active.CustomDamage=100
$player.Skills.Add($active); $player.SkillState.Slots.Add($catalog.CreateSkillSlot($active,0,-10))
$cp.Units.Add($player); $cp.Units.Add($enemy)
$wave=[Zjcs.Combat.Official.FightBattleWaveState]::new(); $wave.Index=0; $wave.UnitIds.Add(2); $cp.Waves.Add($wave)
$executor=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($cp,$formula,$settlement,$catalog)
$step=$executor.Step(0,1)
$actualHits=@($step.Event.DamageEvents | Where-Object SkillEntityId -eq 13830)
Assert-Logic ($actualHits.Count -eq 3) 'The original three-column hit graph did not produce three actual hits.'
Assert-Logic (($player.Statuses | Where-Object ClassId -eq 24031).StackCount -eq 3) 'Real executor did not run all three transient stack flows.'
$restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp))
Assert-Logic (($restored.Units[0].Statuses | Where-Object ClassId -eq 24031).StackCount -eq 3) 'Stack result was not preserved by the checkpoint.'
Write-Output 'Original skill transient stack checks passed: pre-HP timing, actual three-hit execution, conditional targets, miss/immune/shield branches, caps, source and checkpoint.'

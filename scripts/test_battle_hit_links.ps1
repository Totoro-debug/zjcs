. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Get-FightCombatSources)
function Assert-Link($ok, $message) { if (-not $ok) { throw $message } }
function New-Link($id,$delay,$anchor,$ignore) {
    [Zjcs.Combat.Official.FightBattleHitLink]@{EntityId=$id;Delay=$delay;Anchor=$anchor;IgnoreAICount=$ignore}
}
$skill = [Zjcs.Combat.Official.FightBattleSkillDefinition]::new()
$wrapper = [Zjcs.Combat.Official.FightBattleHitEntity]@{EntityId=10;Delay=9;Anchor=1}
$leaf = [Zjcs.Combat.Official.FightBattleHitEntity]@{EntityId=11;HasFixedDamage=$true;Delay=8;Anchor=1;IgnoreAICount=$true}
$skill.HitGraphEntities.Add($wrapper); $skill.HitGraphEntities.Add($leaf)
$skill.HitGraphRoots.Add([Zjcs.Combat.Official.FightBattleHitRoot]@{EntityId=10;Delay=6;Anchor=1})
foreach ($kind in @('Chained','CustomTarget','RandomTarget')) {
    $links = $wrapper.($kind+'HitLinks'); $ids = $wrapper.($kind+'Hits')
    $links.Add((New-Link 11 0.2 0 $false)); $links.Add((New-Link 11 0 1 $true)); $links.Add((New-Link 11 0.7 2 $false))
    $ids.Add(11); $ids.Add(11); $ids.Add(11)
    $hits = [Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)
    Assert-Link ($hits.Count -eq 3) "$kind lost duplicate edges"
    for ($i=0;$i -lt 3;$i++) {
        Assert-Link ([Math]::Abs($hits[$i].Delay-(6+$links[$i].Delay)) -lt 0.00001 -and $hits[$i].Anchor -eq $links[$i].Anchor -and $hits[$i].IgnoreAICount -eq $links[$i].IgnoreAICount) "$kind occurrence $i inherited another edge"
    }
    Assert-Link ($leaf.Delay -eq 8 -and $leaf.Anchor -eq 1 -and $leaf.IgnoreAICount) 'Shared class mutated'
    if($kind -eq 'RandomTarget') { Assert-Link ($hits[0].RandomTargetGroupId -gt 0 -and $hits[0].RandomTargetAnchor -eq 1) 'Random wrapper lost its own incoming anchor/group' }
    $hits[0].Delay=99
    Assert-Link ([Math]::Abs([Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)[0].Delay-6.2) -lt 0.00001) 'Expansion retained mutated occurrence'
    $links.Clear(); $ids.Clear()
}
# An old ID-only graph still consumes its original class metadata.
$wrapper.CustomTargetHits.Add(11)
$legacy = [Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)
Assert-Link ($legacy.Count -eq 1 -and $legacy[0].Delay -eq 8) 'Wrapper root reference overwrote an unrelated descendant or legacy metadata'
$wrapper.CustomTargetHits.Clear()
# The damage-bearing root itself consumes its own reference.
$skill.HitGraphRoots.Clear()
$skill.HitGraphRoots.Add([Zjcs.Combat.Official.FightBattleHitRoot]@{EntityId=11;Delay=0;Anchor=0;IgnoreAICount=$false})
$rootHit = [Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($skill)[0]
Assert-Link ($rootHit.Delay -eq 0 -and $rootHit.Anchor -eq 0 -and -not $rootHit.IgnoreAICount) 'Explicit root zero/false was ignored'
# Real original multi-reference configuration: compare every collected child with its HitCfg.
$repo = Split-Path -Parent $PSScriptRoot
function Read-Rules($name) { [IO.File]::ReadAllText((Join-Path $repo "unity/Assets/Official/Combat/$name.json")) }
function Hash-Rules($s) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($s))).ToLowerInvariant() }
$skills=Read-Rules skill-rules; $battle=Read-Rules battle-rules
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skills,(Hash-Rules $skills),'854_156383',$battle,(Hash-Rules $battle),'854_156383')
$original=$catalog.CreateSkill(10910,0,10910,1,40,0,$null)
$actual=[Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($original)
$parent=@($original.HitGraphEntities | Where-Object {$_.RandomTargetHitLinks.Count -eq 7})
Assert-Link ($parent.Count -eq 1) 'Original seven-reference random parent not found'
$edges=$parent[0].RandomTargetHitLinks
Assert-Link ($actual.Count -eq 7) 'Original 10911 occurrence count differs'
for($i=0;$i -lt 7;$i++) {
 Assert-Link ($actual[$i].EntityId -eq $edges[$i].EntityId -and [Math]::Abs($actual[$i].Delay-($original.HitGraphRoots[0].Delay+$edges[$i].Delay)) -lt 0.00001 -and $actual[$i].Anchor -eq $edges[$i].Anchor) "Original reference $i differs"
}
Write-Output 'HitCfg occurrences passed: all child kinds, repeated IDs, zero/false, wrapper roots, ID-only fallback, immutable classes and seven original 10911 references.'

# The public executor must consume the projected times and persist them without a replay.
$skill.HitGraphRoots.Clear(); $skill.HitGraphRoots.Add([Zjcs.Combat.Official.FightBattleHitRoot]@{EntityId=10;Delay=0.37})
$wrapper.RandomTargetHitLinks.Clear();$wrapper.MiniHitTargetCount=1
$wrapper.Scope.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,0))
for($i=0;$i -lt 7;$i++) {$wrapper.RandomTargetHitLinks.Add((New-Link 11 ($i/10.0) 0 $false))}
$leaf.DamageType=8;$leaf.DamageParam=1;$leaf.ActionType=4
$skill.EntityId=9001;$skill.ItemId=9001;$skill.RangeVerified=$true;$skill.TargetType=1;$skill.Slot=0
$skill.HasCustomDamage=$true;$skill.CustomDamage=1
$skill.Range.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
$source=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=1;Side=1;CharacterType=1;X=4;Y=4;Hp=1000;MaxHp=1000;LastMaxHp=1000}
$enemy=[Zjcs.Combat.Official.FightBattleUnitState]@{Id=2;Side=2;CharacterType=2;X=5;Y=4;Hp=1000;MaxHp=1000;LastMaxHp=1000}
$source.Turn.Id=1;$source.Turn.Hp=1000;$source.Turn.Speed=100
$enemy.Turn.Id=2;$enemy.Turn.Hp=1000;$enemy.Turn.Speed=1
$source.Skills.Add($skill)
$source.SkillState.Slots.Add([Zjcs.Combat.Official.FightSkillSlotState]@{Slot=0;LimitedTimes=-1})
$state=[Zjcs.Combat.Official.FightBattleCheckpoint]@{StageId=1;RandomSeed=7;MaxRound=8}
$state.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=12;Height=12;DefaultGroundType=2}
$state.Units.Add($source);$state.Units.Add($enemy)
$wave=[Zjcs.Combat.Official.FightBattleWaveState]::new();$wave.UnitIds.Add(2);$state.Waves.Add($wave)
$formula=[Zjcs.Combat.Official.FightDamageFormula]::new([Zjcs.Combat.Official.FightDamagePropertySchema]::new(),[Zjcs.Combat.Official.FightDamageFormulaConfig]::new())
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($state,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new())
$step=$runner.Step(0,1)
Assert-Link ($step.Event.DamageEvents.Count -eq 7) ('Executor occurrence count: '+$step.Event.DamageEvents.Count)
for($i=0;$i -lt 7;$i++) {Assert-Link ([Math]::Abs($step.Event.DamageEvents[$i].Delay-(0.37+$i/10.0)) -lt 0.00001) "Executor delay $i differs"}
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($state))
$hp=$saved.Units[1].Hp;$count=$saved.Counter
$restored=[Zjcs.Combat.Official.FightBattleExecutor]::Restore($saved,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new())
Assert-Link ($restored.State.Counter -eq $count -and $restored.State.Units[1].Hp -eq $hp -and $source.SkillState.Slots[0].ReleasedTimes -eq 1) 'Restore replayed settlement or release'
for($i=0;$i -lt 7;$i++) {Assert-Link ([Math]::Abs($saved.Receipts[0].Event.DamageEvents[$i].Delay-(0.37+$i/10.0)) -lt 0.00001) "Saved occurrence $i lost timing"}
Write-Output 'Real executor: seven delayed occurrences, one release, checkpoint timing/HP/counter and restore without replay passed.'

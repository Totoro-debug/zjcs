. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference='Stop'
Add-Type -Path (Get-FightCombatSources)
function Assert-HitSelection($ok,$message){if(-not $ok){throw $message}}
function New-SelectionUnit($id,$side,$x,$y){
 $unit=[Zjcs.Combat.Official.FightBattleUnitState]::new();$unit.Id=$id;$unit.Side=$side;$unit.X=$x;$unit.Y=$y
 $unit.CharacterType=2;$unit.Hp=$unit.MaxHp=10000;return $unit
}
$actor=New-SelectionUnit 1 1 0 0
$units=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new();$units.Add($actor)
$targets=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
foreach($id in 2..5){$unit=New-SelectionUnit $id 2 $id 0;$units.Add($unit);$targets.Add($unit)}
$skill=[Zjcs.Combat.Official.FightBattleSkillDefinition]::new();$skill.TargetType=1
$hit=[Zjcs.Combat.Official.FightBattleHitDefinition]::new();$hit.RandomTargetGroupId=1;$hit.RandomCount=3;$hit.RandomTargetNotSameTarget=$true
$context=[Zjcs.Combat.Official.FightHitTargetSelectionContext]::new()
$context.Source=$actor;$context.Units=$units;$context.Skill=$skill;$context.Hit=$hit;$context.Targets=$targets;$context.Primary=$targets[0]
$context.Random=[Zjcs.Combat.Official.FightRandomStream]::new(91)
$context.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=12;Height=8;DefaultGroundType=2}
$selectors=[Zjcs.Combat.Official.FightHitTargetSelectors]::new();$randomSelector=$selectors.For($hit)
Assert-HitSelection ($randomSelector -is [Zjcs.Combat.Official.IFightHitTargetSelector]) 'Random target selection bypasses its capability interface.'
$first=$randomSelector.Select($context);$second=$randomSelector.Select($context);$empty=$randomSelector.Select($context)
$unique=@(@($first.Targets.Id)+@($second.Targets.Id)|Select-Object -Unique)
Assert-HitSelection ($first.Targets.Count -eq 3 -and $second.Targets.Count -eq 1 -and $null -eq $empty -and $unique.Count -eq 4) 'Linked hit segments repeated targets or lost their limit.'
$hit.RandomTargetGroupId=2
Assert-HitSelection ($randomSelector.Select($context).Targets.Count -eq 3) 'Another parent group inherited exclusions.'
$hit.RandomTargetGroupId=1
$newCast=[Zjcs.Combat.Official.FightHitTargetSelectors]::new()
Assert-HitSelection ($newCast.For($hit).Select($context).Targets.Count -eq 3) 'Another cast inherited exclusions.'
$hit.RandomTargetGroupId=0;$hit.TargetMode=1
$direct=$selectors.For($hit)
Assert-HitSelection ($direct.GetType() -ne $randomSelector.GetType() -and $direct.Select($context).Targets.Count -eq 4) 'Ordinary selection inherited random group restrictions.'

# One chosen entity can expose several independently damaged body cells.
$boss=$targets[0];$boss.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,0));$boss.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,1));$boss.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new(1,0))
$hit.RandomTargetGroupId=3;$hit.RandomCount=1;$hit.RandomTargetAnchor=1;$hit.RandomTargetAllowEmptyScope=$false;$hit.RandomTargetNotAllowHitEmpty=$true
$hit.RandomTargetScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(2,0))
$i=0
foreach($point in @(@(0,0),@(0,1),@(1,0))){
 $position=[Zjcs.Combat.Official.FightGridPoint]::new($point[0],$point[1]);$hit.Scope.Add($position)
 $entry=[Zjcs.Combat.Official.FightBattleHitScopeEntry]::new();$entry.Position=$position;$entry.Delay=$i/10.0;$hit.ScopeEntries.Add($entry);$i++
}
$area=$randomSelector.Select($context)
Assert-HitSelection (@($area.Occurrences|Where-Object {$_.Target.Id -eq $boss.Id}).Count -eq 3) 'One random anchor became a one-body-cell damage cap.'
for($i=0;$i -lt 3;$i++){Assert-HitSelection ([Math]::Abs($area.Occurrences[$i].Delay-$i/10.0) -lt 0.0001) 'Original cell delay/order was lost.'}

# NotSameTarget applies while selecting a batch, not after body grids exhaust its quota.
$wideSource=New-SelectionUnit 11 1 0 0
$wideBoss=New-SelectionUnit 12 2 4 4
$wideOther=New-SelectionUnit 13 2 8 4
$wideRoster=[Collections.Generic.List[Zjcs.Combat.Official.FightBattleUnitState]]::new()
foreach($unit in @($wideSource,$wideBoss,$wideOther)){$wideRoster.Add($unit)}
$wideScope=[Collections.Generic.List[Zjcs.Combat.Official.FightGridPoint]]::new()
foreach($x in -1..1){foreach($y in -1..1){
 $wideBoss.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new($x,$y))
 $wideScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(4+$x,4+$y))
}}
$wideScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(8,4))
$excluded=[Collections.Generic.HashSet[int]]::new()
$emptySet=[Collections.Generic.HashSet[Zjcs.Combat.Official.FightGridPoint]]::new()
$repeatObserved=$false
foreach($seed in 1..12){
 $stream=[Zjcs.Combat.Official.FightRandomStream]::new($seed)
 $chosen=[Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomScopePositions($wideSource,$wideRoster,$skill,$null,$wideScope,$context.Grid,2,$stream,$false,$true,$true,$excluded,$false,$emptySet,$false,$emptySet,$true)
 Assert-HitSelection ($chosen.Count -eq 2 -and @($chosen.Targets.Id|Select-Object -Unique).Count -eq 2) "Seed $seed spent distinct-target quota on two body grids of one boss."
 Assert-HitSelection ($excluded.Count -eq 0) 'A batch mutated its caller-owned exclusion set.'
 $repeat=[Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomScopePositions($wideSource,$wideRoster,$skill,$null,$wideScope,$context.Grid,2,[Zjcs.Combat.Official.FightRandomStream]::new($seed),$false,$true,$false,$excluded,$false,$emptySet,$false,$emptySet,$true)
 if(@($repeat.Targets.Id|Select-Object -Unique).Count -eq 1){$repeatObserved=$true}
}
Assert-HitSelection $repeatObserved 'Allowing repeated targets unexpectedly deduplicated boss body grids.'
$wideRoster.Remove($wideOther)|Out-Null
$single=[Zjcs.Combat.Official.FightBattleTargeting]::SelectRandomScopePositions($wideSource,$wideRoster,$skill,$null,$wideScope,$context.Grid,3,[Zjcs.Combat.Official.FightRandomStream]::new(1),$false,$true,$true,$excluded,$false,$emptySet,$false,$emptySet,$true)
Assert-HitSelection ($single.Count -eq 1 -and $single[0].Targets[0].Id -eq $wideBoss.Id) 'Exhausted distinct targets did not stop safely.'
Write-Output 'Random scope: 12 deterministic seeds preserve distinct-target quota, caller exclusions and explicit repeat permission.'

# Empty anchors still occupy their authored exclusion group and draw stream.
$hit.Scope.Clear();$hit.ScopeEntries.Clear();$hit.RandomTargetScope.Clear();$hit.RandomTargetScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(8,0));$hit.RandomTargetScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(9,0))
$hit.RandomTargetGroupId=4;$hit.RandomTargetAllowEmptyScope=$true;$hit.RandomTargetNotAllowHitEmpty=$false;$hit.RandomTargetNotSameEmptyScope=$true;$hit.RandomTargetNotSameGrid=$true
$seed0=$context.Random.Seed;$empty1=$randomSelector.Select($context);$seed1=$context.Random.Seed;$empty2=$randomSelector.Select($context);$seed2=$context.Random.Seed;$empty3=$randomSelector.Select($context)
Assert-HitSelection ($null -ne $empty1 -and $empty1.Targets.Count -eq 0 -and $empty1.SelectedGridPositions.Count -eq 1 -and $null -ne $empty2 -and $empty2.Targets.Count -eq 0 -and $empty2.SelectedGridPositions.Count -eq 1 -and $null -eq $empty3 -and $seed0 -ne $seed1 -and $seed1 -ne $seed2 -and $context.Random.Seed -eq $seed2) 'Empty-grid exclusions or random draw order changed.'

# Faction reference and actual caster anchor remain separate inputs.
$reference=New-SelectionUnit 8 2 9 0;$units.Add($reference);$boss.Side=1
$context.TargetingSource=$reference;$hit.RandomTargetGroupId=5;$hit.RandomTargetScope.Clear();$hit.RandomTargetScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(2,0));$hit.RandomTargetAllowEmptyScope=$false;$hit.RandomTargetNotAllowHitEmpty=$true
$referenced=$randomSelector.Select($context)
Assert-HitSelection ($referenced.Targets.Count -eq 1 -and $referenced.Targets[0].Id -eq $boss.Id) 'Reference faction replaced the actual caster hit anchor.'
Write-Output 'Hit selection capabilities: ordinary/random variants, linked segments, group/cast isolation, per-body occurrences/delays, empty anchors/RNG and separate source reference passed.'

# Exercise the same composition through an unmodified official skill and real settlement.
$repository=Split-Path -Parent $PSScriptRoot
function Read-SelectionRule($name){[IO.File]::ReadAllText((Join-Path $repository "unity/Assets/Official/Combat/$name.json"))}
function Get-SelectionRuleHash($text){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))).ToLowerInvariant()}
$skillRules=Read-SelectionRule 'skill-rules';$battleRules=Read-SelectionRule 'battle-rules';$rules=Read-SelectionRule 'rules'
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load($skillRules,(Get-SelectionRuleHash $skillRules),'854_156383',$battleRules,(Get-SelectionRuleHash $battleRules),'854_156383')
# The original 13990/13992 status parent requires seven distinct random targets.
$support=$catalog.CreateSkill(13990,0,10211,5,20,1,$null)
$supportHit=@([Zjcs.Combat.Official.FightBattleHitCollector]::CollectAll($support)|Where-Object RandomTargetGroupId -gt 0)[0]
Assert-HitSelection ($supportHit.RandomCount -eq 7 -and $supportHit.RandomTargetNotSameTarget) 'Original 13992 distinct-target configuration was changed.'
$wideBoss.Side=$wideOther.Side=$wideSource.Side
$wideRoster.Add($wideOther)
$wideBoss.X=2;$wideBoss.Y=1;$wideOther.X=4;$wideOther.Y=0
$supportContext=[Zjcs.Combat.Official.FightHitTargetSelectionContext]::new()
$supportContext.Source=$wideSource;$supportContext.Units=$wideRoster;$supportContext.Skill=$support;$supportContext.Hit=$supportHit
$supportContext.Targets=$wideRoster;$supportContext.Primary=$wideSource;$supportContext.Grid=$context.Grid
foreach($seed in 1..12){
 $supportContext.Random=[Zjcs.Combat.Official.FightRandomStream]::new($seed)
 $supportSelectors=[Zjcs.Combat.Official.FightHitTargetSelectors]::new()
 $supported=$supportSelectors.For($supportHit).Select($supportContext)
 Assert-HitSelection ($supported.Targets.Count -eq 2 -and @($supported.Targets.Id|Select-Object -Unique).Count -eq 2) "Original 13992 missed a distinct ally at seed $seed."
 Assert-HitSelection ($null -eq $supportSelectors.For($supportHit).Select($supportContext)) 'Original linked status nodes repeated an exhausted target group.'
}
Write-Output 'Original 13990/13992: seven-target limit selects both allies in its original ring across 12 seeds and retains linked-node exclusions.'
$formula=$catalog.CreateDamageFormula($rules,(Get-SelectionRuleHash $rules),'854_156383')
$items=[Zjcs.Combat.Official.FightOfficialSkillItemCatalog]::Load($rules,(Get-SelectionRuleHash $rules),'854_156383')
$checkpoint=[Zjcs.Combat.Official.FightBattleCheckpoint]::new();$checkpoint.StageId=1;$checkpoint.MaxRound=8;$checkpoint.RandomSeed=43
$checkpoint.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=12;Height=12;DefaultGroundType=2}
$source=New-SelectionUnit 1 1 4 4;$source.CharacterType=1
$target=New-SelectionUnit 2 2 6 4;$target.CharacterType=$catalog.MonsterCharacterType(3665)
foreach($x in -1..1){foreach($y in -1..1){$target.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new($x,$y))}}
foreach($unit in @($source,$target)){
 $unit.Level=40;$unit.SubRank=1;$unit.Hp=$unit.MaxHp=$unit.LastMaxHp=1000000000
 $speed=if($unit.Id -eq 1){1000}else{1};$unit.Turn.Id=$unit.Id;$unit.Turn.Hp=$unit.Hp;$unit.Turn.Speed=$speed
 foreach($pair in @(@($catalog.PropertyType('MaxHp'),1000000000),@($catalog.PropertyType('Attack'),1000),@($catalog.PropertyType('Speed'),$speed))){
  $unit.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$pair[0];Value=$pair[1]})
 }
 $checkpoint.Units.Add($unit)
}
$properties=[Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
foreach($entry in $catalog.CalculateSkillProps(13660,5,20,1).GetEnumerator()){$properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$entry.Key;Value=$entry.Value})}
$original=$catalog.CreateSkill(13660,0,10188,5,20,1,$properties);$source.Skills.Add($original);$source.SkillState.Slots.Add($catalog.CreateSkillSlot($original,0))
$runner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($checkpoint,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
$step=$runner.Step(0,$checkpoint.Counter+1).Event
$hits=@($step.DamageEvents|Where-Object SkillEntityId -eq 13660)
Assert-HitSelection ($hits.Count -eq 35) 'Original 13660 no longer matches its selected body grids and cross-shaped Scope.'
foreach($pair in @(@(13662,11),@(13663,11),@(13664,13))){Assert-HitSelection (@($hits|Where-Object HitEntityId -eq $pair[0]).Count -eq $pair[1]) 'An original random node differs from its selected grid intersections.'}
$expectedDraws=@('5,4;6,3;7,3','5,4;6,3;7,5','6,4;5,4;7,4')
for($i=0;$i -lt 3;$i++){
 $draw=($step.SkillCasts[0].HitSelections[$i].SelectedGridPositions|ForEach-Object {'{0},{1}' -f $_.X,$_.Y}) -join ';'
 Assert-HitSelection ($draw -eq $expectedDraws[$i]) 'Original seeded body-grid draws changed.'
}
Assert-HitSelection (@($hits|Where-Object {-not $_.HasHitGridPosition -or $_.Damage -ge 0 -or $_.AfterHp -ge $_.BeforeHp}).Count -eq 0 -and $source.SkillState.Slots[0].ReleasedTimes -eq 1) 'Original random hits lost actual damage, cells or single release.'
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($checkpoint))
Assert-HitSelection ($saved.RandomSeed -eq $checkpoint.RandomSeed -and @($saved.Receipts[0].Event.DamageEvents|Where-Object SkillEntityId -eq 13660).Count -eq 35) 'Original random hit checkpoint changed its stream or damage occurrences.'
Write-Output 'Original 13660 executor: three nodes, three actual body-grid anchors each, 35 cross-scope damage events, one release and checkpoint passed.'

# Settle the unmodified support skill as well as selecting its original parent.
$supportCheckpoint=[Zjcs.Combat.Official.FightBattleCheckpoint]::new();$supportCheckpoint.StageId=1;$supportCheckpoint.MaxRound=8;$supportCheckpoint.RandomSeed=1
$supportCheckpoint.Grid=[Zjcs.Combat.Official.FightBattleGrid]@{Width=12;Height=12;DefaultGroundType=2}
$supportSource=New-SelectionUnit 1 1 4 4;$supportSource.CharacterType=1
$supportBoss=New-SelectionUnit 2 1 6 4
$supportOther=New-SelectionUnit 3 1 8 4
$supportEnemy=New-SelectionUnit 4 2 10 10
foreach($x in -1..1){foreach($y in -1..1){$supportBoss.BodyOffsets.Add([Zjcs.Combat.Official.FightGridPoint]::new($x,$y))}}
foreach($unit in @($supportSource,$supportBoss,$supportOther,$supportEnemy)){
 $unit.Level=40;$unit.SubRank=1;$unit.MaxHp=$unit.LastMaxHp=1000000000;$unit.Hp=500000000
 $speed=if($unit.Id -eq 1){1000}else{1};$unit.Turn.Id=$unit.Id;$unit.Turn.Hp=$unit.Hp;$unit.Turn.Speed=$speed
 foreach($pair in @(@($catalog.PropertyType('MaxHp'),1000000000),@($catalog.PropertyType('Attack'),1000),@($catalog.PropertyType('Speed'),$speed))){
  $unit.Properties.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$pair[0];Value=$pair[1]})
 }
 $supportCheckpoint.Units.Add($unit)
}
$supportProps=[Collections.Generic.List[Zjcs.Combat.Official.FightPropertyValue]]::new()
foreach($entry in $catalog.CalculateSkillProps(13990,5,20,1).GetEnumerator()){$supportProps.Add([Zjcs.Combat.Official.FightPropertyValue]@{Id=$entry.Key;Value=$entry.Value})}
$supportSkill=$catalog.CreateSkill(13990,0,10211,5,20,1,$supportProps)
$supportSource.Skills.Add($supportSkill);$supportSource.SkillState.Slots.Add($catalog.CreateSkillSlot($supportSkill,0))
$supportRunner=[Zjcs.Combat.Official.FightBattleExecutor]::Begin($supportCheckpoint,$formula,[Zjcs.Combat.Official.FightDamageSettlement]::new(),$catalog,$items)
$supportStep=$supportRunner.Step(0,$supportCheckpoint.Counter+1).Event
$supportHits=@($supportStep.DamageEvents|Where-Object SkillEntityId -eq 13990)
Assert-HitSelection ($supportHits.Count -eq 3 -and @($supportHits.TargetId|Select-Object -Unique).Count -eq 3) ('Original 13990 missed its source or a distinct ally: '+($supportHits.TargetId -join ','))
foreach($unit in @($supportSource,$supportBoss,$supportOther)){Assert-HitSelection (@($unit.Statuses|Where-Object ClassId -eq 24719).Count -eq 1) 'Original 13990 failed to apply 24719 to a selected ally.'}
Assert-HitSelection ($supportSource.SkillState.Slots[0].ReleasedTimes -eq 1) 'Original support repeated the equipped release.'
$supportSaved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($supportCheckpoint))
Assert-HitSelection ($supportSaved.RandomSeed -eq $supportCheckpoint.RandomSeed -and $supportSaved.Receipts[0].Event.DamageEvents.Count -eq $supportStep.DamageEvents.Count) 'Original support checkpoint lost its random stream or events.'
Write-Output 'Original 13990 executor: source plus both distinct allies actually receive original 24719, one release and checkpoint passed.'

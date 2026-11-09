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

# Empty anchors still occupy their authored exclusion group and draw stream.
$hit.Scope.Clear();$hit.ScopeEntries.Clear();$hit.RandomTargetScope.Clear();$hit.RandomTargetScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(8,0));$hit.RandomTargetScope.Add([Zjcs.Combat.Official.FightGridPoint]::new(9,0))
$hit.RandomTargetGroupId=4;$hit.RandomTargetAllowEmptyScope=$true;$hit.RandomTargetNotAllowHitEmpty=$false;$hit.RandomTargetNotSameEmptyScope=$true;$hit.RandomTargetNotSameGrid=$true
$seed0=$context.Random.Seed;$empty1=$randomSelector.Select($context);$seed1=$context.Random.Seed;$empty2=$randomSelector.Select($context);$seed2=$context.Random.Seed;$empty3=$randomSelector.Select($context)
Assert-HitSelection ($null -eq $empty1 -and $null -eq $empty2 -and $null -eq $empty3 -and $seed0 -ne $seed1 -and $seed1 -ne $seed2 -and $context.Random.Seed -eq $seed2) 'Empty-grid exclusions or random draw order changed.'

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
Assert-HitSelection ($hits.Count -eq 45) 'Original 13660 lost one of its three anchors per node or five covered body cells.'
foreach($id in 13662,13663,13664){Assert-HitSelection (@($hits|Where-Object HitEntityId -eq $id).Count -eq 15) 'An original random node lost its independent body occurrences.'}
Assert-HitSelection (@($hits|Where-Object {-not $_.HasHitGridPosition -or $_.Damage -ge 0 -or $_.AfterHp -ge $_.BeforeHp}).Count -eq 0 -and $source.SkillState.Slots[0].ReleasedTimes -eq 1) 'Original random hits lost actual damage, cells or single release.'
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load([Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($checkpoint))
Assert-HitSelection ($saved.RandomSeed -eq $checkpoint.RandomSeed -and @($saved.Receipts[0].Event.DamageEvents|Where-Object SkillEntityId -eq 13660).Count -eq 45) 'Original random hit checkpoint changed its stream or damage occurrences.'
Write-Output 'Original 13660 executor: three nodes, three anchors each, five body cells per anchor, 45 actual damage events, one release and checkpoint passed.'

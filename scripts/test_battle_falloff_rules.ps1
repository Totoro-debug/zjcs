. (Join-Path $PSScriptRoot 'combat_sources.ps1')
$ErrorActionPreference='Stop'
Add-Type -Path (Get-FightCombatSources)
function Assert-Falloff($ok,$message){if(-not $ok){throw $message}}
$source=[Zjcs.Combat.Official.FightBattleUnitState]::new();$source.Id=1
$a=[Zjcs.Combat.Official.FightBattleUnitState]::new();$a.Id=2
$b=[Zjcs.Combat.Official.FightBattleUnitState]::new();$b.Id=3
$skill=[Zjcs.Combat.Official.FightBattleSkillDefinition]::new();$skill.EntityId=13745;$skill.RootEntityId=13740;$skill.ItemId=10193
$hit=[Zjcs.Combat.Official.FightBattleHitDefinition]::new();$hit.DamageId=10023
$status=[Zjcs.Combat.Official.FightBattleStatusState]::new();$status.ClassId=999;$status.InstanceId=71;$status.OwnerId=2;$status.CreatorId=1;$status.RootSkillId=13740
$status.Rate=1;$status.FalloffTarget=1;$status.NumOfStart=1;$status.MaxFalloffCount=2;$status.FalloffPercent=5000;$status.DamageId=10023
$context=[Zjcs.Combat.Official.FightDamageFalloffContext]::new();$context.Source=$source;$context.Target=$a;$context.Skill=$skill;$context.Hit=$hit;$context.Status=$status
$context.Random=[Zjcs.Combat.Official.FightRandomStream]::new(12345)
$serial=0
[Zjcs.Combat.Official.FightDamageFalloff]::BeginCast($skill,[ref]$serial)
$rule=[Zjcs.Combat.Official.FightDamageFalloffRules]::For($status)
Assert-Falloff ($rule -is [Zjcs.Combat.Official.IFightDamageFalloffRule]) 'Per-target rule does not implement the capability interface.'
$values=@($rule.Apply($context,-100),$rule.Apply($context,-100),$rule.Apply($context,-100),$rule.Apply($context,-100))
Assert-Falloff (($values -join ',') -eq '-100,-50,-25,-25') 'Per-target body occurrences lost start/max falloff.'
$context.Target=$b
Assert-Falloff ($rule.Apply($context,-100) -eq -100 -and $b.FalloffCounts.Count -eq 0) 'A target-owned rule applied to another target.'
$status.OwnerId=3
Assert-Falloff ($rule.Apply($context,-100) -eq -100 -and $b.FalloffCounts.Count -eq 1) 'Independent target inherited another target count.'
[Zjcs.Combat.Official.FightDamageFalloff]::EndCast([Zjcs.Combat.Official.FightBattleUnitState[]]@($source,$a,$b),$skill)
Assert-Falloff ($a.FalloffCounts.Count -eq 0 -and $b.FalloffCounts.Count -eq 0) 'Cast cleanup retained body-cell counters.'
[Zjcs.Combat.Official.FightDamageFalloff]::BeginCast($skill,[ref]$serial)
$status.Scope=1
$shared=[Zjcs.Combat.Official.FightDamageFalloffRules]::For($status)
Assert-Falloff ($shared.GetType() -ne $rule.GetType()) 'Configured cast scope did not select another implementation.'
$context.Target=$a;$first=$shared.Apply($context,-100)
$context.Target=$b;$second=$shared.Apply($context,-100)
Assert-Falloff ($first -eq -100 -and $second -eq -50 -and $skill.FalloffCounts.Count -eq 1) 'Shared cast scope reset between different targets.'
$context.ProcessedStatuses=[Collections.Generic.List[int]]::new()
$once=$shared.Apply($context,-100);$twice=$shared.Apply($context,-100)
Assert-Falloff ($once -eq -25 -and $twice -eq -100 -and $skill.FalloffCounts[0].Count -eq 3) 'One damage stage processed the same status twice.'
$context.ProcessedStatuses=$null
[Zjcs.Combat.Official.FightDamageFalloff]::EndCast([Zjcs.Combat.Official.FightBattleUnitState[]]@($source,$a,$b),$skill)
[Zjcs.Combat.Official.FightDamageFalloff]::BeginCast($skill,[ref]$serial)
Assert-Falloff ($shared.Apply($context,-100) -eq -100) 'Another child explosion reused the previous cast count.'
# Filters run before probability, and failed probability never advances counters.
$seed=$context.Random.Seed;$status.RootSkillId=99999;$status.Rate=0.5
Assert-Falloff ($shared.Apply($context,-100) -eq -100 -and $context.Random.Seed -eq $seed -and $skill.FalloffCounts[0].Count -eq 1) 'Root filtering consumed RNG or changed counts.'
$status.RootSkillId=13740;$status.Rate=0
Assert-Falloff ($shared.Apply($context,-100) -eq -100 -and $context.Random.Seed -eq $seed -and $skill.FalloffCounts[0].Count -eq 1) 'Zero probability advanced a counter.'
[Zjcs.Combat.Official.FightDamageFalloff]::EndCast([Zjcs.Combat.Official.FightBattleUnitState[]]@($source,$a,$b),$skill)
# Calls outside an active cast retain the pre-existing persistent status counters.
$context.Skill=$null;$status.Rate=1;$status.FalloffTarget=4;$status.RemoveOnSourceSkillStart=$true
$context.Status=$status
Assert-Falloff ($shared.Apply($context,-100) -eq -100 -and $shared.Apply($context,-100) -eq -50 -and $status.Counts[0].Count -eq 2) 'Persistent status scope did not retain its independent count.'
$b.Statuses.Add($status)
[Zjcs.Combat.Official.FightDamageFalloff]::ResetOnSkillStart([Zjcs.Combat.Official.FightBattleUnitState[]]@($source,$a,$b),13740)
Assert-Falloff ($status.Counts.Count -eq 0) 'Source skill reset did not clear persistent counts.'
Write-Output 'Falloff capability: per-target/body cells, shared cast/targets, start/max, per-stage dedup, independent child casts, pre-probability filters and persistent reset passed.'

$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'combat_sources.ps1')
Add-Type -Path @(Get-FightCombatSources)
$root=Split-Path -Parent $PSScriptRoot
$skillManifest=(Get-Content -LiteralPath "$root/unity/Assets/Official/Combat/skill-rules-source-manifest.json" -Raw | ConvertFrom-Json).manifest
$battleManifest=(Get-Content -LiteralPath "$root/unity/Assets/Official/Combat/battle-rules-source-manifest.json" -Raw | ConvertFrom-Json).manifest
function Text($name){[IO.File]::ReadAllText("$root/unity/Assets/Official/Combat/$name.json")}
function Hash($name){(Get-FileHash -LiteralPath "$root/unity/Assets/Official/Combat/$name.json" -Algorithm SHA256).Hash.ToLowerInvariant()}
$catalog=[Zjcs.Combat.Official.FightOfficialCombatCatalog]::Load((Text 'skill-rules'),(Hash 'skill-rules'),$skillManifest,(Text 'battle-rules'),(Hash 'battle-rules'),$battleManifest)
function Require($condition,$message){if(-not $condition){throw $message}}
function Near($actual,$expected,$message){Require ([Math]::Abs($actual-$expected) -lt 0.00011) "$message expected=$expected actual=$actual"}
$cases=@(
 @(15300,.7,.5),@(15310,.7,.4),@(15320,5,5),@(15330,.7,.7),@(15340,1.533,.767),
 @(19800,.5667,.5667),@(19810,5.1,5),@(19820,.6,.6),@(19830,4.533,4.533),@(19840,1.4,1.4),
 @(19850,2.333,2.333),@(19870,5.1,5),@(19890,5.1,5.1),@(19900,.5667,.5667),@(19910,5.1,5.1),
 @(19920,1.167,1.167),@(19930,1,1),@(19950,5.1,5.1),@(19960,1.5,1.5),@(19970,1.667,1.667),
 @(19980,.833,.833),@(19990,1.767,1.767)
)
foreach($case in $cases){
 $skill=$catalog.CreateOrdinaryMovementSkill([int]$case[0])
 $timing=[Zjcs.Combat.Official.FightOrdinaryMovementTiming]::Create($skill,5)
 Near $timing.EntityEndDelay $case[1] "Original $($case[0]) entity lifetime"
 Near $timing.ActionEndDelay $case[2] "Original $($case[0]) action occupancy"
}
$skill=$catalog.CreateOrdinaryMovementSkill(15300)
$early=[Zjcs.Combat.Official.FightOrdinaryMovementTiming]::Create($skill,.2)
Near $early.EntityEndDelay .3 '15300 must use earlier AllEnded timer'
Near $early.ActionEndDelay .5 '15300 action occupancy remains independent'
$late=[Zjcs.Combat.Official.FightOrdinaryMovementTiming]::Create($skill,1)
Near $late.EntityEndDelay .7 '15300 fixed timer wins before movement End'
$skill.Duration=0
Near ([Zjcs.Combat.Official.FightOrdinaryMovementTiming]::Create($skill,1)).EntityEndDelay 0 'Zero fixed timer ends immediately'
$skill.Duration=-1;$skill.AllEndedDuration=-1;$skill.AllRelevantEndedDuration=.05
Near ([Zjcs.Combat.Official.FightOrdinaryMovementTiming]::Create($skill,1)).EntityEndDelay 1.05 'Independent AllRelevantEnded timer'
$skill.AllRelevantEndedDuration=-1
Near ([Zjcs.Combat.Official.FightOrdinaryMovementTiming]::Create($skill,1)).EntityEndDelay -1 'Disabled timers preserve indefinite lifetime'
Require (-not [Zjcs.Combat.Official.FightOrdinaryMovementTiming]::Supports(13100)) 'Policy leaked into ordinary active skills'
$rejected=$false
try { $catalog.CreateOrdinaryMovementSkill(13100) | Out-Null } catch { $rejected=$true }
Require $rejected 'Movement factory accepted an active skill'
$rejected=$false
try { $catalog.CreateSkill(15300,-1,0,1,1,0,$null) | Out-Null } catch { $rejected=$true }
Require $rejected 'General skill factory silently accepted an unresolved hit graph'
$skill=$catalog.CreateOrdinaryMovementSkill(15300);$skill.HasReplay=$true
Require (-not [Zjcs.Combat.Official.FightOrdinaryMovementTiming]::Supports($skill)) 'Changed movement replay escaped configuration review'
# Exercise the real role-to-movement factory and persisted creation snapshot.
$unit=[Zjcs.Combat.Official.FightBattleUnitState]::new()
$unit.Id=1;$unit.CharacterType=1;$unit.X=3;$unit.Y=2;$unit.Direction=1
$unit.HasFlipX=$true;$unit.FlipX=$true
$unit.Statuses.Add([Zjcs.Combat.Official.FightBattleStatusState]@{InstanceId=91;ClassId=24031;StackCount=2})
$movement=$catalog.CreateRoleMovement($unit,$false)
Require ($movement.MoveSkillEntityId -eq 15300 -and $movement.OrdinarySkillPresentation -ne $null) 'Player role lost original ordinary skill entity'
$movement.Path.Add([Zjcs.Combat.Official.FightGridPoint]::new(3,2))
$movement.Path.Add([Zjcs.Combat.Official.FightGridPoint]::new(3,3))
$movement.Path.Add([Zjcs.Combat.Official.FightGridPoint]::new(3,4))
$unit.X=3;$unit.Y=4;$unit.Direction=2;$unit.FlipX=$false;$unit.Statuses[0].StackCount=4
$catalog.CompleteRoleMovement($movement)
$view=$movement.OrdinarySkillPresentation
Require ($view.SourceX -eq 3 -and $view.SourceY -eq 2 -and $view.TargetX -eq 3 -and $view.TargetY -eq 4) 'Movement parent lost its creation source or settled target'
Require ($view.FacingIndex -eq 1 -and $view.FlipX -and $view.SourceStatuses[0].StackCount -eq 2) 'Movement parent snapshot was rewritten by path settlement'
Near $view.ActionDuration .5 'Player movement action occupancy'
$cp=[Zjcs.Combat.Official.FightBattleCheckpoint]::new()
$cp.StageId=1;$cp.MaxRound=10;$cp.RandomSeed=23
$receipt=[Zjcs.Combat.Official.FightBattleStepReceipt]::new()
$receipt.Event=[Zjcs.Combat.Official.FightBattleStepEvent]::new();$receipt.Event.Movements.Add($movement)
$receipt.Counter=1;$cp.Counter=1
$cp.Receipts.Add($receipt)
$saved=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Save($cp)
$restored=[Zjcs.Combat.Official.FightBattleCheckpointSerializer]::Load($saved)
Require ($restored.Receipts[0].Event.Movements[0].OrdinarySkillPresentation.SourceStatuses[0].StackCount -eq 2) 'Checkpoint lost ordinary skill snapshot'
$movement.OrdinaryMoveDelay=.15;$movement.Delay=.15
Near ([Zjcs.Combat.Official.FightOrdinaryMovementTiming]::ActionStartDelay($movement,0)) .15 'First ordinary prelude'
Near ([Zjcs.Combat.Official.FightOrdinaryMovementTiming]::ActionStartDelay($movement,.9)) 1.05 'Repeated ordinary prelude after previous path'
$movement.OrdinarySkillPresentation=$null
Near ([Zjcs.Combat.Official.FightOrdinaryMovementTiming]::ActionStartDelay($movement,.9)) .9 'Legacy/fallback retains previous action boundary'
# Original action duration uses complete RealDistance, including diagonal segments.
$skill=$catalog.CreateOrdinaryMovementSkill(15320)
$path=[Collections.Generic.List[Zjcs.Combat.Official.FightGridPoint]]::new()
$path.Add([Zjcs.Combat.Official.FightGridPoint]::new(0,0));$path.Add([Zjcs.Combat.Official.FightGridPoint]::new(3,4))
$skill.SourceMoves[0].MoveDuration=-1;$skill.SourceMoves[0].MoveSpeed=2;$skill.SourceMoves[0].Delay=.15
$pathTiming=[Zjcs.Combat.Official.FightOrdinaryMovementTiming]::ForPath($skill,$path,.2)
Near $pathTiming.EntityEndDelay 2.85 'Original distance/speed plus move delay and delayed End'
Near $pathTiming.ActionEndDelay 2.85 'AllEnded occupancy includes delayed End'
Write-Output 'Ordinary movement timing passed: 22 original configurations, independent entity/action clocks, earlier timer competition, zero and disabled timers, scope restriction.'

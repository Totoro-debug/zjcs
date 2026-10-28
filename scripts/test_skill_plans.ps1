$ErrorActionPreference = 'Stop'
$skillRoot = Join-Path $PSScriptRoot '../unity/Assets/Game/Runtime/Offline/Skills'
$skillSources = @('OfflineSkillProfile.cs', 'OfflineSkillProgression.cs', 'OfficialSkillAchievementRules.cs') | ForEach-Object { Join-Path $skillRoot $_ }
$skillSources += Get-ChildItem (Join-Path $PSScriptRoot '../unity/Assets/Game/Runtime/Official/SkillRules') -Filter '*.cs' | ForEach-Object FullName
Add-Type -Path $skillSources
function Assert-Plan($value, $message) { if (-not $value) { throw $message } }

$legacy = [Zjcs.Offline.Skills.OfflineSkillProfile]::new()
$legacy.Plans = $legacy.Plans[0..2]
$legacy.SelectedPlan = 2
$legacy.Plans[2].Name = '保留名称'
$legacy.Plans[2].PetIndex = 42
$legacy.Plans[2].ActiveSkills[1] = 10001
$legacy.ActiveSlotLevels[0] = 7
$original = $legacy.Plans[2]
[Zjcs.Offline.Skills.OfflineSkillProgression]::Normalize($legacy)
Assert-Plan ($legacy.Plans.Length -eq 7 -and $legacy.SelectedPlan -eq 2) 'Legacy save must gain all seven official plans without changing selection.'
Assert-Plan ([object]::ReferenceEquals($original, $legacy.Plans[2]) -and $legacy.Plans[2].Name -eq '保留名称' -and $legacy.Plans[2].PetIndex -eq 42 -and $legacy.Plans[2].ActiveSkills[1] -eq 10001 -and $legacy.ActiveSlotLevels[0] -eq 7) 'Legacy plan, pet, equipped skills and shared slot levels must be preserved.'
Assert-Plan ($legacy.Plans[6].Name -eq '方案七' -and [Zjcs.Offline.Skills.OfflineSkillProgression]::IsValid($legacy)) 'Expanded plans must have official defaults and validate.'
[Zjcs.Offline.Skills.OfflineSkillProgression]::Normalize($legacy)
Assert-Plan ([object]::ReferenceEquals($original, $legacy.Plans[2])) 'Repeated normalization must not replace existing plans.'
$bad = [Zjcs.Offline.Skills.OfflineSkillProfile]::new()
$bad.Plans = $bad.Plans[0..3]
[Zjcs.Offline.Skills.OfflineSkillProgression]::Normalize($bad)
Assert-Plan (-not [Zjcs.Offline.Skills.OfflineSkillProgression]::IsValid($bad)) 'Malformed plan arrays must remain invalid.'
$service = [Zjcs.Offline.Skills.OfflineSkillProgression]::new([Zjcs.Offline.Skills.OfflineSkillRules]::new())
$fresh = [Zjcs.Offline.Skills.OfflineSkillProfile]::new()
Assert-Plan ($service.SelectPlan($fresh, 6).Code.ToString() -eq 'Applied' -and $fresh.SelectedPlan -eq 6) 'Seventh plan must be selectable.'
Assert-Plan ($service.SelectPlan($fresh, 7).Code.ToString() -eq 'InvalidArgument' -and $fresh.SelectedPlan -eq 6) 'Out-of-range selection must not mutate the save.'
Write-Output 'Skill plan migration checks passed.'

Assert-Plan ($service.RenamePlan($fresh, 6, '中文六字方案').IsApplied) 'Six CJK characters must fit the official twelve-unit limit.'
Assert-Plan ($service.CheckPlanName('中文七字方案名') -eq 1149) 'Seven CJK characters must exceed the official limit.'
Assert-Plan ($service.CheckPlanName('abc def') -eq 1150 -and $service.CheckPlanName('abc#') -eq 1150) 'Forbidden spaces and markup characters must be rejected without trimming.'
Assert-Plan ($service.CheckPlanName('') -eq 1204 -and $service.CheckPlanName($null) -eq 1204) 'Empty names must use the original error code.'
$emoji = [char]::ConvertFromUtf32(0x1f600)
$emojiName = $emoji * 12
Assert-Plan ($service.RenamePlan($fresh, 6, $emojiName).IsApplied -and [Zjcs.Offline.Skills.OfflineSkillProgression]::IsValid($fresh)) 'Twelve emoji must survive official grapheme counting and save validation.'
$fresh.Plans[6].Name = '旧版十六字符名称abcdefghijkl'.Substring(0,16)
Assert-Plan ([Zjcs.Offline.Skills.OfflineSkillProgression]::IsValid($fresh)) 'Names accepted by earlier releases must remain loadable.'
$fresh.Plans[6].Name = ''
[Zjcs.Offline.Skills.OfflineSkillProgression]::Normalize($fresh)
Assert-Plan ($fresh.Plans[6].Name -eq '方案七') 'Blank names must recover the original Chinese default.'
Write-Output 'Official skill plan CJK name checks passed.'

$ErrorActionPreference = 'Stop'
$skillRoot = Join-Path $PSScriptRoot '../unity/Assets/Game/Runtime/Offline/Skills'
$skillSources = @('OfflineSkillProfile.cs', 'OfflineSkillProgression.cs', 'OfficialSkillAchievementRules.cs') | ForEach-Object { Join-Path $skillRoot $_ }
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

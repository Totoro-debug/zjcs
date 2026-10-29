$ErrorActionPreference = 'Stop'
$skillRoot = Join-Path $PSScriptRoot '../unity/Assets/Game/Runtime/Offline/Skills'
$skillSources = @('OfflineSkillProfile.cs', 'OfflineSkillProgression.cs', 'OfficialSkillAchievementRules.cs', 'OfflineSkillItemNewMarks.cs') | ForEach-Object { Join-Path $skillRoot $_ }
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

$marks = [Zjcs.Offline.Skills.OfflineSkillItemNewMarks]::SelectPlayer('isolated-player-a')
Assert-Plan (-not $marks.IsNewItem(10014)) 'Existing login items must not be marked new.'
$marks.OnItemCountUpdate(10014, 0, 1)
Assert-Plan $marks.IsNewItem(10014) 'A newly acquired skill must be marked new.'
Assert-Plan ([object]::ReferenceEquals($marks, [Zjcs.Offline.Skills.OfflineSkillItemNewMarks]::SelectPlayer('isolated-player-a'))) 'Opening another view of the current player must retain session marks.'
$marks.RemoveNewItem(10014)
$marks.OnItemCountUpdate(10014, 1, 2)
Assert-Plan (-not $marks.IsNewItem(10014)) 'Viewed skills must not become new when their count increases.'
$marks.OnItemCountUpdate(12014, 0, 10)
$marks.OnItemCountUpdate(12014, 10, 0)
Assert-Plan (-not $marks.IsNewItem(12014)) 'Consuming all new pieces must remove their mark.'
[void][Zjcs.Offline.Skills.OfflineSkillItemNewMarks]::SelectPlayer('isolated-player-b')
$second = [Zjcs.Offline.Skills.OfflineSkillItemNewMarks]::SelectPlayer('isolated-player-b')
$second.OnItemCountUpdate(10014, 0, 1)
[void][Zjcs.Offline.Skills.OfflineSkillItemNewMarks]::SelectPlayer('isolated-player-a')
Assert-Plan (-not [Zjcs.Offline.Skills.OfflineSkillItemNewMarks]::SelectPlayer('isolated-player-b').IsNewItem(10014)) 'Changing the logged-in player must reset transient marks.'
$logout = [Zjcs.Offline.Skills.OfflineSkillItemNewMarks]::SelectPlayer('isolated-player-b')
$logout.OnItemCountUpdate(10014, 0, 1)
[Zjcs.Offline.Skills.OfflineSkillItemNewMarks]::EndPlayerSession()
Assert-Plan (-not $logout.IsNewItem(10014)) 'Logging out must clear marks in retained views.'
Assert-Plan (-not [Zjcs.Offline.Skills.OfflineSkillItemNewMarks]::SelectPlayer('isolated-player-b').IsNewItem(10014)) 'Logging into the same slot again must start without old marks.'
Write-Output 'Official transient skill item new-mark checks passed.'

# PlayerSkillDataExt.CanSkillRankUp / TryGetAllSkillPromoteRank do not filter
# profession. Equipment retains its separate profession restriction.
$rankRules = [Zjcs.Offline.Skills.OfflineSkillRules]::new()
$foreign = [Zjcs.Offline.Skills.OfflineSkillDefinition]::new()
$foreign.ClassId = 10001; $foreign.ItemType = 100; $foreign.AvailableProfessions = @(2); $foreign.MaximumRank = 2
$rankCost = [Zjcs.Offline.Skills.OfflineSkillRankCost]::new(); $rankCost.CurrentRank = 1
$pieceCost = [Zjcs.Offline.Skills.OfflineSkillItemCount]::new(); $pieceCost.ItemId = 12001; $pieceCost.Count = 10
$rankCost.Items = @($pieceCost); $foreign.RankCosts = @($rankCost); $rankRules.Skills = @($foreign)
$rankService = [Zjcs.Offline.Skills.OfflineSkillProgression]::new($rankRules)
$rankProfile = [Zjcs.Offline.Skills.OfflineSkillProfile]::new()
$foreignOwned = [Zjcs.Offline.Skills.OfflineOwnedSkill]::new(); $foreignOwned.ClassId = 10001; $foreignOwned.Rank = 1
$rankProfile.Skills.Add($foreignOwned)
$foreignPieces = [Zjcs.Offline.Skills.OfflineSkillItemCount]::new(); $foreignPieces.ItemId = 12001; $foreignPieces.Count = 20
$rankProfile.Items.Add($foreignPieces)
$rankContext = [Zjcs.Offline.Skills.OfflineSkillPlayerContext]::new(); $rankContext.Profession = 1
Assert-Plan ($rankService.Equip($rankProfile, $rankContext, 10001, 0).Code.ToString() -eq 'SkillUnavailable') 'An unavailable profession skill must remain unequippable.'
$batchProfile = $rankProfile.Clone()
Assert-Plan ($rankService.PromoteSkill($rankProfile, $rankContext, 10001).IsApplied -and $rankProfile.Skills[0].Rank -eq 2 -and $rankProfile.Items[0].Count -eq 10) 'Owned skills of another profession must rank up at the original piece cost.'
Assert-Plan ($rankService.PromoteAllSkills($batchProfile, $rankContext).IsApplied -and $batchProfile.Skills[0].Rank -eq 2 -and $batchProfile.Items[0].Count -eq 10) 'Batch promotion must include owned skills of other professions.'
Write-Output 'Official profession-independent skill rank-up checks passed.'

# The next level must cross the threshold, and every actual (base + current
# season blessing) slot must reach its limit. Old-season blessings do not count.
$gapRules = [Zjcs.Offline.Skills.OfflineSkillRules]::new()
$baseCap = [Zjcs.Offline.Skills.OfflineSkillSubRankLimit]::new(); $baseCap.SubRank = 2; $baseCap.SkillLevel = 120
$gapRules.BaseLevelLimits = @($baseCap)
$currentSeason = [Zjcs.Offline.Skills.OfflineSkillSeason]::new(); $currentSeason.Season = 2; $currentSeason.Rank = 2
$gapRules.Seasons = @($currentSeason)
$gapA = [Zjcs.Offline.Skills.OfflineSkillLevelGapLimit]::new(); $gapA.SeasonId = 2; $gapA.Level = 130; $gapA.LimitLevel = 130; $gapA.IsOpen = $true
$gapB = [Zjcs.Offline.Skills.OfflineSkillLevelGapLimit]::new(); $gapB.SeasonId = 2; $gapB.Level = 140; $gapB.LimitLevel = 140; $gapB.IsOpen = $true
$gapRules.LevelGapLimits = @($gapA, $gapB)
$gapService = [Zjcs.Offline.Skills.OfflineSkillProgression]::new($gapRules)
$gapProfile = [Zjcs.Offline.Skills.OfflineSkillProfile]::new()
$gapProfile.ActiveSlotLevels = @(120,120,120,120); $gapProfile.PassiveSlotLevels = @(120,120,120,120)
$gapContext = [Zjcs.Offline.Skills.OfflineSkillPlayerContext]::new(); $gapContext.SubRank = 2; $gapContext.PlayerRank = 2; $gapContext.CurrentSeasonRank = 2; $gapContext.CurrentSeasonId = 2
$minimum = 0; $otherSlots = $false
Assert-Plan ($gapService.CheckBlessLevelCanUpgrade($gapProfile,$gapContext,130,[ref]$minimum,[ref]$otherSlots)) 'The exact first threshold must not activate the gap rule.'
Assert-Plan (-not $gapService.CheckBlessLevelCanUpgrade($gapProfile,$gapContext,131,[ref]$minimum,[ref]$otherSlots) -and $minimum -eq 130 -and -not $otherSlots) 'Crossing a gap must require all eight actual slot levels.'
$currentSeason.Season = 77; $gapA.SeasonId = 77; $gapB.SeasonId = 77
Assert-Plan (-not $gapService.CheckBlessLevelCanUpgrade($gapProfile,$gapContext,131,[ref]$minimum,[ref]$otherSlots) -and $minimum -eq 130) 'Gap table season ids must be mapped from the current rank, independently of offline blessing record keys.'
$currentSeason.Season = 2; $gapA.SeasonId = 2; $gapB.SeasonId = 2
$bless = [Zjcs.Offline.Skills.OfflineSkillBlessRecord]::new(); $bless.SeasonId = 1; $bless.ActiveSlotLevels = @(10,10,10,10); $bless.PassiveSlotLevels = @(10,10,10,10)
$gapProfile.BlessRecords.Add($bless)
Assert-Plan (-not $gapService.CheckBlessLevelCanUpgrade($gapProfile,$gapContext,131,[ref]$minimum,[ref]$otherSlots)) 'A previous-season blessing must not satisfy the current gap.'
$bless.SeasonId = 2
Assert-Plan ($gapService.CheckBlessLevelCanUpgrade($gapProfile,$gapContext,140,[ref]$minimum,[ref]$otherSlots) -and $minimum -eq 130) 'The next row boundary must remain in the preceding interval.'
Assert-Plan (-not $gapService.CheckBlessLevelCanUpgrade($gapProfile,$gapContext,141,[ref]$minimum,[ref]$otherSlots) -and $minimum -eq 140) 'The last row must apply beyond its threshold.'
$gapProfile.PassiveSlotLevels[3] = 119
Assert-Plan (-not $gapService.CheckBlessLevelCanUpgrade($gapProfile,$gapContext,121,[ref]$minimum,[ref]$otherSlots) -and $otherSlots) 'Every base slot must reach its exact cap before blessing upgrades.'
$gapProfile.PassiveSlotLevels[3] = 120
$capRow = [Zjcs.Offline.Skills.OfflineSkillLevelLimit]::new(); $capRow.SubRank = 2; $capRow.PlayerLevel = 120; $capRow.PlayerBlessLevel = 20; $capRow.SkillLevel = 120; $capRow.SkillBlessLevel = 40
$gapRules.LevelLimits = @($capRow)
$blessCost = [Zjcs.Offline.Skills.OfflineSkillBlessCost]::new(); $blessCost.Rank = 2; $blessCost.BlessLevel = 11
$blessItem = [Zjcs.Offline.Skills.OfflineSkillItemCount]::new(); $blessItem.ItemId = 42200; $blessItem.Count = 5; $blessCost.Items = @($blessItem)
$gapRules.SlotBlessCosts = @($blessCost)
$funds = [Zjcs.Offline.Skills.OfflineSkillItemCount]::new(); $funds.ItemId = 42200; $funds.Count = 50; $gapProfile.Items.Add($funds)
$gapContext.BaseLevel = 120; $gapContext.PlayerBlessLevel = 20
$bless.PassiveSlotLevels[3] = 9
Assert-Plan ($gapService.UpgradeSlot($gapProfile,$gapContext,100,0).Code.ToString() -eq 'LevelLimit' -and $gapProfile.Items[0].Count -eq 50) 'A real upgrade transaction must reject a gap violation without spending.'
$bless.PassiveSlotLevels[3] = 10
Assert-Plan ($gapService.UpgradeSlot($gapProfile,$gapContext,100,0).IsApplied -and $gapService.SlotLevel($gapProfile,100,0,2) -eq 131 -and $gapProfile.Items[0].Count -eq 45) 'Reaching the minimum must allow the same upgrade at the exact original cost.'
Write-Output 'Official skill blessing gap and season boundary checks passed.'

# SkillPromotePanelLevelUpContent checks the next season before player caps.
$nextSeason = [Zjcs.Offline.Skills.OfflineSkillSeason]::new(); $nextSeason.Season = 3; $nextSeason.Rank = 3
$gapRules.Seasons = @($nextSeason); $baseCap.SkillBlessLevel = 40
Assert-Plan ($gapService.NextSeasonLevelLimit($gapContext,160) -eq -1) 'The final level of the current season must remain available.'
Assert-Plan ($gapService.NextSeasonLevelLimit($gapContext,161) -eq 3) 'Exceeding the current season cap must identify the next configured season.'
$nextSeason.Rank = 4
Assert-Plan ($gapService.NextSeasonLevelLimit($gapContext,161) -eq -1) 'An absent immediately following rank must not invent a season gate.'
Write-Output 'Official next-season skill level gate checks passed.'

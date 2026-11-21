using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Skills;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        // Only the explicit diagnostic supplies materials/profession. Acquisition,
        // equipment, cultivation and combat all use their normal production paths.
        private static IEnumerator ExerciseAcquiredActive(string slot, string root, string output, int skillId)
        {
            AudioListener.volume = 0f;
            Action<Selectable> click = Click;
            bool crisisGuard = skillId == 10032;
            bool starShatteringSlash = skillId == 10039;
            bool shieldCharge = skillId == 10073;
            bool holyPurification = skillId == 10079;
            bool surgingTide = skillId == 10047;
            bool waterBlade = skillId == 10082;
            bool lightSwordArray = skillId == 10083;
            bool fourthPromotion = surgingTide || waterBlade || lightSwordArray;
            bool statusSkill = crisisGuard || skillId == 10024 || skillId == 10046 || skillId == 10051 || skillId == 10066;
            bool thirdPromotion = crisisGuard || starShatteringSlash || shieldCharge || holyPurification || skillId == 10017;
            Require(thirdPromotion || fourthPromotion || skillId == 10000 || skillId == 10008 || skillId == 10009 || skillId == 10010 || skillId == 10011 || skillId == 10012 || skillId == 10013 || skillId == 10014 || skillId == 10015 || skillId == 10016 || skillId == 10018 || skillId == 10024 || skillId == 10026 || skillId == 10028 || skillId == 10035 || skillId == 10043 || skillId == 10044 || skillId == 10046 || skillId == 10048 || skillId == 10049 || skillId == 10051 || skillId == 10053 || skillId == 10066,
                "Select an implemented active-skill acquisition diagnostic.");
            int profession = fourthPromotion ? 4 : thirdPromotion ? 3 : skillId == 10000 || skillId == 10028 || skillId == 10035 ? 10 : skillId == 10010 || skillId == 10044 || skillId == 10046 || skillId == 10048 || skillId == 10049 || skillId == 10066 ? 2 : skillId == 10008 || skillId == 10009 || skillId == 10011 || skillId == 10012 || skillId == 10013 || skillId == 10014 || skillId == 10015 || skillId == 10018 || skillId == 10024 || skillId == 10043 || skillId == 10051 ? 1 : 6;
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            var rules = assets.LoadSkillRules();
            var definition = rules.Skills.Single(value => value.ClassId == skillId);
            var catalog = assets.Load();
            var skillItem = catalog.FindItem(skillId);
            var piece = catalog.FindItem(definition.PieceItemId);
            int initialRank = rules.RankDisplays.Where(value => value.Quality == Convert.ToInt32(skillItem["Quality"]))
                .Min(value => value.Rank);
            int cultivatedRank = initialRank + 1;
            var rankCost = definition.RankCosts.Single(value => value.CurrentRank == initialRank);
            var texts = (Dictionary<string, object>)OfficialEquipmentJson.Parse(
                OfficialSkillAssetCatalog.LoadDefault().FidelityTexts.text);
            string skillName = Convert.ToString(texts["item_" + skillId + "_name"]);
            string[] qualities = { "None", "White", "Blue", "Purple", "Orange", "Gold", "Red", "Rainbow" };
            string pieceQuality = Convert.ToString(texts["Quality." + qualities[Convert.ToInt32(piece["Quality"])]]);
            var slots = new ProfileSlotCatalog(root);
            var save = new OfflineMainlineSave();
            var player = save.Player;
            OfflineItemAutoIncrease.InitializeNewCharacter(player, DateTime.UtcNow);
            player.CharacterCreated = true; player.CharacterName = skillName + "\u9a8c\u8bc1";
            player.Rank = fourthPromotion ? 4 : thirdPromotion ? 3 : 2;
            player.SubRank = fourthPromotion ? 8 : 5; player.MaxProfessionRank = player.Rank;
            player.Level = fourthPromotion ? rules.LevelLimits.Where(value => value.SubRank == player.SubRank
                && value.PlayerBlessLevel == 0 && value.SkillLevel >= 2).Min(value => value.PlayerLevel) : 50;
            player.Experience = assets.Load().CumulativeLevelExperience[player.Level];
            player.InitialWeaponType = profession == 10 ? Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.WandInitialWeaponType : 200;
            player.Wardrobe.Profession = profession;
            if (skillId == 10000 || skillId == 10028 || skillId == 10035) SkillBlazingHeartValidation.VerifyOriginalWish(output, save, skillId);
            if (skillId == 10010 || skillId == 10044 || skillId == 10046 || skillId == 10048 || skillId == 10049 || skillId == 10066) SkillBlazingHeartValidation.VerifyOriginalWish(output, save, skillId, 2, 2);
            if (skillId == 10017) SkillBlazingHeartValidation.VerifyOriginalWish(output, save, skillId, 3, 3);
            int mergeCount = assets.Load().SkillMergePiece(skillId);
            Require(mergeCount > 0 && definition.AvailableProfessions.Contains(profession), "Original acquisition inputs missing.");
            void Supply(int item, long count)
            {
                var stack = player.Inventory.SingleOrDefault(value => value.ItemId == item);
                if (stack == null) player.Inventory.Add(new OfflineItemStack { ItemId = item, Count = count });
                else stack.Count += count;
            }
            Supply(definition.PieceItemId, mergeCount);
            foreach (var cost in rankCost.Items) Supply(cost.ItemId, cost.Count);
            foreach (var cost in rules.SlotLevelCosts.Single(value => value.Level == 2).Items) Supply(cost.ItemId, cost.Count);
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            var home = HomeFeatureRuntime.Begin(slot, root, null);
            home.TutorialAutoStartEnabled = false;
            yield return new WaitForSecondsRealtime(1f);
            click(home.Hud.CanvasTransform.GetComponentsInChildren<Selectable>().Single(value =>
                value.name == "UnlockObj" && value.transform.parent.name == "Character"));
            yield return new WaitForSecondsRealtime(.8f);
            Canvas.ForceUpdateCanvases();
            var pageCanvas = FindObjectsOfType<Canvas>().Single(value => value.name == "HomeCharacterPage");
            Require(Mathf.Abs(pageCanvas.pixelRect.width - Screen.width) < 1f
                && Mathf.Abs(pageCanvas.pixelRect.height - Screen.height) < 1f,
                "The native character canvas and rendered input surface have different sizes: " + pageCanvas.pixelRect);
            yield return CaptureAcquiredActive(output, "character");
            click(FlameFlowButton(value => value.name == "CharacterPanelSkillBtn"));
            yield return new WaitForSecondsRealtime(.8f);
            var character = FindObjectsOfType<CharacterFeatureRuntime>().Single();
            var progression = character.Progression;
            Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId) == 0,
                "The original merge flow must start without the skill.");
            click(FlameFlowButton(value => value.name.Contains("SkillPieceTab")));
            yield return new WaitForSecondsRealtime(.4f);
            click(FlameFlowButton(value => value.name.StartsWith("CharacterPanelSkillDetailPanelSkillPieceItem", StringComparison.Ordinal)));
            yield return new WaitForSecondsRealtime(.4f);
            var pieceDialog = FindObjectsOfType<Transform>().Single(value => value.name == "skill_piece_detail");
            yield return CaptureAcquiredActive(output, "pieces");
            var pieceTexts = pieceDialog.GetComponentsInChildren<TMP_Text>();
            Require(pieceTexts.Any(value => value.name == "TypeText" && value.text == "\u6218\u6280\u788e\u7247")
                && pieceTexts.Any(value => value.name == "QualityText" && value.text == pieceQuality)
                && pieceTexts.Any(value => value.name == "SkillEffectDescText" && !string.IsNullOrWhiteSpace(value.text))
                && !pieceTexts.Any(value => value.text.Contains("{0}") || value.name == "DetailText" || value.name == "FuncDetailText"),
                "The fragment detail still displays prefab placeholders or incorrect item metadata.");
            var mergeSlider = pieceDialog.GetComponentsInChildren<Slider>().Single();
            long suppliedPieces = progression.Player.Inventory.Where(value => value.ItemId == definition.PieceItemId).Sum(value => value.Count);
            Require(mergeSlider.maxValue == mergeCount && mergeSlider.value == mergeCount
                && pieceTexts.Any(value => value.name == "text" && value.text.Contains(suppliedPieces.ToString())
                    && value.text.Contains("/" + mergeCount)), "The native fragment progress did not bind owned and required counts.");
            click(FlameFlowButton(value => value.name == "CommonBtn_12_Merge"));
            yield return new WaitForSecondsRealtime(.4f);
            var mergeDialog = FindObjectsOfType<Transform>().Single(value => value.name == "skill_merge_confirm");
            yield return CaptureAcquiredActive(output, "merge-confirm");
            Require(mergeDialog.GetComponentsInChildren<Image>().Count(value => value.name == "icon") == 2
                && mergeDialog.GetComponentsInChildren<TMP_Text>().Count(value => value.name == "BottomText" && value.text.StartsWith(skillName, StringComparison.Ordinal)) == 2
                && mergeDialog.GetComponentsInChildren<TMP_Text>().Any(value => value.name == "LeftTxt" && value.text == suppliedPieces.ToString())
                && mergeDialog.GetComponentsInChildren<TMP_Text>().Any(value => value.name == "RightTxt" && value.text == mergeCount.ToString())
                && !mergeDialog.GetComponentsInChildren<TMP_Text>().Any(value => value.text.Contains("{0}")),
                "The native merge icons contain duplicate children, placeholder names or incorrect material counts.");
            foreach (string iconName in new[] { "ItemIconSkill", "ItemIconSkillPiece" })
            {
                click(mergeDialog.GetComponentsInChildren<Button>().Single(value => value.name == iconName));
                yield return new WaitForSecondsRealtime(.4f);
                string dialogName = iconName == "ItemIconSkill" ? "skill_panel_detail" : "skill_piece_detail";
                var popup = FindObjectsOfType<Transform>().Single(value => value.name == dialogName);
                yield return CaptureAcquiredActive(output, iconName == "ItemIconSkill" ? "merge-skill-detail" : "merge-piece-detail");
                // Skill detail closes through the original popup mask; piece detail has its own button.
                var close = popup.GetComponentsInChildren<Button>().SingleOrDefault(value => value.name == "CloseBtn");
                if (close == null)
                {
                    yield return new WaitForSecondsRealtime(.6f);
                    close = FindObjectsOfType<Button>().Where(value => value.name == "Mask"
                        || value.name == "BlurMask" || value.name == "invisableMask")
                        .OrderByDescending(value => value.GetComponent<Canvas>().sortingOrder).First();
                }
                click(close);
                yield return new WaitForSecondsRealtime(.3f);
                Require(mergeDialog && mergeDialog.gameObject.activeInHierarchy,
                    "The original icon detail closed its parent merge confirmation.");
            }
            click(FlameFlowButton(value => value.name == "MergeBtn"));
            yield return new WaitForSecondsRealtime(.5f);
            Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId) == initialRank
                && progression.Player.Inventory.Where(value => value.ItemId == definition.PieceItemId).Sum(value => value.Count)
                    == rankCost.Items
                        .Where(value => value.ItemId == definition.PieceItemId).Sum(value => value.Count),
                "Original merge did not grant one skill at its exact fragment cost.");
            click(FlameFlowButton(value => value.name.Contains("ActiveSkillTab")));
            yield return new WaitForSecondsRealtime(.4f);
            click(FlameFlowButton(value => value.name == "SkillListItem_" + skillId));
            yield return new WaitForSecondsRealtime(.5f);
            yield return CaptureAcquiredActive(output, "acquired-detail");
            click(FlameFlowButton(value => value.name == "EquipBtn"));
            yield return new WaitForSecondsRealtime(.4f);
            var group = FindObjectsOfType<RectTransform>().Single(value =>
                value.name == "ActiveSkill" && value.parent.name == "SkillContentPanel");
            click(group.GetChild(0).Find("Slot").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.4f);
            Require(progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].ActiveSkills[0] == skillId,
                "Original slot input did not equip the newly merged skill.");
            yield return CaptureAcquiredActive(output, "equipped");
            click(group.GetChild(0).Find("Slot").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.4f);
            click(FlameFlowButton(value => value.name == "UpgradeBtn"));
            yield return new WaitForSecondsRealtime(.5f);
            click(FlameFlowButton(value => value.name == "SkillPromotePanelLevelUpTabGroupItem_11"));
            yield return new WaitForSecondsRealtime(.3f);
            click(FlameFlowButton(value => value.name == "UseItemBtn" && value.GetComponentsInParent<Transform>()
                .Any(parent => parent.name == "LevelUpContent")));
            yield return new WaitForSecondsRealtime(.7f);
            Require(progression.SkillProgression.SlotLevel(progression.Player.Skills, 100, 0,
                progression.SkillContext().CurrentSeasonId) == 2, "Original slot cultivation did not apply.");
            yield return CaptureAcquiredActive(output, "level-up");
            click(FlameFlowButton(value => value.name == "SkillPromotePanelRankUpTabGroupItem_11"));
            yield return new WaitForSecondsRealtime(.3f);
            click(FlameFlowButton(value => value.name == "UseItemBtn" && value.GetComponentsInParent<Transform>()
                .Any(parent => parent.name == "RankUpContent")));
            yield return new WaitForSecondsRealtime(.7f);
            Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId) == cultivatedRank,
                "Original rank cultivation did not apply.");
            yield return CaptureAcquiredActive(output, "rank-up");
            click(FlameFlowButton(value => value.name == "CloseBtnDark" && value.GetComponentsInParent<Transform>()
                .Any(parent => parent.name == "skill_promote")));
            yield return new WaitForSecondsRealtime(.4f);
            Require(!FindObjectsOfType<Transform>().Any(value => value.name == "skill_promote"),
                "Original cultivation close input did not dismiss its dialog.");
            character.ReloadProfile();
            yield return new WaitForSecondsRealtime(.5f);
            progression = character.Progression;
            var disk = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot)));
            Require(JsonUtility.ToJson(disk.Player.Skills) == JsonUtility.ToJson(progression.Player.Skills)
                && OfflineSkillProgression.OwnedRank(disk.Player.Skills, skillId) == cultivatedRank
                && disk.Player.Skills.ActiveSlotLevels[0] == 2,
                "Original acquisition, equipment or cultivation changed during disk reload.");
            yield return CaptureAcquiredActive(output, "reloaded");
            Debug.Log("[Acquired active UI] " + skillId + " fragment acquisition, original slot input, level/rank cultivation and disk reload passed.");
            string expectedSkills = JsonUtility.ToJson(disk.Player.Skills);
            long[] inventory = disk.Player.Inventory.OrderBy(value => value.ItemId)
                .SelectMany(value => new[] { (long)value.ItemId, value.Count }).ToArray();
            click(FlameFlowButton(value => value.name == "StageDamageTestBtn"));
            yield return new WaitForSecondsRealtime(.5f);
            var settings = FindObjectsOfType<Transform>().Single(value => value.name == "damage_test");
            foreach (string setting in new[] { "Round", "MonsterHp", "MonsterHurt" })
            {
                var choice = settings.GetComponentsInChildren<Toggle>().Single(value =>
                    value.transform.parent.name == "ToggleText" && value.GetComponentsInParent<Transform>()
                        .Any(parent => parent.name == setting));
                if (setting == "MonsterHp" && waterBlade)
                    choice = settings.GetComponentsInChildren<Toggle>().Single(value => value != choice
                        && value.GetComponentsInParent<Transform>().Any(parent => parent.name == setting));
                if (!choice.isOn) click(choice);
            }
            yield return CaptureAcquiredActive(output, "settings");
            click(settings.GetComponentsInChildren<Button>().Single(value => value.name == "StartTest"));
            yield return new WaitForSecondsRealtime(1f);
            var battle = home.GetComponent<SkillDamageTestBattleRunner>();
            Require(battle && battle.Controller && home.Hud.AnyModalOpen, "Original skill-page damage test did not mount its battle.");
            var checkpoint = battle.Controller.Checkpoint;
            var actor = checkpoint.Units.Single(value => value.Id == 1);
            var active = actor.Skills.Single(value => value.Slot == 0);
            Require(active.ItemId == skillId && active.Rank == cultivatedRank && active.Level == 2
                && active.SubRank == progression.Player.SubRank && actor.Profession == profession,
                "Production battle assembly lost the actual saved skill/profession/cultivation.");
            if (skillId == 10049) yield return MoveChallengeIntoRange(battle.Controller);
            if (skillId == 10066)
            {
                var combat = OfficialCombatAssetCatalog.LoadDefault().Load();
                actor.Statuses.Add(combat.CreateStatus(24040, 1, 1, 724040));
                actor.Statuses.Add(combat.CreateStatus(24041, 1, 1, 724041));
            }
            int casts = 0, hits = 0;
            battle.Controller.StepCompleted += step => {
                var skillCasts = step.Event.SkillCasts.Where(value => value.ActorId == 1 && value.Slot == 0
                    && value.SkillItemId == skillId).ToArray();
                casts += skillCasts.Length;
                if (lightSwordArray)
                    foreach (var counter in step.Event.SkillCasts.Where(value => value.ActorId == actor.Id
                        && value.SkillEntityId == 12000 && value.SkillItemId == skillId))
                    {
                        var counterHits = step.Event.DamageEvents.Where(value => value.SourceId == actor.Id
                            && value.SkillEntityId == 12000).ToArray();
                        Require(counter.Slot < 0 && counterHits.Length > 0 && counterHits.All(value =>
                            value.TargetId != actor.Id && value.HitEntityId == 12001 && value.DamageEntityId == 10026
                            && value.ElementType == 103 && value.DamageType == 1 && value.Damage < 0
                            && !value.TrueDamage && !value.IsCure), "The original light sword counter lost its owner or light damage.");
                        hits += counterHits.Length;
                    }
                foreach (var cast in skillCasts)
                {
                    File.WriteAllText(Path.Combine(output, "acquired-active-ui-latest.json"), FightBattleCheckpointSerializer.Save(checkpoint));
                    Require(skillId == 10000 ? cast.SkillEntityId == 10500
                        : skillId == 10008 ? cast.SkillEntityId == 10600
                        : skillId == 10009 ? cast.SkillEntityId == 10610
                        : skillId == 10010 ? cast.SkillEntityId == 10620
                        : skillId == 10011 ? cast.SkillEntityId == 10630
                        : skillId == 10012 ? cast.SkillEntityId == 10640
                        : skillId == 10013 ? cast.SkillEntityId == 10650
                        : skillId == 10014 ? cast.SkillEntityId == 10660
                        : skillId == 10015 ? cast.SkillEntityId == 10670
                        : skillId == 10016 ? cast.SkillEntityId == 10680
                        : skillId == 10017 ? cast.SkillEntityId == 10690
                        : skillId == 10018 ? cast.SkillEntityId == 10700
                        : skillId == 10024 ? cast.SkillEntityId == 10760
                        : skillId == 10028 ? cast.SkillEntityId == 10800
                        : crisisGuard ? cast.SkillEntityId == 11170
                        : skillId == 10035 ? cast.SkillEntityId == 10900
                        : starShatteringSlash ? cast.SkillEntityId == 10940
                        : skillId == 10043 ? cast.SkillEntityId == 10590
                        : skillId == 10044 ? cast.SkillEntityId == 10750
                        : skillId == 10046 ? cast.SkillEntityId == 11160
                        : surgingTide ? cast.SkillEntityId == 10880
                        : waterBlade ? cast.SkillEntityId == 11980 || cast.SkillEntityId == 11983 || cast.SkillEntityId == 11984
                        : lightSwordArray ? cast.SkillEntityId == 11990
                        : skillId == 10048 ? cast.SkillEntityId == 11090
                        : skillId == 10049 ? cast.SkillEntityId == 11100
                        : skillId == 10051 ? cast.SkillEntityId == 11260
                        : skillId == 10053 ? cast.SkillEntityId == 11250
                        : skillId == 10066 ? cast.SkillEntityId == 11550
                        : shieldCharge ? cast.SkillEntityId == 11730
                        : holyPurification ? cast.SkillEntityId == 11860
                        : cast.SkillEntityId == 10780 || cast.SkillEntityId == 11640 || cast.SkillEntityId == 11650 || cast.SkillEntityId == 11660,
                        "The acquired active skill selected a foreign entity: " + cast.SkillEntityId);
                    var damageEvents = step.Event.DamageEvents.Where(value => value.SourceId == 1
                        && value.SkillEntityId == (skillId == 10017 ? 10970 : cast.SkillEntityId)).ToArray();
                    if (lightSwordArray)
                    {
                        Require((casts != 1 || step.Event.ActorRound == 0)
                            && actor.Statuses.Any(value => value.ClassId == 24378 && value.CreatorId == actor.Id
                                && value.StackCount == 4 && value.MaxStackedCount == 4 && !value.CanDispersed),
                            "The acquired light sword array did not start with four original swords.");
                        continue;
                    }
                    if (skillId == 10049 && casts == 1)
                        Require(step.Event.ActorRound == 0, "The first challenge did not occur before the actor's first turn.");
                    if (statusSkill)
                    {
                        bool spirit = skillId == 10051;
                        bool guard = skillId == 10046;
                        bool encouragement = skillId == 10066;
                        int statusId = crisisGuard ? 24180 : spirit ? 24214 : guard ? 24170 : encouragement ? 24237 : 24030;
                        int skillEntityId = cast.SkillEntityId;
                        float statusDelay = crisisGuard ? 1.2f : spirit ? .25f : guard ? .44f : .2f;
                        var started = step.Event.StatusViewEvents.Where(value => value.Kind == FightBattleStatusViewEventKind.Start
                            && value.OwnerId == 1 && value.Status.ClassId == statusId && value.Status.CreatorId == 1
                            && (encouragement || Math.Abs(value.Delay - cast.Delay - statusDelay) < .0001f)).ToArray();
                        Require((started.Length == 1 || (crisisGuard || guard || encouragement) && started.Length == 0) && damageEvents.Length == 1
                            && damageEvents[0].TargetId == 1 && damageEvents[0].HitEntityId == skillEntityId + 1
                            && damageEvents[0].DamageEntityId == skillEntityId + 2 && damageEvents[0].Damage == 0,
                            "The acquired skill lost its original self-status or timing.");
                        var status = started.Length == 1
                            ? actor.Statuses.Single(value => value.InstanceId == started[0].Status.InstanceId)
                            : actor.Statuses.Single(value => value.ClassId == statusId && value.CreatorId == 1);
                        Require(status.ApplyingSkillItemId == skillId && status.RootSkillId == skillEntityId
                            && status.SourceSkillRank == cultivatedRank && status.SourceSkillLevel == 2
                            && status.DurationRound == (spirit ? 4 : guard || encouragement ? 2 : 3)
                            && (crisisGuard || guard ? status.ShieldKind != 0 && status.ShieldValue > 0 : status.ShieldKind == 0),
                            "The acquired skill lost its saved cultivation or original property status.");
                        if (encouragement && casts == 1)
                            Require(step.Event.ActorRound == 0
                                && actor.Statuses.Count(value => value.ClassId == 24040 || value.ClassId == 24041) == 1
                                && step.Event.DispersedUnitIds.Count(value => value == actor.Id) == 1,
                                "The opening encouragement did not remove exactly one negative effect.");
                        hits++;
                    }
                    if (skillId == 10011)
                    {
                        float[] delays = { .12f, .45f, .72f };
                        foreach (var target in damageEvents.GroupBy(value => value.TargetId))
                        {
                            var ordered = target.OrderBy(value => value.Delay).ToArray();
                            Require(ordered.Length == delays.Length, "Lion combo lost a damage occurrence.");
                            for (int i = 0; i < ordered.Length; i++)
                                Require(ordered[i].HitEntityId == 10631 + i
                                    && Math.Abs(ordered[i].Delay - cast.Delay - delays[i]) < .0001f,
                                    "Lion combo changed its original hit sequence or timing.");
                        }
                    }
                    if (skillId == 10053)
                    {
                        float[] delays = { .726f, .861f, .987f, 1.14f, 1.39f };
                        foreach (var target in damageEvents.GroupBy(value => value.TargetId))
                        {
                            var ordered = target.OrderBy(value => value.Delay).ToArray();
                            Require(ordered.Length == delays.Length, "Sunset sword lost a damage occurrence.");
                            for (int i = 0; i < ordered.Length; i++)
                                Require(Math.Abs(ordered[i].Delay - cast.Delay - delays[i]) < .0001f,
                                    "Sunset sword changed its original hit timing.");
                        }
                    }
                    if (starShatteringSlash)
                    {
                        Require(damageEvents.Length == 1, "Star-shattering slash must hit exactly one target.");
                        var strike = damageEvents[0];
                        Require(strike.TargetId != 1 && strike.HitEntityId == 10941 && strike.DamageEntityId == 10015
                            && strike.ElementType == 0 && strike.DamageType == 1 && strike.Damage < 0
                            && !strike.TrueDamage && !strike.IsCure && Math.Abs(strike.Delay - cast.Delay - 1.35f) < .0001f,
                            "The acquired star-shattering slash changed its original physical hit.");
                        hits++;
                        continue;
                    }
                    if (shieldCharge)
                    {
                        var charge = step.Event.Movements.Single(value => value.UnitId == actor.Id
                            && value.MoveBehaviourId == 11732);
                        Require(charge.Path.Count == 6 && Math.Abs(charge.Delay - cast.Delay - .82f) < .0001f
                            && damageEvents.Length > 0 && damageEvents.All(value => value.TargetId != actor.Id
                                && value.HitEntityId == 11731 && value.DamageEntityId == 11733 && value.DamageType == 1
                                && value.ElementType == 0 && value.Damage < 0 && !value.TrueDamage && !value.IsCure),
                            "The acquired shield charge lost its original path or physical damage.");
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (holyPurification)
                    {
                        Require(damageEvents.Length > 0 && damageEvents.All(value => value.TargetId != actor.Id
                            && value.HitEntityId == 11861 && value.DamageEntityId == 11862 && value.DamageType == 1
                            && value.ElementType == 103 && value.Damage < 0 && value.TrueDamage && !value.IsCure
                            && Math.Abs(value.Delay - cast.Delay - .4f) < .0001f),
                            "The acquired purification lost its original light destruction hit.");
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (surgingTide)
                    {
                        Require(damageEvents.Length > 0 && damageEvents.All(value => value.TargetId != actor.Id
                            && value.HitEntityId == 10881 && value.DamageEntityId == 10024 && value.DamageType == 1
                            && value.ElementType == 101 && value.Damage < 0 && !value.TrueDamage && !value.IsCure
                            && Math.Abs(value.Delay - cast.Delay - .3f) < .0001f),
                            "The acquired surging tide lost its original water area hit.");
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (waterBlade)
                    {
                        Require(damageEvents.Length == 1, "Water blade must hit one enemy.");
                        var hit = damageEvents[0];
                        float delay = cast.SkillEntityId == 11983 ? .57f : cast.SkillEntityId == 11984 ? .72f : .55f;
                        long shieldValue = Math.Min((long)((hit.BeforeHp - hit.AfterHp) * (double).2f), actor.MaxHp / 2);
                        Require(hit.TargetId != actor.Id && hit.HitEntityId == 11981 && hit.DamageEntityId == 11982
                            && hit.DamageType == 1 && hit.ElementType == 101 && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure
                            && Math.Abs(hit.Delay - cast.Delay - delay) < .0001f && shieldValue > 0
                            && actor.Statuses.Any(value => value.ClassId == 24377 && value.CreatorId == actor.Id
                                && value.DurationRound == 2 && value.ShieldRemainValue == shieldValue),
                            "The acquired water blade lost its water damage or capped HP-only shield conversion.");
                        hits++;
                        continue;
                    }
                    foreach (var hit in damageEvents)
                    {
                        if (statusSkill) continue;
                        int damage = skillId == 10017 ? 10972 : skillId == 10035 ? 10902 : skillId == 10000 ? 10502 : skillId == 10009 ? 10612 : skillId == 10010 ? 10622 : skillId == 10012 ? 10642 : skillId == 10014 ? (hit.HitEntityId == 10661 ? 10022 : 10663) : skillId == 10015 ? 10673 : skillId == 10016 ? 10026 : skillId == 10018 ? 10702 : skillId == 10043 ? 10592 : skillId == 10044 ? 10752 : skillId == 10048 ? 11099 : skillId == 10049 ? 11102 : skillId == 10008 || skillId == 10011 || skillId == 10013 || skillId == 10053 ? 10022
                            : cast.SkillEntityId == 11660 ? 10007 : 10023;
                        Require(hit.TargetId != 1 && (skillId == 10011 ? hit.HitEntityId >= 10631 && hit.HitEntityId <= 10633
                                : skillId == 10014 ? hit.HitEntityId == 10661 || hit.HitEntityId == 10662
                                : skillId == 10048 ? hit.HitEntityId >= 11091 && hit.HitEntityId <= 11094
                                : skillId == 10017 ? hit.HitEntityId == 10971 : hit.HitEntityId == cast.SkillEntityId + 1)
                            && hit.DamageEntityId == damage && hit.Damage < 0 && hit.TrueDamage == (skillId != 10000 && skillId != 10008 && skillId != 10009 && skillId != 10010 && skillId != 10011 && skillId != 10012 && skillId != 10013 && skillId != 10014 && skillId != 10015 && skillId != 10017 && skillId != 10018 && skillId != 10028 && skillId != 10035 && skillId != 10043 && skillId != 10044 && skillId != 10048 && skillId != 10049 && skillId != 10053)
                            && hit.ElementType == (skillId == 10026 || skillId == 10028 ? 102 : skillId == 10000 || skillId == 10017 || skillId == 10035 || skillId == 10044 ? 101 : skillId == 10012 || skillId == 10018 || skillId == 10043 ? 100 : 0),
                            "The acquired active skill lost its original caster-owned damage.");
                        hits++;
                    }
                }
            };
            yield return CaptureAcquiredActive(output, "preparation");
            click(FlameFlowButton(value => value.name == "Btn" && value.GetComponentsInParent<Transform>()
                .Any(parent => parent.name == "StartBtn")));
            bool captured = false, capturedOpening = false;
            while (battle != null)
            {
                if (lightSwordArray && !capturedOpening && casts > 0 && hits == 0)
                {
                    yield return new WaitForSecondsRealtime(1.05f);
                    Require(FindObjectsOfType<Transform>().Count(value => value.name.StartsWith(
                        "Eff_Skill_Paladin04_SwordArray03", StringComparison.Ordinal)
                        && value.GetComponentsInParent<Zjcs.Official.BattleStatusPlayables.MyPlayableOrbitMovement>().Length > 0) == 4,
                        "The original status prefab did not render all four opening orbiters.");
                    yield return CaptureAcquiredActive(output, "opening"); capturedOpening = true;
                }
                if (!captured && hits > 0)
                {
                    if (skillId == 10028)
                    {
                        bool explosionVisible = FindObjectsOfType<ParticleSystem>().Any(particle =>
                            particle.name == "fire" && particle.particleCount > 0
                            && particle.GetComponentsInParent<Transform>().Any(parent =>
                                parent.name.StartsWith("playable10801", StringComparison.Ordinal)));
                        if (!explosionVisible) { yield return null; continue; }
                    }
                    else yield return new WaitForSecondsRealtime(crisisGuard || starShatteringSlash ? 1.45f
                        : shieldCharge ? 1.1f : .7f);
                    yield return CaptureAcquiredActive(output, "battle"); captured = true;
                }
                yield return null;
            }
            Require(checkpoint.Finished && casts > 0 && hits > 0 && captured,
                "The acquired/cultivated active skill did not finish its damage battle.");
            Require(!lightSwordArray || capturedOpening, "The four original opening swords were not captured.");
            File.WriteAllText(Path.Combine(output, "acquired-active-ui-checkpoint.json"), FightBattleCheckpointSerializer.Save(checkpoint));
            yield return new WaitForSecondsRealtime(.8f);
            yield return CaptureAcquiredActive(output, "result");
            var result = FindObjectsOfType<Transform>().Single(value => value.name == "damage_test_result");
            var report = result.GetComponent<OfficialBattleResultReport>();
            Require(report != null && report.FinalReport != null && (statusSkill || report.FinalReport.TotalDamageDealt > 0),
                "Original damage-test result omitted actual active-skill damage.");
            click(result.GetComponentsInChildren<Button>().Single(value => value.name == "Setting"));
            yield return new WaitForSecondsRealtime(.5f);
            settings = FindObjectsOfType<Transform>().Single(value => value.name == "damage_test");
            click(settings.GetComponentsInChildren<Button>().Single(value => value.name == "CloseBtn"));
            yield return new WaitForSecondsRealtime(.4f);
            Require(home.Hud.CharacterPageOpen, "Original damage-test return did not restore its character page.");
            disk = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot)));
            Require(JsonUtility.ToJson(disk.Player.Skills) == expectedSkills && inventory.SequenceEqual(disk.Player.Inventory
                .OrderBy(value => value.ItemId).SelectMany(value => new[] { (long)value.ItemId, value.Count })),
                "Damage test changed the persisted cultivation, equipped plan or material counts.");
            yield return CaptureAcquiredActive(output, "return");
            Debug.Log("[Acquired active UI] " + skillId + " saved rank=" + cultivatedRank + " level=2, original "
                + (statusSkill ? "status" : "damage") + "/result/return and unchanged save passed.");
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result { passed = true }, true));
            Destroy(home.gameObject);
            yield return null;
        }

        private static IEnumerator CaptureAcquiredActive(string output, string phase)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "acquired-active-ui-" + phase + ".png"));
            yield return null;
        }

        private static IEnumerator MoveChallengeIntoRange(OfficialBattleRuntimeController controller)
        {
            var checkpoint = controller.Checkpoint;
            var actor = checkpoint.Units.Single(value => value.Id == 1);
            var enemy = checkpoint.Units.First(value => value.Side != actor.Side && value.Hp > 0);
            int distance = Math.Abs(actor.X - enemy.X) + Math.Abs(actor.Y - enemy.Y);
            if (distance <= 3) yield break;
            var target = actor.X != enemy.X
                ? new FightGridPoint(actor.X + Math.Sign(enemy.X - actor.X), actor.Y)
                : new FightGridPoint(actor.X, actor.Y + Math.Sign(enemy.Y - actor.Y));
            var pointer = FindObjectsOfType<BoxCollider>().FirstOrDefault(value => value.transform.parent != null
                && value.transform.parent.name.StartsWith("PrepareBattlePlayerPointer", StringComparison.Ordinal));
            Require(pointer != null && Camera.main != null, "The original preparation drag surface is missing.");
            var mapper = new OfficialBattleGridMapper(pointer.transform.parent.parent, checkpoint.Grid);
            var frame = new OfficialBattlePointerFrame {
                Position = Camera.main.WorldToScreenPoint(pointer.transform.TransformPoint(pointer.center)), Down = true, Held = true };
            controller.PreparationPointerProvider = () => frame;
            try
            {
                yield return null;
                frame.Down = false;
                frame.Position = Camera.main.WorldToScreenPoint(mapper.GridToWorld(target));
                yield return new WaitForSecondsRealtime(.15f);
                frame.Held = false; frame.Up = true;
                yield return null;
                frame.Up = false;
                yield return null;
                Require(actor.X == target.X && actor.Y == target.Y
                    && Math.Abs(actor.X - enemy.X) + Math.Abs(actor.Y - enemy.Y) <= 3,
                    "The original preparation drag did not put the enemy inside challenge range.");
            }
            finally { controller.PreparationPointerProvider = null; }
        }
    }
}

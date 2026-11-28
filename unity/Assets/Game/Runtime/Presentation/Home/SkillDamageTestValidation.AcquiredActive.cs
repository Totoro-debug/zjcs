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
            string sequence = Environment.GetEnvironmentVariable("ZJCS_SKILLS_ACQUIRED_ACTIVE_SEQUENCE");
            if (string.IsNullOrWhiteSpace(sequence))
            {
                yield return ExerciseAcquiredActiveCore(slot, root, output, skillId);
                yield break;
            }
            int[] skills = sequence.Split(',').Select(value => int.Parse(value.Trim())).ToArray();
            Require(skills.Length > 0 && skills.Length <= 32 && skills[0] == skillId
                && skills.Distinct().Count() == skills.Length, "Invalid acquired-active diagnostic sequence.");
            var runner = FindObjectsOfType<SkillDamageTestValidation>().Single();
            runner.deadline += 600f * (skills.Length - 1);
            var slots = new ProfileSlotCatalog(root);
            foreach (int nextSkill in skills)
            {
                string directory = Path.Combine(output, "skill-" + nextSkill);
                Directory.CreateDirectory(directory);
                yield return ExerciseAcquiredActiveCore(slots.CreateSlot(), root, directory, nextSkill);
            }
        }

        private static IEnumerator ExerciseAcquiredActiveCore(string slot, string root, string output, int skillId)
        {
            AudioListener.volume = 0f;
            Action<Selectable> click = Click;
            bool crisisGuard = skillId == 10032;
            bool starShatteringSlash = skillId == 10039;
            bool eclipseSlashes = skillId == 10065;
            bool asuraHand = skillId == 10063;
            bool extinction = skillId == 10069;
            bool hunt = skillId == 10074;
            bool swiftWind = skillId == 10086;
            bool shieldCharge = skillId == 10073;
            bool holyPurification = skillId == 10079;
            bool surgingTide = skillId == 10047;
            bool waterBlade = skillId == 10082;
            int skillSkinId = int.Parse(Environment.GetEnvironmentVariable("ZJCS_SKILLS_ACQUIRED_SKIN") ?? "0");
            bool frostSong = waterBlade && skillSkinId == 39521;
            bool flowingFire = skillId == 10100;
            bool fireSkin = flowingFire && skillSkinId == 39523;
            int flowingFireExtras = 0;
            Require(skillSkinId == 0 || frostSong || fireSkin, "Unsupported original acquired-skill skin diagnostic.");
            bool lightSwordArray = skillId == 10083;
            bool diversionHarassment = skillId == 10084;
            bool heavenlyLightBlades = skillId == 10085;
            bool vortexSurge = skillId == 10099;
            bool fireLeap = skillId == 10101;
            bool hellfire = skillId == 10068;
            bool swordWindDance = skillId == 10088;
            bool soulPiercer = skillId == 10089;
            bool dreadShadow = skillId == 10104;
            bool starBurst = skillId == 10002;
            bool doomTorrent = skillId == 10056;
            bool spiralWindBlade = skillId == 10091;
            bool battlecry = skillId == 10092;
            bool judgmentThunder = skillId == 10093;
            bool warlockExplosion = skillId == 10098;
            bool warlockThunder = skillId == 10037;
            bool warlockWind = skillId == 10057;
            bool warlockWater = skillId == 10062;
            bool warlockMeteor = skillId == 10070;
            bool protectiveRunes = skillId == 10102;
            bool chaosRune = skillId == 10059;
            bool rockSummon = skillId == 10095;
            bool voodooDoll = skillId == 10097;
            bool heavenlyRain = skillId == 10096;
            bool psychicWard = skillId == 10077;
            bool sageDarkBlast = skillId == 10103;
            bool abyssalHand = skillId == 10040;
            int fighterCompanion = FighterActiveCompanion(skillId);
            int destroyerCompanion = DestroyerActiveCompanion(skillId);
            bool flameSoul = destroyerCompanion == 10599;
            bool fireCore = destroyerCompanion == 10503;
            bool iceCrystal = destroyerCompanion == 10557;
            bool overloadProtection = destroyerCompanion == 10573;
            bool alienation = HasAlienationCompanion(skillId);
            bool destroyer = starBurst || doomTorrent || spiralWindBlade || battlecry || judgmentThunder || protectiveRunes || destroyerCompanion > 0;
            bool fourthPromotion = surgingTide || waterBlade || lightSwordArray || diversionHarassment || heavenlyLightBlades || vortexSurge || hellfire || swordWindDance || soulPiercer || dreadShadow || destroyer || alienation || chaosRune || rockSummon || voodooDoll || heavenlyRain || sageDarkBlast || psychicWard;
            fourthPromotion |= fireLeap || flowingFire;
            bool statusSkill = swiftWind || asuraHand || crisisGuard || skillId == 10024 || skillId == 10046 || skillId == 10051 || skillId == 10066 || hellfire || battlecry || protectiveRunes || psychicWard;
            bool thirdPromotion = abyssalHand || fighterCompanion > 0 || hunt || swiftWind || extinction || asuraHand || eclipseSlashes || crisisGuard || starShatteringSlash || shieldCharge || holyPurification || skillId == 10017 || warlockExplosion || warlockThunder || warlockWind || warlockWater || warlockMeteor;
            Require(thirdPromotion || fourthPromotion || skillId == 10000 || skillId == 10008 || skillId == 10009 || skillId == 10010 || skillId == 10011 || skillId == 10012 || skillId == 10013 || skillId == 10014 || skillId == 10015 || skillId == 10016 || skillId == 10018 || skillId == 10024 || skillId == 10026 || skillId == 10028 || skillId == 10035 || skillId == 10043 || skillId == 10044 || skillId == 10046 || skillId == 10048 || skillId == 10049 || skillId == 10051 || skillId == 10053 || skillId == 10066,
                "Select an implemented active-skill acquisition diagnostic.");
            fourthPromotion |= HasGuardianCompanion(skillId);
            int profession = abyssalHand ? 16 : fighterCompanion > 0 || hunt || swiftWind || extinction || asuraHand || eclipseSlashes ? 7 : warlockExplosion || warlockThunder || warlockWind || warlockWater || warlockMeteor ? 12 : fireLeap || flowingFire ? 8 : alienation || chaosRune || rockSummon || voodooDoll || heavenlyRain || sageDarkBlast || psychicWard ? 17 : destroyer ? 13 : hellfire || swordWindDance || soulPiercer || dreadShadow ? 8 : fourthPromotion ? 4 : thirdPromotion ? 3 : skillId == 10000 || skillId == 10028 || skillId == 10035 ? 10 : skillId == 10010 || skillId == 10044 || skillId == 10046 || skillId == 10048 || skillId == 10049 || skillId == 10066 ? 2 : skillId == 10008 || skillId == 10009 || skillId == 10010 || skillId == 10011 || skillId == 10012 || skillId == 10013 || skillId == 10014 || skillId == 10015 || skillId == 10018 || skillId == 10024 || skillId == 10043 || skillId == 10051 ? 1 : 6;
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
            player.InitialWeaponType = profession == 10 || profession == 13 || profession == 17
                ? Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.WandInitialWeaponType : 200;
            player.Wardrobe.Profession = profession;
            if (asuraHand) PrepareAcquiredAsuraHand(player, output);
            int passiveCompanion = LoadAcquiredPassiveCompanion(player, skillId, output);
            BeginAcquiredGuardianValidation(passiveCompanion, output);
            soulSplash = passiveCompanion == 10545 ? new AcquiredSoulSplash(output) : null;
            var sageRoundSecret = BeginAcquiredSageRoundSecretValidation(passiveCompanion, output);
            if (warlockWind) PrepareAcquiredWindRoar();
            if (warlockWater) PrepareAcquiredWaterVortex();
            if (warlockMeteor) PrepareAcquiredMeteorFire();
            var sageSummonSecret = passiveCompanion == 10591 || passiveCompanion == 10600
                ? new AcquiredSageSummonSecret(passiveCompanion, output) : null;
            bool overflowShield = passiveCompanion == 10601;
            if (sageRoundSecret != null || sageSummonSecret != null || passiveCompanion == 10609 || passiveCompanion == 10594 || passiveCompanion == 10543 || starBurst || spiralWindBlade || battlecry)
            {
                var validationRunner = FindObjectsOfType<SkillDamageTestValidation>().FirstOrDefault();
                if (validationRunner != null) validationRunner.deadline += 600f;
            }
            if (destroyerCompanion == 0 && !alienation && (skillId == 10000 || skillId == 10028 || skillId == 10035)) SkillBlazingHeartValidation.VerifyOriginalWish(output, save, skillId);
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
            if (frostSong || fireSkin) Supply(skillSkinId, 1);
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
            if (frostSong || fireSkin) yield return SelectAcquiredSkillSkin(progression, skillSkinId, output);
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
            if (frostSong || fireSkin) Require(progression.EquippedSkillSkinId(skillId) == skillSkinId
                && disk.Player.Wardrobe.SkillSkins.Any(value => value.SkillId == skillId && value.SkillSkinId == skillSkinId),
                "The original skill skin changed after disk reload.");
            Debug.Log("[Acquired active UI] " + skillId + " fragment acquisition, original slot input, level/rank cultivation and disk reload passed.");
            string expectedSkills = JsonUtility.ToJson(disk.Player.Skills);
            long[] inventory = disk.Player.Inventory.OrderBy(value => value.ItemId)
                .SelectMany(value => new[] { (long)value.ItemId, value.Count }).ToArray();
            click(FlameFlowButton(value => value.name == "StageDamageTestBtn"));
            yield return new WaitForSecondsRealtime(.5f);
            var settings = FindObjectsOfType<Transform>().Single(value => value.name == "damage_test");
            if (abyssalHand || passiveCompanion == 10609 || warlockWater || warlockMeteor || passiveCompanion == 10545)
            {
                int formation = passiveCompanion == 10545 || abyssalHand ? 2 : warlockWater || warlockMeteor ? 4 : 3;
                var dropdown = settings.GetComponentsInChildren<Zjcs.Official.WorldUI.WorldUIDropdown>().Single();
                Require(dropdown.ItemsKey.Count > formation, "The original group formation is missing.");
                click(dropdown.DropdownButton);
                yield return new WaitForSecondsRealtime(.4f);
                var row = FindObjectsOfType<Zjcs.Official.WorldUI.WorldDropdownItem>().Single(value =>
                    value.Texts.Any(text => text.GetComponent<TMP_Text>().text == dropdown.ItemsKey[formation]));
                click(row.Button);
                yield return new WaitForSecondsRealtime(.4f);
                Require(dropdown.SelectedItemIndex == formation, "Original formation input did not select the requested monster group.");
                yield return CaptureAcquiredActive(output, passiveCompanion == 10545 ? "splash-formation" : warlockWater ? "vortex-formation" : warlockMeteor ? "meteor-formation" : "tactical-formation");
            }
            foreach (string setting in new[] { "Round", "MonsterHp", "MonsterHurt" })
            {
                var choice = settings.GetComponentsInChildren<Toggle>().Single(value =>
                    value.transform.parent.name == "ToggleText" && value.GetComponentsInParent<Transform>()
                        .Any(parent => parent.name == setting));
                if (setting == "MonsterHp" && (waterBlade || soulPiercer || chaosRune || rockSummon || sageDarkBlast || passiveCompanion == 10545))
                    choice = settings.GetComponentsInChildren<Toggle>().Single(value => value != choice
                        && value.GetComponentsInParent<Transform>().Any(parent => parent.name == setting));
                if (setting == "MonsterHurt" && (overloadProtection || psychicWard || passiveCompanion == 10580 || passiveCompanion == 10600 || passiveCompanion == 10590))
                    choice = settings.GetComponentsInChildren<Toggle>().Single(value => value != choice
                        && value.GetComponentsInParent<Transform>().Any(parent => parent.name == setting));
                if (setting == "Round" && (passiveCompanion == 10611 || vortexSurge || passiveCompanion == 10607 || passiveCompanion == 10594 || passiveCompanion == 10543 || dreadShadow || starBurst || spiralWindBlade || battlecry))
                    choice = settings.GetComponentsInChildren<Toggle>().Where(value =>
                        value.GetComponentsInParent<Transform>().Any(parent => parent.name == setting))
                        .OrderBy(value => value.transform.parent.GetSiblingIndex()).Last();
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
            if (passiveCompanion > 0) VerifyAcquiredPassiveAssembly(actor, progression, passiveCompanion);
            // Make each required trigger reproducible inside the isolated presentation diagnostic.
            if (swiftWind) PrepareAcquiredSwiftWindDance(actor);
            if (hunt) PrepareAcquiredHuntJudgment(checkpoint);
            if (extinction) PrepareAcquiredExtinctionBlade(actor);
            if (fireCore) SetAcquiredBattleProperty(actor, "CritRatePercent", 100000);
            if (iceCrystal || chaosRune) SetAcquiredBattleProperty(actor, "EffectRate", 1000000);
            if (overloadProtection || psychicWard || passiveCompanion == 10580 || passiveCompanion == 10590)
            {
                Require(!checkpoint.DamageTest.EnableMonsterOneDamage,
                    "The defensive skill diagnostic requires the original normal monster damage option.");
                foreach (var enemy in checkpoint.Units.Where(unit => unit.Side != actor.Side))
                    SetAcquiredBattleProperty(enemy, "Attack", actor.MaxHp / (passiveCompanion == 10590 ? 12 : psychicWard ? 6 : 3));
            }
            Require(active.ItemId == skillId && active.Rank == cultivatedRank && active.Level == 2
                && active.SubRank == progression.Player.SubRank && actor.Profession == profession,
                "Production battle assembly lost the actual saved skill/profession/cultivation.");
            if (frostSong) Require(active.UseItemId == 10184 && active.SkillSkinId == skillSkinId && active.EntityId == 13580,
                "The production battle assembler lost the saved original frost-song variant.");
            if (flowingFire) Require(active.EntityId == (fireSkin ? 13590 : 12090)
                && active.UseItemId == (fireSkin ? 10185 : 10100) && active.SkillSkinId == skillSkinId,
                "The production battle assembler lost the saved flowing-fire appearance.");
            if (skillId == 10049) yield return MoveChallengeIntoRange(battle.Controller);
            if (skillId == 10066)
            {
                var combat = OfficialCombatAssetCatalog.LoadDefault().Load();
                actor.Statuses.Add(combat.CreateStatus(24040, 1, 1, 724040));
                actor.Statuses.Add(combat.CreateStatus(24041, 1, 1, 724041));
            }
            if (hellfire) PrepareAcquiredHellfire(actor);
            if (soulPiercer) { actor.Hp = actor.MaxHp / 2; actor.Turn.Hp = actor.Hp; }
            if (heavenlyRain)
            {
                actor.Hp = Math.Max(1L, overflowShield ? actor.MaxHp - 1L : actor.MaxHp * 2L / 5L);
                actor.Turn.Hp = actor.Hp;
            }
            if (passiveCompanion == 10597)
            {
                var listener = actor.Statuses.Single(status => status.ClassId == 25098
                    && status.ApplyingSkillItemId == 10597 && status.RoundEndEntityCount != null
                    && status.RoundEndEntityCount.Range.Count > 0);
                var offset = listener.RoundEndEntityCount.Range[0];
                checkpoint.GridItems.Add(new FightBattleGridItemState
                {
                    Id = 9059701, GridItemClassId = 3320, EntityClassId = 3051,
                    OwnerId = actor.Id, X = actor.X + offset.X, Y = actor.Y + offset.Y
                });
                actor.Hp = Math.Max(1L, actor.MaxHp - 1L);
                actor.Turn.Hp = actor.Hp;
            }
            if (passiveCompanion == 10600)
            {
                actor.Hp = Math.Max(1L, actor.MaxHp / 2); actor.Turn.Hp = actor.Hp;
                foreach (var enemy in checkpoint.Units.Where(unit => unit.Side != actor.Side))
                    SetAcquiredBattleProperty(enemy, "Attack", 1L);
            }
            int casts = 0, hits = 0, vortexMovements = 0, coldHits = 0, auraHits = 0, standfastTurns = 0, assaultHits = 0, soulBreakHits = 0, windHits = 0, flameHits = 0, fireLifeCures = 0, hellfireCures = 0, lifestealCures = 0, fearApplications = 0;
            int starBlindApplications = 0, starBlindEnds = 0;
            int psychicWardEnds = 0;
            int overflowShields = 0;
            int doomPushes = 0;
            int tearApplications = 0, tearEnds = 0, tearHits = 0;
            int battlecryAdvances = 0;
            int confusionApplications = 0, confusionEnds = 0;
            var rock = rockSummon ? new AcquiredRockSummon() : null;
            var doll = voodooDoll ? new AcquiredVoodooDoll() : null;
            if (passiveCompanion == 10590) PrepareAcquiredSoulGuardianCheckpoint(checkpoint);
            soulSplash?.Prepare(checkpoint);
            var abyssal = abyssalHand ? new AcquiredAbyssalHand(output) : null;
            abyssal?.Prepare(checkpoint);
            PrepareAcquiredAffinity(checkpoint, passiveCompanion);
            battle.Controller.StepCompleted += step => {
                abyssal?.Observe(step.Event, checkpoint);
                if (swiftWind) VerifyAcquiredSwiftWindStep(step.Event, checkpoint);
                if (asuraHand) VerifyAcquiredAsuraStep(step.Event, checkpoint, output);
                if (spiralWindBlade)
                {
                    tearEnds += step.Event.StatusViewEvents.Count(view => view.Status.ClassId == 24376
                        && view.Status.CreatorId == 1 && view.Kind == FightBattleStatusViewEventKind.End);
                    VerifyAcquiredLacerationHits(step.Event, ref tearHits);
                }
                if (starBurst) starBlindEnds += step.Event.StatusViewEvents.Count(view => view.Status.ClassId == 24234
                    && view.Status.CreatorId == 1 && view.Kind == FightBattleStatusViewEventKind.End);
                if (chaosRune) confusionEnds += step.Event.StatusViewEvents.Count(view => view.Status.ClassId == 24217
                    && view.Status.CreatorId == actor.Id && view.Kind == FightBattleStatusViewEventKind.End);
                if (psychicWard) psychicWardEnds += VerifyAcquiredSagePsychicWard(step.Event, checkpoint, actor, output);
                if (hellfire) VerifyAcquiredHellfireStep(step.Event, actor, ref hellfireCures, ref hits);
                if (overflowShield) overflowShields += VerifyAcquiredSageOverflowShield(step.Event, checkpoint, actor, output);
                else if (passiveCompanion > 0 && sageRoundSecret == null && sageSummonSecret == null) VerifyAcquiredPassiveStep(step.Event, checkpoint, output, passiveCompanion, ref coldHits, ref auraHits, ref standfastTurns, ref assaultHits, ref soulBreakHits, ref windHits, ref flameHits, ref fireLifeCures);
                sageRoundSecret?.Observe(step.Event, checkpoint);
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
                        : abyssalHand ? cast.SkillEntityId == 10950
                        : skillId == 10043 ? cast.SkillEntityId == 10590
                        : skillId == 10044 ? cast.SkillEntityId == 10750
                        : skillId == 10046 ? cast.SkillEntityId == 11160
                        : surgingTide ? cast.SkillEntityId == 10880
                        : waterBlade ? frostSong ? cast.SkillEntityId == 13580
                            : cast.SkillEntityId == 11980 || cast.SkillEntityId == 11983 || cast.SkillEntityId == 11984
                        : lightSwordArray ? cast.SkillEntityId == 11990
                        : diversionHarassment ? cast.SkillEntityId == 11890
                        : heavenlyLightBlades ? cast.SkillEntityId == 11900
                        : vortexSurge ? cast.SkillEntityId == 12080
                        : flowingFire ? cast.SkillEntityId == (fireSkin ? 13590 : 12090)
                        : fireLeap ? cast.SkillEntityId == 12100
                        : hellfire ? cast.SkillEntityId == 11520
                        : swordWindDance ? cast.SkillEntityId == 11910
                        : soulPiercer ? cast.SkillEntityId == 11920
                        : dreadShadow ? cast.SkillEntityId == 12130
                        : starBurst ? cast.SkillEntityId == 11450
                        : doomTorrent ? cast.SkillEntityId == 11080
                        : spiralWindBlade ? cast.SkillEntityId == 11960
                        : battlecry ? cast.SkillEntityId == 12010
                        : judgmentThunder ? cast.SkillEntityId == 11970
                        : warlockExplosion ? cast.SkillEntityId == 12070
                        : warlockThunder ? cast.SkillEntityId == 10920
                        : warlockWind ? cast.SkillEntityId == 11130
                        : warlockWater ? cast.SkillEntityId == 11340
                        : warlockMeteor ? cast.SkillEntityId == 11540
                        : protectiveRunes ? cast.SkillEntityId == 12110
                        : chaosRune ? cast.SkillEntityId == 11380
                        : psychicWard ? cast.SkillEntityId == 11760
                        : rockSummon ? cast.SkillEntityId == 12140
                        : voodooDoll ? cast.SkillEntityId == 12200
                        : heavenlyRain ? cast.SkillEntityId == 12020
                        : sageDarkBlast ? cast.SkillEntityId == 12120
                        : skillId == 10048 ? cast.SkillEntityId == 11090
                        : skillId == 10049 ? cast.SkillEntityId == 11100
                        : skillId == 10051 ? cast.SkillEntityId == 11260
                        : skillId == 10053 ? cast.SkillEntityId == 11250
                        : skillId == 10066 ? cast.SkillEntityId == 11550
                        : asuraHand ? cast.SkillEntityId == 11400
                        : swiftWind ? cast.SkillEntityId == 11930
                        : hunt ? cast.SkillEntityId == 11820
                        : extinction ? cast.SkillEntityId == 11530
                        : eclipseSlashes ? cast.SkillEntityId == 11440
                        : shieldCharge ? cast.SkillEntityId == 11730
                        : holyPurification ? cast.SkillEntityId == 11860
                        : cast.SkillEntityId == 10780 || cast.SkillEntityId == 11640 || cast.SkillEntityId == 11650 || cast.SkillEntityId == 11660,
                        "The acquired active skill selected a foreign entity: " + cast.SkillEntityId);
                    var damageEvents = step.Event.DamageEvents.Where(value => value.SourceId == 1
                        && value.SkillEntityId == (skillId == 10017 ? 10970 : cast.SkillEntityId)).ToArray();
                    if (asuraHand)
                    {
                        VerifyAcquiredAsuraBuff(cast, damageEvents, step.Event, actor);
                        hits++;
                        continue;
                    }
                    if (swiftWind)
                    {
                        VerifyAcquiredSwiftWindCast(cast, damageEvents, checkpoint, output);
                        hits++;
                        continue;
                    }
                    if (hunt)
                    {
                        VerifyAcquiredHuntJudgment(step.Event, cast, damageEvents, checkpoint, output);
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (extinction)
                    {
                        VerifyAcquiredExtinctionBlade(step.Event, cast, damageEvents, checkpoint, output);
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (eclipseSlashes)
                    {
                        VerifyAcquiredEclipseSlashes(cast, damageEvents, checkpoint, output);
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (flowingFire)
                    {
                        flowingFireExtras += VerifyAcquiredFlowingFire(step.Event, checkpoint, output, fireSkin);
                        hits += step.Event.DamageEvents.Count(value => value.SourceId == actor.Id && value.SkillItemId == skillId);
                        continue;
                    }
                    if (fireLeap)
                    {
                        VerifyAcquiredFireLeap(step.Event, cast, damageEvents, checkpoint, output, hits == 0);
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (chaosRune)
                    {
                        VerifyAcquiredChaosRune(step.Event, cast, damageEvents, checkpoint, output, ref confusionApplications);
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (abyssalHand)
                    {
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (sageDarkBlast)
                    {
                        VerifyAcquiredSageDarkBlast(cast, damageEvents, checkpoint, output, hits == 0);
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (psychicWard)
                    {
                        Require(damageEvents.Length == 1 && damageEvents[0].TargetId == actor.Id
                            && damageEvents[0].HitEntityId == 11761 && damageEvents[0].DamageEntityId == 11762
                            && damageEvents[0].Damage == 0 && !damageEvents[0].IsCure,
                            "The original psychic ward changed its non-damaging status application.");
                        hits++;
                        continue;
                    }
                    if (heavenlyRain)
                    {
                        VerifyAcquiredHeavenlyRain(cast, damageEvents, checkpoint, output, hits == 0, !overflowShield);
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (rock != null)
                    {
                        rock.VerifySummon(cast, step.Event, checkpoint, actor, active, output);
                        continue;
                    }
                    if (doll != null)
                    {
                        doll.VerifySummon(cast, checkpoint, actor, active, output);
                        continue;
                    }
                    if (protectiveRunes)
                    {
                        VerifyAcquiredProtectiveRunes(checkpoint, output);
                        hits++;
                        continue;
                    }
                    if (warlockWind)
                    {
                        VerifyAcquiredWindRoar(cast, damageEvents, checkpoint, output);
                        hits += damageEvents.Count(hit => hit.Damage < 0);
                        continue;
                    }
                    if (warlockWater)
                    {
                        VerifyAcquiredWaterVortex(cast, damageEvents, step.Event, checkpoint, output);
                        hits += damageEvents.Count(hit => hit.Damage < 0);
                        continue;
                    }
                    if (warlockMeteor)
                    {
                        VerifyAcquiredMeteorFire(cast, damageEvents, checkpoint, output);
                        hits += damageEvents.Count(hit => hit.Damage < 0);
                        continue;
                    }
                    if (warlockThunder)
                    {
                        VerifyAcquiredDivineThunder(cast, damageEvents, checkpoint, output);
                        hits += damageEvents.Count(hit => hit.Damage < 0);
                        continue;
                    }
                    if (warlockExplosion)
                    {
                        Require(damageEvents.Length == 1 && damageEvents[0].TargetId != actor.Id
                            && damageEvents[0].SkillItemId == 10098 && damageEvents[0].HitEntityId == 12071
                            && damageEvents[0].DamageEntityId == 10004 && damageEvents[0].ElementType == 102
                            && damageEvents[0].DamageType == 1 && damageEvents[0].Damage < 0
                            && !damageEvents[0].TrueDamage && !damageEvents[0].IsCure
                            && Math.Abs(damageEvents[0].Delay - cast.Delay - .49f) < .0001f,
                            "The acquired explosion lost its original single fire hit or timing.");
                        hits++;
                        continue;
                    }
                    if (judgmentThunder)
                    {
                        VerifyAcquiredJudgmentThunder(cast, damageEvents);
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (battlecry)
                    {
                        VerifyAcquiredBattlecry(step.Event, checkpoint, output, ref battlecryAdvances);
                        hits++;
                        continue;
                    }
                    if (starBurst)
                    {
                        VerifyAcquiredStarBurst(step.Event, cast, damageEvents, checkpoint, output, ref starBlindApplications);
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (doomTorrent)
                    {
                        VerifyAcquiredDoomTorrent(step.Event, cast, damageEvents, ref doomPushes);
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (spiralWindBlade)
                    {
                        VerifyAcquiredSpiralWindBlade(step.Event, cast, damageEvents, checkpoint, output, ref tearApplications);
                        hits += damageEvents.Length;
                        continue;
                    }
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
                    if (hellfire)
                    {
                        var regeneration = actor.Statuses.Single(value => value.ClassId == 24235);
                        Require(regeneration.SourceSkillRank == cultivatedRank && regeneration.SourceSkillLevel == 2
                            && regeneration.SourceSubRank == 8 && regeneration.DurationRound == 2
                            && regeneration.RoundStartMaxCount == 2 && !regeneration.RoundStartTryAtStart
                            && damageEvents.All(value => !value.IsCure), "The acquired hellfire lost its deferred two-turn regeneration.");
                        continue;
                    }
                    if (statusSkill)
                    {
                        bool spirit = skillId == 10051;
                        bool guard = skillId == 10046;
                        bool encouragement = skillId == 10066;
                        var statusOwner = AcquiredGuardianStatusOwner(checkpoint, actor, passiveCompanion);
                        int statusId = crisisGuard ? 24180 : spirit ? 24214 : guard ? 24170 : encouragement ? 24237 : 24030;
                        int skillEntityId = cast.SkillEntityId;
                        float statusDelay = crisisGuard ? 1.2f : spirit ? .25f : guard ? .44f : .2f;
                        var started = step.Event.StatusViewEvents.Where(value => value.Kind == FightBattleStatusViewEventKind.Start
                            && value.OwnerId == statusOwner.Id && value.Status.ClassId == statusId && value.Status.CreatorId == 1
                            && (encouragement || Math.Abs(value.Delay - cast.Delay - statusDelay) < .0001f)).ToArray();
                        Require((started.Length == 1 || (crisisGuard || guard || encouragement) && started.Length == 0) && damageEvents.Length == 1
                            && damageEvents[0].TargetId == statusOwner.Id && damageEvents[0].HitEntityId == skillEntityId + 1
                            && damageEvents[0].DamageEntityId == skillEntityId + 2 && damageEvents[0].Damage == 0,
                            "The acquired skill lost its original self-status or timing.");
                        var status = started.Length == 1
                            ? statusOwner.Statuses.Single(value => value.InstanceId == started[0].Status.InstanceId)
                            : statusOwner.Statuses.Single(value => value.ClassId == statusId && value.CreatorId == 1);
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
                        float delay = frostSong ? .5833f : cast.SkillEntityId == 11983 ? .57f : cast.SkillEntityId == 11984 ? .72f : .55f;
                        long shieldValue = Math.Min((long)((hit.BeforeHp - hit.AfterHp) * (double).2f), actor.MaxHp / 2);
                        Require(hit.TargetId != actor.Id && hit.HitEntityId == (frostSong ? 13581 : 11981)
                            && hit.DamageEntityId == (frostSong ? 13582 : 11982)
                            && hit.DamageType == 1 && hit.ElementType == 101 && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure
                            && Math.Abs(hit.Delay - cast.Delay - delay) < .0001f && shieldValue > 0
                            && actor.Statuses.Any(value => value.ClassId == (frostSong ? 24659 : 24377) && value.CreatorId == actor.Id
                                && value.DurationRound == 2 && value.ShieldRemainValue == shieldValue),
                            "The acquired water blade lost its water damage or capped HP-only shield conversion.");
                        hits++;
                        continue;
                    }
                    if (diversionHarassment)
                    {
                        Require(damageEvents.Length > 0 && damageEvents.All(value => value.TargetId != actor.Id
                            && value.HitEntityId == 11891 && value.DamageEntityId == 11892 && value.DamageType == 1
                            && value.ElementType == 103 && value.Damage < 0 && !value.TrueDamage && !value.IsCure
                            && value.Delay > cast.Delay + .4f
                            && checkpoint.Units.Single(unit => unit.Id == value.TargetId).Statuses.Any(status =>
                                status.ClassId == 24216 && status.CreatorId == actor.Id && status.DurationRound == 1)),
                            "The acquired diversion harassment lost its light area hit or guaranteed non-character taunt.");
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (heavenlyLightBlades)
                    {
                        Require(damageEvents.Length > 0 && damageEvents.All(value => value.TargetId != actor.Id
                            && value.HitEntityId >= 11901 && value.HitEntityId <= 11903 && value.DamageEntityId == 10026
                            && value.DamageType == 1 && value.ElementType == 103 && value.Damage < 0
                            && !value.TrueDamage && !value.IsCure && value.Delay >= cast.Delay + .7599f
                            && value.Delay <= cast.Delay + .8801f),
                            "The acquired heavenly light blades lost the original light columns or row timing.");
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (dreadShadow)
                    {
                        Require(damageEvents.Length > 0 && damageEvents.All(value => value.TargetId != actor.Id
                            && value.HitEntityId == 12131 && value.DamageEntityId == 12132 && value.ElementType == 104
                            && value.DamageType == 1 && value.Damage < 0 && !value.TrueDamage && !value.IsCure
                            && Math.Abs(value.Delay - cast.Delay - .42f) < .0001f),
                            "The acquired dread shadow lost its original dark area hit.");
                        foreach (var start in step.Event.StatusViewEvents.Where(value => value.Kind == FightBattleStatusViewEventKind.Start
                            && value.OwnerId != actor.Id && value.Status.ClassId == 24194))
                        {
                            var status = checkpoint.Units.Single(unit => unit.Id == start.OwnerId).Statuses.Single(value =>
                                value.InstanceId == start.Status.InstanceId);
                            Require(status.CreatorId == actor.Id && status.SourceSkillRank == cultivatedRank
                                && status.SourceSkillLevel == 2 && status.SourceSubRank == 8
                                && status.DurationRound == 2 && status.ActionType == 78
                                && status.ApplyingSkillItemId == skillId && status.CanDispersed,
                                "The acquired fear lost its original source, cultivation, action or duration.");
                            fearApplications++;
                        }
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (soulPiercer)
                    {
                        VerifyAcquiredSoulPiercer(step.Event, damageEvents, actor, cast.Delay, ref lifestealCures);
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (swordWindDance)
                    {
                        Require(damageEvents.Length > 0 && damageEvents.All(value => value.TargetId != actor.Id
                            && value.ElementType == 100 && value.DamageType == 1 && value.Damage < 0
                            && !value.TrueDamage && !value.IsCure), "The acquired sword wind dance lost its wind damage.");
                        foreach (var target in damageEvents.GroupBy(value => value.TargetId))
                        {
                            var strikes = target.OrderBy(value => value.Delay).ToArray();
                            Require(strikes.Length == 3 && strikes[0].HitEntityId == 11911 && strikes[1].HitEntityId == 11911
                                && strikes[2].HitEntityId == 11913 && strikes[0].DamageEntityId == 10004
                                && strikes[1].DamageEntityId == 10004 && strikes[2].DamageEntityId == 11912
                                && Math.Abs(strikes[1].Delay - strikes[0].Delay - .16f) < .0001f
                                && strikes[0].Delay >= cast.Delay + .4499f && strikes[0].Delay <= cast.Delay + .5601f
                                && strikes[2].Delay >= cast.Delay + 1.1299f && strikes[2].Delay <= cast.Delay + 1.2001f,
                                "The acquired sword wind dance lost its three authored strikes or row timing.");
                        }
                        hits += damageEvents.Length;
                        continue;
                    }
                    if (vortexSurge)
                    {
                        Require(damageEvents.Length > 0 && damageEvents.All(value => value.TargetId != actor.Id
                            && value.HitEntityId == 12081 && value.DamageEntityId == 12082 && value.DamageType == 1
                            && value.ElementType == 101 && value.Damage < 0 && !value.TrueDamage && !value.IsCure
                            && value.Delay >= cast.Delay + .5749f && value.Delay <= cast.Delay + .7201f),
                            "The acquired vortex surge lost its original water rings or damage timing.");
                        foreach (var movement in step.Event.Movements.Where(value => value.MoveBehaviourId == 12083))
                        {
                            Require(movement.UnitId != actor.Id && movement.Path.Count == 2
                                && movement.MoveDuration == -1f && Math.Abs(movement.MoveSpeed - 2.5f) < .0001f,
                                "The original one-cell inward knockback lost its action or speed.");
                            vortexMovements++;
                        }
                        hits += damageEvents.Length;
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
                if (rock != null) hits += rock.VerifyAttacks(step.Event, checkpoint);
                sageSummonSecret?.Observe(step.Event, checkpoint, rock);
                if (doll != null) hits += doll.Observe(step.Event, checkpoint, output);
            };
            yield return CaptureAcquiredActive(output, "preparation");
            click(FlameFlowButton(value => value.name == "Btn" && value.GetComponentsInParent<Transform>()
                .Any(parent => parent.name == "StartBtn")));
            bool captured = false, capturedOpening = false, capturedPassive = false, capturedFear = false;
            bool capturedStarBlind = false, capturedConfusion = false;
            bool capturedTear = false;
            bool capturedTacticalAttack = false, capturedTacticalDefence = false;
            while (battle != null)
            {
                if (sageSummonSecret?.ReadyCapture(rock) == true)
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                    sageSummonSecret.Captured();
                }
                int sageRoundCapture = sageRoundSecret?.ReadyCapture() ?? 0;
                if (sageRoundCapture > 0)
                {
                    yield return CaptureAcquiredActive(output, "sage-round-" + sageRoundCapture);
                    sageRoundSecret.Captured(sageRoundCapture);
                    capturedPassive = true;
                }
                if (acquiredGuardian?.OpeningReady() == true && !capturedOpening)
                {
                    yield return CaptureAcquiredActive(output, "opening");
                    capturedOpening = true;
                    acquiredGuardian.OpeningCaptured();
                }
                if (doll?.IsVisible() == true && !capturedOpening)
                {
                    yield return CaptureAcquiredActive(output, "opening");
                    capturedOpening = true;
                }
                if (rock?.IsVisible() == true && !capturedOpening)
                {
                    yield return CaptureAcquiredActive(output, "opening");
                    capturedOpening = true;
                }
                if (spiralWindBlade && !capturedTear && FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                    && particle.GetComponentsInParent<Transform>().Any(parent => parent.name.StartsWith("playable24376", StringComparison.Ordinal))))
                {
                    yield return CaptureAcquiredActive(output, "laceration");
                    capturedTear = true;
                }
                if (starBurst && !capturedStarBlind && FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                    && particle.GetComponentsInParent<Transform>().Any(parent => parent.name.StartsWith("playable24234", StringComparison.Ordinal))))
                {
                    yield return CaptureAcquiredActive(output, "blinding");
                    capturedStarBlind = true;
                }
                if (passiveCompanion == 10543 && !capturedPassive
                    && FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                        && particle.GetComponentsInParent<Transform>().Any(parent =>
                            parent.name.StartsWith("playable11150", StringComparison.Ordinal))))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (flameSoul && !capturedPassive
                    && File.Exists(Path.Combine(output, "companion-flame-soul-checkpoint.json"))
                    && checkpoint.Units.Single(unit => unit.Id == 1).Statuses.Any(status => status.ClassId == 24386)
                    && IsNativeSkillParticleVisible("playable24386_"))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (fireCore && !capturedPassive && IsNativeSkillParticleVisible("playable10540"))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (iceCrystal && !capturedPassive && IsNativeSkillParticleVisible("playable11410"))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (chaosRune && !capturedConfusion && confusionApplications > 0 && IsNativeSkillParticleVisible("playable24217"))
                {
                    yield return CaptureAcquiredActive(output, "confusion");
                    capturedConfusion = true;
                }
                if (overflowShield && !capturedPassive && IsNativeSkillParticleVisible("playable24174"))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (overloadProtection && !capturedPassive && IsNativeSkillParticleVisible("playable24240"))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (passiveCompanion == 10598 && !capturedPassive && IsHurricaneGriefVisible()
                    && File.Exists(Path.Combine(output, "companion-hurricane-grief-status.json")))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (passiveCompanion == 10597 && !capturedPassive && fireLifeCures > 0
                    && File.Exists(Path.Combine(output, "companion-fire-life-status-events.json")))
                {
                    yield return new WaitForSecondsRealtime(.12f);
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (swiftWind && !swiftWindDance.buffCaptured && IsNativeSkillParticleVisible("playable24373"))
                {
                    yield return CaptureAcquiredActive(output, "swift-buff");
                    swiftWindDance.buffCaptured = true;
                }
                if (hunt)
                {
                    if (!huntJudgment.pullCaptured && huntJudgment.pulls > 0 && ObserveAcquiredHuntPull())
                    {
                        yield return CaptureAcquiredActive(output, "hunt-pull");
                        huntJudgment.pullCaptured = true;
                    }
                    if (!huntJudgment.launchCaptured && ObserveAcquiredAirbornePhase(24269))
                    {
                        yield return CaptureAcquiredActive(output, "airborne-launch");
                        huntJudgment.launchCaptured = true;
                    }
                    if (!huntJudgment.recoveryCaptured && ObserveAcquiredAirbornePhase(24270))
                    {
                        yield return CaptureAcquiredActive(output, "airborne-recovery");
                        huntJudgment.recoveryCaptured = true;
                    }
                }
                if (extinction)
                {
                    if (!extinctionBlade.launchCaptured && ObserveAcquiredAirbornePhase(24269))
                    {
                        yield return CaptureAcquiredActive(output, "airborne-launch");
                        extinctionBlade.launchCaptured = true;
                    }
                    if (!extinctionBlade.recoveryCaptured && ObserveAcquiredAirbornePhase(24270))
                    {
                        yield return CaptureAcquiredActive(output, "airborne-recovery");
                        extinctionBlade.recoveryCaptured = true;
                    }
                    if (!extinctionBlade.armourCaptured && IsNativeSkillParticleVisible("playable24110"))
                    {
                        yield return CaptureAcquiredActive(output, "armour");
                        extinctionBlade.armourCaptured = true;
                    }
                }
                if (warlockMeteor)
                {
                    if (!meteorFire.burnCaptured && ObserveAcquiredMeteorBurn())
                    {
                        yield return CaptureAcquiredActive(output, "meteor-burning");
                        meteorFire.burnCaptured = true;
                    }
                    if (!meteorFire.impactCaptured && IsNativeSkillParticleVisible("playable11541"))
                    {
                        yield return CaptureAcquiredActive(output, "battle");
                        meteorFire.impactCaptured = captured = true;
                    }
                }
                if (warlockWater)
                {
                    if (!waterVortex.moveCaptured && ObserveAcquiredWaterVortexMovement())
                    {
                        yield return CaptureAcquiredActive(output, "vortex-pull");
                        waterVortex.moveCaptured = true;
                    }
                    if (!waterVortex.wetCaptured && ObserveAcquiredWaterVortexWet())
                    {
                        yield return CaptureAcquiredActive(output, "wet");
                        waterVortex.wetCaptured = true;
                    }
                    if (!captured && IsNativeSkillParticleVisible("playable11340"))
                    {
                        yield return CaptureAcquiredActive(output, "battle");
                        captured = true;
                    }
                }
                if (warlockWind)
                {
                    bool launchVisible = ObserveAcquiredWindRoarPhase(24269);
                    if (!windRoar.launchCaptured && launchVisible)
                    {
                        yield return CaptureAcquiredActive(output, "airborne-launch");
                        windRoar.launchCaptured = true;
                    }
                    if (!windRoar.recoveryCaptured && ObserveAcquiredWindRoarPhase(24270))
                    {
                        yield return CaptureAcquiredActive(output, "airborne-recovery");
                        windRoar.recoveryCaptured = true;
                    }
                    if (!captured && IsNativeSkillParticleVisible("playable11130"))
                    {
                        yield return CaptureAcquiredActive(output, "battle");
                        captured = true;
                    }
                }
                if (passiveCompanion == 10545 && !capturedPassive
                    && IsNativeSkillParticleVisible("playable_damage_fire_HitEffect"))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (passiveCompanion == 10590 && !capturedPassive && soulGuardian.paid > 0
                    && IsNativeSkillParticleVisible("playable11870"))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    soulGuardian.castCaptured = capturedPassive = true;
                }
                if (passiveCompanion == 10574)
                {
                    if (!judgmentBlade.markCaptured && IsNativeSkillParticleVisible("playable24316"))
                    {
                        yield return CaptureAcquiredActive(output, "passive-mark");
                        judgmentBlade.markCaptured = true;
                    }
                    if (!capturedPassive && judgmentBlade.strikes > 0 && IsNativeSkillParticleVisible("playable11810"))
                    {
                        yield return CaptureAcquiredActive(output, "passive-battle");
                        judgmentBlade.strikeCaptured = capturedPassive = true;
                    }
                }
                if (passiveCompanion == 10519 && !capturedPassive && wildBladeSiphon.cures > 0
                    && IsNativeSkillParticleVisible("playable_cure_lifesteal"))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    wildBladeSiphon.captured = capturedPassive = true;
                }
                if (passiveCompanion == 10547 && !capturedPassive && desperateValor.hits > 0
                    && IsNativeSkillParticleVisible("playable24242"))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    desperateValor.captured = capturedPassive = true;
                }
                if (passiveCompanion == 10605 && !capturedPassive && iceGuardian.starts > 0
                    && IsNativeSkillParticleVisible("playable24390"))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (passiveCompanion == 10577)
                {
                    if (!capturedPassive && lightScorch.procs > 0
                        && IsNativeSkillParticleVisible("playable_damage_light_HitEffect"))
                    {
                        yield return CaptureAcquiredActive(output, "passive-battle");
                        capturedPassive = true;
                    }
                    if (!lightScorchStatusCaptured && IsNativeSkillParticleVisible("playable24234"))
                    {
                        yield return CaptureAcquiredActive(output, "passive-status");
                        lightScorchStatusCaptured = true;
                    }
                }
                if (passiveCompanion == 10581 && !capturedPassive && potentialRebirth.cures > 0
                    && IsNativeSkillParticleVisible("playable11770"))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (passiveCompanion == 10604 && !capturedPassive && IsNativeSkillParticleVisible("playable12150"))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (acquiredGuardian != null && !capturedPassive && acquiredGuardian.Ready(battle.Controller))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                    acquiredGuardian.Captured(checkpoint);
                }
                if (passiveCompanion == 10595 && !capturedPassive
                    && FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                        && particle.GetComponentsInParent<Transform>().Any(parent =>
                            parent.name.StartsWith("playable24385_", StringComparison.Ordinal))))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (passiveCompanion == 10609)
                    foreach (int statusId in new[] { 24394, 24393 })
                    {
                        if (statusId == 24394 ? capturedTacticalAttack : capturedTacticalDefence) continue;
                        if (!FindObjectsOfType<Transform>().Any(value => value.name == statusId.ToString()
                                || value.name.StartsWith(statusId + "(", StringComparison.Ordinal))
                            || !FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                                && particle.GetComponentsInParent<Transform>().Any(parent =>
                                    parent.name.StartsWith("playable" + statusId, StringComparison.Ordinal)))) continue;
                        yield return CaptureAcquiredActive(output, "tactical-" + statusId);
                        if (statusId == 24394) capturedTacticalAttack = true; else capturedTacticalDefence = true;
                    }
                if (passiveCompanion == 10608 && standfastTurns > 0 && !capturedPassive
                    && FindObjectsOfType<Transform>().Any(value => value.name == "24392"
                        || value.name.StartsWith("24392(", StringComparison.Ordinal))
                    && FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                        && particle.GetComponentsInParent<Transform>().Any(parent =>
                            parent.name.StartsWith("playable24392", StringComparison.Ordinal))))
                {
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                var fearEntity = dreadShadow && fearApplications > 0 && !capturedFear && captured
                    ? FindObjectsOfType<Transform>().FirstOrDefault(value => (value.name == "24194"
                        || value.name.StartsWith("24194(", StringComparison.Ordinal)))
                    : null;
                if (fearEntity != null && FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                    && particle.GetComponentsInParent<Transform>().Any(parent => parent.name.StartsWith("playable24194", StringComparison.Ordinal))))
                {
                    yield return CaptureAcquiredActive(output, "fear-battle");
                    capturedFear = true;
                }
                if (passiveCompanion == 10607 && !capturedPassive && auraHits > 0 && coldHits > 0
                    && FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                        && particle.GetComponentsInParent<Transform>().Any(parent => parent.name.StartsWith(
                            "Eff_Skill_Paladin04_ExtremeColdField01", StringComparison.Ordinal))))
                {
                    yield return new WaitForSecondsRealtime(.12f);
                    yield return CaptureAcquiredActive(output, "passive-battle");
                    capturedPassive = true;
                }
                if (lightSwordArray && !capturedOpening && casts > 0 && hits == 0)
                {
                    yield return new WaitForSecondsRealtime(1.05f);
                    Require(FindObjectsOfType<Transform>().Count(value => value.name.StartsWith(
                        "Eff_Skill_Paladin04_SwordArray03", StringComparison.Ordinal)
                        && value.GetComponentsInParent<Zjcs.Official.BattleStatusPlayables.MyPlayableOrbitMovement>().Length > 0) == 4,
                        "The original status prefab did not render all four opening orbiters.");
                    yield return CaptureAcquiredActive(output, "opening"); capturedOpening = true;
                }
                if (asuraHand && !asuraHandValidation.attackCaptured && asuraHandValidation.convertedHits > 0
                    && IsNativeSkillParticleVisible("playable11440"))
                {
                    yield return CaptureAcquiredActive(output, "asura-attack");
                    asuraHandValidation.attackCaptured = true;
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
                    else if (flowingFire)
                    {
                        if (flowingFireExtras == 0 || !IsNativeSkillParticleVisible(fireSkin ? "playable13590" : "playable12090"))
                        { yield return null; continue; }
                    }
                    else if (asuraHand)
                    {
                        if (!IsNativeSkillParticleVisible("playable11400") && !IsNativeSkillParticleVisible("playable24227"))
                        { yield return null; continue; }
                    }
                    else if (swiftWind)
                    {
                        if (!IsNativeSkillParticleVisible("playable11930")) { yield return null; continue; }
                    }
                    else if (hunt)
                    {
                        if (!IsNativeSkillParticleVisible("playable11820")) { yield return null; continue; }
                        huntJudgment.castCaptured = true;
                    }
                    else if (extinction)
                    {
                        if (!IsNativeSkillParticleVisible("playable11530")) { yield return null; continue; }
                    }
                    else if (eclipseSlashes)
                    {
                        if (!IsNativeSkillParticleVisible("playable11440")) { yield return null; continue; }
                    }
                    else if (fireLeap)
                    {
                        if (!IsNativeSkillParticleVisible("playable12100")) { yield return null; continue; }
                    }
                    else if (diversionHarassment)
                    {
                        bool impactVisible = FindObjectsOfType<ParticleSystem>().Any(particle =>
                            particle.particleCount > 0 && particle.GetComponentsInParent<Transform>().Any(parent =>
                                parent.name == "playable11891" || parent.name.StartsWith("playable11891(", StringComparison.Ordinal)));
                        if (!impactVisible) { yield return null; continue; }
                    }
                    else if (heavenlyLightBlades)
                    {
                        bool bladesVisible = FindObjectsOfType<ParticleSystem>().Any(particle =>
                            particle.particleCount > 0 && particle.GetComponentsInParent<Transform>().Any(parent =>
                                parent.name.StartsWith("Eff_Skill_Paladin04_SkylightPhantomBlade02", StringComparison.Ordinal)));
                        if (!bladesVisible) { yield return null; continue; }
                    }
                    else if (vortexSurge)
                    {
                        bool vortexVisible = FindObjectsOfType<ParticleSystem>().Any(particle =>
                            particle.particleCount > 0 && particle.name == "xuanwo"
                            && particle.GetComponentsInParent<Transform>().Any(parent =>
                                parent.name.StartsWith("Eff_Skill_Paladin04_WhirlpoolRagingWaves", StringComparison.Ordinal)));
                        if (!vortexVisible) { yield return null; continue; }
                    }
                    else if (protectiveRunes)
                    {
                        if (!FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                            && particle.GetComponentsInParent<Transform>().Any(parent => parent.name.StartsWith("playable24201", StringComparison.Ordinal))))
                        { yield return null; continue; }
                    }
                    else if (judgmentThunder)
                    {
                        if (!FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                            && particle.GetComponentsInParent<Transform>().Any(parent => parent.name.StartsWith("playable11970", StringComparison.Ordinal))))
                        { yield return null; continue; }
                    }
                    else if (warlockThunder)
                    {
                        if (!IsNativeSkillParticleVisible("playable10922")
                            && !IsNativeSkillParticleVisible("playable10923")
                            && !IsNativeSkillParticleVisible("playable10924")
                            && !IsNativeSkillParticleVisible("playable10925")) { yield return null; continue; }
                    }
                    else if (warlockExplosion)
                    {
                        if (!FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                            && particle.GetComponentsInParent<Transform>().Any(parent => parent.name.StartsWith("playable12070", StringComparison.Ordinal))))
                        { yield return null; continue; }
                    }
                    else if (battlecry)
                    {
                        if (!FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                            && particle.GetComponentsInParent<Transform>().Any(parent => parent.name.StartsWith("playable24192", StringComparison.Ordinal))))
                        { yield return null; continue; }
                    }
                    else if (doomTorrent)
                    {
                        if (!FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                            && particle.GetComponentsInParent<Transform>().Any(parent => parent.name.StartsWith("playable11080", StringComparison.Ordinal))))
                        { yield return null; continue; }
                    }
                    else if (spiralWindBlade)
                    {
                        if (!FindObjectsOfType<ParticleSystem>().Any(particle => particle.particleCount > 0
                            && particle.GetComponentsInParent<Transform>().Any(parent => parent.name.StartsWith("playable11960", StringComparison.Ordinal))))
                        { yield return null; continue; }
                    }
                    else if (chaosRune)
                    {
                        if (!IsNativeSkillParticleVisible("playable11380") && !IsNativeSkillParticleVisible("playable_damage_11382"))
                        { yield return null; continue; }
                    }
                    else if (abyssalHand)
                    {
                        if (!IsNativeSkillParticleVisible("playable10950") && !IsNativeSkillParticleVisible("playable10951"))
                        { yield return null; continue; }
                    }
                    else if (sageDarkBlast)
                    {
                        if (!IsNativeSkillParticleVisible("playable12120")) { yield return null; continue; }
                    }
                    else if (psychicWard)
                    {
                        if (!IsNativeSkillParticleVisible("playable11762")) { yield return null; continue; }
                    }
                    else if (heavenlyRain)
                    {
                        if (!IsNativeSkillParticleVisible("playable12021")) { yield return null; continue; }
                    }
                    else if (doll != null)
                    {
                        if (!doll.IsVisible()) { yield return null; continue; }
                        doll.Captured();
                    }
                    else if (rock != null)
                    {
                        if (!rock.IsVisible() || !rock.IsAttackVisible()) { yield return null; continue; }
                    }
                    else yield return new WaitForSecondsRealtime(hellfire ? .2f : crisisGuard || starShatteringSlash ? 1.45f
                        : shieldCharge ? 1.1f : flowingFire ? .1f : .7f);
                    yield return CaptureAcquiredActive(output, "battle"); captured = true;
                    if (heavenlyLightBlades || frostSong || swordWindDance || soulPiercer || doomTorrent || spiralWindBlade || judgmentThunder || fireLeap || flowingFire || warlockThunder)
                        for (int frame = 1; frame <= (flowingFire ? 8 : 4); frame++)
                        {
                            yield return new WaitForSecondsRealtime(flowingFire ? .22f : .18f);
                            yield return CaptureAcquiredActive(output, (frostSong ? "skin-hit-" : swordWindDance ? "wind-hit-" : soulPiercer ? "soul-hit-" : fireLeap ? "jump-hit-" : flowingFire ? "fire-hit-" : warlockThunder ? "thunder-hit-" : "blade-") + frame);
                        }
                }
                yield return null;
            }
            if (psychicWard && (!checkpoint.Finished || casts == 0 || hits == 0 || !captured))
                File.WriteAllText(Path.Combine(output, "acquired-sage-psychic-ward-diagnostic.json"),
                    FightBattleCheckpointSerializer.Save(checkpoint));
            Require(checkpoint.Finished && casts > 0 && hits > 0 && captured,
                "The acquired/cultivated active skill did not finish its damage battle.");
            if (passiveCompanion == 10547) CompleteAcquiredDesperateValor(output);
            if (passiveCompanion == 10519) CompleteAcquiredWildBladeSiphon(output);
            if (passiveCompanion == 10574) CompleteAcquiredJudgmentBlade(output);
            if (passiveCompanion == 10590) CompleteAcquiredSoulGuardian(output);
            soulSplash?.Complete(capturedPassive);
            if (swiftWind) CompleteAcquiredSwiftWindDance(output);
            if (hunt) CompleteAcquiredHuntJudgment(output);
            abyssal?.Complete(captured);
            if (extinction) CompleteAcquiredExtinctionBlade(output);
            if (asuraHand) CompleteAcquiredAsuraHand(output);
            Require(!flowingFire || flowingFireExtras > 0, "The cultivated flowing fire never performed an extra cast.");
            Require(!overflowShield || overflowShields > 0 && capturedPassive,
                "The cultivated overflow shield never settled and rendered its original protective effect.");
            Require(!psychicWard || psychicWardEnds > 0,
                "The cultivated psychic ward never consumed its original mark on actual monster damage.");
            Require(rock == null || casts == 1 && rock.RegularHits > 0 && rock.OpeningHits > 0 && capturedOpening,
                "The original rock emperor did not render its summon and both physical attacks.");
            Require(!lightSwordArray || capturedOpening, "The four original opening swords were not captured.");
            Require(!hellfire || hellfireCures >= 2, "The acquired hellfire did not heal on both subsequent turns.");
            Require(!soulPiercer || lifestealCures > 0, "The acquired soul piercer never restored actual HP.");
            Require(!dreadShadow || fearApplications > 0 && capturedFear, "The acquired fear was never applied and rendered.");
            Require(!chaosRune || confusionApplications > 0 && confusionEnds > 0 && capturedConfusion,
                "The acquired chaos rune never rendered and expired its original confusion.");
            Require(!starBurst || starBlindApplications > 0 && starBlindEnds > 0 && capturedStarBlind,
                "The acquired star burst never applied, rendered and consumed its original blinding status.");
            Require(!doomTorrent || doomPushes > 0, "The acquired doom torrent never exercised its original outward knockback.");
            Require(!battlecry || battlecryAdvances > 0, "The acquired Battlecry never immediately advanced its next action.");
            Require(!protectiveRunes || File.Exists(Path.Combine(output, "acquired-protective-runes-checkpoint.json")),
                "The acquired protective runes never reduced an actual configured enemy hit.");
            Require(!spiralWindBlade || tearApplications > 0 && tearEnds > 0 && tearHits > 0 && capturedTear,
                "The acquired spiral wind blade never applied, rendered, triggered and expired its original movement laceration.");
            Require(!vortexSurge || vortexMovements > 0,
                "The original vortex surge never exercised its inward knockback in the rendered battle.");
            Require(passiveCompanion != 10525 || File.Exists(Path.Combine(output, "companion-affinity-checkpoint.json")),
                "The cultivated light affinity never affected the rendered battle.");
            if (warlockWind) CompleteAcquiredWindRoar(output);
            if (warlockWater) CompleteAcquiredWaterVortex(output);
            if (warlockMeteor) CompleteAcquiredMeteorFire(checkpoint, output);
            if (passiveCompanion == 10605) CompleteAcquiredIceGuardian(capturedPassive);
            if (passiveCompanion == 10581) CompleteAcquiredPotentialRebirth(capturedPassive);
            if (passiveCompanion == 10577) CompleteAcquiredLightScorch(capturedPassive);
            Require(passiveCompanion != 10604 || capturedPassive
                && File.Exists(Path.Combine(output, "companion-quick-casting-checkpoint.json")),
                "The cultivated quick casting never reduced cooldowns and rendered its original opener.");
            Require(passiveCompanion != 10606 && passiveCompanion != 10607 || coldHits > 0,
                "The cultivated passive never applied cold in the rendered battle.");
            Require(passiveCompanion != 10608 || standfastTurns > 0 && capturedPassive,
                "The cultivated standfast soul never applied and rendered its turn-bound protection.");
            Require(passiveCompanion != 10568 || assaultHits > 0, "The cultivated defence ignore never affected a successful hit.");
            Require(passiveCompanion != 10594 || soulBreakHits > 0, "The cultivated target missing-health bonus never affected a successful hit.");
            Require(passiveCompanion != 10592 || File.Exists(Path.Combine(output, "companion-steel-faith-checkpoint.json")),
                "The cultivated steel faith never checked an actual taunted enemy's configured hit.");
            Require(passiveCompanion != 10579 || File.Exists(Path.Combine(output, "companion-alienation-checkpoint.json")),
                "The cultivated alienation never checked an afflicted enemy's configured hit.");
            Require(passiveCompanion != 10598 || capturedPassive && windHits > 0
                && HurricaneGriefTriggerCountMatches(checkpoint, windHits)
                && File.Exists(Path.Combine(output, "companion-hurricane-grief-status.json"))
                && File.Exists(Path.Combine(output, "companion-hurricane-grief-movement.json")),
                "The cultivated wind laceration was not applied, rendered and settled during movement.");
            Require(passiveCompanion != 10597 || capturedPassive && fireLifeCures > 0
                && File.Exists(Path.Combine(output, "companion-fire-life-checkpoint.json"))
                && File.Exists(Path.Combine(output, "companion-fire-life-status-events.json")),
                "The cultivated fire life did not invoke its round-end cure, persist HP and render the native status event.");
            acquiredGuardian?.Complete(capturedPassive);
            sageRoundSecret?.Complete();
            sageSummonSecret?.Complete();
            doll?.Complete(output);
            Require(passiveCompanion != 10595 || windHits > 0 && capturedPassive,
                "The cultivated wind orb never rendered and struck at the owner's turn end.");
            Require(passiveCompanion != 10543 || flameHits > 0 && capturedPassive,
                "The cultivated critical blast never rendered and dealt fire damage.");
            Require(!flameSoul || capturedPassive && File.Exists(Path.Combine(output, "companion-flame-soul-checkpoint.json")),
                "The cultivated fire-cast critical stack was not exercised and rendered.");
            Require(!fireCore || capturedPassive && File.Exists(Path.Combine(output, "companion-fire-core-checkpoint.json")),
                "The cultivated fire core burst was not exercised and rendered.");
            Require(!iceCrystal || capturedPassive && File.Exists(Path.Combine(output, "companion-ice-crystal-checkpoint.json")),
                "The cultivated ice crystal thaw was not exercised and rendered.");
            Require(!overloadProtection || capturedPassive && File.Exists(Path.Combine(output, "companion-overload-protection-checkpoint.json")),
                "The cultivated overload protection shield was not exercised and rendered.");
            Require(passiveCompanion != 10609 || capturedTacticalAttack && capturedTacticalDefence
                && File.Exists(Path.Combine(output, "companion-tactical-24394.json"))
                && File.Exists(Path.Combine(output, "companion-tactical-24393.json")),
                "The cultivated tactical adaptation never applied and rendered both threshold effects.");
            Require(passiveCompanion != 10607 || auraHits > 0, "The cultivated frigid aura never struck at the enemy's turn end.");
            Require(passiveCompanion != 10607 || capturedPassive, "The original frigid-aura effect was never rendered.");
            File.WriteAllText(Path.Combine(output, "acquired-active-ui-checkpoint.json"), FightBattleCheckpointSerializer.Save(checkpoint));
            yield return new WaitForSecondsRealtime(.8f);
            yield return CaptureAcquiredActive(output, "result");
            var result = FindObjectsOfType<Transform>().Single(value => value.name == "damage_test_result");
            var report = result.GetComponent<OfficialBattleResultReport>();
            var final = report?.FinalReport;
            Require(final != null && (heavenlyRain
                ? final.Skills.Any(row => row.SkillItemId == skillId && row.CureHits > 0 && row.Heal > 0)
                : statusSkill || final.TotalDamageDealt > 0),
                "Original damage-test result omitted the equipped skill's settled effect.");
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
            if (frostSong || fireSkin) Require(disk.Player.Wardrobe.SkillSkins.Any(value => value.SkillId == skillId && value.SkillSkinId == skillSkinId),
                "The original battle/result flow changed the saved skill appearance.");
            yield return CaptureAcquiredActive(output, "return");
            Debug.Log("[Acquired active UI] " + skillId + " saved rank=" + cultivatedRank + " level=2, original "
                + (statusSkill ? "status" : "damage") + "/result/return and unchanged save passed.");
            Require(home.Hud.TryHandleBack() && !home.Hud.CharacterPageOpen,
                "The completed diagnostic did not close its original character page.");
            // Both native canvases live outside Home. Use its full disposal
            // path to release their bindings before mounting the next profile.
            var hudCanvas = home.Hud.CanvasTransform;
            home.Hud.Dispose();
            Destroy(home.gameObject);
            yield return null;
            Require(!pageCanvas && !hudCanvas && !home,
                "The completed skill diagnostic retained its native UI or Home instance.");
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result { passed = true }, true));
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

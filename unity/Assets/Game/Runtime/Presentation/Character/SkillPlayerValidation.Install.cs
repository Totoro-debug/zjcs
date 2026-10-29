using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;

namespace Zjcs.Presentation.Character
{
    public sealed partial class SkillPlayerValidation
    {
        private IEnumerator ValidateOriginalInstall(OfflineCharacterProgressionService progression, CharacterSkillPanel panel)
        {
            var plan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
            int skillId = progression.Player.Skills.Skills.Select(value => value.ClassId).First(id =>
                progression.SkillDefinition(id).ItemType == 100 && !plan.ActiveSkills.Contains(id)
                && OfflineSkillProgression.IsAvailableFor(progression.SkillDefinition(id), progression.SkillContext()));
            int[] before = (int[])plan.ActiveSkills.Clone();
            Click(ButtonContaining("ActiveSkillTab")); yield return null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Click(ButtonNamed("SkillListItem_" + skillId)); yield return new WaitForSecondsRealtime(.4f);
                Click(ButtonNamed("EquipBtn")); yield return new WaitForSecondsRealtime(.4f);
                var mask = FindObjectsOfType<RectTransform>().Single(value => value.name == "Mask"
                    && value.parent != null && value.parent.name == "SkillContentPanel" && value.gameObject.activeInHierarchy);
                var active = mask.parent.Find("ActiveSkill");
                var passive = mask.parent.Find("PassiveSkill");
                Require(active.GetSiblingIndex() > mask.GetSiblingIndex() && passive.GetSiblingIndex() < mask.GetSiblingIndex(),
                    "The original install mask did not isolate the target skill group.");
                Require(!FindObjectsOfType<Transform>().Any(value => value.name == "SkillInstallMask" || value.name == "SkillInstallHint"),
                    "The hand-written install mask or hint is still present.");
                Require(progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].ActiveSkills.SequenceEqual(before),
                    "Opening the install mask changed the equipped plan before a slot click.");
                yield return new WaitForEndOfFrame(); Capture("skills-original-install-mask");
                if (attempt == 0) Click(mask.Find("SafePanel/CloseBtn").GetComponent<Button>());
                else if (attempt == 1) Require(panel.HandleEscape(), "Escape did not cancel the original installation mask.");
                else Click(active.GetChild(0).Find("Slot").GetComponent<Button>());
                yield return null;
                Require(!mask.gameObject.activeInHierarchy, "The original installation mask remained after selection/cancel.");
                yield return new WaitForEndOfFrame(); Capture("skills-original-install-after-" + attempt);
                if (attempt < 2) Require(progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].ActiveSkills.SequenceEqual(before),
                    "Cancelling installation modified the skill plan.");
            }
            Require(progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].ActiveSkills[0] == skillId,
                "Clicking the original highlighted skill slot did not install its selected skill.");
            Require(FindObjectsOfType<RawImage>().Any(value => value.name == "HeadImg" && value.isActiveAndEnabled),
                "The shared player header did not return after installation.");
            yield return new WaitForEndOfFrame(); Capture("skills-original-install-complete");
        }

        private IEnumerator ValidateOriginalBlessing(OfflineCharacterProgressionService progression, CharacterSkillPanel panel)
        {
            var player = progression.Player;
            var profile = player.Skills.Clone();
            int oldSubRank = player.SubRank, oldRank = player.Rank, oldSeason = player.SeasonRank, oldLevel = player.Level;
            long oldExperience = player.BlessingExperience;
            bool oldActive = player.HasActiveSeasonRecord;
            try
            {
                player.SubRank = 10; player.Rank = player.SeasonRank = 4; player.Level = 100; player.HasActiveSeasonRecord = true;
                player.BlessingExperience = progression.Catalog.BlessLevels.Single(row => row.Rank == 4 && row.BlessLevel == 70).Exp;
                player.Skills.ActiveSlotLevels = new[] { 100, 100, 100, 100 };
                player.Skills.PassiveSlotLevels = new[] { 100, 100, 100, 100 };
                player.Skills.BlessRecords = new System.Collections.Generic.List<OfflineSkillBlessRecord> {
                    new OfflineSkillBlessRecord { SeasonId = 4, ActiveSlotLevels = new[] { 70, 0, 0, 0 }, PassiveSlotLevels = new int[4] } };
                panel.ReloadProfile(); yield return null;
                var active = FindObjectsOfType<RectTransform>().Single(value => value.name == "ActiveSkill" && value.parent.name == "SkillContentPanel");
                var level = active.GetChild(0).Find("BgLevel/LevelText").GetComponent<TMP_Text>();
                Require(level.text == "170级" && level.enableVertexGradient && level.GetComponent<TMPEffect>().outline,
                    "The main skill slot omitted current-season blessing levels or its original gradient/outline.");
                yield return new WaitForEndOfFrame(); Capture("skills-blessing-slot");
                Click(active.GetChild(0).Find("Slot").GetComponent<Button>()); yield return new WaitForSecondsRealtime(.4f);
                Click(ButtonNamed("UpgradeBtn")); yield return new WaitForSecondsRealtime(.5f);
                Require(FindObjectsOfType<TMP_Text>().Any(value => value.name == "TxtText" && value.isActiveAndEnabled && value.text.Contains("解锁")),
                    "The original next-season cultivation limit did not display.");
                yield return new WaitForEndOfFrame(); Capture("skills-cultivation-season-limit");
                Require(panel.HandleEscape(), "The season-limited cultivation did not close."); yield return null;
                player.Skills.BlessRecords[0].ActiveSlotLevels[0] = 0;
                player.Skills.PassiveSlotLevels[3] = 99;
                panel.ReloadProfile(); yield return null;
                Click(active.GetChild(0).Find("Slot").GetComponent<Button>()); yield return new WaitForSecondsRealtime(.4f);
                Click(ButtonNamed("UpgradeBtn")); yield return new WaitForSecondsRealtime(.5f);
                yield return new WaitForEndOfFrame(); Capture("skills-cultivation-blessing-limit");
                Require(FindObjectsOfType<TMP_Text>().Any(value => value.name == "TxtText" && value.isActiveAndEnabled && value.text.Contains("所有")),
                    "The original all-eight-slots blessing prerequisite did not display: " +
                    JsonUtility.ToJson(progression.SkillContext()) + "; " + string.Join(";", FindObjectsOfType<TMP_Text>()
                        .Where(value => value.name == "TxtText" && value.isActiveAndEnabled).Select(value => value.text)));
                Require(panel.HandleEscape(), "The blessing-limited cultivation did not close."); yield return null;
            }
            finally
            {
                player.SubRank = oldSubRank; player.Rank = oldRank; player.SeasonRank = oldSeason; player.Level = oldLevel;
                player.BlessingExperience = oldExperience; player.HasActiveSeasonRecord = oldActive; player.Skills = profile;
                panel.ReloadProfile();
            }
        }
    }
}

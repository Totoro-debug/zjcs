using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Character
{
    public sealed partial class SkillPlayerValidation
    {
        private IEnumerator RunProfessionAvailabilityFlow()
        {
            AudioListener.volume = 0f;
            deadline = Time.realtimeSinceStartup + 240f;
            int profession = int.Parse(Environment.GetEnvironmentVariable("ZJCS_SKILLS_PROFESSION") ?? "2");
            Require(new[] { 1, 10, 2, 6, 11, 15 }.Contains(profession), "Profession fixture is outside the requested scope.");
            while (GameObject.Find("OfficialSaveSelectionPanel") == null) yield return null;
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            var rules = assets.LoadSkillRules();
            int[] kinds = { 100, 101 };
            int[] valid = kinds.Select(kind => rules.Skills.Where(row => row.ItemType == kind
                && row.AvailableWithoutProfession && row.AvailableProfessions.Contains(profession))
                .OrderBy(row => row.ClassId).Last().ClassId).ToArray();
            int[] foreign = kinds.Select(kind => rules.Skills.First(row => row.ItemType == kind
                && row.AvailableWithoutProfession && !row.AvailableProfessions.Contains(profession)).ClassId).ToArray();
            var slots = new ProfileSlotCatalog(saveRoot);
            string slot = slots.CreateSlot();
            var save = new OfflineMainlineSave();
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "职业技能验证";
            save.Player.Level = 50; save.Player.Rank = 2; save.Player.SubRank = 5; save.Player.MaxProfessionRank = 2;
            save.Player.Experience = assets.Load().CumulativeLevelExperience[50];
            save.Player.Wardrobe.Profession = profession;
            foreach (int id in valid.Concat(foreign)) save.Player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = id, Rank = 1 });
            save.Player.Skills.Plans[6].ActiveSkills[0] = foreign[0];
            save.Player.Skills.Plans[6].PassiveSkills[0] = foreign[1];
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            foreach (Canvas other in FindObjectsOfType<Canvas>()) other.gameObject.SetActive(false);
            foreach (Camera other in FindObjectsOfType<Camera>()) other.enabled = false;
            var page = new GameObject("ProfessionSkillCharacterEntry", typeof(RectTransform));
            var runtime = page.AddComponent<CharacterFeatureRuntime>();
            runtime.OpenProfile(slot, saveRoot);
            var panel = page.GetComponentInChildren<CharacterSkillPanel>(true);
            yield return null;
            Click(ButtonNamed("CharacterPanelSkillBtn"));
            yield return new WaitForSecondsRealtime(.8f);
            for (int index = 0; index < kinds.Length; index++)
            {
                Click(ButtonContaining(index == 0 ? "ActiveSkillTab" : "PassiveSkillTab"));
                yield return new WaitForSecondsRealtime(.4f);
                var all = FindObjectsOfType<Toggle>().Single(value => value.name == "ShowAll");
                if (all.isOn) Click(all);
                yield return null;
                Require(!FindObjectsOfType<Transform>().Any(value => value.name == "SkillListItem_" + foreign[index]),
                    "Filtered skill list contains another branch's starting skill.");
                Require(FindObjectsOfType<Transform>().Any(value => value.name == "SkillListItem_" + valid[index]),
                    "Filtered list omitted the inherited starting skill.");
                yield return new WaitForEndOfFrame(); Capture("profession-" + kinds[index] + "-filtered");
                Click(all);
                yield return new WaitForSecondsRealtime(.3f);
                var foreignRow = ButtonNamed("SkillListItem_" + foreign[index]);
                Require(foreignRow.GetComponentsInChildren<Transform>().Any(value => value.name == "OriginalSkillAttachment_10"),
                    "All-skills list omitted the original profession mismatch attachment.");
                yield return new WaitForEndOfFrame(); Capture("profession-" + kinds[index] + "-all");
                string before = JsonUtility.ToJson(runtime.Progression.Player.Skills);
                string diskBefore = File.ReadAllText(slots.GetProgressPath(slot));
                Click(foreignRow);
                // The original popup ignores mask-close clicks for its first half second.
                yield return new WaitForSecondsRealtime(.8f);
                Require(!FindObjectsOfType<Button>().Any(value => value.name == "EquipBtn" || value.name == "ReplaceBtn"),
                    "Foreign skill detail exposes an original profession-restricted equip action.");
                Require(ButtonNamed("UpgradeBtn") != null, "Foreign skill cultivation was incorrectly hidden with equip actions.");
                yield return new WaitForEndOfFrame(); Capture("profession-" + kinds[index] + "-rejected");
                Click(FindObjectsOfType<Button>().Single(value => value.name == "BlurMask" && value.isActiveAndEnabled), new Vector2(8f, Screen.height * .5f));
                yield return new WaitForSecondsRealtime(.3f);
                Require(!FindObjectsOfType<Transform>().Any(value => value.name == "skill_panel_detail"),
                    "Original mask click did not dismiss the foreign skill detail.");
                var group = FindObjectsOfType<RectTransform>().Single(value => value.name == (index == 0 ? "ActiveSkill" : "PassiveSkill")
                    && value.parent.name == "SkillContentPanel");
                Require(JsonUtility.ToJson(runtime.Progression.Player.Skills) == before
                    && File.ReadAllText(slots.GetProgressPath(slot)) == diskBefore,
                    "Rejected foreign skill changed a saved plan or inventory.");
                Click(ButtonNamed("SkillListItem_" + valid[index]));
                yield return new WaitForSecondsRealtime(.3f);
                Click(ButtonNamed("EquipBtn"));
                yield return new WaitForSecondsRealtime(.3f);
                Click(group.GetChild(0).Find("Slot").GetComponent<Button>());
                yield return new WaitForSecondsRealtime(.4f);
                var disk = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot))).Player.Skills;
                Require((index == 0 ? disk.Plans[0].ActiveSkills : disk.Plans[0].PassiveSkills)[0] == valid[index]
                    && disk.Plans[6].ActiveSkills[0] == foreign[0] && disk.Plans[6].PassiveSkills[0] == foreign[1],
                    "Valid inherited equip did not persist or removed an old foreign plan.");
                runtime.ReloadProfile();
                yield return new WaitForSecondsRealtime(.3f);
                Require(JsonUtility.ToJson(runtime.Progression.Player.Skills) == JsonUtility.ToJson(disk), "Saved skill plans changed on reload.");
                yield return new WaitForEndOfFrame(); Capture("profession-" + kinds[index] + "-installed");
                Debug.Log("[Skill profession flow] profession=" + profession + " kind=" + kinds[index]
                    + " inherited=" + valid[index] + " foreign=" + foreign[index] + "; filter, attachment, unavailable detail actions, install and reload passed.");
            }
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result { passed = true }, true));
            Application.logMessageReceived -= CaptureFailure;
            Destroy(page);
            yield return null;
            Application.Quit(0);
        }
    }
}

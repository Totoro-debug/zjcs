#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZJCS_MAINLINE_MODULE_VALIDATION
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11PlayModeAcceptance
    {
        [UnityEditor.MenuItem("Zjcs/Validate/Map11 Lost Goddess Result")]
        public static void RunLostGoddess()
        {
            SessionState.SetBool(Key + ".ui", false);
            SessionState.SetBool(Key + ".background", false);
            SessionState.SetBool(Key + ".lost", true);
            try { RunCore(); }
            catch (Exception error) { Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); }
        }

        private static IEnumerator VerifyLostGoddess(Transform canvas)
        {
            yield return new WaitForSecondsRealtime(2);
            Require(runtime.GetComponentInChildren<OfficialMap10EntityView>().EntityCount == 1803, "Map11 entity count");
            OfflineMainlineProgressSession save = Field<OfflineMainlineProgressSession>("progressionSave");
            OfflineWorldExplorationService service = new OfflineWorldExplorationService(save);
            var group = service.Catalog.Data.entityGroups.First(value => value.Entitys.Contains(8364));
            var map = Field<OfficialWorldMapSession>("session");
            MapGridPoint current = Field<MapGridPoint>("currentGrid");
            OfficialMapNavigationView.TryGetCell(map.Definition.Navigation, current.X, current.Y, out int area, out _);
            int region = 0;
            MapGridPoint spawn = new[] { new MapGridPoint(current.X + 1, current.Y), new MapGridPoint(current.X - 1, current.Y),
                new MapGridPoint(current.X, current.Y + 1), new MapGridPoint(current.X, current.Y - 1) }.First(map.Movement.IsFit);
            int serial = save.Save.World.Exploration.NextSerial;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Require(save.ApplyInventoryTransaction("map11-lost-goddess-ui-fixture", state =>
            {
                state.World.Exploration.Entities.Add(new OfflineExploreEntity
                {
                    Serial = state.World.Exploration.NextSerial++, MapClassId = 11, Area = area, Region = region,
                    X = spawn.X, Y = spawn.Y, Group = group.Group, Variant = group.Id, EntityClassId = 8364,
                    State = service.Catalog.Parameters(8364).PublicDefaultState, CreatedAt = now, ExpiresAt = now + 600
                });
            }).IsApplied, "Lost goddess fixture transaction");
            Invoke("SynchronizeExploreEntities");
            Invoke("RefreshEntityActivity");
            var generated = runtime.GetComponentInChildren<OfficialMap10EntityView>().FindEntityObject(1000000000 + serial);
            Require(generated != null && generated.activeInHierarchy, "Generated lost goddess entity presentation");
            yield return SimulateEntityClick(1000000000 + serial);
            OfficialLostGoddessResultPopup popup = UnityEngine.Object.FindObjectOfType<OfficialLostGoddessResultPopup>();
            float popupDeadline = Time.realtimeSinceStartup + 10;
            while ((popup == null || !popup.IsShown) && Time.realtimeSinceStartup < popupDeadline)
            {
                yield return null;
                popup = UnityEngine.Object.FindObjectOfType<OfficialLostGoddessResultPopup>();
            }
            Require(popup != null && popup.IsShown, "Official lost goddess result window shown");
            Require(save.Save.World.Exploration.Entities.Single(value => value.Serial == serial).Completed,
                "Lost goddess completion persisted before result close");
            Capture("lost-goddess-result");
            Button close = popup.GetComponentsInChildren<Button>(true).FirstOrDefault(button => button.gameObject.name.StartsWith("CloseObj", StringComparison.Ordinal));
            Require(close != null, "Official lost goddess result close button");
            yield return SimulatePointerClick(close);
            yield return new WaitForSecondsRealtime(.8f);
            Require(UnityEngine.Object.FindObjectOfType<OfficialLostGoddessResultPopup>() == null,
                "Lost goddess result closes through the official button");

            OfflineSkillRules skillRules = OfficialCharacterAssetCatalog.LoadDefault().LoadSkillRules();
            var rankSkill = skillRules.Skills.First(value => value.MaximumRank > 1);
            var skillAward = new OfflineExploreLostGoddessAward
            {
                Type = 2, SkillId = rankSkill.ClassId, SourceRank = 1, TargetRank = 2
            };
            OfficialMap11UiData uiData = UnityEngine.JsonUtility.FromJson<OfficialMap11UiData>(
                Resources.Load<TextAsset>("Map11/UI/catalog").text);
            OfficialLostGoddessResultPopup skillPopup = OfficialLostGoddessResultPopup.Open(canvas,
                skillAward, Field<OfflineCharacterProgressionService>("progression"),
                key => WorldMultiLanguageText.Localize == null
                    ? uiData.texts.FirstOrDefault(value => value.key == key)?.value ?? key
                    : WorldMultiLanguageText.Localize(key), null);
            float skillPopupDeadline = Time.realtimeSinceStartup + 10;
            while ((skillPopup == null || !skillPopup.IsShown) && Time.realtimeSinceStartup < skillPopupDeadline)
            {
                yield return null;
            }
            Require(skillPopup != null && skillPopup.IsShown,
                "Original skill-rank result window completed its official show transition");
            Transform skillResult = skillPopup.transform.Find("LostGoddessResultSkillRankDialog");
            Require(skillResult != null, "Original skill-rank result prefab root");
            RequireSkillRankIcon(skillResult, "Content/MiddleGo/Props/AniSkill/UpgradeResultOld/SkillSlot/ItemIconSkill");
            RequireSkillRankIcon(skillResult, "Content/MiddleGo/Props/AniSkill/UpgradeResult/SkillSlot/ItemIconSkill");
            yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForEndOfFrame();
            Capture("lost-goddess-skill-rank-result");
            Button skillClose = skillPopup.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.isActiveAndEnabled && button.interactable
                    && button.gameObject.name.StartsWith("CloseObj", StringComparison.Ordinal));
            Require(skillClose != null, "Official skill-rank result close button");
            yield return SimulatePointerClick(skillClose);
            yield return new WaitForSecondsRealtime(.8f);
            Require(UnityEngine.Object.FindObjectOfType<OfficialLostGoddessResultPopup>() == null,
                "Skill-rank result closes through the official button");

            OfflineMainlineProgressSession cold = OfflineMainlineProgressSession.OpenCharacter(
                new ProfileSlotCatalog(Root), Slot);
            var persistedAward = cold.Save.World.Exploration.Entities.Single(value => value.Serial == serial).LostGoddessAward;
            Require(persistedAward != null, "Lost goddess reward persisted to the cold save");
            Debug.Log("Map11 skill-rank result passed: original ItemIconSkill instances show official icon, quality, and stars; actual 8364 interaction, UGUI close, and cold-save recovery passed.");
            Finish();
        }

        private static void RequireSkillRankIcon(Transform root, string path)
        {
            Transform iconRoot = root.Find(path);
            Require(iconRoot != null, "Original skill icon prefab at " + path);
            Image skill = iconRoot.Find("Content/mask/icon")?.GetComponent<Image>();
            Image quality = iconRoot.Find("Content/QualityImg")?.GetComponent<Image>();
            Transform starRoot = iconRoot.Find("Content/SkillStarInfo");
            Component stars = starRoot == null ? null : starRoot.GetComponents<MonoBehaviour>()
                .FirstOrDefault(value => value != null && value.GetType().Name == "StarMiniInfo");
            Transform starList = starRoot?.Find("StarList");
            Require(skill != null && skill.sprite != null, "Official skill icon sprite at " + path);
            Require(quality != null && quality.sprite != null, "Official skill quality sprite at " + path);
            Require(stars != null && starList != null && starList.childCount > 0
                && Enumerable.Range(0, starList.childCount).Any(index => starList.GetChild(index).gameObject.activeSelf),
                "Official skill star component and visible star at " + path);
        }

        private static IEnumerator SimulateEntityClick(int placement)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            Camera camera = Field<Camera>("worldCamera");
            GameObject actor = Field<GameObject>("avatar");
            while (Field<object>("movementPath") != null || Vector2.Distance(camera.transform.position, actor.transform.position) > 0.005f)
            {
                Require(Time.realtimeSinceStartup < deadline, "Map input camera settled");
                yield return null;
            }
            Collider2D collider = runtime.GetComponentInChildren<OfficialMap10EntityView>()
                .FindEntityObject(placement).GetComponentInChildren<Collider2D>();
            Vector2 point = camera.WorldToScreenPoint(collider.bounds.center);
            bool consumed = false;
            runtime.PointerPressProvider = () =>
            {
                if (consumed) return null;
                consumed = true;
                return point;
            };
            float clickDeadline = Time.realtimeSinceStartup + 2;
            while (!consumed && Time.realtimeSinceStartup < clickDeadline) yield return null;
            Require(consumed, "Map entity simulated pointer press");
            runtime.PointerPressProvider = null;
            yield return new WaitForSecondsRealtime(.5f);
        }

        private static void Finish()
        {
            SessionState.SetInt(Key + ".result", 0);
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }
}
#endif

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        private static IEnumerator VerifyRuntimeSteleSteps(string root, string slot, Transform canvas, string output)
        {
            OfficialMap12SteleCatalog catalog = OfficialMap12SteleCatalog.Load();
            var stele = catalog.Data.Steles.Single(value => value.PlacementId == 3013);
            foreach (var maximum in RuntimeField<Dictionary<int, int>>("regionPointLimits"))
                RuntimeField<Dictionary<int, int>>("regionPoints")[maximum.Key] = maximum.Value;
            RuntimeCall("RefreshEntityActivity");

            MapGridPoint position = RuntimeField<OfflineMapPuzzleSession>("puzzles").Position(stele.PlacementId);
            var movement = RuntimeField<OfficialWorldMapSession>("session").Movement;
            MapGridPoint adjacent = new[] { new MapGridPoint(position.X, position.Y - 1),
                new MapGridPoint(position.X - 1, position.Y), new MapGridPoint(position.X, position.Y + 1),
                new MapGridPoint(position.X + 1, position.Y) }.First(movement.IsFit);
            RuntimeCall("SetAvatarPosition", adjacent);
            Camera camera = RuntimeField<Camera>("worldCamera");
            camera.transform.position = RuntimeField<GameObject>("avatar").transform.position + new Vector3(0, 0, -20);
            yield return new WaitForSecondsRealtime(.5f);
            GameObject target = map12Runtime.GetComponentInChildren<OfficialMap10EntityView>().FindEntityObject(stele.PlacementId);
            Vector2? pointer = camera.WorldToScreenPoint(target.transform.position);
            map12Runtime.PointerPressProvider = () => { Vector2? next = pointer; pointer = null; return next; };
            yield return new WaitForSecondsRealtime(.7f);
            GameObject dialog = RuntimeField<GameObject>("map12SteleDialog");
            var stageUi = dialog == null ? null : dialog.GetComponent<WorldBattleSteleStageUI>();
            if (stageUi == null || !dialog.activeInHierarchy)
                throw new InvalidDataException("Map12 placement 3013 did not open the original battle stele window from a simulated map click.");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "stele-dialog.png"));

            yield return new WaitForEndOfFrame();
            SimulateUiClick(stageUi.mTeamBtn, stageUi.mTeamBtn.GetComponentInParent<Canvas>().worldCamera);
            yield return new WaitForSecondsRealtime(.7f);
            GameObject picker = RuntimeField<GameObject>("map12SteleNpcPicker");
            if (picker == null || !picker.activeInHierarchy)
                throw new InvalidDataException("Original NPC invitation window did not open from the stele team button.");
            Toggle[] candidates = picker.GetComponentsInChildren<Toggle>(true)
                .Where(value => value.interactable && Map12SteleNpcRowName(value) != null)
                .OrderBy(Map12SteleNpcRowName).ToArray();
            if (candidates.Length == 0)
                throw new InvalidDataException("The isolated profile has no NPC satisfying the original assistance conditions and local model availability.");
            OfficialBattleNpcPortraitCatalog portraits = OfficialBattleNpcPortraitCatalog.Load();
            int missingPortraits = 0;
            foreach (Transform row in picker.GetComponentsInChildren<Transform>(true)
                .Where(value => value.name.StartsWith("Npc_", StringComparison.Ordinal)))
            {
                TextMeshProUGUI name = row.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .FirstOrDefault(value => value.gameObject.name == "NameTxt");
                if (name == null || string.IsNullOrWhiteSpace(name.text) || name.text == "六个字的名字")
                    throw new InvalidDataException("Original NPC assistance name was not bound to its row: " + row.name);
                Graphic portrait = row.GetComponentsInChildren<Graphic>(true)
                    .FirstOrDefault(value => value.gameObject.name == "HeadImg");
                if (portrait == null)
                    throw new InvalidDataException("Original NPC assistance portrait control is missing: " + row.name);
                int rowNpcId = int.Parse(row.name.Substring("Npc_".Length));
                Sprite expectedPortrait = portraits.Find(rowNpcId);
                if (expectedPortrait == null)
                {
                    missingPortraits++;
                    continue;
                }
                if (portrait is Image image && image.sprite != expectedPortrait
                    || portrait is RawImage rawImage && rawImage.texture != expectedPortrait.texture)
                    throw new InvalidDataException("Official NPC assistance portrait was not bound to its row: " + row.name);
            }
            Debug.Log("Map12 NPC assistance picker includes official localized names; missing configured portraits in selected source: "
                + missingPortraits + ".");
            if (picker.GetComponentsInChildren<WorldMultiLanguageText>(true).Any(value =>
                value.myKey == "teaminvitepanel_no_friend" && value.gameObject.activeInHierarchy))
                throw new InvalidDataException("The online friend empty state remained visible over the NPC assistance rows.");
            LoopVerticalScrollRect pickerScroll = picker.transform.Find(
                "BgPanel1/Content/MiddleGo/FriendRobotGo/Scroll")?.GetComponent<LoopVerticalScrollRect>();
            if (pickerScroll == null) throw new InvalidDataException("Original NPC invitation loop scroll is missing.");
            GameObject fallbackProbe = UnityEngine.Object.Instantiate(
                Resources.Load<GameObject>("Map12/Team/TeamInvitePanelRobotItem"), picker.transform, false);
            fallbackProbe.SetActive(false);
            OfficialBattleNpcPortraitCatalog emptyPortraitCatalog = ScriptableObject.CreateInstance<OfficialBattleNpcPortraitCatalog>();
            object npcCatalog = RuntimeField<object>("map12NpcCatalog");
            string npcNameKey = (string)npcCatalog.GetType().GetMethod("NpcNameKey").Invoke(npcCatalog, new object[] { 11015 });
            string expectedNpcName = RuntimeField<Dictionary<string, string>>("map12NpcAssistanceNames")[npcNameKey];
            var bindRow = map12Runtime.GetType().GetMethod("BindMap12SteleNpcRow",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (bindRow == null) throw new InvalidDataException("Map12 offline NPC row binder is missing.");
            bindRow.Invoke(map12Runtime, new object[] { fallbackProbe.transform, 11015, false, false,
                false, false, false, 3, stele, emptyPortraitCatalog });
            Transform fallbackHead = fallbackProbe.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name == "HeadImg");
            TMP_Text fallbackText = fallbackHead == null ? null
                : fallbackHead.GetComponentInChildren<TMP_Text>(true);
            Graphic fallbackImage = fallbackHead == null ? null : fallbackHead.GetComponent<Graphic>();
            if (fallbackText == null || fallbackText.text != expectedNpcName || fallbackImage == null || fallbackImage.enabled)
                throw new InvalidDataException("Missing original 11015 NPC portrait did not fall back to its official localized name.");
            Debug.Log("Map12 missing 11015 portrait used its official localized name in the original HeadImg slot.");
            UnityEngine.Object.DestroyImmediate(fallbackProbe);
            UnityEngine.Object.DestroyImmediate(emptyPortraitCatalog);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(pickerScroll.content);
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "stele-npc-picker.png"));
            yield return new WaitForSecondsRealtime(.75f);
            Toggle candidate = null;
            Camera pickerCamera = picker.GetComponentInParent<Canvas>()?.worldCamera;
            Vector2 candidatePosition = default;
            int[] eligibleNpcIds = candidates.Select(value => int.Parse(Map12SteleNpcRowName(value)
                .Substring("Npc_".Length))).Distinct().ToArray();
            foreach (int candidateNpcId in eligibleNpcIds)
                if (TryRevealMap12SteleNpc(candidateNpcId, pickerScroll, pickerCamera, out candidate, out candidatePosition))
                    break;
            if (candidate == null)
                throw new InvalidDataException("No eligible official NPC row could be revealed in the original invitation list.");
            int npcId = int.Parse(Map12SteleNpcRowName(candidate).Substring("Npc_".Length));
            bool toggleChanged = false;
            candidate.onValueChanged.AddListener(_ => toggleChanged = true);
            SimulateMap12SteleToggleClick(candidate, pickerCamera, candidatePosition);
            yield return new WaitForSecondsRealtime(.7f);
            OfflineNpcAssistanceProgress assist = OfflineNpcAssistanceProgress.Open(new ProfileSlotCatalog(root), slot);
            string assistKey = "map12-stele:" + stele.PlacementId;
            if (!assist.Selected(WorldBattleSteleTeamTarget.TargetCatalog, assistKey).SequenceEqual(new[] { npcId }))
            {
                OfflineNpcAssistanceProgress liveAssist = RuntimeField<OfflineNpcAssistanceProgress>("map12NpcProgress");
                string savePath = Path.Combine(new ProfileSlotCatalog(root).GetSlotDirectory(slot), "npc-assistance-save.json");
                throw new InvalidDataException("Simulated NPC selection was not saved for this Map12 stele: npc="
                    + npcId + "; toggleChanged=" + toggleChanged + "; toggleIsOn=" + candidate.isOn
                    + "; liveSelection=" + string.Join(",", liveAssist.Selected(WorldBattleSteleTeamTarget.TargetCatalog, assistKey))
                    + "; savePath=" + savePath);
            }
            picker = RuntimeField<GameObject>("map12SteleNpcPicker");
            Button confirm = picker == null ? null : picker.transform.Find(
                "BgPanel1/Content/MiddleGo/FriendRobotGo/CommonBtn_12")?.GetComponentInChildren<Button>(true);
            confirm ??= picker == null ? null : picker.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == "CommonBtn_12" && value.gameObject.activeInHierarchy);
            if (confirm == null) throw new InvalidDataException("Original NPC invitation confirm button disappeared after selection.");
            yield return new WaitForEndOfFrame();
            SimulateUiClick(confirm, confirm.GetComponentInParent<Canvas>().worldCamera);
            yield return new WaitForSecondsRealtime(.7f);

            Map10RuntimeStage stage = RuntimeField<Dictionary<int, Map10RuntimeStage>>("stages")[stele.StageId];
            Map10RuntimeRoster roster = RuntimeField<Dictionary<int, Map10RuntimeRoster>>("rosters")[stele.StageId];
            Map10RuntimeBattleScene scene = RuntimeField<Dictionary<int, Map10RuntimeBattleScene>>("battleScenes")[stage.GameId];
            object entity = RuntimeField<System.Collections.IDictionary>("mapEntities")[stele.PlacementId];
            var checkpoint = (FightBattleCheckpoint)RuntimeCall("BuildCheckpoint", entity, stage, roster, scene);
            FightBattleUnitState npc = checkpoint.Units.SingleOrDefault(value => value.NpcClassId == npcId);
            if (npc == null || npc.Side != 1 || npc.MasterId != 1 || npc.Id <= 1)
                throw new InvalidDataException("The selected local NPC was not assembled as an official ally in the actual stele battle checkpoint.");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "stele-assist-selected.png"));
            Debug.Log("Map12 runtime stele passed original map click, native dialog raycast, NPC invitation selection/save, confirmation, and official battle checkpoint assembly for NPC " + npcId + ".");
        }

        private static string Map12SteleNpcRowName(Toggle toggle)
        {
            for (Transform current = toggle == null ? null : toggle.transform; current != null; current = current.parent)
                if (current.name.StartsWith("Npc_", StringComparison.Ordinal)) return current.name;
            return null;
        }

        private static bool TryRevealMap12SteleNpc(int npcId, LoopVerticalScrollRect scroll, Camera camera,
            out Toggle candidate, out Vector2 clickPosition)
        {
            candidate = null;
            clickPosition = default;
            RectTransform viewport = scroll.viewport;
            if (viewport == null) return false;
            for (int i = 0; i <= 20; i++)
            {
                scroll.verticalNormalizedPosition = 1f - i / 20f;
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                Transform row = scroll.content.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(value => value.name == "Npc_" + npcId && value.gameObject.activeInHierarchy);
                Toggle current = row == null ? null : row.GetComponent<Toggle>() ?? row.GetComponentInChildren<Toggle>(true);
                if (current == null || !current.isActiveAndEnabled || !current.IsInteractable()) continue;
                RectTransform target = (current.targetGraphic == null ? current.transform : current.targetGraphic.transform)
                    as RectTransform;
                if (target == null) continue;
                int step = Mathf.Max(24, Mathf.Min(Screen.width, Screen.height) / 20);
                var pointer = new PointerEventData(EventSystem.current)
                    { button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>();
                for (int y = step / 2; y < Screen.height; y += step)
                for (int x = step / 2; x < Screen.width; x += step)
                {
                    pointer.position = new Vector2(x, y);
                    hits.Clear();
                    EventSystem.current.RaycastAll(pointer, hits);
                    GameObject hit = hits.FirstOrDefault().gameObject;
                    if (hit == null || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) != current.gameObject)
                        continue;
                    candidate = current;
                    clickPosition = pointer.position;
                    return true;
                }
            }
            Debug.LogError("Map12 NPC had no topmost raycast position after scrolling: npc=" + npcId
                + "; viewport=" + DescribeMap12Rect(viewport, camera)
                + "; content=" + DescribeMap12Rect(scroll.content, camera)
                + "; scroll=" + DescribeMap12Rect(scroll.transform as RectTransform, camera)
                + "; resolution=" + Screen.width + "x" + Screen.height);
            return false;
        }

        private static string DescribeMap12Rect(RectTransform rect, Camera camera)
        {
            if (rect == null) return "missing";
            Vector3 center = rect.TransformPoint(rect.rect.center);
            return HierarchyPath(rect) + " rect=" + rect.rect + " center=" + center
                + " screen=" + RectTransformUtility.WorldToScreenPoint(camera, center)
                + " scale=" + rect.lossyScale;
        }

        private static void SimulateMap12SteleToggleClick(Toggle expected, Camera camera, Vector2 position)
        {
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
                { position = position, button = PointerEventData.InputButton.Left, clickCount = 1 };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            GameObject hit = hits.FirstOrDefault().gameObject;
            if (hit == null || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) != expected.gameObject)
                throw new InvalidDataException("Original NPC row was occluded or missed by simulated pointer input: " + expected.name
                    + "; position=" + position + "; hits=" + string.Join(",", hits.Take(6).Select(value => value.gameObject.name))
                    + "; hitPaths=" + string.Join(",", hits.Take(6).Select(value => HierarchyPath(value.gameObject.transform)))
                    + "; target=" + (expected.targetGraphic == null ? "none" : HierarchyPath(expected.targetGraphic.transform))
                    + "; canvas=" + expected.GetComponentInParent<Canvas>().renderMode);
            pointer.pointerCurrentRaycast = hits[0];
            pointer.pressPosition = position;
            pointer.pointerPressRaycast = hits[0];
            pointer.pointerPress = expected.gameObject;
            pointer.pointerPressRaycast = hits[0];
            ExecuteEvents.Execute(expected.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(expected.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(expected.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static string HierarchyPath(Transform value)
        {
            string result = value == null ? string.Empty : value.name;
            for (Transform parent = value == null ? null : value.parent; parent != null; parent = parent.parent)
                result = parent.name + "/" + result;
            return result;
        }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11PlayModeAcceptance
    {
        public static void RunUiBackground()
        {
            SessionState.SetBool(Key + ".ui", true);
            SessionState.SetBool(Key + ".background", true);
            try { RunCore(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void ImportAndRunUiBackground()
        {
            try { OfficialMap11UiTools.Import(); RunUiBackground(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static Rect ScreenRect(GameObject node)
        {
            var corners = new Vector3[4];
            ((RectTransform)node.transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static IEnumerator VerifyUiPresentation()
        {
            yield return new WaitForSecondsRealtime(1);
            OfficialMap10UiRenderer renderer = Field<OfficialMap10UiRenderer>("ui");
            GameObject hud = renderer.Find("WorldCanvas", "/WorldCanvas");
            Rect hudRect = ScreenRect(hud);
            Transform inlineFunctions = hud.transform.Find("Content/Content/WorldPanel/Anim/Right/FunctionEntryGroup/FunctionEntryFunc/Content/Panel");
            Require(Mathf.Approximately(inlineFunctions.localScale.x, 0) && Mathf.Approximately(inlineFunctions.localScale.y, 0),
                "Original folded Animator pose hides the unused inline panel");
            yield return CaptureStatueFrame("ui-hud");
            string result = "HUD rect=" + hudRect + "; scale=" + hud.transform.lossyScale;
            Debug.Log("Map11 UI geometry: " + result);
            Require(hudRect.width >= Screen.width * .99f && hudRect.height >= Screen.height * .99f,
                "Official WorldCanvas must cover the viewport: " + result);
            Invoke("ToggleExpandedMinimap");
            yield return new WaitForSecondsRealtime(.5f);
            Require(Field<GameObject>("fullMiniMapPanel") != null, "Official full minimap prefab opens");
            var tabs = Field<IDictionary>("nativeMiniMapTabs");
            Require(tabs.Count == 1 && tabs.Contains(11) && !tabs.Contains(10), "Unvisited maps have no native tab");
            yield return CaptureStatueFrame("ui-minimap-full");
            Invoke("SetMiniMapFilterExpanded", true);
            yield return null;
            yield return CaptureStatueFrame("ui-minimap-filter");
            var filters = Field<IDictionary>("miniMapFilters");
            bool selected = (bool)filters["MiniMapGoddess"];
            var filterItems = Field<IDictionary>("miniMapFilterItems");
            ((GameObject)filterItems["MiniMapGoddess"]).GetComponentInChildren<Button>(true).onClick.Invoke();
            Require((bool)filters["MiniMapGoddess"] != selected, "Native filter button changes the goddess mark selection");
            Invoke("CloseNativeFullMiniMap");
            yield return null;
            filters.Clear(); // Force the real local JSON reader rather than its in-memory selection.
            Invoke("ToggleExpandedMinimap");
            yield return null;
            Require((bool)filters["MiniMapGoddess"] != selected, "Filter selection restores from local save");
            filterItems = Field<IDictionary>("miniMapFilterItems");
            ((GameObject)filterItems["MiniMapGoddess"]).GetComponentInChildren<Button>(true).onClick.Invoke();
            Invoke("CloseNativeFullMiniMap");
            var progress = Field<Zjcs.Offline.Progression.OfflineMainlineProgressSession>("progressionSave");
            progress.InitializeMap(10, new[] { 0, 1, 2, 3, 4 }); // Explicit visited-map fixture.
            int mapCount = progress.Save.World.Maps.Count;
            Vector3 avatarPosition = Field<GameObject>("avatar").transform.position;
            Invoke("ToggleExpandedMinimap");
            yield return null;
            tabs = Field<IDictionary>("nativeMiniMapTabs");
            Require(tabs.Count == 2 && tabs.Contains(10), "Visited maps in the official group acquire native tabs");
            ((GameObject)tabs[10]).GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            Require(Field<int>("fullMiniMapClassId") == 10 && Field<GameObject>("browsedMiniMapGeometry").activeInHierarchy
                && !Field<GameObject>("currentMiniMapGeometry").activeSelf && !Field<GameObject>("nativeMiniMapCharacter").activeSelf,
                "Native tab switches geometry and hides current-map character");
            Require(Field<GameObject>("avatar").transform.position == avatarPosition && progress.Save.World.Maps.Count == mapCount,
                "Browsing never teleports the character or adds map records");
            Capture("ui-minimap-map10");
            yield return new WaitForEndOfFrame();
            yield return null;
            ((GameObject)tabs[11]).GetComponent<Button>().onClick.Invoke();
            yield return null;
            Require(Field<int>("fullMiniMapClassId") == 11 && Field<GameObject>("currentMiniMapGeometry").activeSelf,
                "Native tab returns to the current map");
            ((GameObject)tabs[10]).GetComponent<Button>().onClick.Invoke();
            Invoke("CloseNativeFullMiniMap");
            yield return null;
            Require(Field<GameObject>("browsedMiniMapGeometry") == null && Field<GameObject>("currentMiniMapGeometry").activeSelf
                && Field<GameObject>("nativeMiniMapCharacter").activeSelf, "Closing another map restores the circular map scene");
            Invoke("OpenNativeWorldFunctions");
            yield return null;
            yield return CaptureStatueFrame("ui-functions");
            Invoke("CloseNativeWorldFunctions");
            var entities = Field<IDictionary>("mapEntities");
            Invoke("Interact", entities[2307]);
            yield return new WaitForSecondsRealtime(.4f);
            string panel = Field<string>("dialoguePanelKey");
            GameObject content = renderer.Find(panel, "/" + panel + "/Content");
            Rect contentRect = ScreenRect(content);
            var writer = Field<WorldDialogueTypeWriter>("dialogueTypeWriter");
            Require(writer != null && writer.IsPlaying, "Official dialogue reveals characters over time");
            Invoke("RequestDialogueAdvance");
            Require(!writer.IsPlaying && !Field<bool>("dialogueAdvanceRequested"), "First click finishes typing and preserves the current line");
            yield return CaptureStatueFrame("ui-dialogue");
            Require(contentRect.width >= Screen.width * .99f && Math.Abs(contentRect.yMin) <= 2,
                "Official dialogue SafeArea must span the viewport from the bottom: " + contentRect);
            yield return AdvanceScriptedDialogue();
            yield return CaptureStatueFrame("ui-options");
            Rect textRect = ScreenRect(renderer.Find("OptionTalkDialog",
                "/OptionTalkDialog/Content/CharacterAndTalkContent/TalkContent/TalkText"));
            Require(textRect.xMin >= -2 && textRect.xMax <= Screen.width + 2 && textRect.yMax < Screen.height * .3f,
                "Dialogue text must remain inside the original bottom panel: " + textRect);
            var data = JsonUtility.FromJson<Map10RuntimeStoryData>(Resources.Load<TextAsset>("Map11/stories").text);
            var speaker = data.dialogRoles.Single(value => value.RoleId == 840402);
            var line = data.stories.SelectMany(value => value.dialogueGroups).SelectMany(value => value.Lines)
                .First(value => value.RoleId == speaker.RoleId);
            renderer.SetOptionChoicesVisible(false);
            for (int index = 0; index < 5; index++) renderer.SetNodeActive("OptionTalkDialog",
                "/OptionTalkDialog/OptionSlots/Content/OptionItem " + (index == 0 ? "" : " (" + index + ")"), false);
            Invoke("ShowDialogueContent", "OptionTalkDialog", Field<object>("optionDialoguePortraits"), speaker,
                line.RoleFaceType, line.Text, null);
            Require(Field<bool>("dialogueContentSwitchWaiting"), "Switching speaker waits for the source clip event");
            yield return new WaitForSecondsRealtime(.4f);
            Require(!Field<bool>("dialogueContentSwitchWaiting"), "Source board midpoint event releases the dialogue content");
            Field<WorldDialogueTypeWriter>("dialogueTypeWriter").StopType();
            Require(renderer.Find("OptionTalkDialog", "/OptionTalkDialog/Content/CharacterAndTalkContent/TalkContent/NpcName/NpcNameText")
                .GetComponent<TMP_Text>().text == speaker.RoleName, "Name changes at the portrait transition");
            var rightMask = renderer.Find("OptionTalkDialog", "/OptionTalkDialog/Content/CharacterAndTalkContent/CharacterGroup/RightCharacter/Mask")
                .GetComponent<RectMask2D>();
            Require(!rightMask.enabled, "Original disabled right-portrait mask must remain disabled");
            yield return CaptureStatueFrame("ui-dialogue-two-speakers");
            Debug.Log("Map11 UI presentation validation passed. Scripted callbacks and rendered frames; no native input. Output: " + Root);
            Invoke("DisposeRuntime");
            SessionState.SetInt(Key + ".result", 0);
            EditorApplication.isPlaying = false;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        [Serializable] private sealed class Map14HeroProbe
        {
            public int EntityClassId;
            public Map11RuntimeWitchResult[] Effects;
            public Map11RuntimePeriodicityAward[] PeriodicAwards;
        }
        [Serializable] private sealed class Map14InteractionProbe
        {
            public string manifest;
            public int mapClassId;
            public Map14HeroProbe[] heroes;
        }

        public static void RunUiMap14Statues()
        {
            try
            {
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
                ProjectTools.Validate();
                ValidateMap14InteractionData();
                SessionState.SetBool(Key + ".map14Statues", true);
                RunCore(true, false, 14);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void ValidateMap14InteractionData()
        {
            var story = JsonUtility.FromJson<Map10RuntimeStoryData>(Resources.Load<TextAsset>("Map14/stories").text);
            if (story.manifest != "871_156598" || story.entityTemplates.Length != 171
                || story.entityTemplates.Sum(row => row.Components.Length) != 1505
                || story.witchInfos.Length != 14 || story.witchResultEffects.Length != 22
                || story.witchPeriodicityAwards.Length != 7 || story.witchPeriodicityAwards.Sum(row => row.Weight) != 100)
                throw new InvalidDataException("Map14 current template/statue source closure differs.");
            var summon = story.witchResultEffects.Single(row => row.ComponentType == 245);
            var battles = JsonUtility.FromJson<Map10RuntimeBattleData>(Resources.Load<TextAsset>("Map14/battle-runtime").text);
            if (summon.SpawnStageId != 20004 || summon.SpawnEntityClassId != 5100
                || !battles.stages.Any(stage => stage.Id == summon.SpawnStageId))
                throw new InvalidDataException("Map14 witch spawn did not resolve its own original battle.");
            // Exercise the actual mainline getter rather than loading the JSON alone.
            var host = new GameObject("Map14CurrentInteractionBinding").AddComponent<OfficialMainlineMapRuntime>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(OfficialMainlineMapRuntime).GetField("mapClassId", flags).SetValue(host, 14);
            object bound = typeof(OfficialMainlineMapRuntime).GetProperty("Interactions", flags).GetValue(host);
            var catalog = JsonUtility.FromJson<Map14InteractionProbe>(JsonUtility.ToJson(bound));
            UnityEngine.Object.DestroyImmediate(host.gameObject);
            if (catalog.manifest != "871_156598" || catalog.mapClassId != 14 || catalog.heroes.Length != 4
                || !catalog.heroes.Select(hero => hero.EntityClassId).SequenceEqual(new[] { 6570, 6571, 6572, 6573 })
                || catalog.heroes.Any(hero => hero.PeriodicAwards.Length != 3
                    || !hero.PeriodicAwards.Select(award => award.Weight).SequenceEqual(new[] { 5, 25, 70 })))
                throw new InvalidDataException("The mainline Map14 interaction getter selected another map/version.");
            Debug.Log("Map14 current interaction data passed: 171 ordered EC templates/1505 components including public story followers, 14 witch activations, 22 results, 7 periodic awards, own summon stage 20004 and four hero types.");
        }

        private static IEnumerator VerifyMap14StatueSteps(OfficialMap11UiData data, Transform parent,
            Camera uiCamera, Camera camera, RenderTexture frame, string output, int width)
        {
            var story = JsonUtility.FromJson<Map10RuntimeStoryData>(Resources.Load<TextAsset>("Map14/stories").text);
            var catalog = JsonUtility.FromJson<Map14InteractionProbe>(Resources.Load<TextAsset>("Map14/interactions").text);
            Type windowType = typeof(OfficialMainlineMapRuntime).Assembly.GetType("Zjcs.Presentation.World.OfficialMap11WitchWindow", true);
            int verified = 0;
            foreach (var info in story.witchInfos)
            {
                var results = info.StageResults.Select(reference => story.witchResultEffects.Single(row =>
                    row.ComponentType == reference.ComponentType && row.ComponentIndex == reference.ComponentIndex)).ToArray();
                IEnumerator steps = VerifyMap14Preview(windowType, "WorldStageInteractionPreviewDialogWitch", 6620,
                    "ui_witch_result_" + info.ActiveCount, "ui_witch_content_" + info.ActiveCount, results,
                    data, parent, uiCamera, camera, frame, output, width + "-witch-activation-" + info.ActiveCount);
                while (steps.MoveNext()) yield return steps.Current;
                verified++;
            }
            foreach (var award in story.witchPeriodicityAwards)
            {
                var results = award.StageResults.Select(reference => story.witchResultEffects.Single(row =>
                    row.ComponentType == reference.ComponentType && row.ComponentIndex == reference.ComponentIndex)).ToArray();
                IEnumerator steps = VerifyMap14Preview(windowType, "WorldStageInteractionPreviewDialogWitch", 6620,
                    "map_periodicity_random_award_interaction_title_" + award.PeriodicityId,
                    "map_periodicity_random_award_interaction_desc_" + award.PeriodicityId, results,
                    data, parent, uiCamera, camera, frame, output, width + "-witch-visit-" + award.PeriodicityId);
                while (steps.MoveNext()) yield return steps.Current;
                verified++;
            }
            foreach (var hero in catalog.heroes)
                foreach (var award in hero.PeriodicAwards)
                {
                    var results = award.StageResults.Select(reference => hero.Effects.Single(row =>
                        row.ComponentType == reference.ComponentType && row.ComponentIndex == reference.ComponentIndex)).ToArray();
                    IEnumerator steps = VerifyMap14Preview(windowType, "WorldStageInteractionPreviewDialogHeroStatue", hero.EntityClassId,
                        "map_periodicity_random_award_interaction_title_" + award.PeriodicityId,
                        "map_periodicity_random_award_interaction_desc_" + award.PeriodicityId, results,
                        data, parent, uiCamera, camera, frame, output, width + "-hero-" + hero.EntityClassId + "-visit-" + award.PeriodicityId);
                    while (steps.MoveNext()) yield return steps.Current;
                    verified++;
                }
            if (verified != 33) throw new InvalidDataException("Map14 statue preview coverage differs.");
            Debug.Log("Map14 original statue previews passed: " + verified + " configured results and raycast close buttons at " + frame.width + "x" + frame.height + ". Inventory, progression and saves are separate.");
        }

        private static void SimulateMap14PreviewClose(Button expected, Vector2 position)
        {
            // The original close control covers the screen behind its scroll
            // viewport. Its advertised gesture is a click on the blank area.
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
                { position = position, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            GameObject hit = hits.FirstOrDefault().gameObject;
            if (!hit || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) != expected.gameObject)
                throw new InvalidDataException("Original Map14 blank-area close is occluded: " + expected.name);
            pointer.pointerCurrentRaycast = hits[0];
            pointer.pressPosition = position;
            pointer.pointerPressRaycast = hits[0];
            GameObject pressed = ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            pointer.pointerPress = pressed;
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator VerifyMap14Preview(Type type, string key, int entity, string title, string description,
            Map11RuntimeWitchResult[] results, OfficialMap11UiData data, Transform parent,
            Camera uiCamera, Camera camera, RenderTexture frame, string output, string name)
        {
            object window = Activator.CreateInstance(type, new object[] { key, parent, data, "Map14/UI/", entity });
            var root = (GameObject)type.GetProperty("Root").GetValue(window);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.worldCamera = uiCamera;
            int closed = 0;
            Action close = () => { closed++; type.GetMethod("Destroy").Invoke(window, null); };
            type.GetMethod("Preview").Invoke(window, new object[] { title, description, results, close });
            type.GetMethod("Show").Invoke(window, null);
            yield return new WaitForSecondsRealtime(.6f);
            var fields = data.components.Single(value => value.panel == key && value.type == "WorldStageInteractionPreviewDialog").fields;
            var buttonTransform = (Transform)type.GetMethod("Find").Invoke(window, new object[] { fields.mCloseBtn });
            CaptureUi(camera, uiCamera, frame, output, name);
            SimulateMap14PreviewClose(buttonTransform.GetComponent<Button>(), new Vector2(frame.width * .5f, frame.height * .95f));
            yield return null;
            if (closed != 1 || root) throw new InvalidDataException("Original Map14 statue preview close did not finish: " + name);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialSharedBattleReportAcceptance
    {
        public static IEnumerator Exercise(FightBattleCheckpoint checkpoint, bool expectAllPlayers = false)
        {
            Require(checkpoint.Finished && checkpoint.Outcome != null && !checkpoint.Outcome.Invalid,
                "The shared report must consume a completed real battle.");
            var fixture = new GameObject("SharedBattleReportFixture", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = fixture.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = fixture.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            File.WriteAllText(Path.Combine(output, "shared-report-checkpoint.json"),
                FightBattleCheckpointSerializer.Save(checkpoint));
            try
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    int closed = 0;
                    var populatedSkillUnits = new List<int>();
                    using (var view = new OfficialBattleReportView(fixture.transform, checkpoint,
                        new OfficialBattleReportView.Presentation { Closed = () => closed++,
                            PlayerName = id => "UI fixture " + id,
                            SkillIcon = (_, skill, __) => populatedSkillUnits.Add(skill.UnitId) }))
                    {
                        float deadline = Time.realtimeSinceStartup + 10f;
                        while (!view.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
                        Require(view.IsReady, "The original report blur mask did not finish preparing.");
                        Require(fixture.GetComponentsInChildren<RawImage>().Any(image =>
                            image.name == "BlurMask" && image.texture is RenderTexture),
                            "The native report blur mask did not receive its captured background.");
                        WorldTabGroup group = view.Root.GetComponentsInChildren<WorldTabGroup>(true)
                            .Single(value => value.GetAllTabs().Length == 2);
                        WorldTabItem[] tabs = group.GetAllTabs();
                        Require(tabs[0].isOn && tabs.Count(value => value.isOn) == 1,
                            "The report did not initialize the original default player tab.");
                        Require((view.Root.name == "FightEndAllPlayerReportPanel") == expectAllPlayers,
                            "Official report-stage configuration selected the wrong native panel.");
                        var expected = OfficialBattleReportPlayerRowBinder.PlayerRows(
                            FightBattleReportGrouping.Build(FightBattleReportBuilder.Build(checkpoint), 1),
                            OfficialCombatAssetCatalog.LoadDefault().LoadReportScoreSettings());
                        if (expectAllPlayers)
                            foreach (int side in checkpoint.Units.Select(value => value.Side).Distinct().Where(value => value != 1))
                                expected.AddRange(OfficialBattleReportPlayerRowBinder.PlayerRows(
                                    FightBattleReportGrouping.Build(FightBattleReportBuilder.Build(checkpoint), side),
                                    OfficialCombatAssetCatalog.LoadDefault().LoadReportScoreSettings()));
                        Require(view.Root.GetComponentsInChildren<WorldBattleReportPlayerRow>().Length == expected.Count,
                            "The native player report lost real roster/statistics rows.");
                        yield return new WaitForSecondsRealtime(.4f);
                        yield return Capture(output, "shared-report-players-" + pass);
                        Click(tabs[1]);
                        yield return null;
                        var scroll = view.Root.GetComponentInChildren<LoopVerticalScrollRect>();
                        yield return Capture(output, "shared-report-skills-" + pass);
                        Require(tabs[1].isOn && scroll != null && scroll.totalCount > 0
                            && scroll.GetComponentsInChildren<WorldBattleReportSkillRow>().Length > 0,
                            "The native skill tab did not populate its real completed-battle statistics: selected="
                            + tabs[1].isOn + ", list=" + (scroll != null) + ", count=" + scroll?.totalCount
                            + ", rows=" + scroll?.GetComponentsInChildren<WorldBattleReportSkillRow>().Length);
                        var pointer = new PointerEventData(EventSystem.current) {
                            position = new Vector2(Screen.width * .7f, Screen.height * .6f),
                            button = PointerEventData.InputButton.Left };
                        ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
                        ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.beginDragHandler);
                        pointer.delta = new Vector2(-Screen.width * .65f, 0);
                        pointer.position += pointer.delta;
                        ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.dragHandler);
                        ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.endDragHandler);
                        yield return new WaitForEndOfFrame();
                        Require(scroll.content.anchoredPosition.x < -1f,
                            "The shared report did not reveal later columns through simulated dragging.");
                        yield return Capture(output, "shared-report-columns-" + pass);
                        if (expectAllPlayers)
                            yield return ExerciseDropdown(view, checkpoint, populatedSkillUnits, output, pass);
                        Click(tabs[0]);
                        yield return null;
                        Require(tabs[0].isOn && tabs.Count(value => value.isOn) == 1,
                            "The original report tab did not return to player statistics.");
                        var close = view.Root.GetComponentsInChildren<Button>(true)
                            .Single(value => value.name == "CloseBtn");
                        Click(close);
                        yield return null;
                        Require(!fixture.GetComponentsInChildren<RawImage>().Any(image => image.name == "BlurMask"),
                            "Closing the report left an active background mask.");
                        Require(closed == 1 && view.Root == null,
                            "Closing the shared report did not release the native window exactly once.");
                    }
                }
                File.WriteAllText(Path.Combine(output, "shared-report-result.txt"),
                    "completed real battle: two native windows, player/skill tab clicks, statistics rows and close/disposal passed\n");
                Debug.Log("Shared battle report input passed: completed real battle, two native windows, player/skill statistics and close/disposal.");
            }
            finally { UnityEngine.Object.Destroy(fixture); }
        }

        private static IEnumerator Capture(string directory, string name)
        {
            yield return new WaitForEndOfFrame();
            yield return BattleGameViewInput.CaptureGameViewScreenshot(Path.Combine(directory, name + ".png"));
        }

        private static void Click(Selectable target)
        {
            Require(target != null && target.isActiveAndEnabled && target.interactable,
                "The original report control is not clickable.");
            Canvas.ForceUpdateCanvases();
            RectTransform rect = (RectTransform)target.transform;
            var pointer = new PointerEventData(EventSystem.current) {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && (hits[0].gameObject == target.gameObject
                || hits[0].gameObject.transform.IsChildOf(target.transform)),
                "The original report control is obstructed: " + target.name);
            GameObject hit = hits[0].gameObject;
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}

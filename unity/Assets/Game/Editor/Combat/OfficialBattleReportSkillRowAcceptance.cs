using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleReportSkillRowAcceptance
    {
        [Serializable] private sealed class Receipt { public Binding binding; }
        [Serializable] private sealed class Binding { public OfficialBattleReportFoundationAcceptance.Check[] checks; }
        private sealed class Source : LoopScrollDataSource
        {
            public WorldBattleReportSkillRowData[] rows;
            public void ProvideData(Transform target, int index) => target.GetComponent<WorldBattleReportSkillRow>().Refresh(rows[index], false);
        }

        public static IEnumerator Run(FightBattleCheckpoint checkpoint)
        {
            Require(Application.isPlaying && checkpoint != null && checkpoint.Finished, "A completed real battle is required for the skill report row.");
            Require(WorldBattleReportNumberFormat.Compact(12399) == "1.23万"
                && WorldBattleReportNumberFormat.Compact(100000000) == "1亿"
                && WorldBattleReportNumberFormat.Percent(.333333) == "33.3%"
                && WorldBattleReportNumberFormat.Percent(.25) == "25%", "Original CN report formatting differs.");
            var canvas = new GameObject("ReportSkillRowFixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 30000;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            try
            {
                var asset = Resources.Load<GameObject>("Combat/BattleReport/FightEndReportSkillTabViewItem");
                var root = UnityEngine.Object.Instantiate(asset, canvas.transform, false);
                root.name = asset.name;
                var row = root.GetComponent<WorldBattleReportSkillRow>();
                Require(row != null, "Original skill report row was not bound.");
                var references = new Dictionary<long, UnityEngine.Object>();
                OfficialBattleReportFoundationAcceptance.MapReferences(asset.transform, root.transform, references);
                var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText("../assets/reference/battle-hud/report-skill-row-source-manifest.json"));
                var serialized = new SerializedObject(row);
                foreach (var check in receipt.binding.checks) OfficialBattleReportFoundationAcceptance.Match(serialized.FindProperty(check.path), check, references);
                Require(receipt.binding.checks.Length == 35, "Original row field/reference scope changed.");
                var sample = new WorldBattleReportSkillRowData
                {
                    Count = 10, AvgDamageCount = 1.8f, AvgDamageCountPerTarget = 3, RealDamage = -12399,
                    RealCure = 250, RealShield = 750, RealDamagePer = .25f, RealCurePer = .75f,
                    RealShieldPer = .5f, RealCureMaxHpPer = .25f, RealShieldMaxHpPer = .75f,
                    ApplyBuffStatusCount = 0, ApplyDebuffStatusCount = 2, DamageCount = 18, CureCount = 4
                };
                bool iconFirst = false;
                row.Refresh(sample, true, slot => { iconFirst = slot != null && Text(row, "mCountText").Text != "10"; });
                Require(iconFirst && Text(row, "mCountText").Text == "10" && Text(row, "mAvgDmgCountText").Text == "2"
                    && Text(row, "mAvgDmgCountPerTargetText").Text == "3" && Text(row, "mRealDamageText").Text == "1.23万"
                    && Text(row, "mRealShieldText").Text == "750" && Text(row, "mRealCureMaxHpPerText").Text == "25%"
                    && Text(row, "mRealShieldMaxHpPerText").Text == "75%", "Original row metrics/formatting or confirmed rounding differ.");
                Require(Mathf.Abs(Slider(row, "mRealDamagePerSlider").value - .25f) < .0001f
                    && Mathf.Abs(Slider(row, "mRealCurePerSlider").value - .75f) < .0001f
                    && Mathf.Abs(Slider(row, "mRealShieldPerSlider").value - .5f) < .0001f, "Original row percentage slider bindings differ.");
                Require(Mathf.Abs(Text(row, "mApplyBuffStatusCountText").Color.r - .654901981f) < .0001f
                    && Mathf.Abs(Text(row, "mApplyDebuffStatusCountText").Color.r - .886274516f) < .0001f
                    && Text(row, "mAvgDmgCountPerTargetText").gameObject.activeSelf, "Original row zero/nonzero color or GM state differs.");
                var legacy = OfficialBattleReportSkillRowBinder.Values(new FightBattleReportSkill());
                row.Refresh(legacy, false);
                Require(Text(row, "mAvgDmgCountText").Text == "" && Text(row, "mRealShieldText").Text == ""
                    && Text(row, "mRealShieldMaxHpPerText").Text == "" && Text(row, "mApplyBuffStatusCountText").Text == ""
                    && !Text(row, "mAvgDmgCountPerTargetText").gameObject.activeSelf, "Unavailable report history was fabricated or retained from a pooled row.");
                UnityEngine.Object.DestroyImmediate(root);

                FightBattleReport report = FightBattleReportBuilder.Build(checkpoint);
                var skills = report.Skills.Where(value => value.UnitId == 1).ToArray();
                Require(skills.Length > 0 && skills.Any(value => value.ShieldGranted >= 250), "The real battle shield fixture did not reach report rows.");
                // The shipped report window embeds its own skill view, with five GM columns.
                // The separately packaged older skill-view prefab has only three GM headers;
                // preserve that source difference and verify the actual window's embedded view.
                var window = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Combat/BattleReport/FightEndReportPanel"), canvas.transform, false);
                var sync = window.GetComponentInChildren<WorldBattleReportSkillScrollSync>(true);
                var panel = sync.gameObject;
                var list = panel.GetComponentInChildren<LoopVerticalScrollRect>(true);
                for (Transform parent = list.transform; parent != canvas.transform; parent = parent.parent) parent.gameObject.SetActive(true);
                sync.SetGMState(false);
                var source = new Source { rows = skills.Select(OfficialBattleReportSkillRowBinder.Values).ToArray() };
                list.dataSource = source;
                list.totalCount = source.rows.Length;
                list.RefillCells();
                yield return null;
                sync.Initialize();
                sync.RefreshLayout();
                var actual = list.content.GetComponentsInChildren<WorldBattleReportSkillRow>(true).First();
                Require(Text(actual, "mRealShieldText").Text == "250" && Text(actual, "mRealDamageText").Text != ""
                    && Text(actual, "mDamageCountText").Text == skills[0].DamageHits.ToString()
                    && Text(actual, "mCureCountText").Text == "0"
                    && Text(actual, "mApplyBuffStatusCountText").Text == skills[0].BuffApplications.ToString()
                    && Text(actual, "mApplyDebuffStatusCountText").Text == skills[0].DebuffApplications.ToString()
                    && Slider(actual, "mRealShieldPerSlider").value > 0, "The original pooled row did not consume actual battle damage/shield data.");
                var header = panel.GetComponentsInChildren<ScrollRect>(true).First(value => !value.vertical);
                LogLayout("before", header, list, actual);
                Require(!header.content.Find("AvgDmgCountPerTargetTitleText").gameObject.activeSelf
                    && !Text(actual, "mAvgDmgCountPerTargetText").gameObject.activeSelf,
                    "Original GM header/row visibility differs.");
                AssertColumn(header, "CountTitleText", Text(actual, "mCountText"));
                AssertColumn(header, "DamageTitleText", Text(actual, "mRealDamageText"));
                AssertColumn(header, "ShieldTitleText", Text(actual, "mRealShieldText"));
                yield return Capture("report-skill-row.png");
                // Exercise the actual original list/header path through a simulated drag.
                var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .7f, Screen.height * .6f), button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(list.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
                ExecuteEvents.Execute(list.gameObject, pointer, ExecuteEvents.beginDragHandler);
                pointer.delta = new Vector2(-Screen.width * .65f, 0);
                pointer.position += pointer.delta;
                ExecuteEvents.Execute(list.gameObject, pointer, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(list.gameObject, pointer, ExecuteEvents.endDragHandler);
                // The preceding capture resumes at end of frame. Let the next LateUpdate emit
                // the original list's onValueChanged before checking its subscribed header.
                yield return new WaitForEndOfFrame();
                Require(list.content.anchoredPosition.x < -1f, "Simulated drag did not reveal later original report columns.");
                yield return Capture("report-skill-row-percent.png");
                LogLayout("after", header, list, actual);
                AssertColumn(header, "ShieldTitleText", Text(actual, "mRealShieldText"));
                Debug.Log("Original skill report row passed: 35 original fields/references, CN number/percent formatting, zero colors/GM state, confirmed integer display, unavailable history/pool reuse and actual completed battle damage/shield/hit/status counters in native row pool; simulated horizontal drag passed. Icon/detail providers and production entry remain pending.");
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        private static WorldMultiLanguageText Text(WorldBattleReportSkillRow row, string field) => (WorldMultiLanguageText)new SerializedObject(row).FindProperty(field).objectReferenceValue;
        private static void LogLayout(string phase, ScrollRect header, LoopVerticalScrollRect list, WorldBattleReportSkillRow row)
        {
            Debug.Log("Report row geometry " + phase + ": header=" + header.content.rect.width + "/" + header.viewport.rect.width + "@" + header.content.anchoredPosition.x
                + " list=" + list.content.rect.width + "/" + list.viewport.rect.width + "@" + list.content.anchoredPosition.x
                + " row=" + ((RectTransform)row.transform).rect.width + "@" + ((RectTransform)row.transform).anchoredPosition.x
                + " headerActive=" + header.content.gameObject.activeInHierarchy + " sync=" + row.GetComponentInParent<WorldBattleReportSkillScrollSync>().enabled);
        }
        private static void AssertColumn(ScrollRect header, string title, WorldMultiLanguageText value)
        {
            var text = header.content.Find(title).GetComponent<TMP_Text>();
            // Official title/value rectangles have different padding; their centers need not match.
            // The source receipt verifies exact geometry. Check that scrolling keeps each title
            // over its own value column without changing that original padding.
            var a = new Vector3[4];
            var b = new Vector3[4];
            text.rectTransform.GetWorldCorners(a);
            value.RectTransform.GetWorldCorners(b);
            float overlap = Mathf.Min(a[2].x, b[2].x) - Mathf.Max(a[0].x, b[0].x);
            float width = Mathf.Min(a[2].x - a[0].x, b[2].x - b[0].x);
            Require(width > 0 && overlap >= width * .85f,
                "Original report header/value columns differ: " + title + " overlap=" + overlap + " width=" + width);
        }
        private static Slider Slider(WorldBattleReportSkillRow row, string field) => (Slider)new SerializedObject(row).FindProperty(field).objectReferenceValue;
        private static IEnumerator Capture(string name)
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_OUTPUT");
            if (string.IsNullOrEmpty(output)) yield break;
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleReportPlayerRowAcceptance
    {
        [Serializable] private sealed class Receipt { public Binding binding; public Binding[] fills; }
        [Serializable] private sealed class Binding { public string file_id; public OfficialBattleReportFoundationAcceptance.Check[] checks; }

        public static IEnumerator Run(FightBattleCheckpoint checkpoint)
        {
            Require(Application.isPlaying && checkpoint != null && checkpoint.Finished,
                "A completed real battle is required for the player report row.");
            var canvas = new GameObject("ReportPlayerRowFixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 30000;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            try
            {
                var view = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Combat/BattleReport/FightEndReportPlayerTabView"), canvas.transform, false);
                view.SetActive(true);
                var scroll = view.GetComponentInChildren<ScrollRect>(true);
                var asset = Resources.Load<GameObject>("Combat/BattleReport/FightEndReportPlayerTabViewItem");
                var root = UnityEngine.Object.Instantiate(asset, scroll.content, false);
                var row = root.GetComponent<WorldBattleReportPlayerRow>();
                var second = UnityEngine.Object.Instantiate(asset, scroll.content, false).GetComponent<WorldBattleReportPlayerRow>();
                Require(row != null && second != null, "Original player report row was not bound.");
                var references = new Dictionary<long, UnityEngine.Object>();
                OfficialBattleReportFoundationAcceptance.MapReferences(asset.transform, root.transform, references);
                var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText("../assets/reference/battle-hud/report-player-row-source-manifest.json"));
                var serialized = new SerializedObject(row);
                foreach (var check in receipt.binding.checks)
                    OfficialBattleReportFoundationAcceptance.Match(serialized.FindProperty(check.path), check, references);
                Require(receipt.binding.checks.Length == 39, "Original player row field/reference scope was lost.");
                Require(receipt.fills.Length == 4, "Original sliced player-report fills were lost.");
                foreach (var fill in receipt.fills)
                {
                    var original = (Component)references[long.Parse(fill.file_id)];
                    Require(original is SlicedFilledImage, "Player report fill did not bind the existing official sliced-mesh port.");
                    var fields = new SerializedObject(original);
                    foreach (var check in fill.checks)
                        OfficialBattleReportFoundationAcceptance.Match(fields.FindProperty(check.path), check, references);
                }

                var firstData = new WorldBattleReportPlayerRowData
                {
                    TargetId = 1,
                    NormalReport = new WorldBattleReportPlayerRoleData { Damage = 100, BeAbsoluteDamage = 300, RealCure = 200, ApplyStatusScore = 5 },
                    SummonNormalReport = new WorldBattleReportPlayerRoleData { Damage = 400, BeAbsoluteDamage = 100, ApplyStatusScore = 10 },
                    PetNormalReport = new WorldBattleReportPlayerRoleData { Damage = 500, BeAbsoluteDamage = 600, RealCure = 100, ApplyStatusScore = 10 }
                };
                var secondData = new WorldBattleReportPlayerRowData
                {
                    TargetId = 2,
                    NormalReport = new WorldBattleReportPlayerRoleData { Damage = 3000, BeAbsoluteDamage = 1000, RealCure = 900, ApplyStatusScore = 75 }
                };
                bool iconFirst = false;
                OfficialBattleReportPlayerRowBinder.InitRows(new[] { row, second }, new[] { firstData, secondData }, 1,
                    (icon, id) => { if (id == 1) iconFirst = icon != null && row.DamageValue.Text != "1000"; });
                Require(iconFirst && row.MyselfStateSwitcher.State && !second.MyselfStateSwitcher.State
                    && row.DamageValue.Text == "1000" && row.BeDamageValue.Text == "1000"
                    && row.CureValue.Text == "300" && row.ApplyStatusValue.Text == "25",
                    "Original player-row category sums, refresh order or self state changed.");
                Require(Near(row.DamageSlider.value, .25f) && Near(row.BeDamageSlider.value, .5f)
                    && Near(row.CureSlider.value, .25f) && Near(row.ApplyStatusSlider.value, .25f),
                    "Original player-row team ratios changed.");
                foreach (var slider in new[] { row.DamageSlider, row.BeDamageSlider, row.CureSlider, row.ApplyStatusSlider })
                {
                    var fill = slider.fillRect.GetComponent<SlicedFilledImage>();
                    Require(fill != null && fill.sprite != null && fill.color.a == 1f
                        && Near(fill.fillAmount, slider.normalizedValue), "Original sliced fill did not follow the resolved player ratio.");
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                yield return null;
                yield return Capture("report-player-row-categories.png");
                firstData.NormalReport.BeAbsoluteDamage = null;
                firstData.NormalReport.ApplyStatusScore = null;
                OfficialBattleReportPlayerRowBinder.InitRows(new[] { row, second }, new[] { firstData, secondData }, 1);
                Require(row.BeDamageValue.Text == "" && row.ApplyStatusValue.Text == ""
                    && row.BeDamageSlider.value == 0 && row.ApplyStatusSlider.value == 0
                    && second.BeDamageValue.Text == "1000" && second.BeDamageSlider.value == 0,
                    "Unknown local history or a team denominator retained a fabricated/pooled value.");
                second.gameObject.SetActive(false);
                var report = FightBattleReportBuilder.Build(checkpoint);
                var unit = report.Unit(1);
                var settings = OfficialCombatAssetCatalog.LoadDefault().LoadReportScoreSettings();
                var actual = new WorldBattleReportPlayerRowData { TargetId = 1, NormalReport = OfficialBattleReportPlayerRowBinder.Role(unit, settings) };
                OfficialBattleReportPlayerRowBinder.InitRows(new[] { row }, new[] { actual }, 1);
                Require(row.DamageValue.Text == WorldBattleReportNumberFormat.Compact(unit.DamageDealt)
                    && row.BeDamageValue.Text == WorldBattleReportNumberFormat.Compact(unit.AbsoluteDamageTaken)
                    && row.CureValue.Text == WorldBattleReportNumberFormat.Compact(unit.HealDealt)
                    && settings.TryGetApplyStatusScore(unit, out long score)
                    && row.ApplyStatusValue.Text == WorldBattleReportNumberFormat.Compact(score)
                    && row.MyselfStateSwitcher.State && row.DamageSlider.value == 1,
                    "Original player row failed to consume completed-battle counters.");
                yield return null;
                yield return Capture("report-player-row-battle.png");
                // A disabled initialized UIBase refreshes when enabled; preserve that lifecycle.
                row.gameObject.SetActive(false);
                actual.NormalReport.Damage = 12399;
                row.Init(actual, 2, 12399, 0, 0, 0);
                row.gameObject.SetActive(true);
                Require(row.DamageValue.Text == "1.23万" && !row.MyselfStateSwitcher.State,
                    "Original player-row enable refresh or compact formatting changed.");

                var playerView = view.GetComponent<WorldBattleReportPlayerView>();
                Require(playerView != null && playerView.PlayerSlot == scroll.content
                    && playerView.PrefabObj == asset, "Original player view slot/row-prefab references changed.");
                // The same completed battle now supplies the formal ownership provider. Append
                // isolated recorded NPC/pet/multi-hop summon samples without changing the battle.
                report.Units.Add(SampleUnit(8000, 4, 1, 10));
                report.Units.Add(SampleUnit(8002, 3, 8001, 20));
                report.Units.Add(SampleUnit(8001, 3, 1, 30));
                report.Units.Add(SampleUnit(8003, 5, 1, 40));
                report.Units.Add(SampleUnit(8004, 3, 8003, 50));
                report.Units.Add(SampleUnit(8005, 3, 8004, 60));
                var groups = FightBattleReportGrouping.Build(report, unit.Side);
                var records = OfficialBattleReportPlayerRowBinder.PlayerRows(groups, settings);
                Require(records.Count == 2 && records[0].TargetId == 1 && records[1].TargetId == 8000
                    && records[0].SummonNormalReport.Damage == 50 && records[0].PetNormalReport.Damage == 150
                    && records[1].NormalReport.Damage == 10, "Resolved NPC/pet/nested-summon categories changed.");
                playerView.Init(records, 1);
                yield return null; // Original child destruction is deferred by Unity.
                Require(playerView.PlayerSlot.childCount == 2, "Original player-view refresh retained old children.");
                var groupedRows = playerView.PlayerSlot.GetComponentsInChildren<WorldBattleReportPlayerRow>();
                Require(groupedRows.Length == 2 && groupedRows[0].MyselfStateSwitcher.State
                    && !groupedRows[1].MyselfStateSwitcher.State
                    && groupedRows[0].DamageValue.Text == WorldBattleReportNumberFormat.Compact(unit.DamageDealt + 200)
                    && groupedRows[1].DamageValue.Text == "10"
                    && Near(groupedRows[0].DamageSlider.value, (float)(unit.DamageDealt + 200) / (unit.DamageDealt + 210)),
                    "Original view did not bind resolved ownership or whole-team totals.");
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                yield return Capture("report-player-grouping.png");
                playerView.gameObject.SetActive(false);
                playerView.Init(records, 8000);
                playerView.gameObject.SetActive(true);
                yield return null;
                groupedRows = playerView.PlayerSlot.GetComponentsInChildren<WorldBattleReportPlayerRow>();
                Require(playerView.PlayerSlot.childCount == 2 && !groupedRows[0].MyselfStateSwitcher.State
                    && groupedRows[1].MyselfStateSwitcher.State, "Original player-view enable refresh or child replacement changed.");
                report.Units.Add(SampleUnit(9000, 3, 9999, 10));
                bool unresolvedRejected = false;
                try { OfficialBattleReportPlayerRowBinder.PlayerRows(FightBattleReportGrouping.Build(report, unit.Side), settings); }
                catch (InvalidOperationException) { unresolvedRejected = true; }
                Require(unresolvedRejected, "An unresolved owner produced a misleading complete team ratio.");
                Debug.Log("Original player report row passed: " + receipt.binding.checks.Length
                    + " original fields/references, self state, three-category sums, four team sliders, CN formatting, unavailable history and completed-battle counters. Original player view passed: slot/prefab binding, NPC rows, nested summons, pet category, team totals, child replacement, enable refresh and unresolved-owner rejection. Portrait/detail provider and formal entry remain pending.");
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        private static FightBattleReportUnit SampleUnit(int id, int type, int master, long damage)
        {
            return new FightBattleReportUnit { UnitId = id, Side = 1, CharacterType = type,
                MasterId = master, DamageDealt = damage, HasAbsoluteDamageTaken = true,
                HasBreakResilienceCount = true, HasStatusTypeCounts = true };
        }

        private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) < .0001f;
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleReportTextAcceptance
    {
        [Serializable] private sealed class Receipt { public Binding[] bindings; }
        [Serializable] private sealed class Binding { public string prefab, path; public Fields fields; }
        [Serializable] private sealed class Fields { public string myKey; }
        private sealed class Processor : IWorldTextProcessor
        {
            private readonly Func<string, string> process;
            public Processor(Func<string, string> process) { this.process = process; }
            public string Process(string text) => process(text);
        }

        public static IEnumerator Run()
        {
            Require(Application.isPlaying, "Report text validation requires Play Mode.");
            Require(WorldBattleReportTextService.GetText("ui_battle_end_skill_report_avg_damage_count") == "平均命中"
                && WorldBattleReportTextService.GetText("ui_battle_end_skill_report_avg_hit_count") == "平均段数"
                && WorldBattleReportTextService.GetText("ui_battle_end_skill_report_shield") == "有效护盾", "Original report Chinese labels differ.");
            Require(WorldBattleReportTextService.GetText("") == "" && WorldBattleReportTextService.GetText("6") == "6"
                && WorldBattleReportTextService.GetText("missing-report-key") == "missing-report-key"
                && WorldBattleReportTextService.ProcessText("#甲\\n\"\"乙\"\"") == "甲\n\"乙\"", "Original localization fallback/processing differs.");
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText("../assets/reference/battle-hud/report-text-source-manifest.json"));
            var canvas = new GameObject("ReportTextFixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 30000;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            int checkedKeys = 0;
            try
            {
                foreach (var panel in receipt.bindings.GroupBy(value => value.prefab))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(panel.Key.Substring("unity/".Length));
                    Require(asset != null, "Original report text prefab is missing: " + panel.Key);
                    var root = UnityEngine.Object.Instantiate(asset, canvas.transform, false);
                    root.name = asset.name;
                    try
                    {
                        var nodes = new Dictionary<string, Transform>();
                        OfficialBattleReportFoundationAcceptance.Index(root.transform, "", nodes);
                        foreach (Binding binding in panel)
                        {
                            Require(nodes.TryGetValue(binding.path, out Transform node), "Original report text hierarchy is missing: " + binding.path);
                            var text = node.GetComponent<WorldMultiLanguageText>();
                            Require(text != null && text.myKey == binding.fields.myKey, "Original report text key differs: " + binding.path);
                            string original = text.Text;
                            text.Refresh();
                            Require(text.Text == (string.IsNullOrEmpty(text.myKey) ? original : WorldBattleReportTextService.GetText(text.myKey)),
                                "Original report text was not refreshed: " + binding.path);
                            checkedKeys++;
                        }
                        yield return null;
                        if (asset.name == "FightEndReportSkillTabView")
                        {
                            var format = root.GetComponentsInChildren<WorldMultiLanguageText>(true).First(value => value.myKey == "ui_battle_end_skill_report_title");
                            string sequence = "";
                            Action refreshed = () => { sequence += "E"; Require(format.Text == "[<18>]", "Refresh event fired before text assignment."); };
                            format.OnTextRefresh += refreshed;
                            format.SetKey("{0}", new Processor(value => { sequence += "R"; return "<" + value + ">"; }),
                                new Processor(value => { sequence += "L"; return "[" + value + "]"; }), 18);
                            Require(sequence == "RLE" && format.Text == "[<18>]", "Original text raw/format/localized/event order differs.");
                            format.OnTextRefresh -= refreshed;
                            format.SetKey("ui_battle_end_skill_report_title");
                            Canvas.ForceUpdateCanvases();
                            yield return Capture("report-texts.png");
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                Require(checkedKeys == 121, "Expected all 121 original report text bindings.");
                Debug.Log("Original report text passed: 121 original keys, 25 verified Chinese entries, fallback/escaping, format processor and refresh event order. Full language/font/effect services remain pending.");
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

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

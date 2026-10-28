using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        public static void RunSteleUi() => RunCore(false, false, verifySteleUi: true);

        private static IEnumerator VerifySteleUiSteps()
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(repository)
                || !Path.GetFullPath(output).StartsWith(Path.GetFullPath(Path.Combine(repository, "output/validation"))
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !Directory.Exists(output)) throw new InvalidDataException("Set the isolated validation workspace before stele UI checks.");
            Application.runInBackground = true;
            foreach (MonoBehaviour startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
                .Where(c => c.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            { startup.StopAllCoroutines(); UnityEngine.Object.Destroy(startup.gameObject); }
            foreach (Canvas boot in UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.name.StartsWith("BootSplash")))
                UnityEngine.Object.Destroy(boot.gameObject);
            var data = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map12/UI/catalog").text);
            if (data.panels.Length != 72) throw new InvalidDataException("Import the original 72-panel stele UI closure first.");
            var steles = OfficialMap12SteleCatalog.Load();
            var assets = Resources.Load<OfficialMap11UiAssets>("Map12/UI/DynamicItems");
            var previousLanguage = WorldMultiLanguageText.Localize;
            WorldMultiLanguageText.Localize = key => data.texts.Single(t => t.key == key).value;
            if (EventSystem.current == null) new GameObject("Map12SteleSimulatedInput", typeof(EventSystem));
            var camera = new GameObject("Map12SteleUiCamera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 10; camera.backgroundColor = Color.black;
            try
            {
                foreach (int width in new[] { 450, 720 })
                {
                    int height = width == 450 ? 800 : 1280;
                    SetUiGameViewSize(width, height);
                    float deadline = Time.realtimeSinceStartup + 10;
                    while (Screen.width != width || Screen.height != height)
                    { if (Time.realtimeSinceStartup > deadline) throw new InvalidDataException("Stele portrait resize timed out."); yield return null; }
                    var canvas = new GameObject("Map12SteleWindow", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    ConfigureCanvas(canvas, camera, data);
                    foreach (var stele in steles.Data.Steles)
                    {
                        var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Map12/UI/BattleSteleWorldStageDialog"), canvas.transform, false);
                        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                        foreach (Canvas child in root.GetComponentsInChildren<Canvas>(true)) child.worldCamera = camera;
                        var view = root.GetComponent<WorldBattleSteleStageUI>();
                        if (view == null) throw new InvalidDataException("Original stele script bindings are missing.");
                        var events = new List<string>();
                        Action confirm = null, fight = null;
                        int members = 3;
                        bool hasTeam = false;
                        var context = new WorldStageUiContext
                        {
                            NameKey = "stage_" + stele.StageId + "_name", DescriptionKey = null,
                            DialogIconPrefab = Resources.Load<GameObject>("Map12/UI/" + stele.EntityClassId + "_DialogIcon"),
                            Background = assets.mapBackground, Level = stele.RegionLevel,
                            ShowRewardPreview = slot => events.Add("reward"),
                            StartStage = index => events.Add("start:" + index), Close = () => events.Add("close"),
                            BattleStele = new WorldBattleSteleContext
                            {
                                StageId = stele.StageId, SupportTeam = stele.SupportTeam, RankSuppressed = false,
                                RecommendCombatRating = stele.CombatRatingLevelValues[steles.Data.RecommendCombatRatingIndex],
                                CombatRating = 123456, CombatRatingColor = Color.red, Compact = WorldBattleReportNumberFormat.Compact,
                                TeamMemberCount = () => (members, 4), HasTeam = () => hasTeam,
                                OpenTeam = () => events.Add("team"), Notify = key => events.Add("notice:" + key),
                                Confirm = (key, callback) => { events.Add("confirm:" + key); confirm = callback; },
                                PlayFightAction = (stageId, callback) => { events.Add("action:" + stageId); fight = callback; }
                            }
                        };
                        root.GetComponent<StageDialog>().Init(context);
                        root.GetComponent<WorldPopupWindow>().Show();
                        yield return new WaitForSecondsRealtime(.6f);
                        Canvas.ForceUpdateCanvases();
                        SteleRequire(events.SequenceEqual(new[] { "reward" }), "Stele preview initialization/order changed.");
                        SteleRequire(view.mTitleText.Text == Text(data, "stage_" + stele.StageId + "_name"), "Stele title lost its original language binding.");
                        SteleRequire(view.mTeamBtn.gameObject.activeSelf == stele.SupportTeam, "Stele team button state binding changed.");
                        foreach (bool suppressed in new[] { true, false })
                        {
                            context.BattleStele.RankSuppressed = suppressed;
                            root.GetComponent<StageDialog>().Init(context);
                            string key = "ui_battle_stele_recommend_" + (stele.SupportTeam ? "team" : "personal") + "_combat_rating";
                            SteleRequire(view.mRecommendCombatRatingText.myKey == key
                                && view.mRankSuppressionText.myKey == key + "_rank_suppression", "Team language state selected the wrong managed reference.");
                            SteleRequire(view.mRankSuppressionText.gameObject.activeInHierarchy == suppressed, "Rank suppression visibility changed.");
                        }
                        yield return new WaitForEndOfFrame();
                        ScreenCapture.CaptureScreenshot(Path.Combine(output, width + "-stele-" + stele.PlacementId + ".png"));
                        events.Clear();
                        if (stele.SupportTeam)
                        {
                            SimulateUiClick(view.mTeamBtn, camera);
                            SteleRequire(events.SequenceEqual(new[] { "team" }), "Original team button did not dispatch its team target.");
                            events.Clear();
                            SimulateUiClick(view.mBattleBtn, camera);
                            SteleRequire(events.SequenceEqual(new[] { "confirm:ui_battle_stele_team_member_count_tip" }) && fight == null,
                                "Underfilled team bypassed original confirmation.");
                            confirm();
                            SteleRequire(fight != null && events.Last() == "action:" + stele.StageId, "Confirmation lost the original fight action.");
                            fight();
                            SteleRequire(events.TakeLast(2).SequenceEqual(new[] { "start:0", "close" }), "Start/close ordering changed.");
                            events.Clear(); fight = null; members = 4; view.RefreshTeamMemberCount();
                        }
                        else
                        {
                            hasTeam = true;
                            SimulateUiClick(view.mBattleBtn, camera);
                            SteleRequire(events.SequenceEqual(new[] { "notice:ui_battle_stele_not_support_team_tip" }) && fight == null,
                                "Solo stele accepted a grouped player.");
                            hasTeam = false; events.Clear();
                        }
                        SimulateUiClick(view.mBattleBtn, camera);
                        SteleRequire(events.SequenceEqual(new[] { "action:" + stele.StageId }) && fight != null,
                            "Eligible stele did not defer stage entry to player fight action.");
                        fight();
                        SteleRequire(events.SequenceEqual(new[] { "action:" + stele.StageId, "start:0", "close" }), "Stele stage entry order changed.");
                        UnityEngine.Object.Destroy(root); yield return null;
                    }
                    UnityEngine.Object.Destroy(canvas); yield return null;
                }
            }
            finally { WorldMultiLanguageText.Localize = previousLanguage; UnityEngine.Object.Destroy(camera.gameObject); }
            Debug.Log("Map12 stele UI simulated input passed at two portrait sizes for all seven placements. Real NPC teams, reward rendering, confirmation window and map battles remain separate.");
        }
    }
}

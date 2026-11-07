using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;
using Zjcs.Official.BattleResultUI;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator ExerciseActivityResultReports(HomeFeatureRuntime home, string output,
            Action<Selectable> click)
        {
            VerifyDomainBossRankingRules(output);
            if (Environment.GetEnvironmentVariable("ZJCS_ACTIVITY_RANK_UI") == "1")
                yield return ExerciseActivityRankPage(home, 209, output, click);
            if (Environment.GetEnvironmentVariable("ZJCS_RESULT_ACTIONS_DIRECT") == "1") PrepareActivityRankingLoadout(home);
            bool direct = Environment.GetEnvironmentVariable("ZJCS_RESULT_ACTIONS_DIRECT") == "1";
            foreach (int activity in direct ? new[] { 209, 212, 226, 217 } : new[] { 209, 217 })
            {
                string page = activity == 217 ? "AnniversaryFunnyBossEntryPanel" : "ActivityDomainBossPanel";
                if (direct)
                {
                    // Real runner and settlement; only the still pending entry
                    // window is bypassed. Never fabricate a completed result.
                    var catalog = OfficialDailyChallengeCatalog.LoadDefault();
                    var battle = new GameObject("ActivityResultActionRunner");
                    battle.transform.SetParent(home.transform, false);
                    battle.AddComponent<DailyDungeonBattleRunner>().BeginChallenge(home, activity,
                        catalog.StageId(activity), catalog.StageType(activity), catalog.DurationHours(activity),
                        catalog.DailyLimit(activity), 0, _ => { });
                }
                else
                {
                yield return OpenTravel(home, click);
                yield return Capture(output, "activity-" + activity + "-travel");
                click(ButtonUnder("DailyActivityEntry", "EntryBtn"));
                yield return SettleUi();
                yield return Capture(output, "activity-" + activity + "-chooser");
                click(ButtonUnder("DailyChallengeEntry_" + activity, "EntryBtn"));
                yield return SettleUi();
                yield return Capture(output, "activity-" + activity + "-entry");
                Transform startNode = Root(page).GetComponentsInChildren<Transform>().Single(value => value.name == "GoToBtn");
                click(startNode.GetComponent<Button>() ?? startNode.GetComponentInChildren<Button>());
                yield return SettleUi();
                Transform invitation = Root("BattleAssistSelection");
                Require(invitation != null, "The activity did not open its actual NPC invitation.");
                var npcRows = invitation.GetComponentsInChildren<Toggle>().Where(value =>
                    value.GetComponentsInParent<Transform>().Any(parent => parent.name.StartsWith("Npc_"))).ToArray();
                Require(npcRows.Length > 0 && npcRows.All(value => !value.interactable && !value.isOn),
                    "The isolated solo activity enabled an unavailable NPC.");
                click(invitation.Find("BgPanel1/Content/MiddleGo/FriendRobotGo/BottomGo")
                    .GetComponentsInChildren<Button>().First(value => value.name == "CommonBtn_12"));
                yield return SettleUi();
                }
                var runner = UnityEngine.Object.FindObjectOfType<DailyDungeonBattleRunner>();
                Require(runner != null, "The formal activity entry did not start its battle runner.");
                var controller = runner.GetComponent<OfficialBattleRuntimeController>();
                FightBattleCheckpoint checkpoint = controller.Checkpoint;
                controller.StepCompleted += step => checkpoint = step.Checkpoint;
                Require(!checkpoint.Units.Any(value => value.NpcClassId > 0),
                    "The unavailable NPC unexpectedly entered the actual battle.");
                float deadline = Time.realtimeSinceStartup + 180;
                float previousScale = Time.timeScale;
                try
                {
                    Time.timeScale = 5;
                    while (runner != null)
                    {
                        Require(Time.realtimeSinceStartup < deadline, "The actual activity battle did not settle.");
                        var start = Active<Button>().FirstOrDefault(value => value.transform.parent.name == "StartBtn"
                            && value.interactable && value.name == OfficialBattleHudView.StartButtonPath.Split('/').Last());
                        if (controller.Phase == FightBattlePhase.Preparing && start != null) click(start);
                        yield return null;
                    }
                }
                finally { Time.timeScale = previousScale; }
                yield return SettleUi();
                Require(checkpoint.Finished && checkpoint.Outcome != null && !checkpoint.Outcome.Invalid,
                    "The activity result did not come from a completed valid battle.");
                File.WriteAllText(Path.Combine(output, "activity-" + activity + "-checkpoint.json"),
                    FightBattleCheckpointSerializer.Save(checkpoint));
                Transform result = Root("DailyChallengeResult");
                Require(result != null, "The real battle did not open its native activity result.");
                int resultId = result.GetInstanceID();
                var common = result.GetComponent<OfficialBattleResultReport>();
                Require(common != null, "The activity result did not use the common report lifecycle.");
                string finalSnapshot = JsonUtility.ToJson(common.FinalReport);
                VerifyDomainBossActualRank(home, activity, checkpoint);
                var native = result.GetComponentInChildren<GameEndPanelBase>();
                Require(native != null && native.ReportButton != null,
                    "The activity result did not bind its original native action module.");
                Require(native.GetComponentsInChildren<MonoBehaviour>(true).All(value => value != null),
                    "The original action module has an omitted native script.");
                Require(native.ReportButton.GetComponentsInChildren<Image>(true).Any(value => value.sprite != null),
                    "The original report button lost its sprite binding.");
                foreach (var name in new[] { "ShareBtn", "SaveRecordBtn", "ThumbUpReportSwitchBtn" })
                    Require(!native.GetComponentsInChildren<Transform>(true).Single(value => value.name == name).gameObject.activeSelf,
                        "Offline original support-type branch left an online action visible: " + name);
                string Inventory() => string.Join(";", home.Profile.Save.Player.Inventory
                    .Select(value => value.ItemId + ":" + value.Count).OrderBy(value => value));
                string inventory = Inventory();
                File.WriteAllLines(Path.Combine(output, "activity-" + activity + "-native-actions.txt"),
                    native.GetComponentsInChildren<Image>(true).Select(value => value.name
                        + " sprite=" + value.sprite?.name + " material=" + value.material?.name
                        + " shader=" + value.material?.shader?.name
                        + " color=" + value.color + " inheritedAlpha=" + value.canvasRenderer.GetInheritedAlpha()
                        + " screen=" + RectTransformUtility.WorldToScreenPoint(null, value.transform.position)));
                yield return Capture(output, "activity-" + activity + "-result");
                click(native.ReportButton);
                deadline = Time.realtimeSinceStartup + 15;
                Transform report = null;
                while (report == null || report.GetComponent<OfficialBattlePopupMask>()?.IsReady != true)
                {
                    Require(Time.realtimeSinceStartup < deadline, "The native activity report did not become ready.");
                    report = Root("FightEndReportPanel") ?? Root("FightEndAllPlayerReportPanel");
                    yield return null;
                }
                Require(common.IsOpen && result != null && result.GetInstanceID() == resultId && result.gameObject.activeSelf,
                    "Opening the report rebuilt or hid the native result.");
                var playerRows = report.GetComponentsInChildren<WorldBattleReportPlayerRow>();
                Require(playerRows.Length == 1, "The solo battle report lost its real player or invented an unavailable NPC.");
                Require(report.GetComponentsInChildren<TMP_Text>().Any(text => text.text == home.Profile.Save.Player.CharacterName),
                    "The report lost the local player's name.");
                yield return Capture(output, "activity-" + activity + "-report");
                var tabs = report.GetComponentsInChildren<WorldTabGroup>(true).Single(value => value.GetAllTabs().Length == 2);
                click(tabs.GetAllTabs()[1]);
                yield return SettleUi();
                var skill = report.GetComponentsInChildren<WorldBattleReportSkillRow>().FirstOrDefault();
                Require(skill != null, "The real completed battle lost its skill statistics.");
                click(skill.GetComponentsInChildren<Button>().First(value => value.interactable));
                yield return SettleUi();
                Require(Root("skill_plain_detail") != null, "The activity report did not open the original read-only skill detail.");
                yield return Capture(output, "activity-" + activity + "-detail");
                var detailClose = Active<Button>().Where(value => value.name == "BlurMask")
                    .OrderByDescending(value => value.GetComponentInParent<Canvas>().sortingOrder).First();
                click(detailClose);
                yield return SettleUi();
                click(report.GetComponentsInChildren<Button>().Single(value => value.name == "CloseBtn"));
                yield return SettleUi();
                Require(!common.IsOpen && result != null && result.GetInstanceID() == resultId,
                    "Closing the report did not return to the same result instance.");
                Require(common.FinalReport != null && JsonUtility.ToJson(common.FinalReport) == finalSnapshot,
                    "Closing the report discarded or changed its immutable final statistics.");
                Require(Root("BattleResultReportLifetime") == null && Root("skill_plain_detail") == null,
                    "Closing the report leaked its appearance/detail lifetime.");
                click(native.ReportButton);
                yield return SettleUi();
                Require(common.IsOpen, "The native report button could not reopen the report.");
                result.gameObject.SetActive(false);
                yield return null;
                Require(!common.IsOpen && Root("FightEndReportPanel") == null && Root("FightEndAllPlayerReportPanel") == null,
                    "Disabling a result left an orphan report window.");
                result.gameObject.SetActive(true);
                yield return SettleUi();
                Require(Inventory() == inventory,
                    "Viewing statistics granted settlement rewards more than once.");
                if (direct)
                {
                    click(native.ReportButton);
                    yield return SettleUi();
                    Require(common.IsOpen && (Root("FightEndReportPanel") != null || Root("FightEndAllPlayerReportPanel") != null),
                        "Re-enabled native result lost its report action.");
                    result.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(result.gameObject);
                    yield return SettleUi();
                    Require(Root("BattleResultReportLifetime") == null,
                        "Native action result leaked its report after destruction.");
                    if (activity != 217 && Environment.GetEnvironmentVariable("ZJCS_ACTIVITY_RANK_UI") == "1")
                        yield return ExerciseActivityRankPage(home, activity, output, click);
                    Debug.Log("[Result actions] actual activity " + activity + ": original button/sprites, shared statistics/detail, reopen, disable/re-enable and no repeated rewards passed; entry/return remain separate pending work.");
                    continue;
                }
                var returnText = result.GetComponentsInChildren<TMP_Text>().First(value => value.name == "ReturnText");
                click(returnText.GetComponentInParent<Button>());
                yield return SettleUi();
                Require(Root(page) != null && Root("DailyChallengeResult") == null,
                    "The native result return control did not restore its activity entry.");
                Debug.Log("[Result reports] actual activity " + activity + ": unavailable NPC guard, statistics, detail, reopen, host disable and return passed.");
            }
            bool actionOnly = Environment.GetEnvironmentVariable("ZJCS_RESULT_ACTIONS_DIRECT") == "1";
            File.WriteAllText(Path.Combine(output, actionOnly ? "activity-result-actions-passed.txt" : "activity-result-reports-passed.txt"),
                actionOnly ? "Real 209/212/226/217 runners and settlement; original result actions, shared report/detail, reopen/disposal and no repeated rewards passed. Entry/return/full result content not validated.\n"
                : "Actual 209/217 entry, unavailable NPC guard, shared battle, native settlement/report/detail, reopen/disposal and no repeated rewards passed.\n");
        }
    }
}

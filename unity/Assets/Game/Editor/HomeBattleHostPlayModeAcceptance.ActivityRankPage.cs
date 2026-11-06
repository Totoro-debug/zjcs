using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ActivityRankUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator ExerciseActivityRankPage(HomeFeatureRuntime home, int activity, string output, Action<Selectable> click)
        {
            long? score = home.DailyChallenges.DomainBossMaxDamage(activity, DateTime.Now,
                OfficialDailyChallengeCatalog.LoadDefault().DurationHours(activity));
            home.Hud.OpenDailyChallengeRank(activity);
            yield return new WaitForSecondsRealtime(1.5f);
            var view = UnityEngine.Object.FindObjectOfType<ActivityDomainBossRankView>();
            Require(view != null && view.Content != null, "The original activity rank page was not opened.");
            var source = view.Content.GetComponent<TeamTopContentDataActivityDomainBoss>();
            Require(source.GetShowCount() == (score.HasValue ? 1 : 0), "Local actual results did not reach the original rank provider.");
            Require(source.GetMySelfData().Rank == (score.HasValue ? 0 : -1), "The original self/empty rank state is incorrect.");
            Require(view.Content.GetComponentsInChildren<MonoBehaviour>(true).All(item => item != null),
                "The original rank page still has a missing native script.");
            Require(view.Content.GetComponentsInChildren<TMP_Text>().All(item =>
                !item.text.StartsWith("SecretAree") && !item.text.StartsWith("TopRangeType.")),
                "The original dynamic rank labels were not localized.");
            if (score.HasValue)
            {
                Require(view.Content.GetComponentsInChildren<TMP_Text>().Any(item => item.text == home.Profile.Save.Player.CharacterName),
                    "The original first-place row lost the current character name.");
                Require(view.Content.GetComponentsInChildren<TeamPlayerIcon>().Any(item => item.InitFinished),
                    "The original rank actor did not finish initialization.");
                Require(source.GetDataByIndex(0).Score.ToString().Contains(WorldBattleReportNumberFormat.Compact(score.Value)),
                    "The original damage label lost the actual score.");
            }
            yield return Capture(output, "activity-rank-" + activity + (score.HasValue ? "-actual" : "-empty"));
            var range = view.Content.GetComponentInChildren<SwitchTopRangeBtn>();
            Require(range != null && range.RangeType == TopRangeType.Planes, "The original initial range is incorrect.");
            click(range.Buton);
            yield return SettleUi();
            Require(range.RangeType == TopRangeType.Domain && source.GetShowCount() == (score.HasValue ? 1 : 0),
                "The original range button did not preserve the approved local actual ranking.");
            var ruleButtons = view.Content.GetComponentsInChildren<RuleBtn>();
            foreach (var button in ruleButtons)
            {
                click(button.GetComponent<Button>());
                yield return SettleUi();
                var dialog = Root("ActivityRankRuleDialog");
                Require(dialog != null && dialog.GetComponentsInChildren<TipInfo>().Length > 0,
                    "The original rule button did not populate its configured rule dialog.");
                Require(dialog.GetComponentsInChildren<TMP_Text>().All(item => !item.text.StartsWith("rule_tip_")),
                    "The original rule strings were not localized.");
                yield return Capture(output, "activity-rank-" + activity + "-rule-" + button.name);
                var close = dialog.GetComponentsInChildren<CloseWindowBtn>().First(item => item.myButton.isActiveAndEnabled);
                click(close.myButton);
                yield return SettleUi();
                Require(Root("ActivityRankRuleDialog") == null && view.Content.activeInHierarchy,
                    "Closing the original rule dialog did not return to the same rank page.");
            }
            var closeRank = view.Content.GetComponentsInChildren<CloseWindowBtn>().First(item => item.myButton.isActiveAndEnabled);
            click(closeRank.myButton);
            yield return SettleUi();
            Require(UnityEngine.Object.FindObjectOfType<ActivityDomainBossRankView>() == null,
                "Closing the original rank page leaked its binding lifetime.");
            Debug.Log("[Activity rank page] activity=" + activity + " score=" + (score.HasValue ? score.ToString() : "empty")
                + "; original first/self/empty rows, actor, range click and window disposal passed; active original rule buttons=" + ruleButtons.Length + ".");
        }
    }
}

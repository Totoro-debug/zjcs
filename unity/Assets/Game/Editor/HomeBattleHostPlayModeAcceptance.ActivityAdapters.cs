using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        // Exercises the production battle entry seam independently of the unfinished
        // activity page. Combat still advances through the real controller and input path.
        private static IEnumerator RunActivityBattleAdapters(HomeFeatureRuntime home,
            string output, Action<Selectable> click)
        {
            var catalog = OfficialDailyChallengeCatalog.LoadDefault();
            foreach (int activityId in new[] { 209, 212, 226, 217 })
            {
                bool completed = false;
                Action verifyHome = CaptureHomeRestoration(home);
                var node = new GameObject("ActivityBattleAdapter_" + activityId);
                node.transform.SetParent(home.transform, false);
                var runner = node.AddComponent<DailyDungeonBattleRunner>();
                runner.BeginChallenge(home, activityId, catalog.StageId(activityId),
                    catalog.StageType(activityId), catalog.DurationHours(activityId),
                    catalog.DailyLimit(activityId), 0, _ => completed = true);
                yield return RunBattle<DailyDungeonBattleRunner>("activity-" + activityId, output, click);
                Require(completed, "Activity " + activityId + " did not deliver its real settlement callback.");
                verifyHome();
                // OnBattleCompleted opens the production result page. Its full visual and
                // return-input migration is tracked separately from the battle module.
                yield return SettleUi();
                for (int i = 0; i < 8 && home.Hud.TryHandleBack(); i++) yield return SettleUi();
            }
            Debug.Log("[Battle hosts] all four official activity configurations passed through the shared battle module; activity page entry is not covered.");
        }
    }
}

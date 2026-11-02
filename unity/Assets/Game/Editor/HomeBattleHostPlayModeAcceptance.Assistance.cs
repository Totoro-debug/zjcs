using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        // An isolated seam fixture, not the festival activity's entry or reward adapter.
        private static IEnumerator RunConfiguredAssistHost(HomeFeatureRuntime home, int roomId,
            string output, Action<Selectable> click)
        {
            const int playType = (int)NpcAssistPlayType.FestivalBoss;
            const string entryId = "battle-host-assistance-fixture";
            int stageId = home.DailyDungeons.FindRoom(roomId).stageId;
            var invite = home.NpcAssistanceCatalog.InviteRule(playType);
            Require(home.NpcAssistanceProgress.SetSelection(playType, entryId, Array.Empty<int>(),
                invite.MaxPlayer), "Could not prepare the isolated assistance selection.");
            var snapshot = home.NpcAssistanceProgress.CommitBattle(playType, entryId, stageId,
                invite.MaxPlayer, invite.OneNpcMaxInviteCount, invite.MaxInviteCount,
                (NpcAssistRefreshCycle)invite.RefreshCycle, DateTimeOffset.Now.ToUnixTimeSeconds());
            string committed = JsonUtility.ToJson(snapshot);
            Action verifyHome = CaptureHomeRestoration(home);
            var options = new OfficialBattleAssistOptions { PlayType = playType, EntryId = entryId };
            var first = new GameObject("ConfiguredAssistBeforeReload");
            first.transform.SetParent(home.transform, false);
            first.AddComponent<DailyDungeonBattleRunner>().Begin(home, roomId, "legacy-default-entry",
                _ => throw new InvalidOperationException("Preparing a battle settled it."), options);
            first.GetComponent<OfficialBattleRuntimeController>().SetPaused(true);
            Require(!string.IsNullOrEmpty(home.DailyDungeons.ActiveRun.activeBattleCheckpoint),
                "The configured battle did not persist its checkpoint.");
            // Later edits by the caller must not change the identity of the active battle.
            options.PlayType = (int)NpcAssistPlayType.All;
            options.EntryId = "changed-after-begin";
            UnityEngine.Object.Destroy(first);
            yield return null;
            yield return SettleUi();
            verifyHome();
            Require(JsonUtility.ToJson(home.NpcAssistanceProgress.ActiveBattle) == committed,
                "Pausing/disposal modified or consumed the committed assistance transaction.");

            var wrong = new GameObject("WrongAssistRestore");
            bool rejected = false;
            try
            {
                wrong.AddComponent<DailyDungeonBattleRunner>().Begin(home, roomId, entryId, _ => { });
            }
            catch (InvalidOperationException) { rejected = true; }
            finally { UnityEngine.Object.Destroy(wrong); }
            Require(rejected && JsonUtility.ToJson(home.NpcAssistanceProgress.ActiveBattle) == committed,
                "Restoring with another play type did not preserve and reject the active snapshot.");
            yield return null;

            bool callback = false;
            var resumed = new GameObject("DailyDungeonBattleRunner");
            resumed.transform.SetParent(home.transform, false);
            resumed.AddComponent<DailyDungeonBattleRunner>().Begin(home, roomId, "legacy-default-entry",
                _ => callback = true, new OfficialBattleAssistOptions { PlayType = playType, EntryId = entryId });
            Require(JsonUtility.ToJson(home.NpcAssistanceProgress.ActiveBattle) == committed,
                "Restoring the configured battle recommitted its assistance transaction.");
            yield return RunBattle<DailyDungeonBattleRunner>("daily-assist", output, click);
            verifyHome();
            Require(callback && home.NpcAssistanceProgress.ActiveBattle == null,
                "The shared host failed to finish the configured assistance identity.");
            File.WriteAllText(Path.Combine(output, "battle-assistance-result.txt"),
                "PlayType 5 custom entry: checkpoint restore, default-type rejection, unchanged committed snapshot, "
                + "actual battle completion, assistance cleanup and host restoration passed.\n");
        }
    }
}

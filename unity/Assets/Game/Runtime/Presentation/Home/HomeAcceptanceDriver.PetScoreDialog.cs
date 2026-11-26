using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.GachaUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.GachaUI;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeAcceptanceDriver
    {
        private IEnumerator VerifyPetScoreDialog(HomeFeatureRuntime runtime, Action<bool> completed)
        {
            var windows = PrivateField<OfflineHomeCurrencyWindows>(runtime.Hud, "nativeCurrencyWindows");
            ScoreRewardDialog active = null;
            Action<GameObject> opened = root => active = root.GetComponent<ScoreRewardDialog>();
            windows.WindowOpened += opened;
            bool passed = true;
            IEnumerator Open()
            {
                active = null;
                typeof(HomeHudView).GetMethod("ShowPetScoreRewards", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(runtime.Hud, null);
                for (int frame = 0; frame < 240 && (!active || active.GetComponent<WorldPopupWindow>().State != WorldPopupWindow.EState.Show); frame++)
                    yield return null;
                Assert(active && active.GetComponent<WorldPopupWindow>().State == WorldPopupWindow.EState.Show,
                    "production pet score entry opens the original popup");
            }
            IEnumerator Close()
            {
                var popup = active.GetComponent<WorldPopupWindow>();
                var mask = runtime.Hud.Canvas.GetComponentsInChildren<WorldWindowMask>().Single(row => row.AttachWindow == popup);
                // Mask clicks use this process's EventSystem only.
                ClickPartnerUi(mask.Button, new Vector2(Screen.width * .02f, Screen.height * .02f));
                for (int frame = 0; frame < 240 && windows.WindowCount != 0; frame++) yield return null;
                Assert(windows.WindowCount == 0, "original mask closes and releases the score popup");
            }
            AchievementPanelItem[] Rows() => PrivateField<System.Collections.Generic.List<AchievementPanelItem>>(
                active.AchievementPanel.AchievementPanelBase, "mPanelItemList").Where(row => row.gameObject.activeInHierarchy).ToArray();
            OfflinePetScoreRewardServices Rewards()
            {
                var context = PrivateField<object>(runtime.Hud, "petNativeScore");
                return (OfflinePetScoreRewardServices)context.GetType().GetField("Rewards").GetValue(context);
            }
            try
            {
                // Load the existing popup first: separate recovered font assets
                // with the same original name previously collided in its service.
                WorldConfirmDialog primer = null;
                windows.PopupConfirm(dialog => { primer = dialog; dialog.Init("confirmation", () => { }, () => { }); });
                for (int frame = 0; frame < 240 && (!primer || primer.PopupWindow.State != WorldPopupWindow.EState.Show); frame++)
                    yield return null;
                Assert(primer && primer.PopupWindow.State == WorldPopupWindow.EState.Show, "shared confirmation font context is loaded");
                ClickPartnerUi(primer.CancelBtn);
                for (int frame = 0; frame < 240 && windows.WindowCount != 0; frame++) yield return null;
                Assert(windows.WindowCount == 0, "font primer closes before opening the score popup");
                Assert(runtime.Profile.ApplyInventoryTransaction("acceptance:score-dialog-lock", save => {
                    save.Player.UnlockedMapClassIds.Remove(14);
                    save.Player.PetGacha.Score = Math.Max(save.Player.PetGacha.Score, 1100);
                }).IsApplied, "isolated score detail fixture");
                yield return Open();
                var assets = OfficialScoreRewardDialogAssetCatalog.LoadDefault();
                var rule = new GachaTextKeyArgWrap(runtime.PetGacha.Catalog.Gacha.ScoreRuleText,
                    runtime.PetGacha.Catalog.Chest.Rewards.First(row => row.ItemId == runtime.PetGacha.Catalog.Gacha.RewardScoreId).Count);
                var key = new ScoreRewardKey(runtime.PetGacha.Catalog.Gacha.ScoreAchievementType, 16);
                var rewards = Rewards();
                var rows = Rows();
                passed &= Check("pet-gacha-controls-score-dialog-open", () => {
#if UNITY_EDITOR
                    Assert(active.GetComponentsInChildren<Transform>(true).All(row => UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(row.gameObject) == 0),
                        "complete native score hierarchy uses real scripts");
#endif
                    Assert(rows.Length == 5 && rows[0].StateSwitcher.State == OfficialAchievementState.Completed,
                        "original five authored rows show actual unclaimed score milestones");
                    Assert(rows.All(row => row.TitleText.myKey == "ui_achievement_content_PetGachaScoreCount"
                        && PrivateField<WorldStateSwitcher<bool>>(row, "mTipDotStateSwitcher").State),
                        "source content handler and locked AchievementGroup tip state");
                    Assert(active.ScoreRuleText.Text == string.Format(assets.Text(rule.Key), rule.Args)
                        && !PrivateField<WorldStateSwitcher<bool>>(active, "mRewardCompletedState").State,
                        "source rule arguments and incomplete state are initialized after Awake");
                    return "production entry renders the complete original score popup, rows, localized rule and group-limit state";
                });
                yield return Capture("pet-gacha-score-dialog-ready.png");
                var list = active.GetComponentsInChildren<ScrollRect>().Single(scroll =>
                    scroll.GetComponentsInChildren<AchievementPanelItem>(true).Length == 5);
                void Wheel(float delta)
                {
                    Canvas.ForceUpdateCanvases();
                    var viewport = list.viewport;
                    var pointer = new PointerEventData(EventSystem.current) {
                        position = RectTransformUtility.WorldToScreenPoint(null, viewport.TransformPoint(viewport.rect.center)),
                        scrollDelta = new Vector2(0, delta)
                    };
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointer, hits);
                    Assert(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(list.transform), "original achievement viewport receives the simulated wheel");
                    ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.scrollHandler);
                }
                float top = list.verticalNormalizedPosition;
                Wheel(-1000);
                for (int frame = 0; frame < 120 && (list.verticalNormalizedPosition > .01f || list.verticalNormalizedPosition < -.01f); frame++)
                    yield return null;
                passed &= Check("pet-gacha-controls-score-dialog-scroll", () => {
                    Assert(list.verticalNormalizedPosition < top - .1f && Mathf.Abs(list.verticalNormalizedPosition) <= .01f,
                        "native achievement list reaches and settles at its bottom");
                    var viewport = new Vector3[4]; list.viewport.GetWorldCorners(viewport);
                    var last = new Vector3[4]; Rows()[4].GetComponent<RectTransform>().GetWorldCorners(last);
                    Assert(last[0].y >= viewport[0].y - 1 && last[1].y <= viewport[1].y + 1,
                        "the fifth original achievement row is fully reachable inside the native viewport");
                    return "process-local wheel input reaches the original ScrollRect and exposes the fifth authored row";
                });
                yield return Capture("pet-gacha-score-dialog-scroll.png");
                Wheel(1000);
                for (int frame = 0; frame < 120 && Mathf.Abs(list.verticalNormalizedPosition - 1) > .01f; frame++) yield return null;
                int icons = active.GetComponentsInChildren<ItemIcon>(true).Length;
                string beforeText = rows[0].ProgressText.Text;
                Assert(runtime.Profile.ApplyInventoryTransaction("acceptance:score-dialog-progress", save => save.Player.PetGacha.Score++).IsApplied,
                    "isolated real score progress transaction");
                runtime.RefreshCounts();
                passed &= Check("pet-gacha-controls-score-dialog-progress", () => {
                    Assert(rows[0].ProgressText.Text != beforeText && active.GetComponentsInChildren<ItemIcon>(true).Length == icons
                        && rows[0].StateSwitcher.State == OfficialAchievementState.Completed,
                        "committed score synchronizes matching task progress without replacing reward icons or captured states");
                    return "actual save update reaches the source blackboard-only row refresh";
                });
                var available = runtime.PetGacha.Achievements(16, 0, 100).Where(row => row.IsReady && !row.IsClaimed).ToArray();
                Assert(available.Length > 0 && !runtime.PetGacha.GetAchievement(16, 99).IsReady,
                    "fixture covers every currently available original score milestone");
                var expected = available.SelectMany(row => row.Rewards).GroupBy(row => row.ItemId)
                    .ToDictionary(group => group.Key, group => runtime.Profile.InventoryCount(group.Key) + group.Sum(row => row.Count));
                int receipts = runtime.Profile.Save.Player.AppliedInventoryEventIds.Count;
                ClickPartnerUi(rows[0].GetRewardBtn);
                yield return null;
                passed &= Check("pet-gacha-controls-score-dialog-claim", () => {
                    Assert(runtime.Profile.Save.Player.AppliedInventoryEventIds.Count == receipts + 1
                        && available.All(row => runtime.PetGacha.GetAchievement(16, row.Index).IsClaimed)
                        && expected.All(row => runtime.Profile.InventoryCount(row.Key) == row.Value),
                        "original group/-1 request grants every available configured reward and records one atomic receipt");
                    Assert(Rows().All(row => row.StateSwitcher.State == OfficialAchievementState.Processing),
                        "source success callback removes captured completed rows");
                    return "native row claim preserves original group request, rewards and callback order while group-tip function is locked";
                });
                receipts++;
                // Processing hides GetAwardBtn in the original state config.
                // Invoke its stale callback directly to exercise the race guard.
                Rows()[0].GetRewardBtn.onClick.Invoke();
                Assert(runtime.Profile.Save.Player.AppliedInventoryEventIds.Count == receipts,
                    "unready original row click cannot grant again");
                active.Init(true, key, rule);
                Assert(PrivateField<WorldStateSwitcher<bool>>(active, "mRewardCompletedState").State
                    && !active.transform.Find("BgDialog1/Content").gameObject.activeSelf,
                    "original complete state hides content and displays its authored empty branch");
                yield return Capture("pet-gacha-score-dialog-completed.png");
                active.Init(false, key, rule);
                Assert(Rows().Length == 5 && Rows().All(row => row.StateSwitcher.State == OfficialAchievementState.Processing),
                    "original incomplete state restores the task rows");
                passed &= Check("pet-gacha-controls-score-dialog-branches", () =>
                    "unready click guard and original complete/incomplete native state branches verified");
                yield return Close();
                Assert(!rewards.HasPlayer, "closed popup disposes its local reward event source");
                runtime.Profile.Reload();
                passed &= Check("pet-gacha-controls-score-dialog-restore", () => {
                    Assert(available.All(row => runtime.PetGacha.GetAchievement(16, row.Index).IsClaimed)
                        && expected.All(row => runtime.Profile.InventoryCount(row.Key) == row.Value),
                        "actual score-detail rewards and records restore from the isolated profile");
                    return "mask closure, source release and persisted local group claim verified";
                });
                Assert(runtime.Profile.ApplyInventoryTransaction("acceptance:score-dialog-unlock", save =>
                    save.Player.UnlockedMapClassIds.Add(14)).IsApplied, "isolated group function unlock");
                yield return Open();
                passed &= Check("pet-gacha-controls-score-dialog-reopen", () => {
                    Assert(Rows().Length == 5 && Rows().All(row => row.StateSwitcher.State == OfficialAchievementState.Processing
                        && !PrivateField<WorldStateSwitcher<bool>>(row, "mTipDotStateSwitcher").State),
                        "reopened source list uses restored claims and actual unlocked group function");
                    return "reopen restores the next original milestones and unlocked native tip state";
                });
                yield return Capture("pet-gacha-score-dialog-next.png");
                yield return Close();
                completed(passed);
            }
            finally { windows.WindowOpened -= opened; }
        }
    }
}

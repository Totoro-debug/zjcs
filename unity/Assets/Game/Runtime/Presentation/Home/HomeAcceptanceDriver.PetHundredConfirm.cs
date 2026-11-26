using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.GachaUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.GachaUI;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.HomeUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeAcceptanceDriver
    {
        private IEnumerator VerifyPetHundredConfirm(HomeFeatureRuntime runtime, Action<long> balance, Action<bool> completed)
        {
            var windows = PrivateField<OfflineHomeCurrencyWindows>(runtime.Hud, "nativeCurrencyWindows");
            var quantities = PrivateField<OfflineHomeCurrencyPlayer>(runtime.Hud, "nativeCurrencyPlayer");
            var assets = OfficialMarkableConfirmAssetCatalog.LoadDefault();
            MarkableConfirmDialog active = null;
            int opened = 0, draws = 0;
            Func<bool> animationMatches = () => false;
            Action<GameObject> onOpen = root =>
            {
                active = root.GetComponent<MarkableConfirmDialog>();
                opened++;
#if UNITY_EDITOR
                if (!active) return;
                var clip = root.GetComponent<WorldPopupWindow>().mShowClip;
                foreach (var binding in UnityEditor.AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.type != typeof(CanvasGroup) || binding.propertyName != "m_Alpha") continue;
                    var target = (string.IsNullOrEmpty(binding.path) ? root.transform : root.transform.Find(binding.path))
                        ?.GetComponent<CanvasGroup>();
                    var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
                    if (!target || curve == null || Mathf.Abs(curve.Evaluate(0) - curve.Evaluate(clip.length)) < .01f) continue;
                    float initial = target.alpha;
                    animationMatches = () => Mathf.Abs(initial - curve.Evaluate(0)) < .02f
                        && Mathf.Abs(target.alpha - curve.Evaluate(clip.length)) < .02f;
                    break;
                }
#endif
            };
            windows.WindowOpened += onOpen;
            try
            {
                Assert(runtime.Profile.ApplyInventoryTransaction("acceptance:confirm-clear-choice",
                    save => save.Player.PetGacha.HundredReminderSuppressedDayKey = 0).IsApplied, "isolated reminder choice cleared");
                balance(200);
                int receipts = runtime.Profile.Save.Player.AppliedInventoryEventIds.Count;
                int dailyBefore = runtime.PetGacha.DailyCount(DateTime.Now);
                Action<int> draw = count =>
                {
                    Assert(count == 100 && runtime.PetGacha.Draw(count, "acceptance:markable:" + Guid.NewGuid().ToString("N"),
                        DateTimeOffset.Now.ToUnixTimeSeconds(), confirmHundred: true).IsApplied,
                        "original confirmation continues an actual local hundred draw");
                    draws++;
                    quantities.SynchronizeCommittedPlayer();
                };
                void Open(Action<int> continuation)
                {
                    active = null;
                    typeof(HomeHudView).GetMethod("ConfirmPetHundredDraw", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(runtime.Hud, new object[] { 100, continuation });
                }
                IEnumerator Shown()
                {
                    for (int frame = 0; frame < 240 && (!active || active.GetComponent<WorldPopupWindow>().State != WorldPopupWindow.EState.Show); frame++)
                        yield return null;
                    Assert(active && active.GetComponent<WorldPopupWindow>().State == WorldPopupWindow.EState.Show,
                        "original markable window finishes its original show animation");
                }
                IEnumerator Closed()
                {
                    for (int frame = 0; frame < 240 && windows.WindowCount != 0; frame++) yield return null;
                    Assert(windows.WindowCount == 0, "original hide animation releases popup and blur mask");
                }
                Open(draw);
                yield return Shown();
                var inner = active.GetComponent<WorldConfirmDialog>();
                var toggle = active.GetComponentInChildren<Toggle>(true);
                var tip = PrivateField<WorldMultiLanguageText>(active, "mTipTxt");
                bool passed = Check("pet-gacha-controls-confirm-native", () =>
                {
                    Assert(active.GetComponentsInChildren<MonoBehaviour>(true).All(value => value)
                        && !toggle.isOn && inner.PopupWindow.mShowClip && inner.PopupWindow.mHideClip,
                        "all original confirmation components, reset state and animation clips resolve");
                    Assert(animationMatches(), "live popup alpha advances from the original authored show-curve start to its endpoint");
                    Assert(tip.Text == assets.Text("ui_no_prompts_today")
                        && inner.ContentText.GetComponent<TMP_Text>().text == assets.Text("ui_pet_gacha_is_times_hundred"),
                        "confirmation and option use their actual original language entries");
                    var clickSound = active.GetComponentInChildren<Zjcs.Official.ProfessionUI.UIClickSound>(true);
                    Assert(clickSound && clickSound.AkEvent.IsValid
                        && active.GetComponentsInChildren<WorldButtonSound>(true)
                            .All(sound => sound.ClickEvent && sound.ClickEvent.samples > 0 && sound.Post != null),
                        "original toggle event and both locally decoded button clips have live playback bindings");
                    return "complete original modal opens through the production shared blur-mask and popup host";
                });
                yield return Capture("pet-gacha-confirm-unselected.png");
                ClickPartnerUi(toggle);
                Assert(toggle.isOn && PrivateField<WorldStateSwitcher<bool>>(active, "mToggleSelectStateSwitcher").State,
                    "simulated original toggle input drives the authored selected color state");
                yield return Capture("pet-gacha-confirm-selected.png");
                ClickPartnerUi(inner.CancelBtn);
                yield return Closed();
                passed &= Check("pet-gacha-controls-confirm-cancel", () =>
                {
                    runtime.Profile.Reload();
                    Assert(draws == 0 && runtime.Profile.InventoryCount(runtime.PetGacha.Catalog.Gacha.CostItemId) == 200
                        && runtime.PetGacha.State.HundredReminderSuppressedDayKey == 0
                        && runtime.Profile.Save.Player.AppliedInventoryEventIds.Count == receipts,
                        "cancel after selecting neither draws nor persists the option");
                    return "real native toggle and cancel leave inventory, draw count and saved choice unchanged";
                });
                Open(draw);
                yield return Shown();
                Assert(!active.GetComponentInChildren<Toggle>(true).isOn, "reopened original option resets to false");
                ClickPartnerUi(active.GetComponent<WorldConfirmDialog>().ConfirmBtn);
                yield return Closed();
                passed &= Check("pet-gacha-controls-confirm-unselected", () =>
                {
                    runtime.Profile.Reload();
                    Assert(draws == 1 && runtime.Profile.InventoryCount(runtime.PetGacha.Catalog.Gacha.CostItemId) == 100
                        && runtime.PetGacha.State.HundredReminderSuppressedDayKey == 0,
                        "unselected confirm performs one hundred draw and retains future reminders");
                    return "native unselected confirmation commits the actual hundred rewards and costs once";
                });
                Open(draw);
                yield return Shown();
                ClickPartnerUi(active.GetComponentInChildren<Toggle>(true));
                ClickPartnerUi(active.GetComponent<WorldConfirmDialog>().ConfirmBtn);
                yield return Closed();
                passed &= Check("pet-gacha-controls-confirm-selected", () =>
                {
                    runtime.Profile.Reload();
                    Assert(draws == 2 && runtime.Profile.InventoryCount(runtime.PetGacha.Catalog.Gacha.CostItemId) == 0
                        && runtime.PetGacha.DailyCount(DateTime.Now) == dailyBefore + 200
                        && !runtime.PetGacha.ShouldConfirmHundred(DateTime.Now),
                        "selected confirmation persists its independent choice and actual hundred transaction");
                    return "native selected confirmation and real profile reload preserve both choice and draw settlement";
                });
                passed &= Check("pet-gacha-controls-confirm-bypass", () =>
                {
                    int before = opened, count = 0;
                    Open(value => count += value);
                    Assert(count == 100 && opened == before && windows.WindowCount == 0,
                        "same-day production confirmation bypasses popup and forwards original count");
                    var rollback = new OfflinePetHundredReminderServices(runtime.PetGacha, () => DateTime.Now.AddDays(-1),
                        _ => throw new InvalidOperationException("Rollback must not open a popup"));
                    GachaPanelUtils.HundredGachePreRemind(rollback, runtime.PetGacha.Catalog.Gacha.GachaType, 100, value => count += value);
                    var immediate = new OfflinePetHundredReminderServices(runtime.PetGacha,
                        () => throw new InvalidOperationException("Small draws do not read the reminder clock"),
                        _ => throw new InvalidOperationException("Small draws do not open a popup"));
                    GachaPanelUtils.HundredGachePreRemind(immediate, runtime.PetGacha.Catalog.Gacha.GachaType, 10, value => count += value);
                    Assert(count == 210, "rollback and small-draw branches continue exactly once");
                    return "shared original reminder bypasses saved days, rollback and sub-hundred counts";
                });
                DateTime clock = new DateTime(2031, 5, 20, 5, 59, 59, DateTimeKind.Local);
                int continued = 0;
                var delayed = new OfflinePetHundredReminderServices(runtime.PetGacha, () => clock,
                    initialize => windows.PopupConfirm(assets.Prefab, assets.Text,
                        dialog => initialize(dialog.GetComponent<MarkableConfirmDialog>())));
                active = null;
                GachaPanelUtils.HundredGachePreRemind(delayed, runtime.PetGacha.Catalog.Gacha.GachaType, 100, value => continued += value);
                yield return Shown();
                Assert(runtime.PetGacha.SuppressHundredReminder(clock.AddDays(2), "acceptance:other-pending-choice").IsApplied,
                    "another pending dialog can save a choice while this original window remains open");
                clock = clock.AddSeconds(1);
                ClickPartnerUi(active.GetComponentInChildren<Toggle>(true));
                ClickPartnerUi(active.GetComponent<WorldConfirmDialog>().ConfirmBtn);
                yield return Closed();
                passed &= Check("pet-gacha-controls-confirm-time", () =>
                {
                    runtime.Profile.Reload();
                    Assert(continued == 100 && runtime.PetGacha.State.HundredReminderSuppressedDayKey == 20310520,
                        "pending confirmation overwrites a newer choice with its actual confirmation day across 06:00");
                    return "native delayed confirmation records the new refresh day before invoking the continuation";
                });
                WorldConfirmDialog legacy = null;
                int legacyConfirmed = 0, legacyCanceled = 0;
                windows.PopupConfirm(dialog =>
                {
                    legacy = dialog;
                    dialog.Init("confirmation", () => legacyConfirmed++, () => legacyCanceled++);
                });
                for (int frame = 0; frame < 240 && (!legacy || legacy.PopupWindow.State != WorldPopupWindow.EState.Show); frame++)
                    yield return null;
                Assert(legacy && legacy.PopupWindow.State == WorldPopupWindow.EState.Show,
                    "existing unmarked confirmation still uses the shared native popup host");
                ClickPartnerUi(legacy.CancelBtn);
                yield return Closed();
                passed &= Check("pet-gacha-controls-confirm-shared-host", () =>
                {
                    Assert(legacyConfirmed == 0 && legacyCanceled == 1,
                        "existing confirmation overload preserves its native cancel callback exactly once");
                    return "shared popup extension also preserves the existing original unmarked confirmation";
                });
                completed(passed);
            }
            finally { windows.WindowOpened -= onOpen; }
        }
    }
}

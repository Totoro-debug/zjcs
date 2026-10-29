using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Commerce;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Commerce;

namespace Zjcs.Editor.Commerce
{
    /// <summary>Play Mode acceptance for the official commerce window and save behaviour.</summary>
    public static class CommercePlayModeAcceptance
    {
        private const string ActiveKey = "Zjcs.Commerce.Acceptance.Active";
        private const string RootKey = "Zjcs.Commerce.Acceptance.Root";
        private const string SlotKey = "Zjcs.Commerce.Acceptance.Slot";
        private const string ResultKey = "Zjcs.Commerce.Acceptance.Result";
        private const string DeadlineKey = "Zjcs.Commerce.Acceptance.Deadline";
        private static bool started;

        [MenuItem("Zjcs/Validate/Commerce Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running commerce acceptance.");
            ConfigurePortraitGameView();
            string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "output", "commerce-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var slots = new ProfileSlotCatalog(root);
            string slot = slots.CreateSlot();
            OfflineMainlineSave save = new OfflineMainlineSave();
            save.CreatedAtUnixSeconds = DateTimeOffset.Now.ToUnixTimeSeconds();
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "商城验收";
            save.Player.Level = 30;
            save.Player.Wardrobe.Profession = 1;
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 15, Count = 2000 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 16, Count = 600 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 2, Count = 100 });
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));

            SessionState.SetString(RootKey, root);
            SessionState.SetString(SlotKey, slot);
            SessionState.SetInt(ResultKey, 2);
            SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 1800f);
            SessionState.SetBool(ActiveKey, true);
            started = false;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void RestoreAfterDomainReload()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                EditorApplication.update -= OnUpdate;
                EditorApplication.update += OnUpdate;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= OnUpdate;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                string root = SessionState.GetString(RootKey, string.Empty);
                bool passed = SessionState.GetInt(ResultKey, 1) == 0;
                if (Directory.Exists(root)) Directory.Delete(root, true);
                SessionState.SetBool(ActiveKey, false);
                if (Application.isBatchMode
                    || Environment.GetEnvironmentVariable("ZJCS_COMMERCE_AUTOMATION") == "1")
                    EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Finish(false, new TimeoutException("Commerce acceptance exceeded its deadline."));
                return;
            }
            if (started) return;
            CommerceScreenController controller = UnityEngine.Object.FindObjectOfType<CommerceScreenController>();
            if (controller != null)
            {
                if (!controller.IsOpen) return;
                started = true;
                Application.logMessageReceived -= OnRuntimeLog;
                Application.logMessageReceived += OnRuntimeLog;
                var runnerObject = new GameObject("CommerceAcceptanceRunner");
                UnityEngine.Object.DontDestroyOnLoad(runnerObject);
                runnerObject.AddComponent<AcceptanceRunner>().StartCoroutine(Guarded(RunScenario(controller)));
                return;
            }
            // No shop exists yet: open it on the acceptance profile.
            string root = SessionState.GetString(RootKey, string.Empty);
            string slot = SessionState.GetString(SlotKey, string.Empty);
            try
            {
                CommerceScreenController.OpenShop(slot, null, null, false, root);
            }
            catch (Exception error)
            {
                Finish(false, error);
            }
        }

        public sealed class AcceptanceRunner : MonoBehaviour
        {
        }

        private static void OnRuntimeLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            if (message != null && (message.Contains("Material is missing texture")
                || message.Contains("Shader")) ) return;
            if (SessionState.GetInt(ResultKey, 1) == 2)
                Finish(false, new InvalidOperationException(message + "\n" + stackTrace));
        }

        private static IEnumerator Guarded(IEnumerator scenario)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(scenario);
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                object value;
                bool moved;
                try
                {
                    moved = current.MoveNext();
                    value = moved ? current.Current : null;
                }
                catch (Exception error)
                {
                    Finish(false, error);
                    yield break;
                }
                if (!moved) { stack.Pop(); continue; }
                if (value is IEnumerator nested) { stack.Push(nested); continue; }
                yield return value;
            }
        }

        private static IEnumerator RunScenario(CommerceScreenController controller)
        {
            yield return WaitUntil(() => controller.MainWindow != null, "The official shop window did not build.");
            yield return WaitFrames(3);
            var runtime = controller.Feature;
            Require(runtime != null && runtime.Offers != null, "The commerce feature runtime is unavailable.");
            Require(runtime.Catalog.Data.directPay.Length == 6, "The six direct-pay tiers are missing.");
            Require(runtime.Catalog.Data.vouchers.Length == 6, "The six voucher tiers are missing.");
            typeof(CommerceScreenController).GetMethod("SwitchPayTab",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller, new object[] { true });
            yield return WaitFrames(2);
            Require(FindByName("CommerceDirect_1") != null, "The official PayDirectPayItem prefab was not created.");
            Require(FindAnyByName("CommerceVoucher_1") != null, "The official PayVoucherItem prefab was not created.");
            yield return Capture("commerce-01-direct");
            ClickInDialog("CommerceMain", "RuleBtn");
            yield return WaitUntil(() => FindByName("CommercePriceTable") != null,
                "The official price table did not open.");
            yield return WaitFrames(2);
            yield return Capture("commerce-01b-price-table");
            controller.CloseCurrentDialog();
            yield return WaitFrames(2);
            Require(FindByName("CommercePriceTable") == null, "The price table did not close.");

            long voucher0 = Amount(controller, 16);
            long flare0 = Amount(controller, 15);
            long diamond0 = Amount(controller, 2);
            ClickByName("CommerceDirect_1");
            yield return WaitUntil(() => FindByName("CommerceConfirm") != null, "The purchase confirmation did not open.");
            yield return WaitFrames(2);
            yield return Capture("commerce-02-confirm");
            GameObject rewardIcon = null;
            foreach (Transform transform in UnityEngine.Object.FindObjectsOfType<Transform>(true))
            {
                if (!transform.name.StartsWith("ItemIcon_", StringComparison.Ordinal)
                    || !transform.gameObject.activeInHierarchy) continue;
                if (transform.GetComponent<Button>() == null) continue;
                rewardIcon = transform.gameObject;
                break;
            }
            Require(rewardIcon != null, "A clickable reward icon was not created in the confirmation.");
            ExecuteEvents.Execute(rewardIcon, new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            }, ExecuteEvents.pointerClickHandler);
            yield return WaitUntil(() => FindByName("CommerceItemDetail") != null,
                "Clicking a reward icon did not open the official item detail dialog.");
            yield return Capture("commerce-03-item-detail");
            ClickInDialog("CommerceItemDetail", "CommonBtn_11");
            yield return WaitFrames(2);
            Require(FindByName("CommerceItemDetail") == null, "The item detail dialog did not close.");
            ClickActivePayButton();
            yield return WaitUntil(() => FindByName("CommerceConfirm") == null, "The confirmation did not close after purchase.");
            Require(Amount(controller, 16) == voucher0 - 6, "Voucher payment did not deduct the official six vouchers.");
            Require(Amount(controller, 15) == flare0 + 60, "The first direct-pay purchase did not grant 60 flare stars.");
            Require(Amount(controller, 2) == diamond0 + 60, "The first-purchase extra award was not granted.");

            ClickByName("ActivityEntryTabItem");
            yield return WaitFrames(2);
            yield return WaitUntil(() => FindByName("CommerceActivity_daily") != null, "The activity entries did not populate.");
            yield return Capture("commerce-03-activity");

            ClickByName("DirectPayTabItem");
            yield return WaitFrames(2);
            ClickUnder("AccumulatedPayEntryGo", "BgImg");
            yield return WaitUntil(() => FindByName("CommerceAccumulated") != null,
                "The accumulated-pay page did not open.");
            yield return WaitFrames(2);
            yield return Capture("commerce-03b-accumulated");
            int accumulatedRows = 0;
            foreach (Transform transform in UnityEngine.Object.FindObjectsOfType<Transform>(true))
                if (transform.name.StartsWith("Accumulated_", StringComparison.Ordinal)
                    && transform.gameObject.activeInHierarchy) accumulatedRows++;
            int activeGroup = runtime.Offers.CurrentAccumulatedGroup();
            int expectedRows = runtime.Catalog.Data.accumulatedAwards.Count(value => value.group == activeGroup);
            Require(accumulatedRows == expectedRows,
                "The accumulated page did not show exactly the current official group.");
            ClickInDialog("CommerceAccumulated", "CloseBtn");
            yield return WaitFrames(2);
            Require(FindByName("CommerceAccumulated") == null, "The accumulated page did not close.");
            ClickByName("PeriodicCardTabItem");
            yield return WaitFrames(2);
            Require(FindByName("PayPeriodicCardItem1") != null
                && FindByName("PayPeriodicCardItem2") != null,
                "The official periodic-card entries were not shown.");
            yield return Capture("commerce-periodic-cards");
            CommerceOffer monthly = runtime.Offers.Periodic(1);
            CommerceTransactionResult monthlyBuy = runtime.Offers.Purchase(monthly,
                runtime.Offers.PaymentMethod(monthly));
            Require(monthlyBuy.Code == CommerceTransactionCode.Applied
                && runtime.Offers.PeriodicExpiryDay(1) > runtime.Session.State.Day,
                "The monthly card did not activate and persist.");
            RequirePeriodicCharacterProperties(runtime);
            Require(runtime.Offers.PeriodicCanClaim(1), "The monthly daily award was not unlocked.");
            CommerceTransactionResult monthlyClaim = runtime.Offers.ClaimPeriodic(1);
            Require(monthlyClaim.Code == CommerceTransactionCode.Applied
                && !runtime.Offers.PeriodicCanClaim(1), "The monthly daily award did not persist its receipt.");
            CommerceOffer weeklyWish = runtime.Offers.Periodic(2);
            CommerceTransactionResult weeklyBuy = runtime.Offers.Purchase(weeklyWish,
                runtime.Offers.PaymentMethod(weeklyWish));
            Require(weeklyBuy.Code == CommerceTransactionCode.Applied
                && runtime.Offers.PeriodicExpiryDay(2) > runtime.Session.State.Day,
                "The seven-day wish card did not activate and persist.");
            RequirePeriodicCharacterProperties(runtime);
            ClickByName("ActivityEntryTabItem");
            yield return WaitFrames(2);

            ClickByName("CommerceActivity_daily");
            yield return WaitUntil(() => FindByName("CommerceDaily") != null, "The daily gift dialog did not open.");
            yield return WaitFrames(2);
            yield return Capture("commerce-04-daily");
            ClickInDialog("CommerceDaily", "RuleBtn");
            yield return WaitUntil(() => FindByName("CommerceRule") != null,
                "The official daily rule dialog did not open.");
            yield return WaitFrames(2);
            yield return Capture("commerce-04b-rule");
            controller.CloseCurrentDialog();
            yield return WaitFrames(2);
            Require(FindByName("CommerceRule") == null, "The daily rule dialog did not close.");
            long diamondBeforeFree = Amount(controller, 2);
            ClickInDialog("CommerceDaily", "BgImg (1)");
            Debug.Log("[Commerce acceptance] daily free can=" + runtime.Offers.CanClaimDailyFree()
                + " diamond=" + Amount(controller, 2));
            yield return WaitUntil(() => !runtime.Offers.CanClaimDailyFree(), "The daily free gift was not claimed.");
            Require(Amount(controller, 2) == diamondBeforeFree + 20, "The daily free gift reward is wrong.");
            GameObject dailyDialog = FindByName("CommerceDaily");
            Transform firstDailySlot = FindDeepTransform(dailyDialog.transform, "DailyPayGiftItemGo");
            Require(firstDailySlot != null, "The first daily gift slot is missing.");
            Transform firstBuy = firstDailySlot.Find("BuyBtnGo/BuyGo/BuyBtn");
            Require(firstBuy != null && firstBuy.GetComponent<Button>() != null, "The first daily buy button is missing.");
            Click(firstBuy.GetComponent<Button>());
            yield return WaitUntil(() => FindByName("CommerceConfirm") != null, "The daily purchase confirmation did not open.");
            ClickActivePayButton();
            yield return WaitUntil(() => FindByName("CommerceConfirm") == null, "The daily purchase confirmation did not close.");
            yield return WaitFrames(2);
            Require(runtime.Offers.DailyRemaining(1) == 0, "The daily tier was not recorded as bought.");
            yield return Capture("commerce-05-daily-bought");
            controller.CloseCurrentDialog();
            yield return WaitFrames(2);

            ClickByName("CommerceActivity_weekly");
            yield return WaitUntil(() => FindByName("CommerceWeekly") != null, "The weekly gift panel did not open.");
            yield return WaitFrames(3);
            Require(FindByName("CommerceWeekly_2") != null, "Weekly paid tiers were not created from PayGiftItem.");
            yield return Capture("commerce-06-weekly");
            ClickInDialog("CommerceWeekly", "TakeAwardBtn");
            yield return WaitUntil(() => !WeeklyFreeAvailable(runtime), "The weekly free gift was not claimed.");
            controller.CloseCurrentDialog();
            yield return WaitFrames(2);

            ClickByName("CommerceActivity_first");
            yield return WaitUntil(() => FindByName("CommerceFirst") != null, "The first-pay dialog did not open.");
            yield return WaitFrames(2);
            yield return Capture("commerce-07-first");
            ClickInDialog("CommerceFirst", "MoneyPayBtn");
            yield return WaitUntil(() => FindByName("CommerceConfirm") != null, "The first-pay confirmation did not open.");
            ClickInDialog("CommerceConfirm", "MoneyPayBtn");
            yield return WaitUntil(() => FindByName("CommerceConfirm") == null, "The first-pay confirmation did not close.");
            yield return WaitUntil(() => runtime.Offers.FirstPayIsActive(), "The first-pay package was not activated.");
            ClickFirstInteractable("TakeAwardBtn");
            yield return WaitUntil(() => runtime.Offers.HasClaimedFirst(1), "The first-pay day-one award was not claimed.");
            yield return Capture("commerce-08-first-active");
            controller.CloseCurrentDialog();
            yield return WaitFrames(2);

            ClickByName("CommerceActivity_exchange");
            yield return WaitUntil(() => FindByName("CommerceExchange") != null, "The exchange dialog did not open.");
            yield return WaitFrames(2);
            yield return Capture("commerce-09-exchange");
            ClickInDialog("CommerceExchange", "MaxBtn");
            yield return WaitFrames(1);
            long flareBeforeExchange = Amount(controller, 15);
            long diamondBeforeExchange = Amount(controller, 2);
            ClickInDialog("CommerceExchange", "ConfirmBtn");
            Debug.Log("[Commerce acceptance] exchange confirm clicked");
            yield return WaitUntil(() => FindByName("CommerceExchange") == null, "The exchange dialog did not close after exchange.");
            Debug.Log("[Commerce acceptance] exchange closed");
            Require(Amount(controller, 15) == 0, "The maximum exchange did not consume all flare stars.");
            Require(Amount(controller, 2) == diamondBeforeExchange + flareBeforeExchange, "The exchange did not grant the same amount of morning stars.");
            Debug.Log("[Commerce acceptance] exchange balances checked");

            controller.Close();
            yield return WaitFrames(3);
            Debug.Log("[Commerce acceptance] shop closed");
            Require(UnityEngine.Object.FindObjectOfType<CommerceScreenController>() == null, "The shop did not close.");

            // Another feature grants an item after the shop closes. Reopening and buying must
            // preserve it and must not reset the latest main inventory.
            string root = SessionState.GetString(RootKey, string.Empty);
            string slot = SessionState.GetString(SlotKey, string.Empty);
            var slots = new ProfileSlotCatalog(root);
            OfflineMainlineProgressSession profile = OfflineMainlineProgressSession.OpenForCommerce(slots, slot);
            ProgressionResult granted = profile.ApplyInventoryTransaction("commerce-acceptance-extra", save =>
            {
                OfflineItemStack stack = save.Player.Inventory.FirstOrDefault(value => value.ItemId == 9999);
                if (stack == null)
                {
                    stack = new OfflineItemStack { ItemId = 9999 };
                    save.Player.Inventory.Add(stack);
                }
                stack.Count += 1;
            });
            Require(granted.Status == ProgressionResultStatus.Applied, "The external inventory grant failed.");
            Debug.Log("[Commerce acceptance] external grant done");

            CommerceScreenController reopened = CommerceScreenController.OpenShop(slot, null, null, false, root);
            Debug.Log("[Commerce acceptance] reopened requested");
            yield return WaitUntil(() => reopened != null && reopened.MainWindow != null, "The shop did not reopen.");
            yield return WaitFrames(3);
            typeof(CommerceScreenController).GetMethod("SwitchPayTab",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(reopened, new object[] { true });
            yield return WaitFrames(2);
            Debug.Log("[Commerce acceptance] reopened built");
            Require(Amount(reopened, 9999) == 1, "The shop wiped an item granted by another feature.");
            long flareBeforeRepeat = Amount(reopened, 15);
            long voucherBeforeRepeat = Amount(reopened, 16);
            ClickByName("CommerceDirect_1");
            yield return WaitUntil(() => FindByName("CommerceConfirm") != null, "The repeat purchase confirmation did not open.");
            ClickActivePayButton();
            yield return WaitUntil(() => FindByName("CommerceConfirm") == null, "The repeat purchase did not close.");
            Require(Amount(reopened, 9999) == 1, "The repeat purchase reset another feature's item.");
            Require(Amount(reopened, 15) == flareBeforeRepeat + 60, "The repeat purchase reward is wrong.");
            Require(Amount(reopened, 16) == voucherBeforeRepeat - 6, "The repeat purchase voucher cost is wrong.");
            Require(reopened.Feature.Offers.FirstPayIsActive(), "The first-pay activation did not survive closing and reopening.");
            Require(reopened.Feature.Offers.HasClaimedFirst(1), "The first-pay claim did not survive closing and reopening.");
            yield return Capture("commerce-10-reopened");
            reopened.Close();
            yield return WaitFrames(2);

            Debug.Log("Commerce Play Mode acceptance passed: official main/direct/voucher/daily/weekly/periodic/first/accumulated/exchange "
                + "prefabs, voucher payment, free gifts, periodic-card activation/claims and character-property sync, "
                + "daily mutual exclusion, first-pay activation/claim, exchange, "
                + "save persistence, reopening, and preservation of another feature's inventory grant.");
            Finish(true, null);
        }

        private static bool WeeklyFreeAvailable(CommerceFeatureRuntime runtime)
        {
            CommerceGift free = runtime.Catalog.Data.weeklyGifts.FirstOrDefault(value =>
                value.groupId == runtime.Session.State.WeeklyGroup && value.moneyCostId <= 0);
            return free != null && !runtime.Offers.IsWeeklySoldOut(free);
        }

        private static long Amount(CommerceScreenController controller, int itemId)
        {
            foreach (CommerceAmount item in controller.Feature.Session.State.Inventory)
                if (item.ItemId == itemId) return item.Count;
            return 0;
        }

        private static void RequirePeriodicCharacterProperties(CommerceFeatureRuntime runtime)
        {
            string root = SessionState.GetString(RootKey, string.Empty);
            string slot = SessionState.GetString(SlotKey, string.Empty);
            var slots = new ProfileSlotCatalog(root);
            OfflineMainlineSave profile = OfflineMainlineProgressSession.OpenForCommerce(slots, slot).Save;
            OfficialCharacterCatalog catalog = OfficialCharacterAssetCatalog.LoadDefault().Load();
            var source = new OfflineCharacterConfiguredPropSource(catalog, CharacterPropHandleType.PeriodIcCar);
            IReadOnlyDictionary<int, long> actual = source.CalculateProps(profile.Player, null,
                DateTimeOffset.Now.ToUnixTimeSeconds());
            IReadOnlyDictionary<int, long> expected = OfflineCommerceSession.ActivePeriodicProperties(
                runtime.Catalog, runtime.Session.State);
            Require(actual.Count == expected.Count,
                "The active periodic-card properties were not synchronized to the character save.");
            foreach (KeyValuePair<int, long> property in expected)
                Require(actual.TryGetValue(property.Key, out long value) && value == property.Value,
                    "The character property source does not match active periodic-card property " + property.Key + ".");
        }



        private static Transform FindDeepTransform(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root)
            {
                if (child.name == name || child.name.Trim() == name) return child;
                Transform nested = FindDeepTransform(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private static GameObject FindAnyByName(string name)
        {
            foreach (Transform transform in UnityEngine.Object.FindObjectsOfType<Transform>(true))
                if (transform.name == name) return transform.gameObject;
            return null;
        }

        private static GameObject FindByName(string name)
        {
            foreach (Transform transform in UnityEngine.Object.FindObjectsOfType<Transform>(true))
                if (transform.name == name && transform.gameObject.activeInHierarchy) return transform.gameObject;
            return null;
        }

        private static void ClickUnder(string parentName, string childName)
        {
            foreach (Transform parent in UnityEngine.Object.FindObjectsOfType<Transform>(true))
            {
                if (parent.name.Trim() != parentName) continue;
                foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.Trim() != childName) continue;
                    Button button = child.GetComponent<Button>();
                    if (button == null) continue;
                    Click(button);
                    return;
                }
            }
            throw new InvalidOperationException("The official control was not found under " + parentName + ": " + childName);
        }

        private static void ClickByName(string name)
        {
            GameObject target = FindByName(name);
            Require(target != null, "The official commerce control was not found: " + name);
            Button button = target.GetComponent<Button>() ?? target.GetComponentInChildren<Button>(true);
            Require(button != null && button.interactable, "The commerce control has no interactable Button: " + name);
            Click(button);
        }

        private static void ClickFirstInteractable(string name)
        {
            foreach (Transform transform in UnityEngine.Object.FindObjectsOfType<Transform>(true))
            {
                if (transform.name != name || !transform.gameObject.activeInHierarchy) continue;
                Button button = transform.GetComponent<Button>();
                if (button == null || !button.interactable) continue;
                Debug.Log("[Commerce acceptance] click " + name + " under "
                    + (transform.parent == null ? "<root>" : transform.parent.parent == null ? transform.parent.name
                        : transform.parent.parent.name + "/" + transform.parent.name)
                    + " btn=" + button.GetInstanceID());
                Click(button);
                return;
            }
            throw new InvalidOperationException("No interactable commerce control was found: " + name);
        }

        private static void ClickInDialog(string dialogName, string controlName)
        {
            GameObject dialog = FindByName(dialogName);
            Require(dialog != null, "The commerce dialog was not found: " + dialogName);
            foreach (Transform transform in dialog.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name != controlName || !transform.gameObject.activeInHierarchy) continue;
                Button button = transform.GetComponent<Button>();
                if (button == null || !button.interactable) continue;
                Click(button);
                return;
            }
            throw new InvalidOperationException("The control was not found in " + dialogName + ": " + controlName);
        }

        private static void ClickActivePayButton()
        {
            GameObject dialog = FindByName("CommerceConfirm");
            Require(dialog != null, "The purchase confirmation was not found.");
            foreach (Transform transform in dialog.GetComponentsInChildren<Transform>(true))
            {
                if (!transform.gameObject.activeInHierarchy) continue;
                if (transform.name != "PayBtn" && transform.name != "MoneyPayBtn") continue;
                Button button = transform.GetComponent<Button>();
                if (button == null || !button.interactable) continue;
                Click(button);
                return;
            }
            throw new InvalidOperationException("The confirmation has no active pay button.");
        }

        private static void Click(Button button)
        {
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            }, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string error)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 60.0;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException(error);
                yield return null;
            }
        }

        private static IEnumerator Capture(string name)
        {
            if (Application.isBatchMode)
            {
                Debug.Log("[Commerce acceptance] batchmode skips snapshot " + name);
                yield break;
            }
            string directory = Environment.GetEnvironmentVariable("ZJCS_COMMERCE_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory))
                directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "output", "commerce-captures");
            Directory.CreateDirectory(directory);
            yield return null;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            string path = Path.Combine(directory, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            yield return WaitUntil(() => File.Exists(path) && new FileInfo(path).Length > 0,
                "Screenshot was not rendered: " + name);
            Debug.Log("[Commerce acceptance] snapshot " + path);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void ConfigurePortraitGameView()
        {
            System.Reflection.Assembly editor = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[]
            {
                Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeGroupType"), 1),
            });
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            Type kind = editor.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { Enum.ToObject(kind, 1), 720, 1280, "Commerce acceptance" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            Type viewType = editor.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(view, index);
        }

        private static void Finish(bool passed, Exception error)
        {
            SessionState.SetInt(ResultKey, passed ? 0 : 1);
            if (error != null) Debug.LogException(error);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}





using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Offline.Home;
using Zjcs.Offline.State;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    /// <summary>
    /// Play Mode acceptance driver for the home module.  It is only created when
    /// Unity is launched with ``-homeAcceptance`` and drives the real runtime with
    /// an isolated save, captures Game-view screenshots and writes a JSON report.
    /// </summary>
    public sealed class HomeAcceptanceDriver : MonoBehaviour
    {
        private static string Root => Environment.GetEnvironmentVariable("ZJCS_HOME_ACCEPTANCE_ROOT")
            ?? Path.Combine(Path.GetTempPath(), "zjcs-home-acceptance");
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string Output => Environment.GetEnvironmentVariable("ZJCS_HOME_ACCEPTANCE_OUTPUT")
            ?? Path.Combine(ProjectRoot, "output", "home-acceptance");

        private readonly List<string> log = new List<string>();
        private readonly Dictionary<string, object> report = new Dictionary<string, object>();
        private bool mainlandRequested;

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Directory.CreateDirectory(Output);
            var slots = new ProfileSlotCatalog(Root);
            string slotId = slots.GetSlotIds().FirstOrDefault();
            HomeFeatureRuntime runtime = null;
            bool ok = true;
            string setupError = null;
            try
            {
                if (string.IsNullOrEmpty(slotId))
                    throw new InvalidOperationException("The acceptance profile was not created by the launcher.");
                runtime = HomeFeatureRuntime.Begin(slotId, Root, null, null, () => mainlandRequested = true);
                runtime.TutorialAutoStartEnabled = false;
            }
            catch (Exception error)
            {
                setupError = error.ToString();
                ok = false;
            }
            if (runtime == null)
            {
                log.Add("FATAL " + setupError);
                Finish(false);
                yield break;
            }
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            if (Environment.GetCommandLineArgs().Contains("-npcAdventureAcceptance"))
            {
                HideForeignCanvases();
                DumpFullscreen();
                bool passed = false;
                yield return VerifyNpcAdventure(runtime, value => passed = value);
                Finish(passed);
                yield break;
            }

            if (Environment.GetCommandLineArgs().Contains("-partnersAcceptance"))
            {
                HideForeignCanvases();
                DumpFullscreen();
                bool passed = false;
                if (Environment.GetCommandLineArgs().Contains("-partnerTitlesAcceptance"))
                    yield return VerifyPartnerTitles(runtime, value => passed = value);
                else if (Environment.GetCommandLineArgs().Contains("-partnerOverflowAcceptance"))
                    yield return VerifyPartnerGiftOverflow(runtime, value => passed = value);
                else if (Environment.GetCommandLineArgs().Contains("-partnerFriendshipAcceptance"))
                    yield return VerifyPartnerFriendshipOverlay(runtime, value => passed = value);
                else yield return VerifyPartners(runtime, value => passed = value);
                Finish(passed);
                yield break;
            }

            if (Environment.GetCommandLineArgs().Contains("-partnerAutoMeetLoadAcceptance"))
            {
                HideForeignCanvases();
                DumpFullscreen();
                yield return Capture("partner-auto-meet-load.png");
                ok &= Check("partner-auto-meet-on-load", () =>
                {
                    int count = runtime.Profile.Save.Player.CharacterProperties.NpcFriendships
                        .Count(value => value.NpcClassId == 8402);
                    Assert(count == 1, "original auto-meet partner 8402 is added exactly once on profile load");
                    return "partner 8402 loaded into the local friendship roster";
                });
                Finish(ok);
                yield break;
            }

            if (Environment.GetCommandLineArgs().Contains("-dungeonsAcceptance"))
            {
                HideForeignCanvases();
                DumpFullscreen();
                bool passed = false;
                yield return VerifyDungeonsAndArena(runtime, value => passed = value);
                Finish(passed);
                yield break;
            }

            if (Environment.GetCommandLineArgs().Contains("-homeAcceptanceRealInput"))
            {
                bool inputPassed = false;
                yield return VerifyRealShopInput(runtime, value => inputPassed = value);
                ok &= inputPassed;
            }

            HideForeignCanvases();
            DumpFullscreen();
            ok &= Check("catalog", () =>
            {
                Assert(runtime.Catalog.Scene.background.Length == 384, "background layer count");
                Assert(runtime.Catalog.Scene.furniture.Length == 12, "furniture type count");
                Assert(runtime.Catalog.Scene.click_regions.Length >= 16, "click region count");
                Assert(runtime.Catalog.Ui.panels.Length >= 40, "ui panel count");
                Assert(runtime.Rules.Data.items.Length == 12694, "item table size");
                Assert(runtime.Tutorials.Guides.Count == 6, "official tutorial guide count");
                Assert(runtime.Tutorials.TotalNodeCount == 190, "official tutorial node count");
                Assert(runtime.Tutorials.Dialogues("RepaireFurniture2", 0).Count > 0, "official tutorial dialogues");
                return runtime.Catalog.Scene.background.Length + " layers, " +
                       runtime.Catalog.Ui.panels.Length + " panels, " + runtime.Rules.Data.items.Length + " items, "
                       + runtime.Tutorials.Guides.Count + " tutorials / " + runtime.Tutorials.TotalNodeCount + " nodes";
            });
            ok &= Check("scene", () => runtime.transform.childCount + " runtime roots");
            ok &= Check("repair", () =>
            {
                Assert(runtime.TryRepair(10), "cart repair");
                Assert(runtime.TryRepair(7), "storage box repair");
                Assert(runtime.TryRepair(0), "bed repair");
                Assert(runtime.Session.OwnsFurniture(0), "bed ownership persisted");
                return "bed/cart/storage repaired";
            });
            ok &= Check("income", () =>
            {
                runtime.Session.Mutate("acceptance:rewind", save =>
                    save.LastLocalUnixSeconds = Math.Max(0, save.LastLocalUnixSeconds - 3 * 3600));
                runtime.Session.AdvanceTo(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                HangUpIncomePreview preview = runtime.Session.Preview(1);
                int unlocked = runtime.Session.Definitions.Count(value => value.Unlocked);
                Assert(preview.Count > 0, "bed preview count=" + preview.Count + " defs="
                    + runtime.Session.Definitions.Count + " unlocked=" + unlocked
                    + " capacity=" + preview.Capacity + " hourly=" + preview.HourlyRate);
                long collected = runtime.TryCollect(1);
                Assert(collected > 0, "bed collect");
                return "preview " + preview.Count + ", collected " + collected;
            });
            ok &= Check("unlock-levelup", () =>
            {
                int hangUpId = runtime.Rules.HangUpsOf(10).Select(value => value.id)
                    .FirstOrDefault(id => runtime.Rules.HangUp(id).canLevelUp);
                Assert(hangUpId > 0, "levelable cart hang-up");
                if (!runtime.Session.IsHangUpUnlocked(hangUpId))
                    runtime.TryUnlockHangUp(hangUpId);
                HomeLevelUpResult result = runtime.TryLevelUpHangUp(hangUpId);
                Assert(result.Success || result.Error == "items" || result.Error == "home_level",
                    "level up attempt: " + result.Error);
                return "hang-up " + hangUpId + " level " + runtime.Session.HangUpLevel(hangUpId);
            });
            ok &= Check("fast-items", () =>
            {
                long before = runtime.ItemCount(120);
                long gained = runtime.TryFastForwardItems(1, 1);
                Assert(gained > 0, "time crystal acceleration gain");
                Assert(runtime.ItemCount(120) == before - 1, "one crystal consumed once");
                return "item acceleration gained " + gained + ", crystals " + before + " -> " + runtime.ItemCount(120);
            });            ok &= Check("mail", () =>
            {
                long before = runtime.ItemCount(1);
                bool added = runtime.Session.AddLocalMail(new OfflineHomeMail
                {
                    Id = 9001,
                    Sender = "系统",
                    Subject = "家园验收邮件",
                    Body = "这是本地投递的验收邮件。",
                    ExpireUnixSeconds = 0,
                    Attachments = new List<OfflineHomeItemStack>
                    {
                        new OfflineHomeItemStack { ItemId = 1, Count = 5 }
                    }
                });
                Assert(added, "local mail added");
                Assert(runtime.Session.GetInbox(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    .Any(value => value.Id == 9001), "mail listed");
                Assert(runtime.Session.CollectMailAttachments(9001,
                    "acceptance:mail:" + Guid.NewGuid().ToString("N")), "mail attachment claimed");
                runtime.DrainHomeInventory();
                Assert(runtime.ItemCount(1) >= before + 5, "attachment granted");
                return "mail add/claim, gold " + before + " -> " + runtime.ItemCount(1);
            });            ok &= Check("environment", () =>
            {
                Assert(runtime.Environment != null, "environment view");
                Assert(runtime.Environment.Config.stages.Length >= 4, "day stages");
                Assert(runtime.Environment.Config.lightTimes.Length >= 2, "light times");
                FieldInfo clock = typeof(HomeEnvironmentView).GetField("daySeconds",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(clock != null, "environment clock field");
                FieldInfo homeDay = typeof(HomeEnvironmentView).GetField("dayIndex",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(homeDay != null, "environment game-day field");
                long createdAt = runtime.Profile.Save.CreatedAtUnixSeconds;
                Assert(createdAt > 0, "profile creation time");
                double secondsPerDay = (double)runtime.Environment.Config.secondsPerMinute
                    * runtime.Environment.Config.minutesPerHour * runtime.Environment.Config.hoursPerDay;
                long expectedDays = (long)Math.Floor(Math.Max(0L,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds() - createdAt)
                    * Math.Max(1.0, runtime.Environment.Config.timeScale) / secondsPerDay);
                long actualDays = (long)homeDay.GetValue(runtime.Environment);
                Assert(actualDays == expectedDays || actualDays + 1 == expectedDays,
                    "moon cycle must use elapsed home days from profile creation");
                double originalTime = (double)clock.GetValue(runtime.Environment);
                try
                {
                    clock.SetValue(runtime.Environment, 5 * 3600d + 59 * 60d + 59d);
                    runtime.Environment.Tick(0.1f);
                    Assert(runtime.Environment.Hour == 6, "clock crossed the 06:00 light boundary");
                    Assert(!IsLayerGroupVisible(runtime.Scene, "/HouseOutside/HouseOutSideLight/Door_Light"),
                        "official 06:00 door light state is off");
                    clock.SetValue(runtime.Environment, 17 * 3600d + 59 * 60d + 59d);
                    runtime.Environment.Tick(0.1f);
                    Assert(runtime.Environment.Hour == 18, "clock crossed the 18:00 light boundary");
                    Assert(IsLayerGroupVisible(runtime.Scene, "/HouseOutside/HouseOutSideLight/Door_Light"),
                        "official 18:00 door light state is on");
                }
                finally
                {
                    clock.SetValue(runtime.Environment, originalTime);
                    runtime.Environment.Tick(0.1f);
                }
                return "stage " + runtime.Environment.Stage + " time " + runtime.Environment.Hour + ":" +
                       runtime.Environment.Minute + " moon " + runtime.Environment.MoonPhase;
            });
            ok &= Check("rewards", () =>
            {
                runtime.RefreshRewards();
                Assert(runtime.Session.RandomRewards.Count > 0, "random reward slots filled");
                Assert(runtime.Rewards != null && runtime.Rewards.Count > 0, "reward visuals instantiated");
                OfflineHomeRandomRewardSlot slot = runtime.Session.RandomRewards
                    .FirstOrDefault(value => value.ItemId > 0 && value.Count > 0);
                Assert(slot != null, "an active reward");
                long gained = runtime.TryCollectRandomReward(slot.Slot);
                Assert(gained > 0, "reward collected");
                Assert(runtime.Session.RandomRewards.All(value => value.Slot != slot.Slot || value.ItemId == 0),
                    "slot cleared");
                return "slots " + runtime.Session.RandomRewards.Count + " visuals " + runtime.Rewards.Count;
            });            ok &= Check("input", () =>
            {
                int bed = runtime.Scene.HitTest(new Vector2(8.4f, -1.62f));
                int cart = runtime.Scene.HitTest(new Vector2(-8.9f, -4.5f));
                Assert(bed == 0, "bed hit=" + bed);
                Assert(cart == 10, "cart hit=" + cart);
                Assert(runtime.Scene.HitTest(new Vector2(0f, 40f)) == -1, "empty area must not hit");
                Vector3 start = runtime.Scene.Position;
                runtime.Scene.MoveCamera(new Vector2(-100000f, 0f));
                Vector3 right = runtime.Scene.Position;
                Assert(right.x > start.x, "drag moves camera");
                runtime.Scene.MoveCamera(new Vector2(200000f, 0f));
                Vector3 back = runtime.Scene.Position;
                Assert(back.x <= right.x, "clamped drag");
                runtime.Scene.ResetCamera();
                return "bed/cart quads ok, clamp x " + right.x.ToString("0.00") + " -> " + back.x.ToString("0.00");
            });            ok &= Check("hud-button-events", () =>
            {
                HomeUiRenderer home = PrivateField<HomeUiRenderer>(runtime.Hud, "homeRenderer");
                HomeUiRenderer bottom = PrivateField<HomeUiRenderer>(runtime.Hud, "bottomRenderer");
                HomeUiRenderer functions = PrivateField<HomeUiRenderer>(runtime.Hud, "functionsRenderer");
                GameObject functionsPanel = PrivateField<GameObject>(runtime.Hud, "functionsPanel");
                GameObject noticeRoot = PrivateField<GameObject>(runtime.Hud, "noticeRoot");
                TMP_Text toast = PrivateField<TMP_Text>(runtime.Hud, "toast");

                Assert(!runtime.Hud.CharacterPageOpen, "HUD construction must not open the character page");
                Assert(PrivateField<GameObject>(runtime.Hud, "characterRoot") == null,
                    "HUD construction must not instantiate the character page");
                Assert(!functionsPanel.activeSelf && !runtime.Hud.AnyModalOpen,
                    "HUD construction must not open a menu or modal");

                HomeUiRenderer avatarRenderer = PrivateField<List<HomeUiRenderer>>(runtime.Hud, "owned")
                    .FirstOrDefault(value => value.Find<Button>("/PublicPlayerIconHome") != null);
                Assert(avatarRenderer != null, "official player icon button exists");
                Button avatar = avatarRenderer.Find<Button>("/PublicPlayerIconHome");
                avatar.onClick.Invoke();
                Assert(runtime.Hud.CharacterPageOpen, "player icon onClick opens the character page");
                Assert(runtime.Hud.TryHandleBack() && !runtime.Hud.CharacterPageOpen,
                    "Escape back closes the character page");

                Selectable[] tabs = new[] { "Character", "TravelNote", "Home", "Legion", "Mainland" }
                    .Select(name => bottom.Find<Selectable>("/PublicBottomBar/Content/Group/" + name + "/UnlockObj"))
                    .ToArray();
                Assert(tabs.All(value => value != null), "all five official bottom tabs have input controls");
                for (int index = 0; index < tabs.Length; index++)
                {
                    ActivateTab(tabs[index]);
                    Assert(PrivateField<int>(runtime.Hud, "selectedTab") == index,
                        "bottom tab input selects index=" + index);
                    if (index == 4) Assert(mainlandRequested, "mainland tab requests host navigation");
                }
                ActivateTab(tabs[0]);
                Assert(runtime.Hud.CharacterPageOpen, "character tab input opens the character page");
                ActivateTab(tabs[2]);
                Assert(!runtime.Hud.CharacterPageOpen, "home tab returns from the character page");

                Button menuEntry = home.Find<Button>(
                    "/HomeUI/Content/FunctionEntryGroup/FunctionEntryFunc/Content/Entry/EntryBtnFolded");
                Assert(menuEntry != null, "official function menu entry exists");
                menuEntry.onClick.Invoke();
                Assert(functionsPanel.activeSelf, "function entry onClick opens the menu");
                Button menuClose = functions.Find<Button>("/FunctionEntryDialog/CloseBtn");
                Assert(menuClose != null, "official function menu close button exists");
                menuClose.onClick.Invoke();
                Assert(!functionsPanel.activeSelf, "function menu close onClick closes the menu");

                menuEntry.onClick.Invoke();
                int menuButtonCount = 0;
                foreach (GameObject node in functions.AllNodes)
                {
                    Button button = node.GetComponent<Button>();
                    if (button == null) continue;
                    if (node.name == "CloseBtn")
                    {
                        button.onClick.Invoke();
                        Assert(!functionsPanel.activeSelf, "each menu close event folds the menu");
                    }
                    else
                    {
                        runtime.Hud.ShowNotice("menu-event-sentinel");
                        button.onClick.Invoke();
                        string menuPath = PathOf(node.transform);
                        if (menuPath.Contains("FunctionEntryTaskItem"))
                        {
                            Assert(PrivateField<string>(runtime.Hud, "activePanelKey") == "quests",
                                "task menu opens the official task panel");
                            HomeUiRenderer tasks = PrivateField<HomeUiRenderer>(runtime.Hud, "taskPanelRenderer");
                            string[] taskTabs = { "Task", "Delegate", "Daily", "Weekly", "AchievementTitle" };
                            for (int tabIndex = 0; tabIndex < taskTabs.Length; tabIndex++)
                            {
                                Toggle taskTab = tasks.Find<Toggle>("/TaskPanel/BgPanel/Content/BottomGo/TabGroup_11/TabGroup/TaskPanelTabGroupItem_11_" + taskTabs[tabIndex]);
                                Assert(taskTab != null, "official task tab remains a Toggle");
                                ActivateTab(taskTab);
                                Assert(PrivateField<int>(runtime.Hud, "selectedTaskTab") == tabIndex,
                                    "task Toggle input selects index=" + tabIndex);
                            }
                            Assert(runtime.Hud.TryHandleBack(), "back closes the task panel");
                        }
                        else if (menuPath.Contains("FunctionEntrySettingsItem"))
                        {
                            Assert(PrivateField<string>(runtime.Hud, "activePanelKey") == "settings",
                                "settings menu opens its official dialog");
                            HomeUiRenderer settings = PrivateField<HomeUiRenderer>(runtime.Hud, "activeDialogRenderer");
                            foreach (string tabName in new[] { "GameGeneralTab", "QualityTab", "SoundTab", "OtherTab" })
                            {
                                Toggle tab = settings.Find<Toggle>("/SettingsDialog/BgDialog1/Content/TopTabGo/TopTabGroupGO/TabGroup/" + tabName);
                                Assert(tab != null, "official settings Toggle exists: " + tabName);
                                ActivateTab(tab);
                                Assert(tab.isOn, "settings Toggle input selects: " + tabName);
                            }
                            Assert(runtime.Hud.TryHandleBack(), "back closes settings");
                        }
                        else if (menuPath.Contains("FunctionEntryHomeSkinItem"))
                        {
                            HomeSkinScreenController skins = PrivateField<HomeSkinScreenController>(runtime.Hud, "homeSkin");
                            Assert(skins != null && skins.IsOpen, "home skin menu opens its official page");
                            Assert(runtime.Hud.TryHandleBack() && !skins.IsOpen, "back closes the home skin page");
                        }
                        else
                            Assert(toast.text != "menu-event-sentinel", "menu onClick dispatches " + menuPath);
                    }
                    menuButtonCount++;
                }
                Assert(menuButtonCount >= 17, "all official function menu buttons were checked");
                menuEntry.onClick.Invoke();
                Assert(runtime.Hud.TryHandleBack() && !functionsPanel.activeSelf,
                    "Escape back closes the function menu");

                TopBarCurrency[] currencies = GameObject.Find("Home Canvas")
                    .GetComponentsInChildren<TopBarCurrency>(true);
                Assert(currencies.Length == 3, "three official top bar currencies exist");
                foreach (TopBarCurrency currency in currencies)
                {
                    Button details = currency.Renderer.Find<Button>("/MoneyUI/DescBtn");
                    Button add = currency.Renderer.Find<Button>("/MoneyUI/AddBtn");
                    Assert(details != null && add != null, "currency details and add buttons exist");
                    details.onClick.Invoke();
                    Assert(noticeRoot.activeSelf && toast.text.Contains("x"),
                        "currency details onClick displays its local balance");
                    add.onClick.Invoke();
                    Assert(toast.text.Contains("离线模式"), "currency add onClick reports offline policy");
                }

                runtime.Hud.ShowBed();
                GameObject dialog = PrivateField<GameObject>(runtime.Hud, "activeDialog");
                Button overlay = dialog == null ? null : dialog.GetComponent<Button>();
                Assert(overlay != null, "modal backdrop close button exists");
                overlay.onClick.Invoke();
                Assert(!runtime.Hud.AnyModalOpen, "modal backdrop onClick closes the active window");
                return "avatar, three currency controls, five tabs, menu open/close and modal backdrop use Unity events";
            });            yield return CheckUi(runtime, value => ok &= value);
            ok &= Check("storage-input-events", () =>
            {
                Assert(runtime.Scene.TryGetFurnitureClickBounds(7, out Rect bounds), "type 7 scene hit bounds");
                HomeInputController input = PrivateField<HomeInputController>(runtime, "input");
                Vector3 screen = runtime.Scene.Camera.WorldToScreenPoint(
                    new Vector3(bounds.center.x, bounds.center.y, 0f));
                input.PointerDown(screen, false);
                input.PointerUp(screen, false);
                GameObject inventoryRoot = PrivateField<GameObject>(runtime.Hud, "inventoryRoot");
                Assert(inventoryRoot != null && inventoryRoot.activeSelf && runtime.Hud.AnyModalOpen,
                    "type 7 input opens the real inventory panel");
                Button close = inventoryRoot.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value != null
                        && value.name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0);
                Assert(close != null && close.isActiveAndEnabled, "official inventory close button exists");
                var pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left
                };
                ExecuteEvents.Execute(close.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert(!runtime.Hud.AnyModalOpen
                    && PrivateField<GameObject>(runtime.Hud, "inventoryRoot") == null,
                    "inventory pointer-click event closes the real panel");
                return "type 7 scene pointer route opens the official bag and its UI click closes it";
            });
            ok &= Check("persistence", () =>
            {
                string homePath = slots.GetHomePath(slotId);
                Assert(File.Exists(homePath), "home save exists");
                OfflineHomeSave saved = JsonUtility.FromJson<OfflineHomeSave>(File.ReadAllText(homePath));
                Assert(saved != null && saved.Furniture.Any(value => value.Type == 0 && value.Owned),
                    "furniture ownership written");
                Assert(saved.Income.Count > 0, "income checkpoint written");
                return "save bytes " + new FileInfo(homePath).Length;
            });
            ok &= Check("tutorial-fixed-reward", () =>
            {
                int[] itemIds = { 1, 8050, 8101 };
                Dictionary<int, long> before = itemIds.ToDictionary(itemId => itemId,
                    itemId => runtime.ItemCount(itemId));
                bool available = runtime.TutorialCanGetFixedReward(7);
                Assert(runtime.TryClaimTutorialFixedReward(7, "acceptance:home-fixed-reward", out string reason),
                    "fixed reward claim: " + reason);
                Dictionary<int, long> after = itemIds.ToDictionary(itemId => itemId,
                    itemId => runtime.ItemCount(itemId));
                Assert(runtime.TryClaimTutorialFixedReward(7, "acceptance:home-fixed-reward-retry", out reason),
                    "fixed reward retry: " + reason);
                Assert(itemIds.All(itemId => runtime.ItemCount(itemId) == after[itemId]),
                    "fixed reward retry must be idempotent");
                Assert(!runtime.TutorialCanGetFixedReward(7), "fixed reward claim count is one");
                if (available)
                {
                    Assert(after[1] == before[1] + 350, "official item 1 grant count");
                    Assert(after[8050] == before[8050] + 10, "official item 8050 grant count");
                    Assert(after[8101] == before[8101] + 5, "official item 8101 grant count");
                }
                else Assert(itemIds.All(itemId => after[itemId] == before[itemId]),
                    "an already claimed reward must not grant again");
                return "reward 7 available=" + available + ", inventory claim is durable and idempotent";
            });
            ok &= Check("tutorial-runner", () => RunTutorialRunnerAcceptance(runtime));
            Finish(ok);
        }

        private static string RunTutorialRunnerAcceptance(HomeFeatureRuntime runtime)
        {
            HomeTutorialCatalog catalog = runtime.Tutorials;
            string root = Path.Combine(Path.GetTempPath(), "zjcs-home-tutorial-acceptance-"
                + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string graphPath = Path.Combine(root, "graph.json");
                var graphHost = new TutorialAcceptanceHost();
                var graphRunner = new HomeTutorialRunner(catalog, graphHost, graphPath);
                Assert(graphRunner.QueueGuide("RepaireBaGuaLu"), "queue repair tutorial graph");
                for (int tick = 0; tick < 512 && !graphRunner.IsGuideCompleted("RepaireBaGuaLu"); tick++)
                    graphRunner.Tick(1f);
                Assert(graphRunner.IsGuideCompleted("RepaireBaGuaLu") && !graphRunner.IsBlocked,
                    "node/port graph reaches its official finish");

                string resumePath = Path.Combine(root, "resume.json");
                var initialHost = new TutorialAcceptanceHost { PendingStartDialogue = true };
                var initialRunner = new HomeTutorialRunner(catalog, initialHost, resumePath);
                Assert(initialRunner.QueueGuide("RepaireBaGuaLu"), "queue resume tutorial graph");
                initialRunner.Tick(1f);
                Assert(initialRunner.IsPending && File.Exists(resumePath), "pending dialogue was saved");
                var resumedHost = new TutorialAcceptanceHost();
                var resumedRunner = new HomeTutorialRunner(catalog, resumedHost, resumePath);
                for (int tick = 0; tick < 512 && !resumedRunner.IsGuideCompleted("RepaireBaGuaLu"); tick++)
                    resumedRunner.Tick(1f);
                Assert(resumedHost.SawResume, "restart resumes the saved active node through the host");
                Assert(resumedRunner.IsGuideCompleted("RepaireBaGuaLu") && !resumedRunner.IsBlocked,
                    "restarted graph reaches its official finish");

                HomeTutorialGuide optionGuide = catalog.Guide("RepaireFurniture2");
                HomeTutorialNode optionNode = optionGuide.nodes.First(value => value.id == 123);
                HomeTutorialStoryOption officialOption = catalog.StoryOption(optionGuide.name, 980);
                HomeTutorialConnection selectedPort = catalog.FirstConnection(optionNode, "Options 0");
                Assert(officialOption != null && officialOption.text == "我要拯救世界……"
                    && selectedPort != null && selectedPort.node == 125,
                    "official option row and Options 0 target are available");

                string optionPath = Path.Combine(root, "option-resume.json");
                var optionHost = new TutorialAcceptanceHost
                {
                    PendingNodeId = optionNode.id,
                    OwnsCartItem = false
                };
                var optionRunner = new HomeTutorialRunner(catalog, optionHost, optionPath);
                Assert(optionRunner.QueueGuide(optionGuide.name), "queue official option tutorial graph");
                for (int tick = 0; tick < 512 && !optionRunner.IsBlocked
                    && (!optionRunner.IsPending || optionRunner.ActiveNodeId != optionNode.id); tick++)
                    optionRunner.Tick(1f);
                Assert(optionRunner.IsPending && optionRunner.ActiveNodeId == optionNode.id
                    && optionHost.ObservedNodeIds.Contains(optionNode.id),
                    "a character without item 4011 reaches the official option node; active="
                    + optionRunner.ActiveNodeId + " pending=" + optionRunner.IsPending
                    + " blocked=" + optionRunner.IsBlocked + " error=" + optionRunner.LastError
                    + " visited=" + string.Join(",", optionHost.ObservedNodeIds));

                IReadOnlyList<HomeTutorialDialogue> optionDialogues = catalog.Dialogues(optionGuide.name, 0);
                Assert(runtime.Hud != null, "Home HUD is available for the option input test");
                runtime.Hud.ShowOfficialTutorialOptionDialog(optionDialogues,
                    new[] { officialOption }, index => optionRunner.RecordOptionSelection(optionGuide,
                        optionNode, "home-tutorial:" + optionGuide.name + ":" + optionNode.id, index));
                HomeUiRenderer optionRenderer = PrivateField<HomeUiRenderer>(runtime.Hud, "tutorialDialogueRenderer");
                for (int line = 0; line < optionDialogues.Count; line++)
                {
                    Button next = optionRenderer.Find<Button>("/OptionTalkDialog/NextBtn");
                    var nextPointer = new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left
                    };
                    ExecuteEvents.Execute(next.gameObject, nextPointer, ExecuteEvents.pointerClickHandler);
                }
                Transform optionContent = optionRenderer.Find("/OptionTalkDialog/OptionSlots/Content").transform;
                Button optionButton = optionContent.GetChild(0).GetComponent<Button>();
                TMP_Text optionText = optionButton.transform.Find("Content/OptionText/Text").GetComponent<TMP_Text>();
                Assert(optionText.text == officialOption.text, "official option label is presented");
                var optionPointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left
                };
                ExecuteEvents.Execute(optionButton.gameObject, optionPointer, ExecuteEvents.pointerClickHandler);
                Assert(!runtime.Hud.HasTutorialDialogue && optionRunner.IsPending
                    && File.ReadAllText(optionPath).Contains("\"OptionSelectionIndex\": 0"),
                    "option pointer input durably stores its selected output index");

                var optionResumedHost = new TutorialAcceptanceHost { PendingNodeId = selectedPort.node };
                var optionResumedRunner = new HomeTutorialRunner(catalog, optionResumedHost, optionPath);
                optionResumedRunner.Tick(1f);
                Assert(optionResumedRunner.ActiveNodeId == selectedPort.node
                    && optionResumedRunner.IsPending && optionResumedHost.ObservedNodeIds.Contains(selectedPort.node),
                    "restart follows the selected Options 0 port to its official node");

                var blockedHost = new TutorialAcceptanceHost { UnsupportedNodeType = "GetFixAwardNode" };
                var blockedRunner = new HomeTutorialRunner(catalog, blockedHost,
                    Path.Combine(root, "unsupported.json"));
                Assert(blockedRunner.QueueGuide("FirstMergeItem"), "queue fixed-reward graph");
                for (int tick = 0; tick < 512 && !blockedRunner.IsBlocked; tick++) blockedRunner.Tick(1f);
                Assert(blockedRunner.IsBlocked && blockedRunner.ActiveNodeId > 0
                    && blockedRunner.LastError.Contains("unsupported"),
                    "unsupported server-only award action stays blocked at its node");
                return "graph progression, restart resume, official option input/branch recovery, and unsupported action blocking passed";
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private sealed class TutorialAcceptanceHost : IHomeTutorialHost
        {
            public bool PendingStartDialogue;
            public string UnsupportedNodeType;
            public int PendingNodeId = -1;
            public bool OwnsCartItem = true;
            public bool SawResume;
            public readonly List<int> ObservedNodeIds = new List<int>();
            private bool pendingIssued;

            public bool IsReady => true;

            public bool TryEvaluateCondition(HomeTutorialGuide guide, HomeTutorialNode node,
                out bool result, out string reason)
            {
                result = guide?.name != "RepaireFurniture2" || node?.id != 121 || OwnsCartItem;
                reason = null;
                return true;
            }

            public HomeTutorialHostResult BeginOrResumeNode(HomeTutorialGuide guide, HomeTutorialNode node,
                IReadOnlyList<HomeTutorialDialogue> dialogues, string receipt, bool isResume, out string reason)
            {
                if (isResume) SawResume = true;
                ObservedNodeIds.Add(node.id);
                if (PendingStartDialogue && !pendingIssued && node.type == "StartStoryNode" && !isResume)
                {
                    pendingIssued = true;
                    reason = null;
                    return HomeTutorialHostResult.Pending;
                }
                if (node.type == UnsupportedNodeType)
                {
                    reason = "unsupported server-only action in acceptance host";
                    return HomeTutorialHostResult.Unsupported;
                }
                if (node.id == PendingNodeId)
                {
                    reason = null;
                    return HomeTutorialHostResult.Pending;
                }
                reason = null;
                return HomeTutorialHostResult.Succeeded;
            }

            public HomeTutorialHostResult PollNode(HomeTutorialGuide guide, HomeTutorialNode node,
                string receipt, out string reason)
            {
                reason = null;
                if (node.id == PendingNodeId) return HomeTutorialHostResult.Pending;
                return node.type == UnsupportedNodeType
                    ? HomeTutorialHostResult.Unsupported : HomeTutorialHostResult.Succeeded;
            }

            public void EndGuide(HomeTutorialGuide guide, bool completed) { }
        }

        private static void HideForeignCanvases()
        {
            foreach (Transform node in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (node == null || !node.gameObject.scene.IsValid()) continue;
                string name = node.name;
                if (name.IndexOf("SaveSelection", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("AgeTip", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("BootSplash", StringComparison.OrdinalIgnoreCase) >= 0)
                    node.gameObject.SetActive(false);
            }
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas == null || canvas.name == "Home Canvas") continue;
                // Bootstrap keeps its splash/age and save-selection canvases alive
                // while the home runtime is created. They must not cover the
                // partner window captured by this Play Mode acceptance.
                if (canvas.gameObject.scene.IsValid()) canvas.gameObject.SetActive(false);
            }
        }
        private void DumpFullscreen()
        {
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas == null || !canvas.isActiveAndEnabled) continue;
                log.Add("CANVAS " + PathOf(canvas.transform) + " scene=" + canvas.gameObject.scene.name
                    + " mode=" + canvas.renderMode + " order=" + canvas.sortingOrder);
            }
            foreach (Image image in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (image == null || !image.isActiveAndEnabled || image.color.a < 0.5f) continue;
                RectTransform rect = image.rectTransform;
                if (rect == null || rect.rect.width < 900 || rect.rect.height < 1200) continue;
                log.Add("IMG " + PathOf(rect) + " sprite=" + (image.sprite == null ? "null" : image.sprite.name)
                        + " color=" + image.color + " size=" + rect.rect.width + "x" + rect.rect.height);
            }
            foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
                if (camera != null && camera.name == "Home Camera")
                    log.Add("CAM clear=" + camera.clearFlags + " bg=" + camera.backgroundColor
                            + " pos=" + camera.transform.position);
        }

        private static string PathOf(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }
        private void Finish(bool ok)
        {
            report["success"] = ok;
            report["log"] = log;
            File.WriteAllText(Path.Combine(Output, "report.json"),
                JsonUtility.ToJson(new SerializedReport { success = ok, lines = log.ToArray() }, true));
            Debug.Log("[HomeAcceptance] success=" + ok + "\n" + string.Join("\n", log));
            Quit(ok ? 0 : 1);
        }
        private IEnumerator CheckUi(HomeFeatureRuntime runtime, Action<bool> result)
        {
            bool ok = true;
            ok &= Ui(() => runtime.Hud.ShowBed(), "open bed");
            yield return null;
            yield return null;
            yield return Capture("home-bed.png");
            ok &= Ui(() =>
            {
                Assert(runtime.Hud.AnyModalOpen, "bed modal");
                runtime.Hud.CloseDialog();
                runtime.Hud.ShowCart();
            }, "open cart");
            yield return null;
            yield return null;
            yield return Capture("home-cart.png");
            ok &= Ui(() =>
            {
                runtime.Hud.CloseDialog();
                runtime.Hud.ShowMail();
            }, "open mail");
            yield return null;
            yield return null;
            yield return Capture("home-mail.png");
            ok &= Ui(() =>
            {
                runtime.Hud.CloseDialog();
                runtime.Hud.ToggleFunctions(true);
            }, "open functions");
            yield return null;
            yield return Capture("home-functions.png");
            ok &= Ui(() =>
            {
                runtime.Hud.ToggleFunctions(false);
            }, "close functions");
            yield return null;
            yield return Capture("home-scene.png");
            HomeShopScreenController shop = null;
            Zjcs.Offline.Progression.OfflineHomeShopService shopService = null;
            ok &= Ui(() =>
            {
                MethodInfo openShop = typeof(HomeFeatureRuntime).GetMethod("OpenHomeShop",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(openShop != null, "home shop route exists");
                openShop.Invoke(runtime, null);
                shop = UnityEngine.Object.FindObjectOfType<HomeShopScreenController>();
                Assert(shop != null && shop.IsOpen && shop.CurrentPageIndex == 0,
                    "type 8 route opens the grocery page");

                string rulePath = Path.Combine(Application.streamingAssetsPath, "HomeShop", "rules.json");
                var catalog = Zjcs.Offline.Progression.OfficialHomeShopCatalog.Load(rulePath);
                shopService = new Zjcs.Offline.Progression.OfflineHomeShopService(runtime.Profile, catalog);
                IReadOnlyList<Zjcs.Offline.Progression.OfflineHomeShopShelfItem> grocery =
                    shopService.GetGroceryShelf();
                Assert(runtime.Profile.Save.Player.SubRank == 3
                    && grocery.Count == 13 && grocery.Count(value => value.IsRandom) == 8
                    && grocery.Count(value => !value.IsRandom) == 5,
                    "SubRank 3/map 17 grocery has eight eligible random and five fixed goods; group 10601 unlocks at SubRank 5");

                GameObject grid = GameObject.Find("HomeShopPanel/BgPanel1/Content/MiddleGo/ShopContent/ShopContent/ShopContentGroup/GoodsGrid");
                GameObject gridContent = GameObject.Find("HomeShopPanel/BgPanel1/Content/MiddleGo/ShopContent/ShopContent/ShopContentGroup/GoodsGrid/ViewPort/Content");
                RectTransform viewportRect = grid == null ? null : grid.transform.Find("ViewPort") as RectTransform;
                GridLayoutGroup gridLayout = gridContent == null ? null : gridContent.GetComponent<GridLayoutGroup>();
                Assert(grid != null && gridContent != null && viewportRect != null && gridLayout != null,
                    "official grocery scroll grid and layout are instantiated");
                Canvas.ForceUpdateCanvases();
                Rect viewportBounds = RectTransformUtility.PixelAdjustRect(viewportRect, viewportRect.GetComponentInParent<Canvas>());
                int renderedRows = 0;
                var cardBounds = new List<Rect>();
                foreach (Transform candidate in gridContent.transform)
                {
                    RectTransform rowRect = candidate as RectTransform;
                    if (rowRect == null || !candidate.name.StartsWith("ShopGoodsItem_", StringComparison.Ordinal)) continue;
                    Canvas.ForceUpdateCanvases();
                    Vector3[] corners = new Vector3[4];
                    rowRect.GetWorldCorners(corners);
                    Rect worldBounds = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                    Image[] images = candidate.GetComponentsInChildren<Image>(true);
                    int visibleImages = images.Count(image => image.isActiveAndEnabled && image.color.a > 0.05f);
                    bool intersectsViewport = RectTransformUtility.RectangleContainsScreenPoint(viewportRect,
                        RectTransformUtility.WorldToScreenPoint(null, worldBounds.center));
                    if (candidate.gameObject.activeInHierarchy && intersectsViewport && visibleImages > 0)
                    {
                        Assert(cardBounds.All(bounds => Mathf.Abs(bounds.center.y - worldBounds.center.y) > 1f
                            || !bounds.Overlaps(worldBounds)), "visible grocery cards in one row do not overlap");
                        cardBounds.Add(worldBounds);
                        renderedRows++;
                    }
                    log.Add("SHOPROW " + candidate.name + " active=" + candidate.gameObject.activeInHierarchy
                        + " rect=" + rowRect.rect + " world=" + worldBounds + " images=" + visibleImages
                        + "/" + images.Length + " texts=" + candidate.GetComponentsInChildren<TMP_Text>(true).Length
                        + " intersect=" + intersectsViewport);
                }
                log.Add("SHOPGRID viewport=" + viewportRect.rect + " adjusted=" + viewportBounds
                    + " content=" + ((RectTransform)gridContent.transform).rect + " cell=" + gridLayout.cellSize
                    + " spacing=" + gridLayout.spacing + " columns=" + gridLayout.constraintCount
                    + " rendered=" + renderedRows + "/" + grocery.Count);
                Assert(gridLayout.spacing.x >= 0f, "the reconstructed three-column grocery grid uses fitted spacing");
                Assert(renderedRows > 0, "at least one grocery item card is visible inside the official grid viewport");

                Zjcs.Offline.Progression.OfflineHomeShopShelfItem offer =
                    grocery.First(value => value.IsRandom && value.RemainingInventory > 0);
                Transform row = GameObject.Find("ShopGoodsItem_" + offer.Goods.goods_id)?.transform;
                Button buy = row == null ? null : row.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value.gameObject.name == "BuyBtn");
                Assert(buy != null && buy.interactable, "random grocery purchase button is enabled");
                long before = runtime.ItemCount(offer.Goods.item_id);
                buy.onClick.Invoke();
                Assert(runtime.ItemCount(offer.Goods.item_id) >= before + offer.Goods.item_count,
                    "grocery purchase adds the configured item count");
                runtime.Profile.Reload();
                Zjcs.Offline.Progression.OfflineHomeShopShelfItem saved = shopService.GetGroceryShelf()
                    .First(value => value.SlotIndex == offer.SlotIndex);
                Assert(saved.Goods.goods_id == offer.Goods.goods_id && saved.BoughtCount == 1,
                    "grocery purchase and random shelf survive profile reload");
                int currencyId;
                int refreshCost;
                int remainingRefreshes;
                Assert(shopService.TryGetGroceryRefreshCost(out currencyId, out refreshCost,
                    out remainingRefreshes) && remainingRefreshes > 0, "grocery refresh is available");
                long goldBeforeRefresh = runtime.ItemCount(currencyId);
                Button refresh = GameObject.Find("HomeShopPanel/BgPanel1/Content/MiddleGo/ShopContent/TopGo/RefreshGo/RefreshInfoGo/RefreshBtn/CommonBtn_CostItem")
                    ?.GetComponent<Button>();
                Assert(refresh != null && refresh.interactable, "official grocery refresh button is enabled");
                refresh.onClick.Invoke();
                Assert(runtime.Profile.Save.Player.HomeShop.GroceryRefreshedCount == 1
                    && runtime.ItemCount(currencyId) == goldBeforeRefresh - refreshCost,
                    "grocery refresh spends its configured cost and advances the refresh count");
                runtime.Profile.Reload();
                IReadOnlyList<Zjcs.Offline.Progression.OfflineHomeShopShelfItem> refreshedShelf =
                    shopService.GetGroceryShelf();
                Assert(refreshedShelf.Count == grocery.Count
                    && runtime.Profile.Save.Player.HomeShop.GroceryRefreshedCount == 1,
                    "refreshed grocery shelf and refresh count survive profile reload");
                log.Add("PASS grocery: displayed goods, purchase, refresh cost and shelf state survive reload");
            }, "open and buy grocery");
            yield return null;
            yield return null;
            yield return Capture("home-shop-grocery.png");
            ok &= Ui(() =>
            {
                long matureCreatedAt = System.DateTimeOffset.UtcNow.AddDays(-4).ToUnixTimeSeconds();
                var ageResult = runtime.Profile.ApplyInventoryTransaction(
                    "acceptance:treasury-registered-days",
                    save =>
                    {
                        save.CreatedAtUnixSeconds = matureCreatedAt;
                        Zjcs.Offline.Progression.OfflineItemStack stars = save.Player.Inventory
                            .FirstOrDefault(value => value.ItemId == 2);
                        if (stars == null)
                        {
                            stars = new Zjcs.Offline.Progression.OfflineItemStack { ItemId = 2 };
                            save.Player.Inventory.Add(stars);
                        }
                        stars.Count = Math.Max(stars.Count, 10000000);
                    });
                Assert(ageResult.Status == Zjcs.Offline.Progression.ProgressionResultStatus.Applied,
                    "matured local account state persisted: " + ageResult.Status);
                IReadOnlyList<Zjcs.Offline.Progression.OfflineHomeShopShelfItem> matureTreasury =
                    shopService.GetTreasuryShelf();
                Assert(matureTreasury.Count == 12
                    && matureTreasury.Select(value => value.SlotIndex).Distinct().Count() == 12,
                    "registered-day conditions fill all twelve official treasury slots; count="
                    + matureTreasury.Count);
                log.Add("PASS treasury-rules: registered-day progression exposes all 12 official slots");
            }, "treasury registered-day progression");
            ok &= Ui(() => Assert(shop != null && shop.SelectPage(1) && shop.CurrentPageIndex == 1,
                "the second official shop tab opens the treasury"), "open treasury");
            yield return null;
            ok &= Ui(() =>
            {
                IReadOnlyList<Zjcs.Offline.Progression.OfflineHomeShopShelfItem> shelf = shopService.GetTreasuryShelf();
                Zjcs.Offline.Progression.OfflineHomeShopShelfItem purchase = shelf.FirstOrDefault(value =>
                    value.Goods.currency_id == 2 && value.Goods.original_price > 0
                    && value.RemainingInventory != 0
                    && (long)(value.Goods.original_price * value.PriceOff) > 0);
                Assert(purchase != null, "treasury has an available paid star-currency item");
                GameObject row = GameObject.Find("ShopGoodsItem_" + purchase.Goods.goods_id);
                Button buy = row == null ? null : row.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value.gameObject.name == "BuyBtn");
                Assert(buy != null && buy.interactable, "treasury item purchase button is enabled");
                long starsBefore = runtime.ItemCount(2);
                long itemBefore = runtime.ItemCount(purchase.Goods.item_id);
                long scoreBefore = runtime.Profile.Save.Player.HomeShop.ShopScore;
                buy.onClick.Invoke();
                Assert(runtime.ItemCount(2) < starsBefore
                    && runtime.ItemCount(purchase.Goods.item_id) >= itemBefore + purchase.Goods.item_count
                    && runtime.Profile.Save.Player.HomeShop.ShopScore > scoreBefore,
                    "treasury purchase spends stars, grants the item and increases gift score");
                runtime.Profile.Reload();
                Zjcs.Offline.Progression.OfflineHomeShopShelfItem savedPurchase = shopService.GetTreasuryShelf()
                    .First(value => value.SlotIndex == purchase.SlotIndex);
                Assert(savedPurchase.Goods.goods_id == purchase.Goods.goods_id && savedPurchase.BoughtCount == 1,
                    "treasury purchase and shelf stock survive profile reload");
                log.Add("PASS treasury: purchase settlement and reloaded stock");
            }, "buy treasury item");
            yield return null;
            yield return Capture("home-shop-treasury.png");
            ok &= Ui(() =>
            {
                int targetScore = shopService.GetGiftEntries(1)[0].TargetScore;
                var scoreResult = runtime.Profile.ApplyInventoryTransaction("acceptance:home-shop-gift-score",
                    save => save.Player.HomeShop.ShopScore = Math.Max(save.Player.HomeShop.ShopScore, targetScore));
                Assert(scoreResult.Status == Zjcs.Offline.Progression.ProgressionResultStatus.Applied
                    || scoreResult.Status == Zjcs.Offline.Progression.ProgressionResultStatus.Unchanged,
                    "gift claim fixture score persisted: " + scoreResult.Status);
                runtime.Profile.Reload();
                Assert(shop != null && shop.SelectPage(2) && shop.CurrentPageIndex == 2,
                    "the gift tab opens local rewards");
                IReadOnlyList<Zjcs.Offline.Progression.OfflineHomeShopGiftEntry> entries = shopService.GetGiftEntries(5);
                Zjcs.Offline.Progression.OfflineHomeShopGiftEntry claimable = entries.FirstOrDefault(value => value.CanClaim);
                Assert(claimable != null, "the first official score milestone is claimable");
                GameObject shopRoot = GameObject.Find("HomeShopPanel");
                Button claim = shopRoot == null ? null : shopRoot.GetComponentsInChildren<Button>(true).FirstOrDefault(value =>
                    value.name == "ReceiveBtn" && value.isActiveAndEnabled && value.interactable);
                Assert(claim != null, "the official gift claim button is enabled");
                long rewardBefore = runtime.ItemCount(claimable.Milestone.rewards[0].item_id);
                claim.onClick.Invoke();
                Assert(runtime.ItemCount(claimable.Milestone.rewards[0].item_id)
                    >= rewardBefore + claimable.Milestone.rewards[0].count,
                    "gift claim grants its configured reward");
                runtime.Profile.Reload();
                Assert(runtime.Profile.Save.Player.HomeShop.ClaimedGiftAchievementIds.Contains(claimable.AchievementId)
                    && !shopService.GetGiftEntries(5).Any(value => value.AchievementId == claimable.AchievementId),
                    "gift claim persists and removes the milestone from the unclaimed list");
                Assert(shopService.ClaimGift(claimable.AchievementId, "acceptance:home-shop-gift-duplicate").Status
                    == Zjcs.Offline.Progression.HomeShopPurchaseStatus.Duplicate,
                    "a reloaded gift claim cannot be granted twice");
                log.Add("PASS gifts: claim reward and reloaded claim state");
            }, "claim gift reward");
            yield return null;
            yield return Capture("home-shop-gifts.png");
            shop?.CloseScreen();
            yield return null;
            if (ok) log.Add("PASS ui: bed/cart/mail/functions screenshots");
            result(ok);
        }

        private bool Ui(Action action, string name)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception error)
            {
                log.Add("FAIL ui/" + name + ": " + error.Message);
                return false;
            }
        }

        private IEnumerator VerifyNpcAdventure(HomeFeatureRuntime runtime, Action<bool> completed)
        {
            bool ok = Check("npc-adventure-unlock", () =>
            {
                // The generic Home fixture skips character creation. Populate the
                // original default appearance before mounting an actual battle actor.
                if (runtime.Profile.Save.Player.Wardrobe.BaseSkins.Count == 0)
                {
                    var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig");
                    var tables = ProfessionAppearanceTables.Load(
                        Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
                    var wardrobe = runtime.Profile.Save.Player.Wardrobe;
                    var appearance = new ProfessionAppearanceResolver(tables).BuildDefaultAppearanceData(
                        config, (Sex)wardrobe.Sex, (Zjcs.Official.ProfessionUI.Profession)wardrobe.Profession);
                    ProgressionResult prepared = runtime.Profile.ApplyCharacterMutationIfChanged(player =>
                    {
                        foreach (var pair in appearance.SkinMap)
                            player.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin
                                { Type = (int)pair.Key, Id = pair.Value.SkinId });
                        foreach (var pair in appearance.DyeMap)
                            player.Wardrobe.Dyes.Add(new OfflineAppearanceDye
                            {
                                Type = (int)pair.Key, ColorId = pair.Value.ColorId,
                                Hue = pair.Value.Hsv.H, Saturation = pair.Value.Hsv.S, Value = pair.Value.Hsv.V
                            });
                        return true;
                    });
                    Assert(prepared.IsApplied, "could not prepare the isolated original character appearance");
                }
                if (!runtime.Profile.Save.Player.UnlockedMapClassIds.Contains(17))
                {
                    ProgressionResult unlock = runtime.Profile.ApplyCharacterMutationIfChanged(player =>
                    {
                        if (!player.UnlockedMapClassIds.Contains(17)) player.UnlockedMapClassIds.Add(17);
                        return true;
                    });
                    Assert(unlock.IsApplied || unlock.Status == ProgressionResultStatus.Unchanged,
                        "could not prepare the isolated Feather Kingdom unlock");
                }
                Assert(runtime.NpcAdventure.RemainingBattleCount(DateTime.Now) == 10,
                    "the isolated profile must start with all ten daily attempts");
                return "Feather Kingdom unlock and isolated daily attempts are ready";
            });
            if (!ok) { completed(false); yield break; }

            NpcTravelNode node = runtime.NpcAdventure.NextNode;
            ok = Check("npc-adventure-page", () =>
            {
                Assert(node != null, "the first authorized official Bond Adventure node is missing");
                MethodInfo open = typeof(HomeHudView).GetMethod("OpenNpcAdventurePage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(open != null, "the original NPC Adventure page entry is missing");
                open.Invoke(runtime.Hud, new object[] { node.map_group_id, node.region_id });
                GameObject page = PrivateField<GameObject>(runtime.Hud, "npcTravelRoot");
                Assert(page && page.name == "NpcAdventurePanel",
                    "the original NPC Adventure page did not open");
                return "original NPC Adventure page opened for " + node.stage_id;
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return new WaitForSecondsRealtime(1.1f);
            yield return new WaitForEndOfFrame();
            yield return Capture("npc-adventure-page.png");

            ok = Check("npc-adventure-battle-entry", () =>
            {
                GameObject page = PrivateField<GameObject>(runtime.Hud, "npcTravelRoot");
                Button battle = page.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value && value.name == "BattleBtn");
                Assert(battle && battle.gameObject.activeInHierarchy,
                    "the original BattleBtn is missing or inactive");
                ClickPartnerUi(battle);
                GameObject selection = PrivateField<GameObject>(runtime.Hud, "npcAdventureSelectRoot");
                Assert(selection && selection.name == "NpcAdventureSelectNpcDialog",
                    "BattleBtn did not open the original three-partner selection window");
                return "BattleBtn opened the original partner picker";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return new WaitForEndOfFrame();
            ok = Check("npc-adventure-selection-cancel", () =>
            {
                GameObject selection = PrivateField<GameObject>(runtime.Hud, "npcAdventureSelectRoot");
                Button cancel = selection.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value && value.name == "CancelBtn");
                Assert(cancel, "the original partner selection cancel button is missing");
                ClickPartnerUi(cancel);
                Assert(PrivateField<GameObject>(runtime.Hud, "npcAdventureSelectRoot") == null,
                    "the original partner selection window did not close");
                Assert(runtime.NpcAdventure.State.DailyBattleCount == 0
                    && runtime.NpcAssistanceProgress.ActiveBattle == null,
                    "opening and cancelling selection must not consume an attempt or commit assistance");
                return "BattleBtn opened the original partner picker; cancel left counts and assistance unchanged";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;

            ok = Check("npc-adventure-sweep-fixture", () =>
            {
                Assert(string.IsNullOrEmpty(runtime.NpcAdventure.CreateSweepReceipt(node, DateTime.Now)),
                    "the first stage must not authorize a sweep");
                int[] candidates = ((IReadOnlyList<int>)typeof(HomeHudView).GetMethod("CandidateNpcIds",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runtime.Hud, null)).Take(3).ToArray();
                // Isolated rule fixture only; the manual battle's rendering is checked below.
                Assert(runtime.NpcAdventure.SetSelectedNpcClassIds(candidates).IsApplied,
                    "the sweep fixture could not select three partners");
                Assert(runtime.NpcAdventure.BeginBattle(node, DateTime.Now).IsApplied,
                    "the sweep fixture could not begin its first manual stage");
                string firstReceipt = runtime.NpcAdventure.State.ActiveBattleReceipt;
                ulong seed = unchecked((ulong)NpcAdventureBattleRunner.StableSeed(firstReceipt, node.stage_id));
                var firstRewards = runtime.NpcAdventureBattleCatalog.RollRewards(node.stage_id,
                    runtime.Profile.Save.Player.Level, runtime.Profile.Save.Player.SubRank, ref seed);
                Assert(runtime.NpcAdventure.CompleteBattle(node.stage_id, true, firstRewards.Select(value =>
                    new KeyValuePair<int, long>(value.ItemId, value.Count)).ToArray(), firstReceipt).IsApplied,
                    "the isolated first-stage completion fixture failed");
                runtime.NpcAdventure.SetSelectedNpcClassIds(Array.Empty<int>());
                node = runtime.NpcAdventure.NextNode;
                typeof(HomeHudView).GetMethod("CloseNpcTravelPages", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(runtime.Hud, null);
                typeof(HomeHudView).GetMethod("OpenNpcAdventurePage", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(runtime.Hud, new object[] { node.map_group_id, node.region_id });
                return "first-stage sweep rejected; isolated completed-first-stage fixture prepared";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return new WaitForSecondsRealtime(1.1f);
            yield return new WaitForEndOfFrame();

            string receipt = runtime.NpcAdventure.CreateSweepReceipt(node, DateTime.Now);
            List<NpcAdventureRewardItem> expectedRewards = null;
            ok = Check("npc-adventure-sweep-preview", () =>
            {
                Assert(!string.IsNullOrEmpty(receipt), "the current next stage did not authorize a sweep");
                ulong random = unchecked((ulong)NpcAdventureBattleRunner.StableSeed(receipt, node.stage_id));
                expectedRewards = runtime.NpcAdventureBattleCatalog.RollRewards(node.stage_id,
                    runtime.Profile.Save.Player.Level, runtime.Profile.Save.Player.SubRank, ref random);
                return "rolled " + expectedRewards.Count + " configured stage.Awards rows";
            });
            if (!ok) { completed(false); yield break; }
            Dictionary<int, long> expectedRewardCounts = expectedRewards.GroupBy(value => value.ItemId)
                .ToDictionary(value => value.Key, value => value.Sum(item => item.Count));
            Dictionary<int, long> before = runtime.Profile.Save.Player.Inventory
                .GroupBy(value => value.ItemId).ToDictionary(value => value.Key, value => value.Sum(item => item.Count));

            ok = Check("npc-adventure-sweep-input", () =>
            {
                GameObject page = PrivateField<GameObject>(runtime.Hud, "npcTravelRoot");
                Button sweep = page.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value && value.name == "FastBattleBtn");
                Assert(sweep && sweep.gameObject.activeInHierarchy,
                    "the original FastBattleBtn is missing or inactive");
                ClickPartnerUi(sweep);
                runtime.Profile.Reload();
                OfflineNpcAdventureProgress sweepState = runtime.NpcAdventure.State;
                Assert(sweepState.DailyBattleCount == 2,
                    "one-click sweep did not consume exactly one shared daily attempt");
                Assert(sweepState.ActiveStageId == 0
                    && sweepState.MapGroupId == node.map_group_id
                    && sweepState.RegionId == node.region_id
                    && sweepState.NodeId == node.node_id,
                    "one-click sweep did not complete only the current next node");
                Assert(!SameNode(runtime.NpcAdventure.NextNode, node),
                    "one-click sweep did not advance beyond its completed node");
                GameObject result = PrivateField<GameObject>(runtime.Hud, "npcAdventureResultRoot");
                Assert(result && result.name == "NpcAdventureBattleResult",
                    "one-click sweep did not show the original Bond Adventure result panel");
                foreach (int rewardId in expectedRewardCounts.Keys)
                {
                    Transform reward = result.GetComponentsInChildren<Transform>(true)
                        .SingleOrDefault(value => value.name == "AwardPanelItem_" + rewardId);
                    Image icon = reward?.GetComponentsInChildren<Image>(true)
                        .SingleOrDefault(value => value.name == "icon");
                    Assert(icon && icon.sprite && icon.gameObject.activeInHierarchy,
                        "configured sweep reward icon was not restored: " + rewardId);
                }
                Dictionary<int, long> after = runtime.Profile.Save.Player.Inventory
                    .GroupBy(value => value.ItemId).ToDictionary(value => value.Key, value => value.Sum(item => item.Count));
                foreach (int itemId in before.Keys.Concat(after.Keys).Concat(expectedRewardCounts.Keys).Distinct())
                {
                    long prior = before.TryGetValue(itemId, out long count) ? count : 0;
                    long awarded = expectedRewardCounts.TryGetValue(itemId, out long expected) ? expected : 0;
                    long current = after.TryGetValue(itemId, out long total) ? total : 0;
                    Assert(current - prior == awarded,
                        "sweep inventory delta differs from this stage's stage.Awards item " + itemId);
                }
                return "simulated FastBattleBtn click committed one attempt, exact stage.Awards, next node, and original result panel";
            });
            if (ok)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                yield return Capture("npc-adventure-sweep-result.png");
            }
            if (!ok) { completed(false); yield break; }

            node = runtime.NpcAdventure.NextNode;
            ok = Check("npc-adventure-assist-continue", () =>
            {
                GameObject result = PrivateField<GameObject>(runtime.Hud, "npcAdventureResultRoot");
                Button continueButton = result == null ? null : result.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value && value.name == "ContinueBtn" && value.gameObject.activeInHierarchy);
                Assert(continueButton, "the original sweep result did not expose its continue button");
                ClickPartnerUi(continueButton);
                GameObject selection = PrivateField<GameObject>(runtime.Hud, "npcAdventureSelectRoot");
                Assert(selection && selection.name == "NpcAdventureSelectNpcDialog",
                    "the original continue button did not open the partner picker");
                Assert(PrivateField<List<GameObject>>(runtime.Hud, "npcAdventureSelectItems").Count >= 3,
                    "fewer than three model-backed Bond Adventure partners are available");
                return "original result continue opened the official partner picker";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return Capture("npc-adventure-assist-selection.png");

            for (int slot = 0; slot < OfflineNpcAdventureSession.MaximumCompanions; slot++)
            {
                int selectedNpcId = 0;
                ok = Check("npc-adventure-assist-select-" + (slot + 1), () =>
                {
                    List<int> selected = PrivateField<List<int>>(runtime.Hud, "npcAdventureSelectedNpcIds");
                    string prefix = "NpcAdventureSelectNpcItem_";
                    GameObject item = PrivateField<List<GameObject>>(runtime.Hud, "npcAdventureSelectItems")
                        .FirstOrDefault(value => value && value.name.StartsWith(prefix, StringComparison.Ordinal)
                            && int.TryParse(value.name.Substring(prefix.Length), out int id) && !selected.Contains(id));
                    Assert(item, "the official picker has no additional unselected model-backed partner");
                    Assert(int.TryParse(item.name.Substring(prefix.Length), out selectedNpcId),
                        "the official partner item id is malformed");
                    Button select = item.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(value => value && value.name == "SelectBtn");
                    Assert(select, "an original partner item has no SelectBtn");
                    ClickPartnerUi(select);
                    return "simulated original SelectBtn click selected NPC " + selectedNpcId;
                });
                if (!ok) { completed(false); yield break; }
                yield return null;
            }

            NpcAdventureBattleRunner battle = null;
            ok = Check("npc-adventure-assist-confirm", () =>
            {
                GameObject selection = PrivateField<GameObject>(runtime.Hud, "npcAdventureSelectRoot");
                Button confirm = selection == null ? null : selection.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value && value.name == "ConfirmBtn");
                Assert(confirm, "the original partner picker confirm button is missing");
                ClickPartnerUi(confirm);
                battle = UnityEngine.Object.FindObjectOfType<NpcAdventureBattleRunner>();
                Assert(battle && battle.Controller, "confirming three partners did not mount the official battle runner");
                return "simulated original ConfirmBtn click mounted the Bond Adventure battle";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return new WaitForEndOfFrame();

            ok = Check("npc-adventure-assist-battle", () =>
            {
                OfflineNpcAdventureProgress state = runtime.NpcAdventure.State;
                OfflineNpcAssistBattle assist = runtime.NpcAssistanceProgress.ActiveBattle;
                int[] expected = state.SelectedNpcClassIds.OrderBy(value => value).ToArray();
                Assert(state.DailyBattleCount == 3 && state.ActiveStageId == node.stage_id,
                    "starting the assisted battle did not consume exactly one additional attempt: count="
                        + state.DailyBattleCount + ", activeStage=" + state.ActiveStageId + ", expected=" + node.stage_id);
                Assert(assist != null && assist.playType == (int)NpcAssistPlayType.BondAdventure
                    && assist.stageId == node.stage_id && assist.npcClassIds != null
                    && assist.npcClassIds.OrderBy(value => value).SequenceEqual(expected),
                    "the assisted battle did not commit its PlayType 6 snapshot");
                Assert(battle && battle.Controller && battle.Controller.HasBattleEnvironment,
                    "the original battle environment failed to mount");
                int[] actual = battle.Controller.Checkpoint.Units
                    .Where(value => expected.Contains(value.NpcClassId))
                    .Select(value => value.NpcClassId).OrderBy(value => value).ToArray();
                Assert(expected.Length == OfflineNpcAdventureSession.MaximumCompanions
                    && actual.SequenceEqual(expected),
                    "the battle checkpoint does not contain the three selected partner units");
                Assert(!string.IsNullOrEmpty(state.ActiveBattleCheckpoint),
                    "the initial assisted battle checkpoint was not persisted");
                return "PlayType 6 snapshot and all three selected partners are present in the mounted battle";
            });
            if (ok)
            {
                yield return new WaitForEndOfFrame();
                yield return Capture("npc-adventure-assist-preparation.png");
            }
            completed(ok);
        }

        private static bool SameNode(NpcTravelNode left, NpcTravelNode right) => left != null && right != null
            && left.map_group_id == right.map_group_id && left.region_id == right.region_id
            && left.node_id == right.node_id && left.stage_id == right.stage_id;

        private IEnumerator VerifyPartners(HomeFeatureRuntime runtime, Action<bool> completed)
        {
            bool ok = true;
            ok &= Check("partner-model-10709", () =>
            {
                var assets = Resources.Load<PartnerNativeDetailActorAssets>("Partners/PartnerNativeDetailActorAssets");
                Assert(assets, "original partner model asset catalog is missing");
                GameObject model = assets.LoadPrefab("Appearance/Npc/10709/NpcModel_UI_10709");
                Assert(model && model.name == "NpcModel_UI_10709",
                    "10709 did not resolve to its own official detail prefab");
                Assert(!assets.LoadPrefab("Appearance/Npc/10611/NpcModel_UI_10709"),
                    "incorrect original 10709 folder path is still installed");
                return "official 10709 prefab loaded from its own NPC folder";
            });
            ok &= Check("partner-model-10611-static", () =>
            {
                var assets = Resources.Load<PartnerNativeDetailActorAssets>("Partners/PartnerNativeDetailActorAssets");
                GameObject model = assets.LoadPrefab("Appearance/Npc/10611/NpcModel_UI_10611");
                Assert(model, "official 10611 detail prefab is missing");
                Image image = model.GetComponentsInChildren<Image>(true).FirstOrDefault(value => value.sprite);
                Assert(image, "10611 official static Image has no bound sprite");
                MonoBehaviour proxy = model.GetComponentsInChildren<MonoBehaviour>(true)
                    .FirstOrDefault(value => value.GetType().Name == "SkeletonGraphicProxy");
                Component graphic = proxy == null ? null : proxy.GetType().GetField("mSkeletonGraphic",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(proxy) as Component;
                Assert(graphic == null, "10611 must remain a static official Image with no bone animation binding");
                return "official 10611 Image retained with no fabricated skeleton animation";
            });
            HomeUiRenderer home = PrivateField<HomeUiRenderer>(runtime.Hud, "homeRenderer");
            HomeUiRenderer functions = PrivateField<HomeUiRenderer>(runtime.Hud, "functionsRenderer");
            bool functionMenuOpened = Check("partner-function-menu", () =>
            {
                Button menuEntry = home.Find<Button>(
                    "/HomeUI/Content/FunctionEntryGroup/FunctionEntryFunc/Content/Entry/EntryBtnFolded");
                Assert(menuEntry != null, "official function menu entry is missing");
                ClickPartnerUi(menuEntry);
                GameObject functionsPanel = PrivateField<GameObject>(runtime.Hud, "functionsPanel");
                Assert(functionsPanel && functionsPanel.activeInHierarchy,
                    "official function menu did not open from its HUD entry");
                return "official function menu opened from its HUD entry";
            });
            ok &= functionMenuOpened;
            if (!functionMenuOpened)
            {
                completed(false);
                yield break;
            }
            yield return null;
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
            yield return Capture("partners-function-menu.png");
            bool rosterOpened = Check("partner-roster", () =>
            {
                GameObject friendNode = functions.AllNodes.SingleOrDefault(value =>
                    value && value.name == "FunctionEntryFriendItem");
                Selectable friendEntry = friendNode ? friendNode.GetComponent<Selectable>() : null;
                Assert(friendEntry && friendEntry.gameObject.activeInHierarchy,
                    "official partner roster function entry is missing or hidden");
                RectTransform friendRect = friendEntry.transform as RectTransform;
                Assert(friendRect && friendRect.rect.width > 0f && friendRect.rect.height > 0f,
                    "official partner roster entry has no laid-out hit area");
                ClickPartnerUi(friendEntry);
                GameObject root = PrivateField<GameObject>(runtime.Hud, "activeDialog");
                Assert(root != null && root.name == "PartnerRosterDialog", "official partner roster root opens");
                PartnerPanelView panel = PrivateField<PartnerPanelView>(runtime.Hud, "partnerPanel");
                Assert(panel != null && panel.Content && panel.Content.GetComponent<NpcPanel>(),
                    "complete original partner panel controller is bound");
                return root.name;
            });
            ok &= rosterOpened;
            if (!rosterOpened)
            {
                completed(false);
                yield break;
            }
            float rosterDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < rosterDeadline)
            {
                yield return null;
                var current = PrivateField<GameObject>(runtime.Hud, "activeDialog");
                var visible = current.GetComponentsInChildren<NpcPanelNpcItem>();
                if (visible.Length > 0 && visible.All(card =>
                    PrivateField<GameObject>(card, "mContentRoot").activeInHierarchy)) break;
            }
            ok &= Check("partner-roster-content", () =>
            {
                var root = PrivateField<GameObject>(runtime.Hud, "activeDialog");
                var cards = root.GetComponentsInChildren<NpcPanelNpcItem>();
                Assert(cards.Length > 0, "official roster has no cells");
                foreach (var card in cards)
                    Assert(PrivateField<GameObject>(card, "mContentRoot").activeInHierarchy,
                        "official card content did not appear after the display sequence");
                return cards.Length + " visible official cards";
            });
            yield return Capture("partners-roster.png");
            PartnerPanelView partnerPanel = PrivateField<PartnerPanelView>(runtime.Hud, "partnerPanel");
            NpcPanel npcPanel = partnerPanel.Content.GetComponent<NpcPanel>();
            IDictionary tabItems = PrivateField<IDictionary>(npcPanel, "mTabType2TabItemDict");
            WorldTabItem dialogueTab = (WorldTabItem)tabItems[NpcPanelTabType.Dialogue];
            bool dialogueAvailable = !runtime.Rules.IsFunctionLimited(57);
            ok &= Check("partner-dialogue-page", () =>
            {
                Assert(dialogueTab != null && dialogueTab.gameObject.activeSelf == dialogueAvailable,
                    "original conversation tab visibility does not match its local unlock rule");
                if (dialogueAvailable)
                {
                    ClickPartnerUi(dialogueTab);
                    NpcPanelDialogueList list = partnerPanel.Content.GetComponentInChildren<NpcPanelDialogueList>(true);
                    Assert(list && list.gameObject.activeInHierarchy,
                        "original partner conversation list did not open through its tab");
                }
                return dialogueAvailable ? "original partner conversation page opened by simulated tab click"
                    : "original conversation tab remains locked by the local rule";
            });
            if (dialogueAvailable)
            {
                var presentation = PrivateField<object>(runtime.Hud, "partnerDialoguePresentation");
                var conversations = PrivateField<OfflinePartnerConversationUiSession>(presentation, "conversations");
                float dialoguePageDeadline = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < dialoguePageDeadline)
                {
                    var dialogueList = partnerPanel.Content.GetComponentInChildren<NpcPanelDialogueList>(true);
                    bool emptyTip = PrivateField<GameObject>(dialogueList, "mEmptyTip").activeSelf;
                    if (emptyTip == (conversations.Source.Dialogues.Count == 0)) break;
                    yield return null;
                }
                ok &= Check("partner-dialogue-list-state", () =>
                {
                    var dialogueList = partnerPanel.Content.GetComponentInChildren<NpcPanelDialogueList>(true);
                    bool emptyTip = PrivateField<GameObject>(dialogueList, "mEmptyTip").activeSelf;
                    Assert(emptyTip == (conversations.Source.Dialogues.Count == 0),
                        "original empty/list state does not match locally saved conversations");
                    return conversations.Source.Dialogues.Count + " locally saved conversations";
                });
                yield return Capture("partners-dialogues.png");
                if (conversations.Source.Dialogues.Count > 0)
                {
                    var dialogueList = partnerPanel.Content.GetComponentInChildren<NpcPanelDialogueList>(true);
                    var npcItem = dialogueList.GetComponentsInChildren<NpcPanelDialogueListNpcItem>(true)
                        .FirstOrDefault(item => item.gameObject.activeInHierarchy);
                    ok &= Check("partner-dialogue-playback", () =>
                    {
                        Assert(npcItem != null, "saved conversations have no original NPC row");
                        ClickPartnerUi(PrivateField<Button>(npcItem, "mBtn"));
                        return "expanded saved conversation group through its original button";
                    });
                    float rowDeadline = Time.realtimeSinceStartup + 3f;
                    NpcPanelDialogueListDialogueItem dialogueItem = null;
                    while (Time.realtimeSinceStartup < rowDeadline)
                    {
                        dialogueItem = dialogueList.GetComponentsInChildren<NpcPanelDialogueListDialogueItem>(true)
                            .FirstOrDefault(item => item.gameObject.activeInHierarchy
                                && PrivateField<GameObject>(item, "mContentRoot").activeInHierarchy);
                        if (dialogueItem != null) break;
                        yield return null;
                    }
                    ok &= Check("partner-dialogue-open", () =>
                    {
                        Assert(dialogueItem != null, "expanded original conversation group has no dialogue item");
                        ClickPartnerUi(PrivateField<Button>(dialogueItem, "mButton"));
                        PartnerDialoguePlaybackView playback = PrivateField<PartnerDialoguePlaybackView>(runtime.Hud,
                            "partnerDialoguePlayback");
                        Assert(playback && playback.Content && playback.Content.GetComponent<NpcDialoguePanel>(),
                            "official conversation playback window did not open");
                        return "official conversation playback opened by simulated row click";
                    });
                    yield return Capture("partners-dialogue-playback.png");
                    var playback = PrivateField<PartnerDialoguePlaybackView>(runtime.Hud, "partnerDialoguePlayback");
                    CloseWindowBtn playbackClose = playback ? playback.Content.GetComponentsInChildren<CloseWindowBtn>(true)
                        .FirstOrDefault(value => value.myButton && value.myButton.gameObject.activeInHierarchy) : null;
                    ok &= Check("partner-dialogue-close", () =>
                    {
                        Assert(playbackClose != null, "original conversation window has no active close button");
                        ClickPartnerUi(playbackClose.myButton);
                        return "closed official playback without closing the partner panel";
                    });
                    for (int frame = 0; frame < 180 && PrivateField<PartnerDialoguePlaybackView>(runtime.Hud,
                        "partnerDialoguePlayback") != null; frame++) yield return null;
                    ok &= Check("partner-dialogue-parent", () =>
                    {
                        Assert(PrivateField<PartnerDialoguePlaybackView>(runtime.Hud, "partnerDialoguePlayback") == null,
                            "conversation close animation did not complete");
                        Assert(PrivateField<PartnerPanelView>(runtime.Hud, "partnerPanel") == partnerPanel
                            && partnerPanel.Content && partnerPanel.Content.activeInHierarchy,
                            "closing playback also closed the partner panel");
                        return "partner page remains open after playback close";
                    });
                }
            }
            WorldTabItem starGodTab = (WorldTabItem)tabItems[NpcPanelTabType.StarGod];
            ok &= Check("partner-star-god-page", () =>
            {
                bool limited = runtime.Rules.IsFunctionLimited(174);
                Assert(starGodTab.gameObject.activeSelf == !limited,
                    "original star-god tab visibility does not match its local unlock rule");
                if (!limited)
                {
                    ClickPartnerUi(starGodTab);
                    var content = partnerPanel.Content.GetComponentInChildren<NpcPanelStarGodContent>(true);
                    Assert(content && content.gameObject.activeInHierarchy,
                        "original star-god page did not open through its tab");
                }
                return limited ? "original star-god tab remains locked by the local rule"
                    : "original star-god page opened by simulated tab click";
            });
            if (starGodTab.gameObject.activeSelf) yield return Capture("partners-star-god.png");
            WorldTabItem npcListTab = (WorldTabItem)tabItems[NpcPanelTabType.NpcList];
            GameObject npcListRoot = PrivateField<GameObject>(npcPanel, "mNpcListGo");
            if (!npcListRoot.activeInHierarchy && npcListTab.gameObject.activeInHierarchy)
            {
                ClickPartnerUi(npcListTab);
                float listDeadline = Time.realtimeSinceStartup + 3f;
                while (!npcListRoot.activeInHierarchy && Time.realtimeSinceStartup < listDeadline)
                    yield return null;
            }
            ok &= Check("partner-list-return", () =>
            {
                Assert(npcListRoot.activeInHierarchy || !npcListTab.gameObject.activeInHierarchy,
                    "original partner list page did not return after changing tabs");
                return npcListRoot.activeInHierarchy ? "original partner list page active"
                    : "partner list tab unavailable under the current local function rules";
            });
            if (npcListRoot.activeInHierarchy)
            {
                ok &= Check("partner-detail", () =>
                {
                    var roster = PrivateField<GameObject>(runtime.Hud, "activeDialog");
                    var card = roster.GetComponentsInChildren<NpcPanelNpcItem>().Single(value =>
                        PrivateField<Zjcs.Official.Partners.NpcRosterItem>(value, "mNpcDataWrap").NpcClassId == 8402
                        && PrivateField<Button>(value, "mDetailBtn").gameObject.activeInHierarchy);
                    ClickPartnerUi(PrivateField<Button>(card, "mDetailBtn"));
                    GameObject root = PrivateField<GameObject>(runtime.Hud, "activeDialog");
                    Assert(root != null && root.name == "PartnerDetailDialog", "official partner detail root opens");
                    PartnerDetailWindowView detailWindow = PrivateField<PartnerDetailWindowView>(runtime.Hud,
                        "partnerDetailWindow");
                    Assert(detailWindow && detailWindow.Panel && detailWindow.Content
                        && detailWindow.Content.activeInHierarchy,
                        "official partner detail window and panel are bound");
                    Assert(root.GetComponentsInChildren<MonoBehaviour>(true).Any(value =>
                        value.GetType().Name == "NpcDetailPanelPropContent"),
                        "official partner property content exists");
                    return root.name;
                });
                yield return Capture("partners-detail.png");
                if (Environment.GetCommandLineArgs().Contains("-partnerProfileAcceptance"))
                {
                    bool profileOpened = Check("partner-profile-page", () =>
                    {
                        PartnerDetailWindowView detailWindow = PrivateField<PartnerDetailWindowView>(runtime.Hud,
                            "partnerDetailWindow");
                        NpcDetailPanel detailPanel = detailWindow.Content.GetComponentInChildren<NpcDetailPanel>(true);
                        NpcDetailPanel.TabData profileTab = PrivateField<List<NpcDetailPanel.TabData>>(detailPanel,
                            "mTabDatas").Single(value => value.TabType == NpcDetailPanel.TabType.Profile);
                        Assert(profileTab.TabItem && profileTab.TabItem.gameObject.activeInHierarchy,
                            "original profile tab is unavailable");
                        ClickPartnerUi(profileTab.TabItem);
                        NpcDetailPanelProfileContent content = detailPanel.GetComponentInChildren<NpcDetailPanelProfileContent>(true);
                        Assert(content && content.gameObject.activeInHierarchy,
                            "original partner archive page did not open through its tab");
                        return "original partner archive page opened by simulated tab click";
                    });
                    ok &= profileOpened;
                    if (profileOpened)
                    {
                        yield return null;
                        yield return Capture("partners-profile.png");
                        NpcDetailPanelProfileContent profile = PrivateField<NpcDetailPanelProfileContent>(
                            PrivateField<PartnerDetailWindowView>(runtime.Hud, "partnerDetailWindow").Content
                                .GetComponentInChildren<NpcDetailPanel>(true), "mProfileContent");
                        object info = PrivateField<object>(profile, "mProfileInfo");
                        int chapter = (int)info.GetType().GetField("ProfileId").GetValue(info);
                        var rewards = (Dictionary<int, long>)info.GetType().GetField("Reward").GetValue(info);
                        Button unlock = PrivateField<Button>(profile, "mUnlockBtn");
                        var profileInventoryBefore = rewards.Keys.ToDictionary(itemId => itemId,
                            itemId => runtime.ItemCount(itemId));
                        ok &= Check("partner-profile-claim-input", () =>
                        {
                            Assert(unlock && unlock.gameObject.activeInHierarchy && unlock.interactable,
                                "original profile unlock button is not enabled at the configured friendship threshold");
                            ClickPartnerUi(unlock);
                            return "claimed the unlocked archive chapter through its original button";
                        });
                        yield return null;
                        ok &= Check("partner-profile-reward-save", () =>
                        {
                            runtime.Profile.Reload();
                            var profileFriendship = runtime.Profile.Save.Player.CharacterProperties.NpcFriendships
                                .Single(value => value.NpcClassId == 8402);
                            Assert(profileFriendship.ReceivedProfileRewards.Contains(chapter),
                                "archive chapter receipt was not restored from disk");
                            foreach (var reward in rewards)
                                Assert(runtime.ItemCount(reward.Key) == profileInventoryBefore[reward.Key] + reward.Value,
                                    "original archive reward was not restored from disk: " + reward.Key);
                            return "original archive reward and chapter receipt restored after reload";
                        });
                        yield return Capture("partners-profile-claimed.png");
                    }
                }
            }
            else log.Add("SKIP partner-detail: the original NPC list tab is locked by current local function rules");
            ok &= Check("partner-gift", () =>
            {
                runtime.OpenPartnerGift(8402);
                GameObject root = PrivateField<GameObject>(runtime.Hud, "activeDialog");
                Assert(root != null && root.name == "PartnerGiftDialog", "official partner gift root opens");
                PartnerDetailWindowView giftWindow = PrivateField<PartnerDetailWindowView>(runtime.Hud,
                    "partnerDetailWindow");
                Assert(giftWindow && giftWindow.Panel && giftWindow.Content
                    && giftWindow.Content.activeInHierarchy,
                    "official partner gift window and panel are bound");
                Assert(root.GetComponentsInChildren<MonoBehaviour>(true).Any(value =>
                    value.GetType().Name == "NpcDetailPanelGiftContent"),
                    "official partner gift content exists");
                Assert(root.GetComponentsInChildren<MonoBehaviour>(true).Any(value =>
                    value.GetType().Name == "NpcFriendshipLevelContent"),
                    "official partner friendship level content exists");
                return root.name;
            });
            yield return Capture("partners-gift.png");
            // Opening a page can show a temporary Home notice over the lower
            // controls. Let its original lifetime finish before pointer input.
            yield return new WaitForSecondsRealtime(2.5f);
            var giftRoot = PrivateField<GameObject>(runtime.Hud, "activeDialog");
            var giftPanel = giftRoot.GetComponentInChildren<NpcDetailPanelGiftContent>();
            var giftIcons = PrivateField<Dictionary<int, ItemIcon>>(giftPanel, "mGiftItemIconDict");
            int giftId = giftIcons.Keys.First();
            long inventoryBefore = runtime.ItemCount(giftId);
            long friendshipBefore = runtime.Profile.Save.Player.CharacterProperties.NpcFriendships
                .Single(value => value.NpcClassId == 8402).FriendshipExp;
            int expectedExperience = OfficialPartnerGiftAssetCatalog.LoadDefault().GetAllExpByDict(
                new Dictionary<int, long> { [giftId] = 1 }, runtime.PartnerGiftServices.GetNpcInfo(8402).GiftPreferenceDict);
            ok &= Check("partner-gift-selection-input", () =>
            {
                var icon = giftIcons[giftId];
                Assert(PrivateField<ImageProxy>(icon, "mIconProxy").Sprite != null,
                    "official gift icon is missing: " + giftId + "/" + icon.myInfo.Icon);
                ClickPartnerUi(icon.Button);
                Assert(PrivateField<Dictionary<int, long>>(giftPanel, "mSelectedGiftDict")[giftId] == 1,
                    "gift pointer did not select one item");
                ClickPartnerUi(PrivateField<Button>(giftPanel, "mResetBtn"));
                Assert(PrivateField<Dictionary<int, long>>(giftPanel, "mSelectedGiftDict").Count == 0,
                    "gift reset pointer did not clear selection");
                ClickPartnerUi(icon.Button);
                return "selected, reset and selected through official buttons";
            });
            // A slow import/render frame can outlast a real-time delay before the
            // HUD has ticked its notice clock. Observe the visible layer itself.
            float noticeWaitDeadline = Time.realtimeSinceStartup + 15f;
            while (GameObject.Find("HomeToast") != null && Time.realtimeSinceStartup < noticeWaitDeadline)
                yield return null;
            ok &= Check("partner-gift-submit-input", () =>
            {
                ClickPartnerUi(PrivateField<Button>(giftPanel, "mGiveGiftBtn"));
                return "submitted through the exposed original gift button";
            });
            float giftDeadline = Time.realtimeSinceStartup + 5f;
            while (runtime.ItemCount(giftId) == inventoryBefore && Time.realtimeSinceStartup < giftDeadline)
                yield return null;
            ok &= Check("partner-gift-save", () =>
            {
                runtime.Profile.Reload();
                Assert(runtime.ItemCount(giftId) == inventoryBefore - 1, "gift inventory was not saved");
                Assert(runtime.Profile.Save.Player.CharacterProperties.NpcFriendships
                    .Single(value => value.NpcClassId == 8402).FriendshipExp == friendshipBefore + expectedExperience,
                    "original gift experience was not saved");
                Assert(PrivateField<Dictionary<int, long>>(giftPanel, "mSelectedGiftDict").Count == 0,
                    "gift selection was not cleared after saving");
                return "original experience and one gift consumed, restored from disk";
            });
            yield return Capture("partners-gift-saved.png");
            MethodInfo friendship = runtime.Hud.GetType().GetMethod("OpenPartnerFriendshipDialog",
                BindingFlags.Instance | BindingFlags.NonPublic);
            ok &= Check("partner-friendship-route", () =>
            {
                Assert(friendship != null, "friendship dialog route exists");
                PartnerGiftNpcInfo npc = runtime.PartnerGiftServices.GetNpcInfo(8402);
                Assert(npc != null, "friendship dialog partner metadata exists");
                Action<IPartnerGiftFriendshipDialog> initialize = dialog => dialog.Init(npc);
                friendship.Invoke(runtime.Hud, new object[] { initialize });
                return "opening original friendship window";
            });
            for (int frame = 0; frame < 120 && GameObject.Find("OfficialPartnerFriendshipDialog") == null; frame++)
                yield return null;
            ok &= Check("partner-friendship", () =>
            {
                GameObject root = GameObject.Find("OfficialPartnerFriendshipDialog");
                Assert(root != null, "official friendship dialog root opens");
                Assert(root.GetComponents<MonoBehaviour>().Any(value => value.GetType().Name == "NpcFriendshipDialog"),
                    "official friendship dialog controller is bound");
                return root.name;
            });
            yield return Capture("partners-friendship.png");
            runtime.Hud.TryHandleBack();
            completed(ok);
        }

        private IEnumerator VerifyPartnerFriendshipOverlay(HomeFeatureRuntime runtime, Action<bool> completed)
        {
            bool ok = true;
            GameObject giftRoot = null;
            NpcDetailPanelGiftContent gift = null;
            Button tip = null;
            int giftId = 0;
            var initialPool = ProfessionGameObjectPoolBridge.Provider;
            var initialLocalize = WorldMultiLanguageText.Localize;
            var initialFont = WorldMultiLanguageText.LanguageFont;
            var initialSound = OfficialWwiseEvent.Host;
            ok &= Check("partner-friendship-gift-entry", () =>
            {
                runtime.OpenPartnerGift(8402);
                giftRoot = PrivateField<GameObject>(runtime.Hud, "activeDialog");
                gift = giftRoot.GetComponentInChildren<NpcDetailPanelGiftContent>();
                tip = PrivateField<Button>(giftRoot.GetComponentInChildren<NpcFriendshipLevelContent>(), "mTipBtn");
                return "original gift page and friendship information button bound";
            });
            float deadline = Time.realtimeSinceStartup + 5f;
            while (gift != null && PrivateField<Dictionary<int, ItemIcon>>(gift, "mGiftItemIconDict").Count == 0
                && Time.realtimeSinceStartup < deadline) yield return null;
            var giftPool = ProfessionGameObjectPoolBridge.Provider;
            var giftLocalize = WorldMultiLanguageText.Localize;
            var giftFont = WorldMultiLanguageText.LanguageFont;
            ok &= Check("partner-friendship-gift-tip", () =>
            {
                var icons = PrivateField<Dictionary<int, ItemIcon>>(gift, "mGiftItemIconDict");
                giftId = icons.Keys.First();
                ClickPartnerUi(icons[giftId].Button);
                ClickPartnerUi(tip);
                return "selected one gift and clicked original information button";
            });
            yield return new WaitForSeconds(.6f);
            ok &= Check("partner-friendship-overlay", () =>
            {
                var root = GameObject.Find("OfficialPartnerFriendshipDialog");
                Assert(root != null && root.GetComponent<NpcFriendshipDialog>() != null, "original friendship window did not open");
                var blur = GameObject.Find("OfficialPartnerFriendshipMask").GetComponent<WorldWindowBlurMask>();
                Assert(blur.MaskType == 3 && blur.Image.texture is RenderTexture, "original blur mask capture is unbound");
                Assert(giftRoot != null && giftRoot.activeInHierarchy
                    && PrivateField<GameObject>(runtime.Hud, "activeDialog") == giftRoot, "information popup replaced the underlying gift page");
                var dialog = root.GetComponent<NpcFriendshipDialog>();
                Assert(PrivateField<List<ItemIcon>>(dialog, "mItemIcons").Count > 0, "original preferred gifts are absent");
                Assert(PrivateField<List<NpcFriendshipPreviewPropItem>>(dialog, "mPropItems").Count > 0,
                    "original friendship property preview is absent");
                Assert(PrivateField<Dictionary<int, long>>(gift, "mSelectedGiftDict")[giftId] == 1,
                    "opening information cleared the pending gift");
                return "original preference icons and property preview overlay the existing selected gift";
            });
            yield return Capture("partners-friendship-overlay.png");
            ok &= Check("partner-friendship-mask", () =>
            {
                var mask = GameObject.Find("OfficialPartnerFriendshipMask").GetComponent<WorldWindowMask>();
                Assert(mask.Canvas.sortingOrder > gift.GetComponentInParent<Canvas>().sortingOrder,
                    "original blur mask sorts behind the underlying gift page");
                ClickPartnerUi(mask, new Vector2(1f, 1f));
                return "outside pointer reaches original window mask";
            });
            yield return new WaitForSeconds(.6f);
            ok &= Check("partner-friendship-return", () =>
            {
                Assert(GameObject.Find("OfficialPartnerFriendshipDialog") == null
                    && GameObject.Find("OfficialPartnerFriendshipMask") == null, "friendship popup or mask remained after closing");
                Assert(giftRoot != null && giftRoot.activeInHierarchy, "mask closed the underlying gift page");
                Assert(PrivateField<Dictionary<int, long>>(gift, "mSelectedGiftDict")[giftId] == 1,
                    "closing information cleared the selected gift");
                Assert(ProfessionGameObjectPoolBridge.Provider == giftPool && WorldMultiLanguageText.Localize == giftLocalize
                    && WorldMultiLanguageText.LanguageFont == giftFont, "underlying page services were not restored");
                ClickPartnerUi(tip);
                return "original gift selection and page services preserved after mask close";
            });
            yield return new WaitForSeconds(.6f);
            ok &= Check("partner-friendship-back", () =>
            {
                Assert(GameObject.Find("OfficialPartnerFriendshipDialog") != null, "friendship window did not reopen");
                Assert(runtime.Hud.TryHandleBack(), "back did not handle the friendship popup");
                return "back routed to upper friendship window";
            });
            yield return new WaitForSeconds(.6f);
            ok &= Check("partner-friendship-back-return", () =>
            {
                Assert(GameObject.Find("OfficialPartnerFriendshipDialog") == null && giftRoot != null && giftRoot.activeInHierarchy,
                    "back did not preserve the underlying gift page");
                ClickPartnerUi(tip);
                return "gift page remains interactive after reopening and back";
            });
            yield return new WaitForSeconds(.6f);
            runtime.Hud.CloseDialog();
            yield return null;
            ok &= Check("partner-friendship-owner-close", () =>
            {
                Assert(giftRoot == null && GameObject.Find("OfficialPartnerFriendshipDialog") == null
                    && GameObject.Find("OfficialPartnerFriendshipMask") == null, "closing the owner leaked its popup");
                Assert(ProfessionGameObjectPoolBridge.Provider == initialPool && WorldMultiLanguageText.Localize == initialLocalize
                    && WorldMultiLanguageText.LanguageFont == initialFont, "owner close did not restore original host services");
                Assert(ReferenceEquals(OfficialWwiseEvent.Host, initialSound), "owner close did not restore sound host");
                runtime.OpenPartnerDetail(8402);
                return "owner close cleaned popup and restored services";
            });
            yield return new WaitForSeconds(.3f);
            ok &= Check("partner-friendship-property-tip", () =>
            {
                var detail = PrivateField<GameObject>(runtime.Hud, "activeDialog").GetComponentInChildren<NpcDetailPanelPropContent>();
                ClickPartnerUi(PrivateField<Button>(detail, "mTipBtn"));
                return "owner close cleaned popup and restored services; original property information button clicked";
            });
            yield return new WaitForSeconds(.6f);
            yield return Capture("partners-friendship-property-overlay.png");
            ok &= Check("partner-friendship-property-owner", () =>
            {
                Assert(GameObject.Find("OfficialPartnerFriendshipDialog") != null
                    && PrivateField<GameObject>(runtime.Hud, "activeDialog")?.GetComponentInChildren<NpcDetailPanelPropContent>() != null,
                    "property information popup replaced the underlying detail page");
                runtime.Hud.CloseDialog();
                return "original property page also retains its owner beneath the information popup";
            });
            yield return null;
            completed(ok);
        }

        private IEnumerator VerifyPartnerGiftOverflow(HomeFeatureRuntime runtime, Action<bool> completed)
        {
            bool ok = true;
            NpcDetailPanelGiftContent gift = null;
            ItemIcon icon = null;
            int giftId = 0;
            long inventory = 0, friendship = 0;
            ok &= Check("partner-overflow-entry", () =>
            {
                runtime.OpenPartnerGift(8402);
                gift = PrivateField<GameObject>(runtime.Hud, "activeDialog")
                    .GetComponentInChildren<NpcDetailPanelGiftContent>();
                return "original gift page opened";
            });
            float populationDeadline = Time.realtimeSinceStartup + 5f;
            while (gift != null && PrivateField<Dictionary<int, ItemIcon>>(gift, "mGiftItemIconDict").Count == 0
                && Time.realtimeSinceStartup < populationDeadline) yield return null;
            ok &= Check("partner-overflow-gifts", () =>
            {
                var icons = PrivateField<Dictionary<int, ItemIcon>>(gift, "mGiftItemIconDict");
                Assert(icons.Count > 0, "original gift list did not populate");
                giftId = icons.Keys.First();
                icon = icons[giftId];
                inventory = runtime.ItemCount(giftId);
                friendship = runtime.Profile.Save.Player.CharacterProperties.NpcFriendships
                    .Single(value => value.NpcClassId == 8402).FriendshipExp;
                return "original configuration places friendship one point below its maximum";
            });
            yield return null;
            yield return null;
            ok &= Check("partner-overflow-open", () =>
            {
                ClickPartnerUi(icon.Button);
                ClickPartnerUi(PrivateField<Button>(gift, "mGiveGiftBtn"));
                return "original selected gift and give button trigger overflow confirmation";
            });
            yield return new WaitForSeconds(.6f);
            yield return Capture("partners-overflow-confirm.png");
            ok &= Check("partner-overflow-cancel", () =>
            {
                var dialog = GameObject.Find("OfficialPartnerGiftConfirmDialog").GetComponent<WorldConfirmDialog>();
                Assert(dialog.TitleText.Text == "确认", "original confirmation title was not localized");
                Assert(dialog.HyperLinkClickListener is HyperLinkClickListener,
                    "original confirmation hyperlink component reference was not restored");
                Assert(!string.IsNullOrEmpty(dialog.ContentText.mTxt.Text)
                    && dialog.ContentText.mTxt.myKey == "ui_npc_detail_give_gift_overflow_tip",
                    "original gift overflow content was not bound");
                ClickPartnerUi(dialog.CancelBtn);
                return "original cancel button clicked";
            });
            yield return new WaitForSeconds(.4f);
            ok &= Check("partner-overflow-cancel-save", () =>
            {
                runtime.Profile.Reload();
                Assert(GameObject.Find("OfficialPartnerGiftConfirmDialog") == null, "cancelled confirmation remains visible");
                Assert(runtime.ItemCount(giftId) == inventory, "cancel consumed a gift");
                Assert(runtime.Profile.Save.Player.CharacterProperties.NpcFriendships
                    .Single(value => value.NpcClassId == 8402).FriendshipExp == friendship,
                    "cancel changed friendship");
                Assert(PrivateField<Dictionary<int, long>>(gift, "mSelectedGiftDict")[giftId] == 1,
                    "cancel cleared the pending original gift selection");
                ClickPartnerUi(PrivateField<Button>(gift, "mGiveGiftBtn"));
                return "cancel preserves gift, friendship and pending selection across disk reload";
            });
            yield return new WaitForSeconds(.6f);
            ok &= Check("partner-overflow-confirm", () =>
            {
                var dialog = GameObject.Find("OfficialPartnerGiftConfirmDialog").GetComponent<WorldConfirmDialog>();
                ClickPartnerUi(dialog.ConfirmBtn);
                return "original confirm invokes the existing local gift transaction";
            });
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((runtime.ItemCount(giftId) == inventory || GameObject.Find("OfficialPartnerGiftConfirmDialog") != null)
                && Time.realtimeSinceStartup < deadline) yield return null;
            ok &= Check("partner-overflow-confirm-save", () =>
            {
                runtime.Profile.Reload();
                var config = OfficialPartnerGiftAssetCatalog.LoadDefault();
                var npc = runtime.Profile.Save.Player.CharacterProperties.NpcFriendships.Single(value => value.NpcClassId == 8402);
                Assert(runtime.ItemCount(giftId) == inventory - 1, "confirmed overflow gift was not consumed once");
                long expectedFriendship = friendship + config.GetAllExpByDict(new Dictionary<int, long> { [giftId] = 1 },
                    config.GetNpcInfo(8402).GiftPreferenceDict);
                Assert(npc.FriendshipExp == expectedFriendship
                    && npc.Level == config.GetMaxLevel(8402), "confirmed gift did not preserve overflow experience at maximum level");
                Assert(GameObject.Find("OfficialPartnerGiftConfirmDialog") == null, "confirmed window did not close");
                Assert(gift.gameObject.activeInHierarchy, "confirmation closed the underlying gift page");
                return "one gift consumed, maximum level and confirmed overflow experience restored from disk";
            });
            yield return Capture("partners-overflow-saved.png");
            runtime.Hud.TryHandleBack();
            completed(ok);
        }

        private IEnumerator VerifyPartnerTitles(HomeFeatureRuntime runtime, Action<bool> completed)
        {
            bool ok = true;
            NpcDetailPanelGiftContent gift = null;
            ok &= Check("partner-title-entry", () =>
            {
                runtime.OpenPartnerGift(8402);
                gift = PrivateField<GameObject>(runtime.Hud, "activeDialog")
                    .GetComponentInChildren<NpcDetailPanelGiftContent>();
                Assert(gift != null, "official gift page is missing");
                var button = PrivateField<Button>(gift, "mCustomFriendshipTagBtn");
                Assert(button.gameObject.activeInHierarchy, "original friendship title eligibility is not satisfied");
                return "original custom-title eligibility is satisfied";
            });
            yield return null;
            yield return null;
            ok &= Check("partner-title-open", () =>
            {
                ClickPartnerUi(PrivateField<Button>(gift, "mCustomFriendshipTagBtn"));
                return "original custom-title button opened the official InputDialog";
            });
            yield return new WaitForSeconds(.4f);
            yield return Capture("partners-title-input.png");
            WorldInputDialog input = null;
            ok &= Check("partner-title-cancel", () =>
            {
                input = GameObject.Find("OfficialPartnerInputDialog").GetComponent<WorldInputDialog>();
                Assert(input.TitleText.Text == "修改标签", "original custom-title header is not localized: " + input.TitleText.Text);
                Assert(!input.ResetBtn.gameObject.activeSelf, "unused original reset button was not hidden");
                ClickPartnerUi(input.CancelBtn);
                return "original cancel button closes only the input window";
            });
            yield return new WaitForSeconds(.4f);
            ok &= Check("partner-title-empty", () =>
            {
                Assert(GameObject.Find("OfficialPartnerInputDialog") == null, "cancelled input window remains visible");
                Assert(gift.gameObject.activeInHierarchy, "cancel closed the underlying gift page");
                ClickPartnerUi(PrivateField<Button>(gift, "mCustomFriendshipTagBtn"));
                input = GameObject.Find("OfficialPartnerInputDialog").GetComponent<WorldInputDialog>();
                return "reopened original input window";
            });
            yield return new WaitForSeconds(.4f);
            ok &= Check("partner-title-empty-submit", () =>
            {
                ClickPartnerUi(input.ConfirmBtn);
                Assert(input != null && input.gameObject.activeInHierarchy, "empty invalid title closed the dialog");
                Assert(string.IsNullOrEmpty(runtime.Profile.Save.Player.CharacterProperties.NpcFriendships
                    .Single(value => value.NpcClassId == 8402).CustomFriendshipTag), "invalid empty title changed the save");
                ClickPartnerUi(input.InputField);
                return "empty title rejected; pointer focused the official TMP input";
            });
            yield return null;
            ok &= Check("partner-title-keyboard", () =>
            {
                Assert(input.InputField.isFocused, "official input field did not receive pointer focus");
                foreach (char value in "挚友") input.InputField.ProcessEvent(new Event
                    { type = EventType.KeyDown, character = value, keyCode = KeyCode.None });
                // TMP.ProcessEvent exposes KeyPressed alone. OnUpdateSelected
                // follows that call with UpdateLabel; use its public equivalent
                // while simulating keys without focusing an operating-system window.
                input.InputField.ForceLabelUpdate();
                return "Chinese keys processed with the normal TMP label refresh";
            });
            yield return null;
            yield return null;
            ok &= Check("partner-title-keyboard-display", () =>
            {
                Assert(input.InputField.text == "挚友", "simulated keyboard input was not consumed: " + input.InputField.text);
                Assert(input.InputField.textComponent.text.Contains("挚友"),
                    "official input label does not display the entered title: " + input.InputField.textComponent.text);
                return "Chinese text consumed and displayed through TMP selected-input events";
            });
            yield return Capture("partners-title-entered.png");
            ok &= Check("partner-title-submit", () => { ClickPartnerUi(input.ConfirmBtn); return "official confirm clicked"; });
            float deadline = Time.realtimeSinceStartup + 5f;
            while (GameObject.Find("OfficialPartnerInputDialog") != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            ok &= Check("partner-title-save", () =>
            {
                runtime.Profile.Reload();
                Assert(runtime.Profile.Save.Player.CharacterProperties.NpcFriendships
                    .Single(value => value.NpcClassId == 8402).CustomFriendshipTag == "挚友", "custom title was not restored from disk");
                Assert(GameObject.Find("OfficialPartnerInputDialog") == null, "successful input did not close");
                Assert(PrivateField<WorldMultiLanguageText>(gift, "mFriendshipTagText").Text == "挚友",
                    "official gift page did not refresh the custom title");
                return "confirmed local title saved, refreshed and restored from disk";
            });
            yield return Capture("partners-title-saved.png");
            ok &= Check("partner-title-mask-open", () =>
            {
                ClickPartnerUi(PrivateField<Button>(gift, "mCustomFriendshipTagBtn"));
                return "reopened for original modal mask pointer validation";
            });
            yield return new WaitForSeconds(.6f);
            ok &= Check("partner-title-mask-click", () =>
            {
                var mask = GameObject.Find("OfficialPartnerInputMask").GetComponent<WorldWindowMask>();
                Assert(mask.AttachWindow != null && mask.Canvas.sortingOrder == mask.AttachWindow.Canvas.sortingOrder - 1,
                    "original modal mask is not attached below the input window");
                ClickPartnerUi(mask.Button, new Vector2(10, 10));
                return "outside pointer raycast reaches the official mask";
            });
            yield return new WaitForSeconds(.4f);
            ok &= Check("partner-title-mask-close", () =>
            {
                Assert(GameObject.Find("OfficialPartnerInputDialog") == null,
                    "outside mask click did not close the input window");
                Assert(gift.gameObject.activeInHierarchy, "mask click closed the underlying gift page");
                return "mask closed only the original input window";
            });
            runtime.Hud.TryHandleBack();
            completed(ok);
        }

        private static void ClickPartnerUi(Component target, Vector2? screenPosition = null)
        {
            Assert(target != null && target.gameObject.activeInHierarchy, "partner pointer target is inactive");
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)target.transform;
            var canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = screenPosition ?? RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert(hits.Count > 0 && (hits[0].gameObject == target.gameObject
                || hits[0].gameObject.transform.IsChildOf(target.transform)),
                "partner pointer blocked at " + target.name + " at " + pointer.position
                    + " (rect=" + rect.rect + ", world=" + rect.TransformPoint(rect.rect.center)
                    + ", hits=" + string.Join(",", hits.Take(4).Select(value => PathOf(value.gameObject.transform)
                        + ":sort=" + value.sortingOrder)) + ")");
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private IEnumerator VerifyRealShopInput(HomeFeatureRuntime runtime, Action<bool> completed)
        {
            MethodInfo openShop = typeof(HomeFeatureRuntime).GetMethod("OpenHomeShop",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (openShop == null)
            {
                log.Add("FAIL real-input: the HomeShop route is missing");
                completed(false);
                yield break;
            }
            var accountAgeResult = runtime.Profile.ApplyInventoryTransaction("acceptance:treasury-account-age",
                save => save.CreatedAtUnixSeconds = DateTimeOffset.UtcNow.AddDays(-4).ToUnixTimeSeconds());
            if (accountAgeResult.Status != Zjcs.Offline.Progression.ProgressionResultStatus.Applied)
            {
                log.Add("FAIL real-input: mature account fixture was rejected: " + accountAgeResult.Status);
                completed(false);
                yield break;
            }
            openShop.Invoke(runtime, null);
            HomeShopScreenController shop = UnityEngine.Object.FindObjectOfType<HomeShopScreenController>();
            if (shop == null || !shop.IsOpen)
            {
                log.Add("FAIL real-input: the grocery page did not open");
                completed(false);
                yield break;
            }
            yield return null;
            yield return null;

            HomeUiRenderer renderer = PrivateField<HomeUiRenderer>(shop, "panelRenderer");
            const string treasuryTabPath = "/HomeShopPanel/BgPanel1/Content/BottomGo/TabBottom/TabGroup/HomeShopPanelTabBottomBtn (1)";
            Toggle treasuryTab = renderer == null ? null : renderer.Find<Toggle>(treasuryTabPath);
            if (treasuryTab == null || !treasuryTab.interactable)
            {
                log.Add("FAIL real-input: the official treasury tab is unavailable");
                completed(false);
                yield break;
            }

            Vector2 target = RectTransformUtility.WorldToScreenPoint(null, treasuryTab.transform.position);
            Vector2 topLeftTarget = new Vector2(target.x, Screen.height - target.y);
            string marker = Path.Combine(Output, "real-input-waiting.txt");
            File.WriteAllText(marker, "Click the official treasury tab at Game View pixel "
                + topLeftTarget + " (top-left origin).");
            Debug.Log("[HomeAcceptance] waiting for real mouse click on treasury tab at Game View pixel " + topLeftTarget);
            float deadline = Time.realtimeSinceStartup + 180f;
            while (shop.CurrentPageIndex != 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (shop.CurrentPageIndex != 1)
            {
                log.Add("FAIL real-input: mouse click did not open the treasury page");
                completed(false);
                yield break;
            }

            yield return null;
            yield return new WaitForEndOfFrame();
            string screenshot = Path.Combine(Output, "home-shop-real-input-treasury.png");
            ScreenCapture.CaptureScreenshot(screenshot);
            for (int attempt = 0; attempt < 120 && !File.Exists(screenshot); attempt++)
                yield return null;
            if (!File.Exists(screenshot))
            {
                log.Add("FAIL real-input: treasury page screenshot was not written");
                completed(false);
                yield break;
            }
            File.WriteAllText(marker, "Press PageDown, then Escape, in the focused Game View.");
            Debug.Log("[HomeAcceptance] waiting for real PageDown input");
            deadline = Time.realtimeSinceStartup + 180f;
            while (shop.CurrentPageIndex != 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (shop.CurrentPageIndex != 2)
            {
                log.Add("FAIL real-input: PageDown did not open the gifts page");
                shop.CloseScreen();
                completed(false);
                yield break;
            }
            File.WriteAllText(marker, "Press Escape in the focused Game View.");
            Debug.Log("[HomeAcceptance] waiting for real Escape input");
            deadline = Time.realtimeSinceStartup + 180f;
            while (shop.IsOpen && Time.realtimeSinceStartup < deadline)
                yield return null;
            bool escaped = !shop.IsOpen;
            log.Add(escaped
                ? "PASS real-input: mouse opened the treasury tab, PageDown opened gifts, and Escape closed the shop"
                : "FAIL real-input: Escape did not close the shop");
            if (!escaped) shop.CloseScreen();
            completed(escaped);
        }

        private IEnumerator VerifyDungeonsAndArena(HomeFeatureRuntime runtime, Action<bool> completed)
        {
            var regions = runtime.DailyDungeons.Catalog.dungeons
                .Where(value => value != null && value.entryMapClassId > 0 && value.entryRegionId >= 0)
                .Select(value => new KeyValuePair<int, int>(value.entryMapClassId, value.entryRegionId))
                .Concat(runtime.Arena.Catalog.function_limits
                    .Where(value => value != null && value.Type == 58 && value.Condition != null)
                    .Select(value => new KeyValuePair<int, int>(value.Condition.MapClassId,
                        value.Condition.Region)))
                .GroupBy(value => value.Key)
                .SelectMany(group => group.Select(value => value.Value).Distinct()
                    .Select(region => new KeyValuePair<int, int>(group.Key, region)))
                .ToArray();
            bool ok = Check("dungeons-region-fixture", () =>
            {
                foreach (IGrouping<int, KeyValuePair<int, int>> map in regions.GroupBy(value => value.Key))
                {
                    ProgressionResult initialized = runtime.Profile.InitializeMap(map.Key,
                        map.Select(value => value.Value).ToArray());
                    Assert(initialized.Status == ProgressionResultStatus.Applied
                        || initialized.Status == ProgressionResultStatus.Unchanged,
                        "could not initialize map " + map.Key + ": " + initialized.Message);
                }
                foreach (KeyValuePair<int, int> region in regions)
                {
                    ProgressionResult result = runtime.Profile.SetMapRegionProgress(region.Key, region.Value, 10000);
                    Assert(result.Status == ProgressionResultStatus.Applied
                        || result.Status == ProgressionResultStatus.Unchanged,
                        "could not publish region " + region.Key + ":" + region.Value);
                }
                Assert(runtime.DailyDungeons.PublicDungeons((mapId, regionId) =>
                    runtime.MapRegionProgress(mapId, regionId) >= 10000).Any(),
                    "no official daily dungeon became public");
                Assert(runtime.Arena.IsFunctionLimitMet(58, 0, runtime.MapRegionProgress),
                    "official arena entry limit stayed closed");
                return regions.Length + " official map regions published in the isolated profile";
            });
            if (!ok) { completed(false); yield break; }

            ok = Check("dungeons-travel-notes", () =>
            {
                InvokePrivate(runtime.Hud, "OpenTravelNotes");
                GameObject panel = GetPrivateGameObject(runtime.Hud, "travelNotesRoot");
                AssertModalOnTop(panel, "travel notes");
                DumpModalState("travel-notes", panel);
                return "official travel notes panel opened";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            yield return Capture("dungeons-travel-notes.png");

            GameObject travelNotes = GetPrivateGameObject(runtime.Hud, "travelNotesRoot");
            Transform dailyDungeonEntry = travelNotes.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name == "DailyDungeonEntry");
            Button dungeonEntry = dailyDungeonEntry == null ? null
                : dailyDungeonEntry.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value.name == "EntryBtn");
            ok = Check("dungeons-entry-input", () =>
            {
                ClickUi(dungeonEntry);
                GameObject panel = GetPrivateGameObject(runtime.Hud, "dailyDungeonRoot");
                AssertModalOnTop(panel, "dungeon list");
                DumpModalState("dungeon-list", panel);
                return "official travel entry pointer event opened the daily dungeon list";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            yield return Capture("dungeons-entry.png");

            GameObject giftEntryPanel = GetPrivateGameObject(runtime.Hud, "dailyDungeonRoot");
            Button giftTab = giftEntryPanel.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == "AchievementTabGroupItem_11");
            ok = Check("dungeons-gift-page-input", () =>
            {
                ClickUi(giftTab);
                Transform giftPanel = giftEntryPanel.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(value => value.name == "AchievementPanel");
                Assert(giftPanel != null && giftPanel.gameObject.activeInHierarchy,
                    "official cumulative dungeon gift panel did not open");
                Transform rewardGrid = giftPanel.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(value => value.name == "Grid");
                Assert(rewardGrid != null, "official cumulative dungeon gift rows are missing");
                var giftModel = runtime.DailyDungeons.BuildGiftModel(5);
                var reward = giftModel.Achievements.Select((milestone, index) => new { milestone, index })
                    .SelectMany(value => value.milestone.Rewards.Select(item => new
                        { RowIndex = value.index, Item = item }))
                    .FirstOrDefault(value => value.Item != null && value.Item.ItemId > 0
                        && value.Item.Count > 0);
                Assert(reward != null, "official cumulative dungeon gift catalog has no reward icon fixture");
                Transform[] rows = rewardGrid.GetComponentsInChildren<Transform>(true)
                    .Where(value => (value.name == "AchievementPanelItem"
                        || value.name.StartsWith("AchievementPanelItem (", StringComparison.Ordinal))
                        && value.gameObject.activeInHierarchy)
                    .OrderBy(value => value.GetSiblingIndex()).ToArray();
                Assert(reward.RowIndex < rows.Length,
                    "official cumulative dungeon gift reward row did not render");
                Transform row = rows[reward.RowIndex];
                Transform rewardIcon = row.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(value => value.name == "OfflineReward_" + reward.Item.ItemId);
                DailyDungeonResultItemIconAdapter adapter = rewardIcon == null ? null
                    : rewardIcon.GetComponent<DailyDungeonResultItemIconAdapter>();
                Image icon = rewardIcon == null ? null
                    : rewardIcon.Find("Content/mask/icon")?.GetComponent<Image>();
                Assert(adapter != null && icon != null
                    && icon.sprite == DailyDungeonRewardAssetCatalog.LoadDefault().Find(reward.Item.ItemId)?.sprite,
                    "official item-icon prefab did not bind the configured dungeon gift sprite");
                return "official cumulative gift page rendered its configured reward through the native item icon prefab";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            yield return Capture("dungeons-gifts.png");

            Button entriesTab = giftEntryPanel.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == "DailyDungeonEntryPanelTabGroupItem_11");
            ok = Check("dungeons-gift-return-input", () =>
            {
                ClickUi(entriesTab);
                Transform selectContent = giftEntryPanel.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(value => value.name == "SelectContent");
                Assert(selectContent != null && selectContent.gameObject.activeInHierarchy,
                    "official dungeon directory tab did not return from cumulative gifts");
                return "official dungeon directory tab returned from the cumulative gift page";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;

            GameObject entryPanel = GetPrivateGameObject(runtime.Hud, "dailyDungeonRoot");
            Button dungeonRow = entryPanel.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name.StartsWith("Dungeon_", StringComparison.Ordinal));
            ok = Check("dungeons-detail-input", () =>
            {
                ClickUi(dungeonRow);
                GameObject panel = GetPrivateGameObject(runtime.Hud, "dailyDungeonDialogRoot");
                AssertModalOnTop(panel, "dungeon detail");
                DumpModalState("dungeon-detail", panel);
                return dungeonRow.name + " opened its official detail panel";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            yield return Capture("dungeons-detail.png");

            ok = Check("dungeons-detail-rewards", () =>
            {
                const string prefix = "Dungeon_";
                Assert(dungeonRow.name.StartsWith(prefix, StringComparison.Ordinal),
                    "selected official dungeon row has no id");
                Assert(int.TryParse(dungeonRow.name.Substring(prefix.Length), out int dungeonId),
                    "selected official dungeon row id is invalid");
                int difficulty = runtime.DailyDungeons.DefaultDifficulty(dungeonId);
                DailyDungeonRewardAssetCatalog metadata = DailyDungeonRewardAssetCatalog.LoadDefault();
                IReadOnlyList<DailyDungeonRewardPreviewItem> rewards =
                    runtime.PreviewDailyDungeonDetailRewards(dungeonId, difficulty, metadata);
                GameObject panel = GetPrivateGameObject(runtime.Hud, "dailyDungeonDialogRoot");
                Transform awardGrid = panel.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(value => value.name == "AwardGrid");
                Transform grid = awardGrid == null ? null : awardGrid.Find("ViewPort/Grid");
                Assert(grid != null, "official difficulty reward grid is missing");
                var rows = new List<Transform>();
                const string iconPrefix = "ItemIcon_";
                foreach (Transform child in grid)
                    if (child.name.StartsWith(iconPrefix, StringComparison.Ordinal)) rows.Add(child);
                Assert(rows.Count == rewards.Count,
                    "official difficulty reward icon count differs from the shared module preview ("
                    + rows.Count + " vs " + rewards.Count + ")");
                for (int index = 0; index < rows.Count; index++)
                {
                    Transform row = rows[index];
                    DailyDungeonResultItemIconAdapter adapter =
                        row.GetComponentInChildren<DailyDungeonResultItemIconAdapter>(true);
                    Assert(adapter != null, "official difficulty item icon adapter is missing");
                    Assert(int.TryParse(row.name.Substring(iconPrefix.Length), out int itemId),
                        "official difficulty item icon id is invalid");
                    Assert(itemId == rewards[index].ItemId,
                        "official difficulty reward grid order differs from the shared module preview");
                    DailyDungeonRewardAssetCatalog.Item item = metadata.Find(itemId);
                    Image icon = row.Find("Content/mask/icon")?.GetComponent<Image>();
                    Assert(item != null && icon != null && icon.sprite == item.sprite,
                        "official difficulty reward item did not bind its selected official sprite");
                    Transform probability = row.Find("Content/ItemIconAttachment/ProbabilityGo");
                    Assert((probability != null) == !item.isFiltrationItem,
                        "official probability attachment differs from the item filtration rule for item "
                        + itemId + " (present=" + (probability != null) + ", filtration="
                        + item.isFiltrationItem + ")");
                }
                return "official reward grid rendered " + rows.Count
                    + " item icons and the configured ProbabilityGo attachments";
            });
            if (!ok) { completed(false); yield break; }

            GameObject detailLeaderboard = GetPrivateGameObject(runtime.Hud, "dailyDungeonDialogRoot");
            Button leaderboardButton = detailLeaderboard.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == "DailyDungeonTopGrade");
            ok = Check("dungeons-leaderboard-input", () =>
            {
                Assert(leaderboardButton != null && leaderboardButton.interactable,
                    "official dungeon leaderboard button is unavailable");
                ClickPartnerUi(leaderboardButton);
                GameObject leaderboard = GetPrivateGameObject(runtime.Hud, "dailyDungeonLeaderboardRoot");
                AssertModalOnTop(leaderboard, "dungeon leaderboard");
                Transform content = leaderboard.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(value => value.name == "TeamTopContent");
                Assert(content != null, "official dungeon leaderboard content is missing");
                LoopVerticalScrollRect records = content.GetComponentInChildren<LoopVerticalScrollRect>(true);
                Assert(records != null && records.content != null,
                    "official dungeon leaderboard record scroll binding is missing");
                Assert(leaderboard.GetComponentsInChildren<Image>(true)
                    .Any(value => value.isActiveAndEnabled && value.sprite != null),
                    "official dungeon leaderboard rendered no bound artwork");
                DumpModalState("dungeon-leaderboard", leaderboard);
                return "official dungeon leaderboard panel opened with grade tabs and local record content";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            yield return Capture("dungeons-leaderboard.png");
            ok = Check("dungeons-leaderboard-grade-input", () =>
            {
                GameObject leaderboard = GetPrivateGameObject(runtime.Hud, "dailyDungeonLeaderboardRoot");
                WorldTabItem[] tabs = leaderboard.GetComponentsInChildren<WorldTabItem>(true)
                    .Where(value => value.isActiveAndEnabled && value.name.StartsWith(
                        "DailyDungeonTopPanelTabBottomBtn", StringComparison.Ordinal)).ToArray();
                Assert(tabs.Length > 0, "official leaderboard grade tab bindings are missing");
                WorldTabItem next = tabs.FirstOrDefault(value => !value.isOn) ?? tabs[0];
                ClickPartnerUi(next);
                Assert(next.isOn && tabs.Count(value => value.isOn) == 1,
                    "official leaderboard grade tabs did not switch selection");
                return "pointer raycast selected the original grade tab";
            });
            if (!ok) { completed(false); yield break; }
            ok = Check("dungeons-leaderboard-gifts-input", () =>
            {
                GameObject leaderboard = GetPrivateGameObject(runtime.Hud, "dailyDungeonLeaderboardRoot");
                WorldTabItem giftsTab = leaderboard.GetComponentsInChildren<WorldTabItem>(true)
                    .Single(value => value.name == "DailyDungeonTopPanelAchievementTabBottomBtn");
                ClickPartnerUi(giftsTab);
                Assert(giftsTab.isOn && leaderboard.GetComponentsInChildren<Transform>(true)
                    .Any(value => value.name == "AchievementPanel" && value.gameObject.activeInHierarchy),
                    "original leaderboard gift tab did not open its panel");
                return "pointer raycast opened the original leaderboard gift panel";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            yield return Capture("dungeons-leaderboard-gifts.png");
            ok = Check("dungeons-leaderboard-close-input", () =>
            {
                GameObject leaderboard = GetPrivateGameObject(runtime.Hud, "dailyDungeonLeaderboardRoot");
                Button close = leaderboard.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value.name == "CloseBtn_dark");
                bool closeClicked = false;
                close?.onClick.AddListener(() => closeClicked = true);
                ClickPartnerUi(close);
                Assert(closeClicked, "official leaderboard close button did not receive its pointer click");
                Assert(GetPrivateGameObject(runtime.Hud, "dailyDungeonLeaderboardRoot") == null,
                    "official leaderboard close button did not return to dungeon details");
                return "official leaderboard close button returned to dungeon details";
            });
            if (!ok) { completed(false); yield break; }

            GameObject detail = GetPrivateGameObject(runtime.Hud, "dailyDungeonDialogRoot");
            Button enter = detail.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == "BattleBtn" || value.name == "GoToBtn");
            ok = Check("dungeons-enter", () =>
            {
                Assert(enter != null && enter.interactable, "official dungeon enter button is unavailable");
                ClickUi(enter);
                Assert(runtime.DailyDungeons.ActiveRun != null, "dungeon enter button did not save a route");
                Assert(GetPrivateGameObject(runtime.Hud, "dailyDungeonDialogRoot") != null,
                    "official daily dungeon route did not render");
                AssertModalOnTop(GetPrivateGameObject(runtime.Hud, "dailyDungeonDialogRoot"), "dungeon route");
                DumpModalState("dungeon-route", GetPrivateGameObject(runtime.Hud, "dailyDungeonDialogRoot"));
                return "route " + runtime.DailyDungeons.ActiveRun.id + " entered with no cleared rooms";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            yield return Capture("dungeons-route.png");

            int usedEntries = runtime.DailyDungeons.Save.usedEntries;
            int runDungeonId = runtime.DailyDungeons.ActiveRun.dungeonId;
            int runDifficulty = runtime.DailyDungeons.ActiveRun.difficulty;
            int routeStartRoomId = runtime.DailyDungeons.ActiveRun.currentRoomId;
            GameObject route = GetPrivateGameObject(runtime.Hud, "dailyDungeonDialogRoot");
            Button routeClose = route.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == "CloseBtn");
            ok = Check("dungeons-save-route-input", () =>
            {
                Assert(routeClose != null && routeClose.isActiveAndEnabled,
                    "official route close button is unavailable before room selection");
                ClickUi(routeClose);
                GameObject confirmation = GetPrivateGameObject(runtime.Hud, "dailyDungeonLeaveConfirmRoot");
                AssertModalOnTop(confirmation, "route save confirmation");
                DumpModalState("route-save-confirmation", confirmation);
                Button saveReturn = confirmation.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value.name == "CommonBtn_11");
                ClickUi(saveReturn);
                Assert(runtime.DailyDungeons.ActiveRun != null
                    && runtime.DailyDungeons.ActiveRun.currentRoomId == routeStartRoomId
                    && runtime.DailyDungeons.Save.usedEntries == usedEntries,
                    "saving and returning discarded the route or charged an entry");
                DailyDungeonModule reload = DailyDungeonModule.Open(
                    new ProfileSlotCatalog(Root), runtime.ActiveProfileSlotId);
                Assert(reload.ActiveRun != null && reload.ActiveRun.currentRoomId == routeStartRoomId
                    && reload.Save.usedEntries == usedEntries,
                    "saved route did not survive reload or changed the entry count");
                return "route save-and-return persisted room " + routeStartRoomId
                    + " without changing entry count " + usedEntries;
            });
            if (!ok) { completed(false); yield break; }
            yield return null;

            GameObject resumeEntryPanel = GetPrivateGameObject(runtime.Hud, "dailyDungeonRoot");
            Button resumeDungeon = resumeEntryPanel.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == "Dungeon_" + runtime.DailyDungeons.ActiveRun.dungeonId);
            ok = Check("dungeons-resume-route-input", () =>
            {
                ClickUi(resumeDungeon);
                GameObject detail = GetPrivateGameObject(runtime.Hud, "dailyDungeonDialogRoot");
                AssertModalOnTop(detail, "saved dungeon detail");
                Button resumeRoute = detail.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value.name == "BattleBtn" || value.name == "GoToBtn");
                ClickUi(resumeRoute);
                GameObject panel = GetPrivateGameObject(runtime.Hud, "dailyDungeonDialogRoot");
                AssertModalOnTop(panel, "resumed dungeon route");
                DumpModalState("resumed-dungeon-route", panel);
                Assert(runtime.DailyDungeons.ActiveRun != null
                    && runtime.DailyDungeons.ActiveRun.dungeonId == runDungeonId
                    && runtime.DailyDungeons.ActiveRun.difficulty == runDifficulty
                    && runtime.DailyDungeons.ActiveRun.currentRoomId == routeStartRoomId,
                    "resuming entered a different route");
                return "official dungeon selection and detail actions resumed the saved route";
            });
            if (!ok) { completed(false); yield break; }

            Assert(runtime.DailyDungeons.RecordRoomCleared(routeStartRoomId, string.Empty),
                "could not record the cleared route room");
            InvokePrivate(runtime.Hud, "RenderDailyDungeonRoute");
            yield return null;
            yield return null;
            yield return Capture("dungeons-route-cleared.png");

            int[] rewardPath = FindRewardRoomPath(runtime.DailyDungeons,
                runtime.DailyDungeons.ActiveRun);
            Assert(rewardPath.Length >= 3,
                "official route has no reachable configured reward room with a selectable successor");
            for (int index = 1; index < rewardPath.Length - 1; index++)
            {
                Assert(runtime.DailyDungeons.ChooseNextRoom(rewardPath[index]),
                    "could not advance through official route to room " + rewardPath[index]);
                Assert(runtime.DailyDungeons.RecordRoomCleared(rewardPath[index], string.Empty),
                    "could not record cleared reward route room " + rewardPath[index]);
            }
            int rewardRoomId = rewardPath[rewardPath.Length - 2];
            Assert(runtime.DailyDungeons.ActiveRun.clearedRoomIds.Contains(rewardRoomId),
                "the configured reward room was not cleared before leaving");
            Assert(runtime.DailyDungeons.ChooseNextRoom(rewardPath[rewardPath.Length - 1]),
                "could not advance to the un-cleared successor room");
            InvokePrivate(runtime.Hud, "RenderDailyDungeonRoute");
            int[] expectedRoomRewards = runtime.DailyDungeons.ConfiguredRewardRuleIds(
                runtime.DailyDungeons.ActiveRun, false);
            route = GetPrivateGameObject(runtime.Hud, "dailyDungeonDialogRoot");
            routeClose = route.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == "CloseBtn");
            ok = Check("dungeons-leave-settle-input", () =>
            {
                ClickUi(routeClose);
                GameObject confirmation = GetPrivateGameObject(runtime.Hud, "dailyDungeonLeaveConfirmRoot");
                Assert(confirmation != null, "route close did not open leave confirmation");
                AssertModalOnTop(confirmation, "route leave confirmation");
                DumpModalState("route-leave-confirmation", confirmation);
                Button confirm = confirmation.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value.name == "CommonBtn_12");
                ClickUi(confirm);
                Assert(runtime.DailyDungeons.ActiveRun == null
                    && runtime.DailyDungeons.Save.usedEntries == usedEntries,
                    "leaving changed entry count or retained the route");
                Assert(expectedRoomRewards.Length > 0,
                    "official cleared room has no configured reward rules");
                DailyDungeonModule reload = DailyDungeonModule.Open(
                    new ProfileSlotCatalog(Root), runtime.ActiveProfileSlotId);
                Assert(reload.ActiveRun == null && reload.Save.usedEntries == usedEntries,
                    "daily dungeon leave state did not survive reload: difficulty=" + runDifficulty
                    + ", expected entries=" + usedEntries + ", reloaded entries=" + reload.Save.usedEntries
                    + ", active=" + (reload.ActiveRun == null ? "none" : reload.ActiveRun.id)
                    + ", savePath=" + Path.Combine(new ProfileSlotCatalog(Root)
                        .GetSlotDirectory(runtime.ActiveProfileSlotId), "daily-dungeon-save.json")
                    + ", saved=" + File.ReadAllText(Path.Combine(new ProfileSlotCatalog(Root)
                        .GetSlotDirectory(runtime.ActiveProfileSlotId), "daily-dungeon-save.json")));
                return expectedRoomRewards.Length + " configured room reward rules granted from route "
                    + string.Join("->", rewardPath) + "; entry count "
                    + usedEntries + " persisted";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;

            entryPanel = GetPrivateGameObject(runtime.Hud, "dailyDungeonRoot");
            dungeonRow = entryPanel.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name.StartsWith("Dungeon_", StringComparison.Ordinal));
            ok = Check("dungeons-settlement-entry", () =>
            {
                ClickUi(dungeonRow);
                GameObject panel = GetPrivateGameObject(runtime.Hud, "dailyDungeonDialogRoot");
                AssertModalOnTop(panel, "settlement dungeon detail");
                Button battle = panel.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value.name == "BattleBtn" || value.name == "GoToBtn");
                ClickUi(battle);
                Assert(runtime.DailyDungeons.ActiveRun != null,
                    "second official dungeon entry did not start a route");
                return "official detail button started the settlement route";
            });
            if (!ok) { completed(false); yield break; }

            int[] terminalPath = FindTerminalRoomPath(runtime.DailyDungeons,
                runtime.DailyDungeons.ActiveRun);
            Assert(terminalPath.Length > 1,
                "official route has no reachable terminal room for settlement");
            Assert(runtime.DailyDungeons.RecordRoomCleared(terminalPath[0], string.Empty),
                "could not clear the settlement route start room");
            for (int index = 1; index < terminalPath.Length; index++)
            {
                Assert(runtime.DailyDungeons.ChooseNextRoom(terminalPath[index]),
                    "could not advance the settlement route to room " + terminalPath[index]);
                Assert(runtime.DailyDungeons.RecordRoomCleared(terminalPath[index], string.Empty),
                    "could not clear the settlement route room " + terminalPath[index]);
            }
            int settlementGrade = runtime.DailyDungeons.CurrentGrade();
            Assert(settlementGrade > 0 && runtime.DailyDungeons.Finish(settlementGrade),
                "official grade rows did not finish the settlement route");
            int usedBeforeClaim = runtime.DailyDungeons.Save.usedEntries;
            int remainingBeforeClaim = runtime.DailyDungeons.RemainEntries;
            long claimedBefore = runtime.DailyDungeons.DailyDungeonGetRewardCount;
            InvokePrivate(runtime.Hud, "CloseDungeonDetail");
            InvokePrivate(runtime.Hud, "RefreshDailyDungeonEntryPage");
            InvokePrivate(runtime.Hud, "OpenPendingDailyDungeonClaim");
            GameObject resultPanel = GetPrivateGameObject(runtime.Hud, "dailyDungeonClaimConfirmRoot");
            var resultPopup = resultPanel.GetComponent<Zjcs.Official.WorldUI.WorldPopupWindow>();
            float resultAnimationDeadline = Time.realtimeSinceStartup + 4f;
            while (resultPopup != null && resultPopup.State != Zjcs.Official.WorldUI.WorldPopupWindow.EState.Show
                && Time.realtimeSinceStartup < resultAnimationDeadline) yield return null;
            DailyDungeonPendingClaimSave pending = runtime.DailyDungeons.PendingClaims
                .FirstOrDefault(value => value != null && value.run != null);
            DailyDungeonSettlementHudModel settlement = pending == null ? null
                : runtime.DailyDungeons.BuildSettlementHudModel(pending,
                    runtime.DailyDungeons.MaxAwardTimesPerClaim, runtime.Level, runtime.SubRank);
            ok = Check("dungeons-settlement-panel", () =>
            {
                AssertModalOnTop(resultPanel, "official daily dungeon result");
                DumpModalState("dungeon-result", resultPanel);
                foreach (string path in new[] { "Content", "Content/WinObj", "Content/WinObj/GoTitleBg",
                    "Content/WinObj/DifficultyTitleGo/TitleText", "Content/WinObj/Content",
                    "Content/WinObj/Content/PlayerTeam", "Content/WinObj/Content/PlayerTeam/DailyDungeonLocalPlayer",
                    "Content/WinObj/Content/PlayerTeam/DailyDungeonLocalPlayer/Content/GoLayout/PlayerName",
                    "Content/WinObj/Content/GameEndRewardContent",
                    "Content/WinObj/Content/GameEndRewardContent/Title",
                    "Content/WinObj/Content/GameEndRewardContent/ScrollRect",
                    "Content/WinObj/Content/GameEndRewardContent/ScrollRect/ViewPort",
                    "Content/WinObj/Content/GameEndRewardContent/ScrollRect/ViewPort/Grid",
                    "Content/WinObj/Content/CloseBtn",
                    "Content/WinObj/Content/DailyDungeonCostMoneyContent/CommonBtn_CostItem" })
                    DumpDungeonResultNode(resultPanel, path);
                Assert(resultPanel.GetComponent<DailyDungeonResultPanelAdapter>() != null,
                    "official result panel adapter is missing");
                Assert(resultPopup != null && resultPopup.State == Zjcs.Official.WorldUI.WorldPopupWindow.EState.Show,
                    "original result popup show animation did not finish");
                CanvasGroup contentGroup = resultPanel.transform.Find("Content/WinObj/Content").GetComponent<CanvasGroup>();
                Image background = resultPanel.transform.Find("Content/WinObj/Bg").GetComponent<Image>();
                Assert(contentGroup.alpha > 0.99f && Math.Abs(background.color.a - 76f / 255f) < 0.0001f,
                    "original result animation left the content or background invisible");
                Assert(resultPanel.GetComponentsInChildren<Image>(true).Where(value => value.isActiveAndEnabled)
                    .All(value => value.material == null || value.material.shader.isSupported),
                    "result contains an unsupported Image shader");
                int[] gradeRuleIds = runtime.DailyDungeons.FindMap(settlement.Run.mapGroupId)
                    .gradeRewards.First(value => value.difficulty == settlement.Run.difficulty
                        && value.grade == settlement.Run.grade).rewardRuleIds;
                Assert(settlement.CanClaim && settlement.RewardRules.Count > 0
                    && settlement.RewardRules.Any(value => gradeRuleIds.Contains(value.RuleId))
                    && settlement.RewardRules.All(value => Math.Abs(value.DoubleProbability
                        - (gradeRuleIds.Contains(value.RuleId) ? 0.05f : 0f)) < 0.000001f),
                    "official result model did not show configured rewards and the 5% base double probability");
                return "official result panel shows grade " + settlement.Run.grade
                    + " and " + settlement.RewardRules.Count + " configured reward rules";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            yield return Capture("dungeons-result.png");

            Button claimButton = resultPanel.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == "CommonBtn_CostItem");
            int pendingBeforeClaim = runtime.DailyDungeons.PendingClaims.Count;
            ok = Check("dungeons-successful-claim", () =>
            {
                int awardTimes = settlement.AwardTimes;
                ClickPartnerUi(claimButton);
                Assert(runtime.DailyDungeons.PendingClaims.Count == pendingBeforeClaim - 1
                    && runtime.DailyDungeons.PendingClaims.All(value => value == null || value.id != pending.id)
                    && runtime.DailyDungeons.Save.usedEntries == usedBeforeClaim + awardTimes
                    && runtime.DailyDungeons.RemainEntries == remainingBeforeClaim - awardTimes
                    && runtime.DailyDungeons.DailyDungeonGetRewardCount == claimedBefore + awardTimes,
                    "successful result claim did not atomically charge and persist its award times");
                TMP_Text claimText = claimButton == null ? null : claimButton.GetComponentInChildren<TMP_Text>(true);
                Assert(claimButton != null && !claimButton.interactable && claimText != null
                    && claimText.text == "已领取",
                    "successful result claim did not update the official button state and label");
                DailyDungeonModule reload = DailyDungeonModule.Open(
                    new ProfileSlotCatalog(Root), runtime.ActiveProfileSlotId);
                Assert(reload.Save.usedEntries == usedBeforeClaim + awardTimes
                    && reload.DailyDungeonGetRewardCount == claimedBefore + awardTimes
                    && reload.PendingClaims.Count == pendingBeforeClaim - 1
                    && reload.PendingClaims.All(value => value == null || value.id != pending.id),
                    "successful result claim did not survive reload");
                return "successful pointer claim charged " + awardTimes
                    + " award time(s) after granting the result";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            yield return Capture("dungeons-result-claimed.png");

            ok = Check("dungeons-leaderboard-record", () =>
            {
                MethodInfo open = runtime.Hud.GetType().GetMethod("OpenDailyDungeonDetail",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(open != null, "dungeon detail entry is missing");
                open.Invoke(runtime.Hud, new object[] { settlement.Run.dungeonId, (int?)settlement.Run.difficulty });
                return "official detail reopened after the successful local claim";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            ok = Check("dungeons-leaderboard-record-open-input", () =>
            {
                GameObject recordDetail = GetPrivateGameObject(runtime.Hud, "dailyDungeonDialogRoot");
                Button top = recordDetail.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value.name == "DailyDungeonTopGrade");
                ClickPartnerUi(top);
                GameObject leaderboard = GetPrivateGameObject(runtime.Hud, "dailyDungeonLeaderboardRoot");
                AssertModalOnTop(leaderboard, "dungeon leaderboard after settlement");
                return "official leaderboard reopened after the successful local claim";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            yield return Capture("dungeons-leaderboard-record.png");
            ok = Check("dungeons-leaderboard-record-rows", () =>
            {
                GameObject leaderboard = GetPrivateGameObject(runtime.Hud, "dailyDungeonLeaderboardRoot");
                Transform team = leaderboard.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(value => value.name == "TeamTopContent");
                LoopVerticalScrollRect records = team == null ? null : team.GetComponentInChildren<LoopVerticalScrollRect>(true);
                DumpModalState("dungeon-leaderboard-record", leaderboard);
                Assert(records != null && records.totalCount > 0 && records.content.childCount > 0,
                    "completed local route did not populate the official leaderboard list: "
                    + (records == null ? "no scroll" : "count=" + records.totalCount + ", children="
                        + records.content.childCount + ", active=" + records.isActiveAndEnabled
                        + ", viewport=" + records.viewport.rect + ", content=" + records.content.rect)
                    + ", local records=" + runtime.DailyDungeons.Leaderboard(settlement.Run.dungeonId,
                        settlement.Run.difficulty, settlement.Run.grade).Count);
                Assert(records.content.GetComponentsInChildren<TMP_Text>(true)
                    .Any(value => value.name == "ScoreText" && !string.IsNullOrEmpty(value.text)),
                    "official leaderboard row did not bind the completion duration");
                Transform personalRow = team.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(value => value.name == "TopRankTeamItem"
                        && value.parent != null && value.parent.name == "Content"
                        && value.parent.parent != null && value.parent.parent.name == "TeamTopContent");
                TMP_Text personalRank = personalRow == null ? null : personalRow.Find("RankText")?.GetComponent<TMP_Text>();
                TMP_Text personalTime = personalRow == null ? null : personalRow.Find("ScoreText")?.GetComponent<TMP_Text>();
                Assert(personalRow != null && personalRow.gameObject.activeInHierarchy
                    && personalRank != null && personalRank.text == "1"
                    && personalTime != null && !string.IsNullOrEmpty(personalTime.text),
                    "official personal leaderboard row did not show the local rank and completion duration");
                return "completed local route populated the list and official personal row with rank and completion time";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            DailyDungeonLeaderboardGift giftFixture = runtime.DailyDungeons.BuildLeaderboardGiftModel(
                settlement.Run.dungeonId, settlement.Run.difficulty).First(row => row.RequiredCount == 4);
            while (giftFixture.Progress < giftFixture.RequiredCount)
            {
                Assert(runtime.DailyDungeons.TryBegin(settlement.Run.dungeonId, settlement.Run.difficulty,
                    _ => true, (_, __) => true, (_, __, ___) => true,
                    runtime.DailyDungeons.Catalog.settings.DailyDungeonBuyCostItemId,
                    DateTimeOffset.Now.ToUnixTimeSeconds(), out string giftError), giftError);
                int[] giftPath = FindTerminalRoomPath(runtime.DailyDungeons, runtime.DailyDungeons.ActiveRun);
                Assert(giftPath.Length > 0, "leaderboard gift fixture has no terminal path");
                foreach (int room in giftPath)
                {
                    if (runtime.DailyDungeons.ActiveRun.currentRoomId != room)
                        Assert(runtime.DailyDungeons.ChooseNextRoom(room), "gift fixture could not select next room");
                    Assert(runtime.DailyDungeons.RecordRoomCleared(room, string.Empty), "gift fixture room did not clear");
                }
                Assert(runtime.DailyDungeons.CurrentGrade() == giftFixture.Grade
                    && runtime.DailyDungeons.Finish(giftFixture.Grade), "gift fixture did not finish with the configured grade");
                giftFixture = runtime.DailyDungeons.BuildLeaderboardGiftModel(settlement.Run.dungeonId,
                    settlement.Run.difficulty).Single(row => row.Id == giftFixture.Id);
            }
            Dictionary<int, long> beforeGift = giftFixture.Rewards.ToDictionary(row => row.ItemId,
                row => runtime.Profile.InventoryCount(row.ItemId));
            ok = Check("dungeons-leaderboard-gift-completed-input", () =>
            {
                GameObject leaderboard = GetPrivateGameObject(runtime.Hud, "dailyDungeonLeaderboardRoot");
                ClickPartnerUi(leaderboard.GetComponentsInChildren<WorldTabItem>(true)
                    .Single(value => value.name == "DailyDungeonTopPanelAchievementTabBottomBtn"));
                Button receive = leaderboard.GetComponentsInChildren<Button>(true)
                    .First(value => value.name == "ReceiveBtn" && value.isActiveAndEnabled && value.interactable);
                Image chest = receive.transform.parent.parent.GetComponentsInChildren<Image>(true)
                    .Single(value => value.name == "FinishedChestImg");
                Assert(chest.isActiveAndEnabled && chest.sprite != null,
                    "the chest shared by Completed and Received was hidden by another state");
                return "completed the isolated route fixtures and selected the native gift tab";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            yield return Capture("dungeons-leaderboard-gift-ready.png");
            ok = Check("dungeons-leaderboard-gift-claim-input", () =>
            {
                GameObject leaderboard = GetPrivateGameObject(runtime.Hud, "dailyDungeonLeaderboardRoot");
                Button receive = leaderboard.GetComponentsInChildren<Button>(true)
                    .First(value => value.name == "ReceiveBtn" && value.isActiveAndEnabled && value.interactable);
                ClickPartnerUi(receive);
                DailyDungeonModule reload = DailyDungeonModule.Open(new ProfileSlotCatalog(Root), runtime.ActiveProfileSlotId);
                Assert(reload.BuildLeaderboardGiftModel(settlement.Run.dungeonId, settlement.Run.difficulty)
                    .Single(row => row.Id == giftFixture.Id).State == DailyDungeonAchievementState.Received,
                    "leaderboard gift receipt did not persist");
                foreach (DailyDungeonAchievementReward reward in giftFixture.Rewards)
                    Assert(runtime.Profile.InventoryCount(reward.ItemId) == beforeGift[reward.ItemId] + reward.Count,
                        "leaderboard gift did not grant its official item count");
                return "pointer raycast claimed the official Type 57 rewards and cold reopen preserved Received";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            yield return Capture("dungeons-leaderboard-gift-received.png");
            InvokePrivate(runtime.Hud, "CloseDailyDungeonLeaderboard");
            InvokePrivate(runtime.Hud, "CloseDungeonDetail");

            ok = Check("arena-panel", () =>
            {
                InvokePrivate(runtime.Hud, "OpenArena");
                GameObject panel = GetPrivateGameObject(runtime.Hud, "arenaRoot");
                AssertModalOnTop(panel, "arena");
                DumpModalState("arena", panel);
                return "official arena panel opened";
            });
            if (!ok) { completed(false); yield break; }
            yield return null;
            yield return null;
            yield return Capture("arena-panel.png");

            ok = Check("arena-win-persistence", () =>
            {
                Assert(runtime.Arena.Tickets == runtime.Arena.Catalog.settings.InitChallengeCount,
                    "new arena save did not initialize official challenge tickets: "
                    + runtime.Arena.Tickets + "/" + runtime.Arena.Catalog.settings.InitChallengeCount);
                int opponentRank = runtime.Arena.Opponents[0].rank;
                Assert(runtime.Arena.TryBeginChallenge(0, out string error),
                    "arena challenge did not start: " + error);
                int guardId = runtime.Arena.ActiveGuardTemplateId;
                int guardSex = runtime.Arena.ActiveGuardSex;
                ArenaRuntimeGuard guard = runtime.Arena.FindGuard(guardId);
                Assert(guard != null && guard.appendAppearances.Any(value => value.sex == guardSex),
                    "arena challenge did not persist an official guard appearance");
                Assert(runtime.Arena.CompleteChallenge(true, runtime.GrantBattleReward),
                    "arena victory settlement failed");
                Assert(runtime.Arena.Rank == opponentRank && runtime.Arena.Wins == 1,
                    "arena victory did not move the player to the opponent rank");
                OfflineArenaSession reload = OfflineArenaSession.Open(
                    new ProfileSlotCatalog(Root), runtime.ActiveProfileSlotId);
                Assert(reload.Rank == opponentRank && reload.Wins == 1 && !reload.HasActiveChallenge,
                    "arena victory state did not survive reload");
                return "rank " + opponentRank + " persisted after victory; guard " + guardId
                    + " appearance sex " + guardSex;
            });
            completed(ok);
        }

        private static void InvokePrivate(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null) throw new MissingMethodException(target.GetType().FullName, method);
            info.Invoke(target, null);
        }

        private static GameObject GetPrivateGameObject(object target, string field)
        {
            FieldInfo info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null) throw new MissingFieldException(target.GetType().FullName, field);
            return info.GetValue(target) as GameObject;
        }

        private static void AssertModalOnTop(GameObject panel, string name)
        {
            Assert(panel != null && panel.activeInHierarchy, name + " panel is missing or inactive");
            Canvas homeCanvas = GameObject.Find("Home Canvas")?.GetComponent<Canvas>();
            Canvas panelCanvas = panel.GetComponent<Canvas>();
            Assert(homeCanvas != null && panelCanvas != null && panelCanvas.overrideSorting
                && panelCanvas.sortingOrder > homeCanvas.sortingOrder,
                name + " panel canvas is not sorted above the Home UI");
        }

        private void DumpModalState(string label, GameObject panel)
        {
            Canvas.ForceUpdateCanvases();
            Canvas modalCanvas = panel == null ? null : panel.GetComponent<Canvas>();
            RectTransform panelRect = panel == null ? null : panel.transform as RectTransform;
            log.Add("PANEL " + label + " active=" + (panel != null && panel.activeInHierarchy)
                + " rect=" + (panelRect == null ? "missing" : panelRect.rect.ToString())
                + " canvas=" + (modalCanvas == null ? "missing" : modalCanvas.renderMode
                    + ":enabled=" + modalCanvas.isActiveAndEnabled + ":sorting=" + modalCanvas.sortingOrder
                    + ":pixelRect=" + modalCanvas.pixelRect + ":scale=" + modalCanvas.scaleFactor)
                + " groups=" + string.Join("|", panel.GetComponentsInChildren<CanvasGroup>(true)
                    .Take(4).Select(value => PathOf(value.transform) + ":" + value.alpha
                        + ":" + value.isActiveAndEnabled)));
            foreach (Image image in panel.GetComponentsInChildren<Image>(true)
                .Where(value => value.isActiveAndEnabled).Take(6))
                log.Add("PANELIMAGE " + label + " " + PathOf(image.transform) + " sprite="
                    + (image.sprite == null ? "null" : image.sprite.name) + " color=" + image.color
                    + " renderer=" + image.canvasRenderer.GetAlpha() + " culled=" + image.canvasRenderer.cull
                    + " rect=" + image.rectTransform.rect);
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true)
                .Where(value => value.isActiveAndEnabled && !string.IsNullOrEmpty(value.text)).Take(8))
                log.Add("PANELTEXT " + label + " " + PathOf(text.transform) + " text=" + text.text
                    + " color=" + text.color + " renderer=" + text.canvasRenderer.GetAlpha()
                    + " culled=" + text.canvasRenderer.cull + " rect=" + text.rectTransform.rect);
        }

        private void DumpDungeonResultNode(GameObject panel, string path)
        {
            Transform node = panel.transform.Find(path);
            RectTransform rect = node as RectTransform;
            if (node == null || rect == null)
            {
                log.Add("RESULTNODE " + path + " missing");
                return;
            }
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            string groups = string.Join("|", node.GetComponentsInParent<CanvasGroup>(true)
                .Select(value => PathOf(value.transform) + ":" + value.alpha + ":" + value.interactable
                    + ":" + value.blocksRaycasts));
            LayoutGroup layout = node.GetComponent<LayoutGroup>();
            ContentSizeFitter fitter = node.GetComponent<ContentSizeFitter>();
            ScrollRect scroll = node.GetComponent<ScrollRect>();
            Graphic graphic = node.GetComponent<Graphic>();
            TMP_Text text = node.GetComponent<TMP_Text>();
            log.Add("RESULTNODE " + path + " active=" + node.gameObject.activeInHierarchy
                + " rect=" + rect.rect + " anchored=" + rect.anchoredPosition
                + " screen=" + RectTransformUtility.WorldToScreenPoint(null, corners[0]) + "/"
                + RectTransformUtility.WorldToScreenPoint(null, corners[2])
                + " scale=" + rect.lossyScale + " groups=" + groups
                + " layout=" + (layout == null ? "none" : layout.GetType().Name + ":" + layout.isActiveAndEnabled)
                + " fitter=" + (fitter == null ? "none" : fitter.horizontalFit + "/" + fitter.verticalFit)
                + " scroll=" + (scroll == null ? "none" : scroll.isActiveAndEnabled + ":" + scroll.viewport
                    + ":" + scroll.content)
                + " graphic=" + (graphic == null ? "none" : graphic.isActiveAndEnabled + ":" + graphic.color
                    + ":" + graphic.canvasRenderer.cull)
                + " text=" + (text == null ? "none" : text.text + ":" + text.isActiveAndEnabled));
        }

        private static int[] FindRewardRoomPath(DailyDungeonModule module,
            DailyDungeonRunSave run)
        {
            if (module == null || run == null) return Array.Empty<int>();
            var pending = new Queue<int[]>();
            pending.Enqueue(new[] { run.currentRoomId });
            var visited = new HashSet<int>();
            while (pending.Count > 0)
            {
                int[] path = pending.Dequeue();
                int roomId = path[path.Length - 1];
                if (!visited.Add(roomId)) continue;
                DailyDungeonRuntimeRoom room = module.FindRoom(roomId);
                if (room == null) continue;
                int[] nextRoomIds = (room.nextRoomIds ?? Array.Empty<int>())
                    .Where(value => value > 0).ToArray();
                if ((room.surpriseRewards ?? Array.Empty<DailyDungeonRuntimeSurpriseReward>())
                    .Any(value => value != null && value.difficulty == run.difficulty
                        && value.rewardRuleIds != null && value.rewardRuleIds.Length > 0)
                    && nextRoomIds.Length > 0)
                    return path.Concat(new[] { nextRoomIds[0] }).ToArray();
                foreach (int nextRoomId in nextRoomIds)
                    pending.Enqueue(path.Concat(new[] { nextRoomId }).ToArray());
            }
            return Array.Empty<int>();
        }

        private static int[] FindTerminalRoomPath(DailyDungeonModule module,
            DailyDungeonRunSave run)
        {
            if (module == null || run == null) return Array.Empty<int>();
            var pending = new Queue<int[]>();
            pending.Enqueue(new[] { run.currentRoomId });
            var visited = new HashSet<int>();
            while (pending.Count > 0)
            {
                int[] path = pending.Dequeue();
                int roomId = path[path.Length - 1];
                if (!visited.Add(roomId)) continue;
                DailyDungeonRuntimeRoom room = module.FindRoom(roomId);
                if (room == null) continue;
                int[] nextRoomIds = (room.nextRoomIds ?? Array.Empty<int>())
                    .Where(value => value > 0).ToArray();
                if (nextRoomIds.Length == 0) return path;
                foreach (int nextRoomId in nextRoomIds)
                    pending.Enqueue(path.Concat(new[] { nextRoomId }).ToArray());
            }
            return Array.Empty<int>();
        }

        private static void ClickUi(Button button)
        {
            Assert(button != null, "UI button is missing");
            string groups = string.Join(",", button.GetComponentsInParent<CanvasGroup>(true)
                .Select(value => value.name + ":active=" + value.isActiveAndEnabled
                    + ":interactable=" + value.interactable + ":blocks=" + value.blocksRaycasts));
            Assert(button.interactable, "UI button is not interactable: " + PathOf(button.transform)
                + ", active=" + button.isActiveAndEnabled + ", groups=" + groups);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void ActivateTab(Selectable tab)
        {
            if (tab is Toggle toggle)
            {
                toggle.isOn = false;
                toggle.isOn = true;
                return;
            }
            Button button = tab as Button;
            if (button == null) throw new InvalidOperationException("Bottom tab has no supported input control.");
            button.onClick.Invoke();
        }
        private bool Check(string name, Func<object> body)
        {
            try
            {
                object value = body();
                log.Add("PASS " + name + ": " + value);
                return true;
            }
            catch (Exception error)
            {
                log.Add("FAIL " + name + ": " + error.Message);
                Debug.LogException(error);
                return false;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static T PrivateField<T>(object target, string name)
        {
            FieldInfo field = null;
            for (Type type = target.GetType(); type != null && field == null; type = type.BaseType)
                field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            return (T)field.GetValue(target);
        }

        private static bool IsLayerGroupVisible(HomeSceneView scene, string pathFragment)
        {
            List<KeyValuePair<string, GameObject>> layers = PrivateField<List<KeyValuePair<string, GameObject>>>(
                scene, "layerObjects");
            return layers.Any(pair => pair.Value != null
                && pair.Key.IndexOf(pathFragment, StringComparison.Ordinal) >= 0
                && pair.Value.activeInHierarchy);
        }

        private IEnumerator Capture(string file)
        {
            string path = Path.Combine(Output, file);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForEndOfFrame();
            if (file.StartsWith("dungeons-", StringComparison.Ordinal)) DumpDungeonMaterialState(file);
            if (file.StartsWith("home-shop-", StringComparison.Ordinal)) DumpShopRenderState();
            ScreenCapture.CaptureScreenshot(path);
            for (int attempt = 0; attempt < 120 && !File.Exists(path); attempt++)
                yield return null;
            if (!File.Exists(path))
            {
                log.Add("FAIL screenshot: Game View did not write " + file);
                yield break;
            }
            log.Add("PASS screenshot: " + file + " at " + Screen.width + "x" + Screen.height);
        }

        private void DumpDungeonMaterialState(string screenshot)
        {
            foreach (UIMesh mesh in FindObjectsOfType<UIMesh>(true)
                .Where(value => value != null && value.isActiveAndEnabled && value.gameObject.activeInHierarchy))
            {
                var names = new List<string>();
                for (Transform node = mesh.transform; node != null; node = node.parent) names.Add(node.name);
                names.Reverse();
                Material material = null;
                string error = null;
                try { material = mesh.materialForRendering; }
                catch (Exception exception) { error = exception.GetType().Name + ": " + exception.Message; }
                Shader shader = material == null ? null : material.shader;
                log.Add("dungeon-uimesh " + screenshot + " path=" + string.Join("/", names)
                    + " meshVertices=" + (mesh.Mesh == null ? 0 : mesh.Mesh.vertexCount)
                    + " material=" + (material == null ? "<null>" : material.name)
                    + " shader=" + (shader == null ? "<null>" : shader.name)
                    + " supported=" + (shader != null && shader.isSupported)
                    + (error == null ? string.Empty : " error=" + error));
            }

            foreach (Graphic graphic in FindObjectsOfType<Graphic>(true)
                .Where(value => value != null && value.isActiveAndEnabled && value.gameObject.activeInHierarchy))
            {
                try
                {
                    Material material = graphic.materialForRendering;
                    Shader shader = material == null ? null : material.shader;
                    if (shader != null && shader.isSupported && !shader.name.Contains("InternalError")) continue;
                    var names = new List<string>();
                    for (Transform node = graphic.transform; node != null; node = node.parent) names.Add(node.name);
                    names.Reverse();
                    log.Add("dungeon-unsupported-graphic " + screenshot + " path=" + string.Join("/", names)
                        + " material=" + (material == null ? "<null>" : material.name)
                        + " shader=" + (shader == null ? "<null>" : shader.name)
                        + " supported=" + (shader != null && shader.isSupported));
                }
                catch (Exception exception)
                {
                    log.Add("dungeon-graphic-inspection-error " + screenshot + " " + graphic.name + ": "
                        + exception.GetType().Name + ": " + exception.Message);
                }
            }
        }

        private void DumpShopRenderState()
        {
            GameObject panel = GameObject.Find("HomeShopPanel");
            Transform grid = panel == null ? null : panel.transform.Find(
                "BgPanel1/Content/MiddleGo/ShopContent/ShopContent/ShopContentGroup/GoodsGrid");
            Transform content = grid == null ? null : grid.Find("ViewPort/Content");
            Canvas canvas = panel == null ? null : panel.GetComponentInParent<Canvas>();
            log.Add("SHOPVIEW screen=" + Screen.width + "x" + Screen.height + " canvas="
                + (canvas == null ? "missing" : canvas.renderMode + " scale=" + canvas.scaleFactor));
            if (content == null) return;
            RectTransform viewport = grid.Find("ViewPort") as RectTransform;
            log.Add("SHOPVIEWPORT rect=" + (viewport == null ? "missing" : viewport.rect.ToString())
                + " active=" + grid.gameObject.activeInHierarchy + " children=" + content.childCount);
            Transform row = content.Cast<Transform>().FirstOrDefault(value =>
                value.name.StartsWith("ShopGoodsItem_", StringComparison.Ordinal));
            if (row == null) return;
            HomeUiAnimationPlayer animation = row.GetComponent<HomeUiAnimationPlayer>();
            log.Add("SHOPANIM playing=" + (animation != null && animation.IsPlaying)
                + " elapsed=" + (animation == null ? -1f : animation.PlaybackTime)
                + " unscaledDelta=" + (animation == null ? -1f : animation.PlaybackDeltaTime)
                + " timeScale=" + Time.timeScale);
            foreach (CanvasGroup group in row.GetComponentsInParent<CanvasGroup>(true))
                log.Add("SHOPGROUP " + PathOf(group.transform) + " alpha=" + group.alpha
                    + " interactable=" + group.interactable);
            foreach (Image image in row.GetComponentsInChildren<Image>(true))
            {
                log.Add("SHOPIMAGE " + PathOf(image.transform) + " enabled=" + image.isActiveAndEnabled
                    + " sprite=" + (image.sprite == null ? "null" : image.sprite.name)
                    + " alpha=" + image.color.a + " renderer=" + image.canvasRenderer.GetAlpha()
                    + " culled=" + image.canvasRenderer.cull
                    + " rect=" + image.rectTransform.rect);
            }
            foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
                log.Add("SHOPTEXT " + PathOf(text.transform) + " value=" + text.text
                    + " alpha=" + text.color.a + " renderer=" + text.canvasRenderer.GetAlpha()
                    + " rect=" + text.rectTransform.rect);
            }
        }
        private static void Quit(int code)
        {
            Type application = Type.GetType("UnityEditor.EditorApplication, UnityEditor");
            if (application != null)
            {
                PropertyInfo playing = application.GetProperty("isPlaying", BindingFlags.Public | BindingFlags.Static);
                if (playing != null) playing.SetValue(null, false);
                MethodInfo exit = application.GetMethod("Exit", BindingFlags.Public | BindingFlags.Static);
                if (exit != null)
                {
                    exit.Invoke(null, new object[] { code });
                    return;
                }
            }
            Application.Quit(code);
        }
    }

    [Serializable]
    public sealed class SerializedReport
    {
        public bool success;
        public string[] lines;
    }

    public static class HomeAcceptanceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Environment.GetCommandLineArgs().Any(value =>
                    string.Equals(value, "-homeAcceptance", StringComparison.OrdinalIgnoreCase))) return;
            if (UnityEngine.Object.FindObjectOfType<HomeAcceptanceDriver>() != null) return;
            var go = new GameObject("HomeAcceptanceDriver");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<HomeAcceptanceDriver>();
        }
    }
}

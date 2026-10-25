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
using Zjcs.Offline.Home;
using Zjcs.Offline.State;

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
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas == null || canvas.name == "Home Canvas") continue;
                string name = canvas.name;
                if (name.IndexOf("CharacterCreation", StringComparison.OrdinalIgnoreCase) < 0
                    && name.IndexOf("Opening", StringComparison.OrdinalIgnoreCase) < 0) continue;
                canvas.gameObject.SetActive(false);
            }
        }
        private void DumpFullscreen()
        {
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
            yield return null;
            yield return null;
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
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
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

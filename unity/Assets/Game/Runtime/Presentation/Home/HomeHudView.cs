using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Home;

namespace Zjcs.Presentation.Home
{
    /// <summary>
    /// Main-city HUD rebuilt from the official panels: top bar, avatar, five-tab
    /// bottom bar, function menu and the furniture/mail windows.
    /// </summary>
    public sealed class HomeHudView
    {
        private static readonly string[] CurrencyIcons = { "Money_1", "Money_2", "Money_15" };
        private static readonly int[] CurrencyItems = { 1, 2, 15 };

        private readonly HomeFeatureRuntime host;
        private readonly HomeAssetCatalog catalog;
        private readonly Transform canvasParent;
        private readonly List<HomeUiRenderer> owned = new List<HomeUiRenderer>();
        private readonly List<GameObject> dialogs = new List<GameObject>();
        private Canvas canvas;
        private GameObject homePanel;
        private HomeUiRenderer homeRenderer;
        private HomeUiRenderer topRenderer;
        private HomeUiRenderer bottomRenderer;
        private HomeUiRenderer functionsRenderer;
        private GameObject functionsPanel;

        private TextMeshProUGUI nameLabel;
        private TextMeshProUGUI levelLabel;
        private TextMeshProUGUI toast;
        private GameObject noticeRoot;
        private int selectedTab;
        private GameObject activeDialog;
        private HomeUiRenderer activeDialogRenderer;
        private bool cartOpen;
        private bool bedOpen;

        private readonly string[] tabNames = { "Character", "TravelNote", "Home", "Legion", "Mainland" };

        public HomeHudView(HomeFeatureRuntime host, Transform parent)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            catalog = host.Catalog;
            canvasParent = parent;
        }

        public bool AnyModalOpen => activeDialog != null || (functionsPanel != null && functionsPanel.activeSelf);

        public void Build()
        {
            CreateCanvas();
            EnsureEventSystem();
            BuildHomePanel();
            BuildTopBar();
            BuildBottomBar();
            BuildFunctions();
            BuildToast();
            SelectTab(2, false);
        }

        private void CreateCanvas()
        {
            var go = new GameObject("Home Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(null, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("Home Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private HomeUiRenderer Renderer()
        {
            var renderer = new HomeUiRenderer(catalog);
            owned.Add(renderer);
            return renderer;
        }

        private void BuildHomePanel()
        {
            homeRenderer = Renderer();
            homePanel = homeRenderer.Create("home", canvas.transform, OnHomeButton);
            RectTransform rect = (RectTransform)homePanel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            BuildAvatar();
            // Official fade/screenshot guider nodes start hidden; their white mask must not cover the scene.
            homeRenderer.SetActiveByName("Guider", false);
            homeRenderer.SetActiveByName("HomeFadeProxy", false);
        }

        private void BuildAvatar()
        {
            Transform slot = Find(homePanel.transform, "NewPublicTopBarParent");
            if (slot == null) return;
            HomeUiRenderer avatar = Renderer();
            avatar.Create("player_icon", slot, null);
            nameLabel = CreateLabel(slot, "HomeName", 26, new Vector2(96, -8), TextAlignmentOptions.Left);
            levelLabel = CreateLabel(slot, "HomeLevel", 20, new Vector2(96, -42), TextAlignmentOptions.Left);
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string name, float size, Vector2 offset,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(320, 36);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = catalog.Font;
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.color = new Color32(250, 236, 210, 255);
            return text;
        }

        private void BuildTopBar()
        {
            Transform slot = Find(homePanel.transform, "Slot");
            if (slot == null) slot = homePanel.transform;
            topRenderer = Renderer();
            GameObject bar = topRenderer.Create("top_bar", slot, null);
            RectTransform barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(1, 1);
            barRect.anchorMax = new Vector2(1, 1);
            barRect.pivot = new Vector2(1, 1);
            barRect.anchoredPosition = new Vector2(-16, -12);
            Transform list = Find(bar.transform, "ResourceList") ?? bar.transform;
            for (int index = 0; index < CurrencyItems.Length; index++)
            {
                HomeUiRenderer money = Renderer();
                GameObject entry = money.Create("money", list, null);
                money.SetSpriteByPrefix("IconImg", CurrencyIcons[index]);
                entry.name = "Money_" + CurrencyItems[index];
                entry.AddComponent<TopBarCurrency>().Renderer = money;
            }
        }

        private void BuildBottomBar()
        {
            bottomRenderer = Renderer();
            GameObject bar = bottomRenderer.Create("bottom_bar", canvas.transform, OnBottomButton);
            RectTransform rect = (RectTransform)bar.transform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 8);
        }

        private void BuildFunctions()
        {
            functionsRenderer = Renderer();
            functionsPanel = functionsRenderer.Create("functions", canvas.transform, OnFunctionButton);
            RectTransform rect = (RectTransform)functionsPanel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            functionsPanel.SetActive(false);
        }

        private void BuildToast()
        {
            noticeRoot = new GameObject("HomeToast", typeof(RectTransform));
            noticeRoot.transform.SetParent(canvas.transform, false);
            RectTransform rect = (RectTransform)noticeRoot.transform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 210);
            rect.sizeDelta = new Vector2(760, 90);
            Image background = noticeRoot.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.72f);
            toast = CreateLabel(noticeRoot.transform, "ToastText", 26, new Vector2(16, -10),
                TextAlignmentOptions.Center);
            RectTransform textRect = (RectTransform)toast.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 8);
            textRect.offsetMax = new Vector2(-12, -8);
            toast.enableWordWrapping = true;
            noticeRoot.SetActive(false);
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                Transform nested = Find(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private void OnHomeButton(string path, GameObject node)
        {
            if (path.EndsWith("FunctionEntryGroup", StringComparison.Ordinal)
                || path.Contains("FunctionEntryGroup")) ToggleFunctions();
        }

        private void OnBottomButton(string path, GameObject node)
        {
            for (int index = 0; index < tabNames.Length; index++)
                if (path.Contains("/" + tabNames[index] + "/")) SelectTab(index, true);
        }

        private void OnFunctionButton(string path, GameObject node)
        {
            if (path.EndsWith("CloseBtn", StringComparison.Ordinal)) { ToggleFunctions(false); return; }
            if (path.Contains("HomeSkinBtn")) { Notify("装潢页面由家园装潢子模块提供"); return; }
            if (path.Contains("SettingsBtn")) { Notify("设置入口见设置与存档页面"); return; }
            Notify("该入口的页面由对应功能模块提供");
        }

        public void ToggleFunctions(bool? open = null)
        {
            bool next = open ?? (functionsPanel != null && !functionsPanel.activeSelf);
            if (functionsPanel != null) functionsPanel.SetActive(next);
        }

        public void SelectTab(int index, bool notify)
        {
            selectedTab = Mathf.Clamp(index, 0, tabNames.Length - 1);
            for (int i = 0; i < tabNames.Length; i++)
            {
                if (bottomRenderer == null) break;
                bool selected = i == selectedTab;
                bottomRenderer.SetActiveByName(tabNames[i] + "/UnlockObj/Anim/SelectedBgImg", selected);
            }
            if (notify && selectedTab != 2)
            {
                Notify(selectedTab == 0 ? "人物页由人物模块提供" :
                    selectedTab == 1 ? "游记页由游记模块提供" :
                    selectedTab == 3 ? "公会页由单人公会模块提供" : "大陆页由世界地图模块提供");
            }
        }

        public bool IsClosedByNetworkPolicy(int type)
        {
            if (type == 13)
            {
                Notify("交易所与拍卖已按离线范围关闭");
                return true;
            }
            return false;
        }

        public void ShowNotice(string message) => Notify(message);

        public void ShowFurniturePlaceholder(int type)
        {
            string name = catalog.FurnitureName(type);
            Notify(name + " 的内部页面由所属功能模块提供");
        }

        private void Notify(string message)
        {
            if (toast == null) return;
            toast.text = message ?? string.Empty;
            noticeRoot.SetActive(true);
            noticeRoot.transform.SetAsLastSibling();
            CancelInvoke();
            Invoke(() => { if (noticeRoot != null) noticeRoot.SetActive(false); }, 2.4f);
        }

        private float noticeDeadline;

        private void CancelInvoke() => noticeDeadline = 0f;

        private void Invoke(Action action, float delay) => noticeDeadline = Time.unscaledTime + delay;

        public void Tick()
        {
            if (noticeDeadline > 0f && Time.unscaledTime >= noticeDeadline)
            {
                noticeDeadline = 0f;
                if (noticeRoot != null) noticeRoot.SetActive(false);
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (activeDialog != null) CloseDialog();
                else if (functionsPanel != null && functionsPanel.activeSelf) ToggleFunctions(false);
            }
        }

        public void RefreshAll()
        {
            RefreshCounts();
            if (nameLabel != null) nameLabel.text = host.CharacterName;
            if (levelLabel != null) levelLabel.text = "Lv." + host.Level + "  " + host.GuildName;
            if (bedOpen) RefreshBed();
            if (cartOpen) RefreshCart();
        }

        public void RefreshCounts()
        {
            if (canvas == null) return;
            foreach (TopBarCurrency currency in canvas.GetComponentsInChildren<TopBarCurrency>(true))
                currency.Refresh(host);
        }

        // ---------------------------------------------------------------- windows

        private GameObject OpenDialog(string panelKey, Action<HomeUiRenderer, GameObject> build)
        {
            CloseDialog();
            var root = new GameObject("HomeDialog", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(canvas.transform, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image overlay = root.GetComponent<Image>();
            overlay.color = new Color(0, 0, 0, 0.62f);
            Button button = root.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(CloseDialog);
            HomeUiRenderer renderer = Renderer();
            GameObject panel = renderer.Create(panelKey, root.transform, null);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            build(renderer, panel);
            activeDialog = root;
            activeDialogRenderer = renderer;
            return panel;
        }

        public void CloseDialog()
        {
            if (activeDialog != null) UnityEngine.Object.Destroy(activeDialog);
            activeDialog = null;
            activeDialogRenderer = null;
            cartOpen = false;
            bedOpen = false;
        }

        public void ShowRepair(int type)
        {
            HomeFurnitureRecord record = host.Rules.FurnitureOf(type);
            if (record == null) return;
            OpenDialog("repair", (renderer, panel) =>
            {
                renderer.SetTextByName("NameTxt", host.Rules.FurnitureNameForType(type));
                renderer.SetTextByName("SubTxt", host.Catalog.FurnitureTypeName(type));
                renderer.SetTextByName("DescTxt", "维修后可启用该设施。");
                Button repair = renderer.Find<Button>("/FurnitureRepairDialog/BgDialog3/BgImg/Mask/RepaireBtn");
                if (repair == null) repair = FirstButton(renderer, "RepaireBtn");
                if (repair != null) repair.onClick.AddListener(() =>
                {
                    if (host.TryRepair(type)) { CloseDialog(); Notify("维修完成"); }
                    else Notify("维修条件未满足");
                });
            });
        }

        public void ShowBed()
        {
            bedOpen = true;
            OpenDialog("bed", (renderer, panel) => { RefreshBed(renderer); });
        }

        private void RefreshBed() => RefreshBed(activeDialogRenderer);

        private void RefreshBed(HomeUiRenderer renderer)
        {
            if (renderer == null) return;
            int hangUpId = 1;
            HomeHangUpRecord record = host.Rules.HangUp(hangUpId);
            if (record == null) return;
            renderer.SetTextByName("NameTxt", host.Rules.ItemName(host.Rules.FurnitureClassId(0)));
            renderer.SetTextByName("SubTxt", host.Catalog.FurnitureTypeName(0));
            HomePreview(hangUpId, out long count, out long capacity, out double hourly);
            renderer.SetTextByName("ValueTxt", count + "/" + capacity);
            renderer.SetTextByName("DataText", "每小时 " + Format(hourly));
            Button collect = FirstButton(renderer, "CollectBtn");
            if (collect != null)
            {
                collect.onClick.RemoveAllListeners();
                collect.onClick.AddListener(() =>
                {
                    long gained = host.TryCollect(hangUpId);
                    Notify(gained > 0 ? "领取 " + Format(gained) + " 经验" : "暂无可领取产出");
                    RefreshBed();
                });
            }
            Button speed = FirstButton(renderer, "SpeedBtn");
            if (speed != null)
            {
                speed.onClick.RemoveAllListeners();
                speed.onClick.AddListener(() => ShowFastForward(hangUpId));
            }
        }

        private void HomePreview(int hangUpId, out long count, out long capacity, out double hourly)
        {
            HangUpIncomePreview preview = host.Session.Preview(hangUpId);
            count = preview.Count;
            capacity = preview.Capacity;
            hourly = preview.HourlyRate;
        }

        public void ShowCart()
        {
            cartOpen = true;
            OpenDialog("cart", (renderer, panel) =>
            {
                renderer.SetTextByName("TitleTxt", host.Rules.FurnitureNameForType(10));
                renderer.SetTextByName("DescText", "小推车资源产出");
                BuildCartRows(renderer, panel);
                Button collect = FirstButton(renderer, "CommonBtn_12");
                if (collect != null)
                {
                    collect.onClick.RemoveAllListeners();
                    collect.onClick.AddListener(() =>
                    {
                        long total = 0;
                        foreach (HomeHangUpRecord record in host.Rules.HangUpsOf(10))
                            if (host.Session.IsHangUpUnlocked(record.id)) total += host.TryCollect(record.id);
                        Notify(total > 0 ? "领取 " + Format(total) : "暂无可领取产出");
                        RefreshCart();
                    });
                }
            });
        }

        private void RefreshCart()
        {
            if (activeDialogRenderer == null || !cartOpen) return;
            Transform content = Find(activeDialog != null ? activeDialog.transform : null, "Content");
            if (content == null) return;
            for (int index = content.childCount - 1; index >= 0; index--)
                UnityEngine.Object.Destroy(content.GetChild(index).gameObject);
            BuildCartRows(activeDialogRenderer, activeDialog);
        }

        private void BuildCartRows(HomeUiRenderer renderer, GameObject panel)
        {
            Transform content = Find(panel.transform, "HangUpScrollRect");
            Transform list = content == null ? null : Find(content, "Content");
            if (list == null) list = Find(panel.transform, "Content");
            if (list == null) return;
            foreach (HomeHangUpRecord record in host.Rules.HangUpsOf(10))
            {
                HomeUiRenderer rowRenderer = Renderer();
                GameObject row = rowRenderer.Create("cart_row", list, null);
                bool unlocked = host.Session.IsHangUpUnlocked(record.id);
                rowRenderer.SetActiveByName("Unlock", unlocked);
                rowRenderer.SetActiveByName("Lock", !unlocked && !host.Rules.IsMet(record.unlock, host.Context));
                rowRenderer.SetActiveByName("CanUnlock", !unlocked && host.Rules.IsMet(record.unlock, host.Context));
                rowRenderer.SetTextByName("Name", host.Rules.ItemName(host.Rules.HangUpReward(record.rewardId)?.rewardRuleId ?? 0));
                string rowName = host.ItemNameForHangUp(record.id);
                rowRenderer.SetTextByName("Name", rowName);
                rowRenderer.SetTextByName("LevelNumber", host.Session.HangUpLevel(record.id).ToString());
                if (unlocked)
                {
                    HangUpIncomePreview preview = host.Session.Preview(record.id);
                    rowRenderer.SetTextByName("ValueTxt", Format(preview.Count) + "/" + Format(preview.Capacity));
                }
                int id = record.id;
                Button levelUp = FirstButton(rowRenderer, "CommonBtn_LevelUp");
                if (levelUp != null && unlocked && record.canLevelUp)
                {
                    levelUp.onClick.AddListener(() =>
                    {
                        HomeLevelUpResult result = host.TryLevelUpHangUp(id);
                        Notify(result.Success ? "升级到 " + result.Level + " 级" : ErrorText(result.Error));
                        RefreshCart();
                    });
                }
                else if (levelUp != null) levelUp.gameObject.SetActive(false);
                Button unlock = FirstButton(rowRenderer, "UnlockBtn");
                if (unlock != null)
                {
                    unlock.onClick.AddListener(() =>
                    {
                        Notify(host.TryUnlockHangUp(id) ? "已解锁" : "解锁条件未满足");
                        RefreshCart();
                    });
                }
            }
        }

        private static string ErrorText(string error) => error switch
        {
            "max_level" => "已满级",
            "home_level" => "家园等级不足",
            "items" => "升级材料不足",
            "locked" => "尚未解锁",
            "condition" => "条件未满足",
            _ => "升级失败"
        };

        public void ShowMail()
        {
            OpenDialog("mail_mail", (renderer, panel) =>
            {
                IReadOnlyList<OfflineHomeMail> inbox = host.Session.GetInbox(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                Transform list = Find(panel.transform, "Content");
                if (list == null) list = Find(panel.transform, "ScrollRect");
                if (list == null) return;
                Transform content = Find(list, "Content") ?? list;
                foreach (OfflineHomeMail mail in inbox)
                {
                    HomeUiRenderer rowRenderer = Renderer();
                    GameObject row = rowRenderer.Create("mail_row", content, null);
                    rowRenderer.SetTextByName("Title", mail.Subject);
                    rowRenderer.SetTextByName("Sender", mail.Sender);
                    rowRenderer.SetTextByName("Time", string.Empty);
                    long id = mail.Id;
                    Button button = row.GetComponent<Button>();
                    if (button != null) button.onClick.AddListener(() => ShowMailContent(id));
                }
                if (inbox.Count == 0) Notify("邮箱暂无邮件");
                Button collectAll = FirstButton(renderer, "PickupAllBtn");
                if (collectAll != null)
                    collectAll.onClick.AddListener(() =>
                    {
                        int count = host.Session.CollectAllMailAttachments(
                            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            "home:mail:" + Guid.NewGuid().ToString("N"));
                        host.DrainHomeInventory();
                        Notify(count > 0 ? "领取 " + count + " 封邮件附件" : "暂无可领取附件");
                        CloseDialog();
                    });
                Button readAll = FirstButton(renderer, "ReadAllBtn");
                if (readAll != null)
                    readAll.onClick.AddListener(() =>
                    {
                        host.Session.MarkAllMailRead(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                        Notify("已全部标记为已读");
                        CloseDialog();
                    });
            });
        }

        private void ShowMailContent(long mailId)
        {
            OfflineHomeMail mail = host.Session.GetInbox(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                .FirstOrDefault(value => value.Id == mailId);
            if (mail == null) return;
            host.Session.MarkMailRead(mailId);
            var panel = new GameObject("MailContent", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(820, 900);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.11f, 0.1f, 0.98f);
            TextMeshProUGUI title = CreateLabel(panel.transform, "Title", 34, new Vector2(32, -28),
                TextAlignmentOptions.TopLeft);
            title.text = mail.Subject;
            TextMeshProUGUI body = CreateLabel(panel.transform, "Body", 24, new Vector2(32, -110),
                TextAlignmentOptions.TopLeft);
            body.text = mail.Body;
            RectTransform bodyRect = (RectTransform)body.transform;
            bodyRect.sizeDelta = new Vector2(756, 640);
            body.enableWordWrapping = true;
            var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            close.transform.SetParent(panel.transform, false);
            RectTransform closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = new Vector2(0.5f, 0);
            closeRect.anchorMax = new Vector2(0.5f, 0);
            closeRect.pivot = new Vector2(0.5f, 0);
            closeRect.anchoredPosition = new Vector2(0, 24);
            closeRect.sizeDelta = new Vector2(240, 72);
            close.GetComponent<Image>().color = new Color(0.36f, 0.3f, 0.2f, 1f);
            TextMeshProUGUI closeText = CreateLabel(close.transform, "Text", 26, Vector2.zero,
                TextAlignmentOptions.Center);
            closeText.text = "关闭";
            RectTransform closeTextRect = (RectTransform)closeText.transform;
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero;
            closeTextRect.offsetMax = Vector2.zero;
            close.GetComponent<Button>().onClick.AddListener(() => UnityEngine.Object.Destroy(panel));
            panel.transform.SetAsLastSibling();
        }

        public void ShowFastForward(int hangUpId)
        {
            OpenDialog("fast_idle", (renderer, panel) =>
            {
                HomeFastHangUpRecord fast = host.Rules.FastHangUp(1);
                int hours = fast?.freeHours ?? 2;
                renderer.SetTextByName("SpeedTxt", hours + "小时");
                renderer.SetTextByName("TitleText", "快速挂机");
                Button speed = FirstButton(renderer, "SpeedBtn");
                if (speed != null)
                    speed.onClick.AddListener(() =>
                    {
                        long total = host.TryFastForward(hangUpId);
                        Notify(total > 0 ? "加速获得 " + Format(total) : "免费加速次数已用完");
                        CloseDialog();
                    });
            });
        }

        private static Button FirstButton(HomeUiRenderer renderer, string name)
        {
            foreach (GameObject node in renderer.AllNodes)
            {
                if (!node.name.Equals(name, StringComparison.Ordinal)) continue;
                Button button = node.GetComponent<Button>();
                if (button != null) return button;
            }
            return null;
        }

        private static string Format(long value)
        {
            if (value >= 100000000) return (value / 100000000f).ToString("0.##") + "亿";
            if (value >= 10000) return (value / 10000f).ToString("0.##") + "万";
            return value.ToString();
        }

        private static string Format(double value) => Format((long)Math.Round(value));

        public void Dispose()
        {
            owned.Clear();
            if (canvas != null) UnityEngine.Object.Destroy(canvas.gameObject);
            canvas = null;
        }
    }

    /// <summary>Refreshes one official ``MoneyUI`` entry from the bag.</summary>
    public sealed class TopBarCurrency : MonoBehaviour
    {
        public HomeUiRenderer Renderer;
        private int itemId;

        private void Awake()
        {
            if (int.TryParse(name.Replace("Money_", string.Empty), out int parsed)) itemId = parsed;
        }

        public void Refresh(HomeFeatureRuntime host)
        {
            if (Renderer == null) return;
            long count = host.ItemCount(itemId);
            Renderer.SetTextByName("CountTxt", count >= 100000 && count < 100000000
                ? (count / 10000f).ToString("0.#") + "万"
                : count.ToString());
        }
    }
}

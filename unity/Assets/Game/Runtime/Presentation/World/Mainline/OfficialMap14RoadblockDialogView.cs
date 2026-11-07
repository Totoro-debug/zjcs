using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.World
{
    [Serializable]
    public sealed class OfficialRoadblockUiData
    {
        [Serializable] public sealed class Text { public string key, value; }
        [Serializable] public sealed class Colors { public Color EnoughColor, LackColor; }
        public string manifest;
        public Text[] texts;
        public Colors conditionColors;
        public OfficialBattlePopupMask.Binding mask;
        private static OfficialRoadblockUiData loaded;
        private Dictionary<string, string> lookup;
        public static OfficialRoadblockUiData Load()
        {
            if (loaded != null) return loaded;
            var asset = Resources.Load<TextAsset>("Map14/UI/roadblock-texts");
            var data = asset == null ? null : JsonUtility.FromJson<OfficialRoadblockUiData>(asset.text);
            if (data?.manifest != "884_156692" || data.texts == null || data.conditionColors == null || data.mask == null)
                throw new InvalidOperationException("Pinned roadblock localization and colors are missing.");
            data.lookup = data.texts.ToDictionary(t => t.key, t => t.value);
            foreach (string path in new[] { "Map12/Team/invite-texts", "Map12/Team/npc-assistance-texts" })
                foreach (var row in JsonUtility.FromJson<OfficialRoadblockUiData>(Resources.Load<TextAsset>(path).text).texts)
                    if (!data.lookup.ContainsKey(row.key)) data.lookup.Add(row.key, row.value);
            return loaded = data;
        }
        public string Localize(string key) => lookup.TryGetValue(key, out var value) ? value : key;
        public bool TryLocalize(string key, out string value) => lookup.TryGetValue(key, out value);
    }

    internal sealed class OfficialMap14RoadblockDialogView : IDisposable
    {
        private static readonly HashSet<int> TextPortraitFallbackNpcIds =
            new HashSet<int>(Enumerable.Range(11015, 8));

        private readonly GameObject root;
        private readonly WorldPopupWindow popup;
        private readonly Func<string, string> localize;
        private readonly Action<int> invite;
        private readonly Action<int> remove;
        private readonly Action confirm;
        private readonly Action close;
        private readonly Transform[] partnerSlots = new Transform[3];
        private readonly Button[] inviteButtons = new Button[3];
        private readonly Button[] removeButtons = new Button[3];
        private readonly Button confirmButton;
        private readonly TMP_FontAsset fallbackFont;
        private bool closed;
        private GameObject rulesWindow;
        private sealed class RoadblockRules : IProfessionRuleTipData
        {
            public string GetTitleKey(string key) => "rule_tip_" + key + "_0";
            public IReadOnlyList<string> GetTipsKey(string key) => Enumerable.Range(1, 3).Select(i => "rule_tip_" + key + "_" + i).ToArray();
        }

        public OfficialMap14RoadblockDialogView(Transform parent,
            Func<string, string> localize, Action<int> invite, Action<int> remove,
            Action confirm, Action close, WorldStageUiContext stage, Texture playerPortrait, string playerName)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (localize == null) throw new ArgumentNullException(nameof(localize));
            var ui = OfficialRoadblockUiData.Load();
            this.localize = key => ui.TryLocalize(key, out var value) ? value : localize(key);
            this.invite = invite ?? throw new ArgumentNullException(nameof(invite));
            this.remove = remove ?? throw new ArgumentNullException(nameof(remove));
            this.confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
            this.close = close ?? throw new ArgumentNullException(nameof(close));

            GameObject prefab = Resources.Load<GameObject>("Map14/UI/RoadblockTeamPropStageDialog");
            if (prefab == null)
                throw new InvalidOperationException("Official Map14 roadblock dialog prefab is missing.");
            root = UnityEngine.Object.Instantiate(prefab, parent, false);
            root.name = "RoadblockTeamPropStageDialog";
            root.SetActive(true);
            fallbackFont = Resources.Load<TMP_FontAsset>("Combat/FZY4K-Dynamic");
            Localize(root);
            root.GetComponent<StageDialog>().Init(stage);

            Transform content = Node("BgDialog3WorldMap/Content/DialogContent (1)");
            Transform players = content.Find("TeamGo/PlayersGo");
            if (players == null) throw new InvalidOperationException("Original roadblock team slots are missing.");
            Transform self = players.Find("MePlayer");
            if (self == null) throw new InvalidOperationException("Original roadblock player slot is missing.");
            SetActive(self.Find("InviteBtn"), false);
            SetActive(self.Find("PublicPlayerIcon/ExitBtn"), false);
            SetText(self.Find("BottomSlot/Name"), playerName);
            var head = self.Find("PublicPlayerIcon/IconGo/ImgMask/PlayerHeadGo/HeadImg").GetComponent<RawImage>();
            head.texture = playerPortrait;
            if (playerPortrait != null) head.SetNativeSize();
            for (int index = 0; index < partnerSlots.Length; index++)
            {
                partnerSlots[index] = players.Find("Player" + (index + 1).ToString(CultureInfo.InvariantCulture));
                if (partnerSlots[index] == null)
                    throw new InvalidOperationException("Original roadblock partner slot is missing.");
                int captured = index;
                inviteButtons[index] = RequireButton(partnerSlots[index].Find("InviteBtn"), "InviteBtn");
                removeButtons[index] = RequireButton(partnerSlots[index].Find("PublicPlayerIcon/ExitBtn"), "ExitBtn");
                inviteButtons[index].onClick.AddListener(() => this.invite(captured));
                removeButtons[index].onClick.AddListener(() => this.remove(captured));
            }

            confirmButton = RequireButton(content.Find("BottomContent/CommonBtn_12"), "CommonBtn_12");
            confirmButton.onClick.AddListener(() => this.confirm());
            RequireButton(content.Find("TitleGo/TitleGo/RuleBtn"), "RuleBtn").onClick.AddListener(OpenRules);
            Animator animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            if (root.GetComponent<CanvasGroup>() == null) root.AddComponent<CanvasGroup>();
            if (root.GetComponent<Canvas>() == null) root.AddComponent<Canvas>();
            popup = root.GetComponent<WorldPopupWindow>();
            if (popup == null) popup = root.AddComponent<WorldPopupWindow>();
            popup.CloseByClickMask = true;
            Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                canvas.worldCamera = parentCanvas == null ? null : parentCanvas.rootCanvas.worldCamera;
            popup.SetMaskClickAction(this.close);
            ShowMasked(root, popup, this.close);
        }

        public void Refresh(string title, string description, string conditionKey,
            string propertyName, string requiredValue, string currentValue,
            string playerPropertyValue, IReadOnlyList<int> selected,
            Func<int, string> nameOf, Func<int, string> propertyOf,
            Func<int, Sprite> portraitOf, bool canOpen)
        {
            if (closed) return;
            Transform content = Node("BgDialog3WorldMap/Content/DialogContent (1)");
            SetText(content.Find("TitleGo/TitleGo/TitleTxt"), title);
            SetText(content.Find("DescGo/DescText"), description);
            bool met = canOpen;
            Transform condition = content.Find("LockGo/ConditionGo/DialogContentConditionItem0");
            if (condition != null)
                condition.GetComponent<WorldConditionItem>().SetMet(met);
            Transform conditionLabel = condition == null ? null : condition.Find("Text");
            TMP_Text conditionText = TextComponent(conditionLabel);
            if (conditionText != null)
            {
                WorldMultiLanguageText languageText = conditionText.GetComponent<WorldMultiLanguageText>();
                if (languageText != null) languageText.myKey = string.Empty;
                var colors = OfficialRoadblockUiData.Load().conditionColors;
                string color = ColorUtility.ToHtmlStringRGB(met ? colors.EnoughColor : colors.LackColor);
                string template = this.localize(conditionKey);
                conditionText.text = string.Format(CultureInfo.InvariantCulture,
                    template, propertyName, requiredValue, color, currentValue);
            }

            Transform players = content.Find("TeamGo/PlayersGo");
            SetSlot(players.Find("MePlayer"), 0, null, playerPropertyValue,
                null, false, true);
            for (int index = 0; index < partnerSlots.Length; index++)
            {
                bool hasNpc = selected != null && index < selected.Count;
                int npcId = hasNpc ? selected[index] : 0;
                SetSlot(partnerSlots[index], npcId, hasNpc ? nameOf(npcId) : string.Empty,
                    hasNpc ? propertyOf(npcId) : string.Empty,
                    hasNpc ? portraitOf(npcId) : null, hasNpc, false);
            }
            confirmButton.gameObject.SetActive(canOpen);
            confirmButton.interactable = canOpen;
        }

        private void SetSlot(Transform slot, int npcId, string name, string property,
            Sprite portrait, bool occupied, bool self)
        {
            if (slot == null) return;
            SetActive(slot.Find("PublicPlayerIcon"), occupied || self);
            SetActive(slot.Find("BottomSlot"), occupied || self);
            if (name != null) SetText(slot.Find("BottomSlot/Name"), name);
            var propertyText = slot.Find("BottomSlot/PropValue").GetComponent<WorldMultiLanguageText>();
            propertyText.Format(property);
            if (!self)
            {
                SetActive(inviteButtons[Array.IndexOf(partnerSlots, slot)], !occupied);
                SetActive(removeButtons[Array.IndexOf(partnerSlots, slot)], occupied);
            }

            Transform mask = slot.Find("PublicPlayerIcon/IconGo/ImgMask");
            if (mask == null) return;
            Transform iconNode = mask.Find("IconImg");
            if (iconNode == null) iconNode = mask.Find("PlayerHeadGo/IconImg");
            Image icon = iconNode == null ? null : iconNode.GetComponent<Image>();
            if (icon != null)
            {
                if (portrait != null) icon.sprite = portrait;
                icon.gameObject.SetActive(occupied && portrait != null);
            }
            SetActive(mask.Find("PlayerHeadGo"), self);
            TMP_Text fallback = GetPortraitFallback(mask);
            bool textPortrait = occupied && portrait == null
                && TextPortraitFallbackNpcIds.Contains(npcId);
            fallback.gameObject.SetActive(textPortrait);
            fallback.text = textPortrait ? name : string.Empty;
        }

        private TMP_Text GetPortraitFallback(Transform mask)
        {
            Transform node = mask.Find("OfflineNpcNameFallback");
            if (node == null)
            {
                var gameObject = new GameObject("OfflineNpcNameFallback", typeof(RectTransform),
                    typeof(TextMeshProUGUI));
                node = gameObject.transform;
                node.SetParent(mask, false);
                RectTransform rect = (RectTransform)node;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            TMP_Text text = node.GetComponent<TMP_Text>();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 8;
            text.fontSizeMax = 14;
            if (fallbackFont != null)
            {
                text.font = fallbackFont;
                text.fontSharedMaterial = fallbackFont.material;
            }
            return text;
        }

        private void OpenRules()
        {
            if (rulesWindow != null) return;
            rulesWindow = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Map14/UI/RuleDialog"), root.transform.parent, false);
            rulesWindow.name = "RoadblockRuleDialog";
            rulesWindow.SetActive(true);
            Localize(rulesWindow);
            var rules = rulesWindow.GetComponent<RuleDialog>();
            rules.Rules = new RoadblockRules();
            rules.SetRuleWithShowDelegate("PropStageDialogRule", (tip, key, index) => {
                Localize(tip.gameObject); tip.SetKey(key);
            });
            var window = rulesWindow.GetComponent<WorldPopupWindow>();
            Action dismiss = () => { var old = rulesWindow; rulesWindow = null; window.Hide(() => DestroyRoot(old)); };
            window.SetMaskClickAction(dismiss);
            ShowMasked(rulesWindow, window, dismiss);
        }

        private static void ShowMasked(GameObject window, WorldPopupWindow popup, Action dismiss)
        {
            window.AddComponent<OfficialBattlePopupMask>().Show(OfficialRoadblockUiData.Load().mask,
                3, popup.CloseByClickMask, dismiss, popup.Show);
        }

        private void Localize(GameObject window)
        {
            foreach (WorldMultiLanguageText text in window.GetComponentsInChildren<WorldMultiLanguageText>(true))
            {
                text.Localizer = localize;
                text.Refresh();
            }
        }

        private void SetText(Transform node, string value)
        {
            TMP_Text text = TextComponent(node);
            if (text == null) return;
            WorldMultiLanguageText languageText = text.GetComponent<WorldMultiLanguageText>();
            if (languageText != null) languageText.myKey = string.Empty;
            text.text = value ?? string.Empty;
        }

        private static TMP_Text TextComponent(Transform node) => node == null ? null
            : node.GetComponent<TMP_Text>() ?? node.GetComponentInChildren<TMP_Text>(true);

        private Transform Node(string path) => root.transform.Find(path)
            ?? throw new InvalidOperationException("Original roadblock hierarchy is missing: " + path);

        private static Button RequireButton(Transform node, string name)
        {
            Button button = node == null ? null : node.GetComponent<Button>()
                ?? node.GetComponentInChildren<Button>(true);
            return button ?? throw new InvalidOperationException("Original roadblock button is missing: " + name);
        }

        private static void SetActive(Transform node, bool active)
        {
            if (node != null) node.gameObject.SetActive(active);
        }

        private static void SetActive(Button button, bool active)
        {
            if (button != null) button.gameObject.SetActive(active);
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            if (rulesWindow != null) DestroyRoot(rulesWindow);
            popup.Hide(() => DestroyRoot(root));
        }

        public void Dispose()
        {
            if (closed) return;
            closed = true;
            if (rulesWindow != null) DestroyRoot(rulesWindow);
            DestroyRoot(root);
        }

        private static void DestroyRoot(GameObject instance)
        {
            if (instance == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(instance);
            else UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}

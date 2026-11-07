using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Original native invitation window with host-owned eligibility and selection.</summary>
    public sealed class OfficialBattleAssistSelectionView : IDisposable, LoopScrollDataSource
    {
        private static readonly HashSet<int> PortraitNamePlaceholderIds = new HashSet<int>
            { 11015, 11016, 11017, 11018, 11019, 11020, 11021, 11022 };
        public sealed class Candidate
        {
            public int Id;
            public int FriendshipLevel = 1;
            public string CombatRating;
            public string PropertyValue;
            public long PropertyAmount;
            public Sprite PropertyIcon;
            public string Name, Reason;
            public Sprite Portrait;
            public bool Available, Selected, FriendshipBlocked;
        }
        [Serializable] private sealed class Texts { public string manifest; public Text[] texts; }
        [Serializable] private sealed class Text { public string key, value; }
        private readonly TMP_FontAsset font;
        private readonly Dictionary<string, string> texts;
        private readonly Action<int, bool> select;
        private readonly LoopVerticalScrollRect scroll;
        private readonly GameObject rowTemplate;
        private IReadOnlyList<Candidate> candidates;
        private int maximum;
        private bool disposed;
        private readonly WindowAdapter windows;
        private readonly WorldMultiLanguageText selectedProperty;
        private readonly string propertyName;
        private readonly long playerProperty;
        private readonly Button confirmation;
        private sealed class WindowAdapter : IProfessionWindowService
        {
            private readonly Action cancel;
            private readonly HashSet<Action> destroyed = new HashSet<Action>();
            internal WindowAdapter(Action cancel) => this.cancel = cancel;
            public void CloseLast() => cancel?.Invoke();
            public void Close(GameObject window) => cancel?.Invoke();
            public void ObserveDestroyed(WorldPopupWindow window, Action callback) => destroyed.Add(callback);
            public void StopObservingDestroyed(WorldPopupWindow window, Action callback) => destroyed.Remove(callback);
            internal void Release() { foreach (Action callback in destroyed.ToArray()) callback(); destroyed.Clear(); }
        }
        public GameObject Root { get; }

        public OfficialBattleAssistSelectionView(Transform parent, IReadOnlyList<Candidate> candidates,
            int maximum, Action<int, bool> select, Action confirm, Action cancel,
            string propertyName = null, long playerProperty = 0)
        {
            this.propertyName = propertyName;
            this.playerProperty = playerProperty;
            this.select = select ?? throw new ArgumentNullException(nameof(select));
            windows = new WindowAdapter(cancel);
            font = Resources.Load<TMP_FontAsset>("Combat/FZY4K-Dynamic");
            var data = Resources.Load<TextAsset>("Map12/Team/invite-texts");
            var prefab = Resources.Load<GameObject>("Map12/Team/TeamInvitePanel");
            var row = Resources.Load<GameObject>("Map12/Team/TeamInvitePanelRobotItem");
            if (font == null || data == null || prefab == null || row == null)
                throw new InvalidOperationException("Original native battle invitation assets are missing.");
            Texts original = JsonUtility.FromJson<Texts>(data.text);
            if (original?.manifest != "871_156598" || original.texts == null)
                throw new InvalidOperationException("Original invitation text identity differs.");
            texts = original.texts.ToDictionary(value => value.key, value => value.value);
            var staging = new GameObject("Battle invitation staging", typeof(RectTransform));
            staging.transform.SetParent(parent, false); staging.SetActive(false);
            try
            {
                Root = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
                Root.name = "BattleAssistSelection";
                Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
                foreach (Canvas canvas in Root.GetComponentsInChildren<Canvas>(true))
                    canvas.worldCamera = parentCanvas == null ? null : parentCanvas.rootCanvas.worldCamera;
                if (propertyName != null)
                    selectedProperty = Root.GetComponentInChildren<TeamInviteRobotPanel>(true)
                        .SelectedPlayerPropValueType;
                RemoveNetworkLogic(Root);
                foreach (CanvasGroup group in Root.GetComponentsInChildren<CanvasGroup>(true))
                { group.interactable = true; group.blocksRaycasts = true; }
                Localize(Root);
                Transform pages = Node("BgPanel1/Content/MiddleGo");
                foreach (string name in new[] { "ChallengeGo", "FriendOnlineGo", "CompanionGo", "RoomOnlineGo" })
                    pages.Find(name).gameObject.SetActive(false);
                Transform robot = pages.Find("FriendRobotGo"); robot.gameObject.SetActive(true);
                Node("BgPanel1/Content/BottomGo/TabGroup_11").gameObject.SetActive(false);
                foreach (WorldMultiLanguageText text in robot.GetComponentsInChildren<WorldMultiLanguageText>(true))
                {
                    if (text.myKey != "teaminvitepanel_no_friend") continue;
                    Transform empty = text.transform;
                    while (empty.parent != null && empty != robot && !empty.name.StartsWith("EmptyTip", StringComparison.Ordinal)) empty = empty.parent;
                    if (empty.name.StartsWith("EmptyTip", StringComparison.Ordinal)) empty.gameObject.SetActive(false);
                }
                Transform list = Node("BgPanel1/Content/MiddleGo/FriendRobotGo/Scroll");
                scroll = list.GetComponent<LoopVerticalScrollRect>();
                if (scroll == null) throw new InvalidOperationException("Original invitation virtual list is missing.");
                rowTemplate = UnityEngine.Object.Instantiate(row, Root.transform, false);
                RemoveNetworkLogic(rowTemplate); rowTemplate.SetActive(false);
                scroll.content = list.Find("Viewport/Content") as RectTransform;
                scroll.viewport = list.Find("Viewport") as RectTransform;
                scroll.DefaultPrefabSource = new LoopScrollSinglePrefabSource { Prefab = rowTemplate };
                scroll.dataSource = this;
                confirmation = Node("BgPanel1/Content/MiddleGo/FriendRobotGo/BottomGo")
                    .GetComponentsInChildren<Button>(true).First(value => value.name == "CommonBtn_12");
                confirmation.onClick.RemoveAllListeners(); confirmation.onClick.AddListener(() => confirm?.Invoke());
                Button close = Root.GetComponentsInChildren<Button>(true).First(value => value.name == "CloseBtn_dark");
                close.onClick.RemoveAllListeners();
                foreach (CloseWindowBtn binding in Root.GetComponentsInChildren<CloseWindowBtn>(true)) binding.Init(windows);
                Root.GetComponent<WorldPopupWindow>().SetMaskClickAction(cancel);
                this.candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
                this.maximum = maximum;
                scroll.totalCount = candidates.Count;
                RefreshCount();
                Root.transform.SetParent(parent, false); Root.transform.SetAsLastSibling();
                WorldPopupWindow popup = Root.GetComponent<WorldPopupWindow>();
                if (parentCanvas != null)
                {
                    Canvas hostCanvas = parentCanvas.rootCanvas;
                    int order = hostCanvas.sortingOrder;
                    foreach (Canvas existing in hostCanvas.GetComponentsInChildren<Canvas>(true))
                        if (existing != Root.GetComponent<Canvas>() && existing.overrideSorting)
                            order = Math.Max(order, existing.sortingOrder);
                    popup.SetCanvasSortingOrder(order + 1);
                }
                popup.Show();
                Canvas.ForceUpdateCanvases(); scroll.RefillCells();
            }
            catch
            {
                if (Root != null) OfficialBattleSceneBindings.DestroyCreatedObject(Root);
                throw;
            }
            finally { OfficialBattleSceneBindings.DestroyCreatedObject(staging); }
        }

        public void Refresh(IReadOnlyList<Candidate> values, int maximum)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleAssistSelectionView));
            if (values == null) throw new ArgumentNullException(nameof(values));
            bool countChanged = candidates.Count != values.Count;
            candidates = values; this.maximum = maximum;
            scroll.totalCount = values.Count; RefreshCount();
            if (countChanged) scroll.RefillCells(); else scroll.RefreshCells();
        }
        private void RefreshCount()
        {
            int count = candidates.Count(value => value.Selected);
            foreach (WorldMultiLanguageText text in Root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                if (text.myKey == "ui_team_robot_selected_count_text") text.SetKey(text.myKey, count, maximum);
            if (selectedProperty != null)
            {
                selectedProperty.gameObject.SetActive(true);
                selectedProperty.SetKey(selectedProperty.myKey, propertyName,
                    checked(playerProperty + candidates.Where(value => value.Selected).Sum(value => value.PropertyAmount)));
                confirmation.interactable = count > 0;
            }
        }
        public void ProvideData(Transform cell, int index)
        {
            if (index < 0 || index >= candidates.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Candidate value = candidates[index]; GameObject row = cell.gameObject;
            row.name = "Npc_" + value.Id; RemoveNetworkLogic(row); Localize(row);
            TMP_Text candidateName = null;
            foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "NameTxt") { text.text = value.Name; candidateName = text; }
                if (text.name == "TipText") text.text = value.Reason ?? string.Empty;
            }
            foreach (WorldMultiLanguageText text in row.GetComponentsInChildren<WorldMultiLanguageText>(true))
            {
                if (text.name == "LevelText") text.SetKey("{0}", value.FriendshipLevel);
                if (text.name == "TxtCombatRating") text.SetKey("{0}", value.CombatRating ?? string.Empty);
            }
            // The original robot item initializes friendship gain to zero, hiding its gain row.
            SetActive(row, "AddExpGo", false);
            if (propertyName != null)
            {
                row.GetComponentInChildren<TeamInvitePlayerInfoView>(true)
                    .SetProp(value.PropertyIcon, value.PropertyValue);
                SetActive(row, "TxtCombatRating", false);
            }
            Graphic portrait = row.GetComponentsInChildren<Graphic>(true).FirstOrDefault(item => item.name == "HeadImg");
            if (portrait != null)
            {
                portrait.enabled = value.Portrait != null;
                if (portrait is Image image)
                {
                    image.sprite = value.Portrait;
                }
                if (portrait is RawImage raw)
                {
                    if (value.Portrait == null)
                    {
                        raw.texture = null;
                        raw.uvRect = new Rect(0f, 0f, 1f, 1f);
                    }
                    else
                    {
                        raw.texture = value.Portrait.texture; Rect rect = value.Portrait.textureRect;
                        raw.uvRect = new Rect(rect.x / raw.texture.width, rect.y / raw.texture.height,
                            rect.width / raw.texture.width, rect.height / raw.texture.height);
                    }
                }
                if (value.Portrait == null && PortraitNamePlaceholderIds.Contains(value.Id))
                    BindMissingPortraitName(portrait, string.IsNullOrWhiteSpace(value.Name)
                        ? "伙伴 " + value.Id.ToString() : value.Name,
                        candidateName == null ? Color.black : candidateName.color);
                else
                    SetMissingPortraitNameActive(portrait, false);
            }
            SetActive(row, "MyPlayerModel_UI", false);
            SetActive(row, "LimitedGo", !value.Available); SetActive(row, "ImgSelected", value.Selected);
            SetActive(row, "FriendshipLockGo", value.FriendshipBlocked);
            Toggle toggle = row.transform.Find("Content/NormalGo/Option2")?.GetComponent<Toggle>();
            if (toggle == null || toggle.targetGraphic == null)
                throw new InvalidOperationException("Original invitation row selection reference is missing.");
            foreach (Graphic graphic in row.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = graphic == toggle.targetGraphic;
            toggle.SetIsOnWithoutNotify(value.Selected);
            toggle.enabled = true; toggle.interactable = value.Available || value.Selected;
            toggle.onValueChanged.RemoveAllListeners(); toggle.onValueChanged.AddListener(selected => select(value.Id, selected));
        }
        private void BindMissingPortraitName(Graphic portrait, string name, Color nameColor)
        {
            Transform fallbackTransform = portrait.transform.Find("OfflineNpcNameFallback");
            if (fallbackTransform == null)
            {
                var fallbackObject = new GameObject("OfflineNpcNameFallback", typeof(RectTransform),
                    typeof(TextMeshProUGUI));
                RectTransform rect = (RectTransform)fallbackObject.transform;
                rect.SetParent(portrait.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                fallbackTransform = rect;
            }
            fallbackTransform.SetAsLastSibling();
            fallbackTransform.gameObject.SetActive(true);
            TextMeshProUGUI label = fallbackTransform.GetComponent<TextMeshProUGUI>();
            if (label == null) return;
            label.font = font;
            label.fontSharedMaterial = font.material;
            label.text = name;
            label.color = nameColor;
            label.fontSize = 16;
            label.enableAutoSizing = true;
            label.fontSizeMin = 8;
            label.fontSizeMax = 16;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
        }
        private static void SetMissingPortraitNameActive(Graphic portrait, bool active)
        {
            Transform fallback = portrait.transform.Find("OfflineNpcNameFallback");
            if (fallback != null) fallback.gameObject.SetActive(active);
        }
        private void Localize(GameObject root)
        {
            foreach (WorldMultiLanguageText text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
            {
                text.Localizer = key => texts.TryGetValue(key, out string value) ? value : key;
                if (text.myKey != "ui_team_robot_selected_count_text") text.Refresh();
            }
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) { text.font = font; text.fontSharedMaterial = font.material; }
        }
        private Transform Node(string path) => Root.transform.Find(path)
            ?? throw new InvalidOperationException("Original invitation hierarchy is missing: " + path);
        private static void SetActive(GameObject root, string name, bool active)
        {
            Transform node = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(value => value.name == name);
            if (node != null) node.gameObject.SetActive(active);
        }
        private static void RemoveNetworkLogic(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Type type = behaviour.GetType(); bool online = type.FullName == "Zjcs.Official.TeamUI.TeamInvitePanel";
                for (Type current = type; current != null; current = current.BaseType)
                    online |= current.FullName == "Zjcs.Official.TeamUI.TeamInviteSubPanelBase" || current.FullName == "Zjcs.Official.TeamUI.TeamInviteItemBase";
                online |= type.FullName == "Zjcs.Official.TeamUI.PlayerCompanionTag" || type.FullName == "Zjcs.Official.TeamUI.TeamPlayerIconMeHpSliderNormal"
                    || type.FullName == "Zjcs.Official.ProfessionUI.AppearanceUI" || type.FullName == "Zjcs.Official.ProfessionUI.AppearanceUIPartHandler"
                    || type.FullName == "Zjcs.Official.ProfessionUI.AppearanceSkinClip";
                if (online) UnityEngine.Object.DestroyImmediate(behaviour);
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            windows.Release();
            OfficialBattleSceneBindings.DestroyCreatedObject(Root);
        }
    }
}

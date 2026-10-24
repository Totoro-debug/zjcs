using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private const string AstrologicalRootPath = "/AstrologicalDestinyEntryPanel";
        private const string AstrologicalContentPath = AstrologicalRootPath
            + "/BgPanel1/Content/MiddleGo/";
        private const float AstrologicalRowHeight = 240f;

        private sealed class AstrologicalRow
        {
            public HomeUiRenderer Renderer;
            public RectTransform Rect;
        }

        private readonly List<AstrologicalRow> astrologicalRows = new List<AstrologicalRow>();
        private readonly List<HomeUiRenderer> astrologicalPropertyRows = new List<HomeUiRenderer>();
        private HomeUiRenderer astrologicalRenderer;
        private ScrollRect astrologicalScroll;
        private RectTransform astrologicalListContent;
        private HomeUiRenderer astrologicalOverlayRenderer;
        private GameObject astrologicalOverlayRoot;
        private bool astrologicalClosing;

        public void ShowAstrologicalDestiny()
        {
            OfflineAstrologicalService service = host.Astrological;
            int rank = host.Profile.Save.Player.CharacterProperties.TreasurePlaneRank;
            if (service.SeasonForRank(rank) < service.MinimumSeason)
            {
                ShowNotice("当前位阶尚未开放命运星象");
                return;
            }

            OpenDialog("astrological_entry", (renderer, panel) =>
            {
                astrologicalRenderer = renderer;
                Bind(renderer, AstrologicalRootPath + "/BgPanel1/Content/BottomGo/CloseBtn", CloseDialog);
                Bind(renderer, AstrologicalRootPath + "/BgPanel1/Content/BottomGo/Tab/TabGroup/SeasonLadder",
                    () => ShowAstrologicalTab("ladder"));
                Bind(renderer, AstrologicalRootPath + "/BgPanel1/Content/BottomGo/Tab/TabGroup/Contract",
                    () => ShowAstrologicalTab("contract"));
                Bind(renderer, AstrologicalRootPath + "/BgPanel1/Content/BottomGo/Tab/TabGroup/Shop",
                    () => ShowAstrologicalTab("shop"));
                Bind(renderer, AstrologicalRootPath + "/BgPanel1/Content/TopGo/WikiBtn/Btn/Icon",
                    ShowAstrologicalRules);
                Bind(renderer, AstrologicalContentPath + "AstrologicalContractContent/DefendBtn",
                    ShowAstrologicalProperties);
                AddAstrologicalHoldEvents(renderer.Find(
                    AstrologicalContentPath + "AstrologicalContractContent/ChargeButton"));
                AddAstrologicalHoldEvents(renderer.Find(
                    AstrologicalContentPath + "AstrologicalContractContent/ChargeButton/OriginStar/Icon"));

                GameObject shopTab = renderer.Find(AstrologicalRootPath
                    + "/BgPanel1/Content/BottomGo/Tab/TabGroup/Shop");
                if (shopTab != null) shopTab.SetActive(false);
                ConfigureAstrologicalContract(renderer);
                ConfigureAstrologicalSeasonEmptyState(renderer);
                ShowAstrologicalTab("contract");
                RefreshAstrologicalDestiny();
            });
        }

        public void RefreshAstrologicalDestiny()
        {
            if (astrologicalClosing || astrologicalRenderer == null) return;
            OfflineAstrologicalService service = host.Astrological;
            string chargeButton = AstrologicalContentPath + "AstrologicalContractContent/ChargeButton/";
            string progressText = service.IsCharging ? "松开保存" : "长按灌注";
            astrologicalRenderer.SetText(chargeButton + "Content/Text", progressText);
            astrologicalRenderer.SetText(chargeButton + "Content/Text1", progressText);
            astrologicalRenderer.SetActive(chargeButton + "Content/Text1", false);
            astrologicalRenderer.SetText(chargeButton + "OriginStar/Text",
                service.PreviewMaterialCount.ToString());
            astrologicalRenderer.SetText(AstrologicalContentPath + "AstrologicalContractContent/DefendBtn/BgImg/Text",
                "属性详情");
            Sprite materialIcon = host.ItemIcon(61);
            if (materialIcon != null)
                astrologicalRenderer.SetSprite(chargeButton + "OriginStar/Icon", materialIcon);

            long currentCost = service.CurrentCost;
            long previewCost = service.PreviewCost;
            if (astrologicalListContent != null && astrologicalScroll != null)
            {
                float currentPosition = 1f - service.CostProcess(previewCost);
                float visibleLimit = 1f - service.CostProcess(service.MaximumVisibleCost(previewCost));
                astrologicalScroll.verticalNormalizedPosition = Mathf.Max(currentPosition, visibleLimit);
                RenderAstrologicalRows(currentCost, previewCost);
            }
        }

        private void ShowAstrologicalTab(string tab)
        {
            if (astrologicalRenderer == null) return;
            string season = AstrologicalContentPath + "AstrologicalSeasonLadderContent";
            string contract = AstrologicalContentPath + "AstrologicalContractContent";
            string shop = AstrologicalContentPath + "ShopContent";
            astrologicalRenderer.SetActive(season, tab == "ladder");
            astrologicalRenderer.SetActive(contract, tab == "contract");
            astrologicalRenderer.SetActive(shop, tab == "shop");
            string tabPath = AstrologicalRootPath + "/BgPanel1/Content/BottomGo/Tab/TabGroup/";
            Toggle ladderToggle = astrologicalRenderer.Find<Toggle>(tabPath + "SeasonLadder");
            Toggle contractToggle = astrologicalRenderer.Find<Toggle>(tabPath + "Contract");
            if (ladderToggle != null) ladderToggle.isOn = tab == "ladder";
            if (contractToggle != null) contractToggle.isOn = tab == "contract";
            if (tab == "shop") ShowNotice("星命商店尚无本地货架数据");
        }

        private void ConfigureAstrologicalContract(HomeUiRenderer renderer)
        {
            astrologicalScroll = renderer.Find<ScrollRect>(AstrologicalContentPath
                + "AstrologicalContractContent/Ladder");
            astrologicalListContent = renderer.Find<RectTransform>(AstrologicalContentPath
                + "AstrologicalContractContent/Ladder/Viewport/Content");
            RectTransform viewport = renderer.Find<RectTransform>(AstrologicalContentPath
                + "AstrologicalContractContent/Ladder/Viewport");
            if (astrologicalScroll == null || astrologicalListContent == null || viewport == null)
                throw new InvalidOperationException("The official astrological contract list is incomplete.");

            foreach (VerticalLayoutGroup layout in astrologicalListContent.GetComponents<VerticalLayoutGroup>())
                layout.enabled = false;
            ContentSizeFitter fitter = astrologicalListContent.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            astrologicalListContent.sizeDelta = new Vector2(astrologicalListContent.sizeDelta.x,
                AstrologicalRowHeight * host.Astrological.LevelCount);
            astrologicalScroll.onValueChanged.AddListener(OnAstrologicalScrollChanged);

            float viewportHeight = viewport.rect.height;
            int rowCount = Mathf.Clamp(Mathf.CeilToInt(viewportHeight / AstrologicalRowHeight) + 2, 8, 14);
            for (int index = 0; index < rowCount; index++)
            {
                HomeUiRenderer rowRenderer = Renderer();
                GameObject row = rowRenderer.Create("astrological_contract_tag", astrologicalListContent);
                RectTransform rect = (RectTransform)row.transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, AstrologicalRowHeight);
                astrologicalRows.Add(new AstrologicalRow { Renderer = rowRenderer, Rect = rect });
            }
            Canvas.ForceUpdateCanvases();
        }

        private void ConfigureAstrologicalSeasonEmptyState(HomeUiRenderer renderer)
        {
            RectTransform viewport = renderer.Find<RectTransform>(AstrologicalContentPath
                + "AstrologicalSeasonLadderContent/Viewport");
            TMP_Text reference = renderer.Find<TMP_Text>(AstrologicalRootPath
                + "/BgPanel1/Content/TopGo/PanelTitle/TitleGo/TitleTxt");
            if (viewport == null || reference == null) return;
            GameObject empty = new GameObject("LocalSeasonRecordEmpty", typeof(RectTransform),
                typeof(TextMeshProUGUI));
            empty.transform.SetParent(viewport, false);
            RectTransform rect = (RectTransform)empty.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(32f, 32f);
            rect.offsetMax = new Vector2(-32f, -32f);
            TextMeshProUGUI text = empty.GetComponent<TextMeshProUGUI>();
            text.font = reference.font;
            text.fontSize = 32f;
            text.color = new Color(0.88f, 0.88f, 0.88f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            text.text = "暂无本地赛季记录";
        }

        private void OnAstrologicalScrollChanged(Vector2 _)
        {
            if (astrologicalRenderer == null) return;
            OfflineAstrologicalService service = host.Astrological;
            long previewCost = service.PreviewCost;
            float visibleLimit = 1f - service.CostProcess(service.MaximumVisibleCost(previewCost));
            if (astrologicalScroll.verticalNormalizedPosition < visibleLimit)
                astrologicalScroll.verticalNormalizedPosition = visibleLimit;
            RenderAstrologicalRows(service.CurrentCost, previewCost);
        }

        private void RenderAstrologicalRows(long currentCost, long previewCost)
        {
            if (astrologicalListContent == null || astrologicalRows.Count == 0) return;
            int lastStart = Math.Max(0, host.Astrological.LevelCount - astrologicalRows.Count);
            float viewportHeight = astrologicalScroll.viewport == null
                ? 0f : astrologicalScroll.viewport.rect.height;
            float scrollableHeight = Mathf.Max(0f, astrologicalListContent.rect.height - viewportHeight);
            float topOffset = (1f - Mathf.Clamp01(astrologicalScroll.verticalNormalizedPosition)) * scrollableHeight;
            int firstIndex = Mathf.Clamp(Mathf.FloorToInt(topOffset / AstrologicalRowHeight), 0, lastStart);
            for (int slot = 0; slot < astrologicalRows.Count; slot++)
            {
                int index = firstIndex + slot;
                int level = host.Astrological.LevelCount - index;
                AstrologicalRow row = astrologicalRows[slot];
                row.Rect.anchoredPosition = new Vector2(0f, -index * AstrologicalRowHeight);
                RefreshAstrologicalRow(row.Renderer, level, currentCost, previewCost);
            }
        }

        private void RefreshAstrologicalRow(HomeUiRenderer renderer, int level, long currentCost, long previewCost)
        {
            OfflineAstrologicalService service = host.Astrological;
            string root = "/AstrologicalContractTag/";
            long target = service.TotalCostForLevel(level);
            long before = service.TotalCostForLevel(level - 1);
            long required = service.MaterialCostForLevel(level);
            IReadOnlyDictionary<int, long> properties = service.LevelAttributes(level);
            KeyValuePair<int, long> first = properties.FirstOrDefault();
            OfficialPropDefinition definition = service.Property(first.Key);
            renderer.SetText(root + "Tag/Text", definition == null
                ? string.Empty : definition.Name + " " + OfficialPropFormat.Signed(definition, first.Value));
            renderer.SetText(root + "SourceStarCost Text", target.ToString());

            long nextCost = service.MaterialCostForLevel(level + 1);
            if (nextCost <= 0) nextCost = required;
            bool isTop = level == service.LevelCount;
            float currentProgress;
            bool showCurrentTag;
            if (currentCost > target && !isTop)
            {
                currentProgress = Mathf.Clamp01((float)(currentCost - target) / nextCost * 0.5f + 0.5f);
                showCurrentTag = currentCost < target + nextCost * 0.5f;
            }
            else
            {
                float progress = (float)(currentCost - before) / required * (isTop ? 1f : 0.5f);
                currentProgress = Mathf.Clamp01(progress);
                showCurrentTag = progress >= 0f;
            }
            bool previewAtMax = previewCost >= service.MaximumCost;
            float previewProgress;
            bool showPreviewTag;
            if (previewCost > target && !previewAtMax)
            {
                previewProgress = Mathf.Clamp01((float)(previewCost - target) / nextCost * 0.5f + 0.5f);
                showPreviewTag = previewCost < target + nextCost * 0.5f;
            }
            else
            {
                float progress = (float)(previewCost - before) / required * (previewAtMax ? 1f : 0.5f);
                previewProgress = Mathf.Clamp01(progress);
                showPreviewTag = previewAtMax ? target >= service.MaximumCost : progress >= 0f;
            }
            SetAstrologicalFill(renderer, root + "MiddleMask/Slider/SliderImg", currentProgress);
            SetAstrologicalFill(renderer, root + "MiddleMask/PreviewSlider/SliderImg", previewProgress);
            SetAstrologicalFill(renderer, root + "MiddleMask/EffectSlider/SliderImg", currentProgress);
            bool reached = currentCost >= target;
            renderer.SetActive(root + "Tag/BgImgMask", !reached);
            renderer.SetActive(root + "DividerLine/ActiveImg", reached);
            renderer.SetActive(root + "DividerLine/DeactiveImg", !reached);
            renderer.SetActive(root + "DividerLine", !isTop);
            renderer.SetActive(root + "StarIcon", target == required);
            renderer.SetActive(root + "PreviewGo", showPreviewTag);
            renderer.SetText(root + "PreviewGo/PreviewTag/Text", previewCost.ToString());
            Sprite materialIcon = host.ItemIcon(61);
            if (materialIcon != null)
            {
                renderer.SetSprite(root + "PreviewGo/PreviewTag/IconImage", materialIcon);
                renderer.SetSprite(root + "FloatingTag/IconImage", materialIcon);
            }
            renderer.SetActive(root + "FloatingTag", showCurrentTag);
            renderer.SetText(root + "FloatingTag/Text", currentCost.ToString());
            float rowHeight = ((RectTransform)renderer.Find("/AstrologicalContractTag").transform).rect.height;
            SetAstrologicalMarker(renderer, root + "FloatingTag", currentProgress, rowHeight, isTop);
            SetAstrologicalMarker(renderer, root + "PreviewGo/PreviewTag", previewProgress, rowHeight, previewAtMax);
            if (definition != null)
            {
                HomeAstrologicalTagArt art = catalog.AstrologicalTagArt(first.Key);
                if (art != null)
                {
                    Sprite icon = catalog.FindSprite(art.icon);
                    if (icon != null) renderer.SetSprite(root + "Tag/IconImg", icon);
                    Image background = renderer.Find<Image>(root + "Tag/ColorfulBgImg");
                    if (background != null && art.color != null && art.color.Length >= 4)
                        background.color = new Color(art.color[0], art.color[1], art.color[2], art.color[3]);
                }
            }
        }

        private static void SetAstrologicalFill(HomeUiRenderer renderer, string path, float amount)
        {
            Image image = renderer.Find<Image>(path);
            if (image != null) image.fillAmount = Mathf.Clamp01(amount);
        }

        private static void SetAstrologicalMarker(HomeUiRenderer renderer, string path,
            float progress, float rowHeight, bool isTop)
        {
            RectTransform rect = renderer.Find<RectTransform>(path);
            if (rect == null) return;
            Vector2 position = rect.anchoredPosition;
            position.y = Mathf.Lerp(-rowHeight * 0.5f,
                rowHeight * (isTop ? 0.5f : 1.5f), Mathf.Clamp01(progress));
            rect.anchoredPosition = position;
        }

        private void ShowAstrologicalProperties()
        {
            ShowAstrologicalOverlay("astrological_props", renderer =>
            {
                IReadOnlyDictionary<int, long> properties = host.Astrological.AttributesAtCost(
                    host.Astrological.CurrentCost);
                const string baseSlotPath = "/AstrologicalDestinyDetailPropsDialog/BgDialog1/Content/OverallContent/Scroll/Viewprot/Content/BasePropSlot";
                const string additionSlotPath = "/AstrologicalDestinyDetailPropsDialog/BgDialog1/Content/OverallContent/Scroll/Viewprot/Content/AdditionSlot";
                Transform baseSlot = renderer.Find(baseSlotPath)?.transform;
                Transform additionSlot = renderer.Find(additionSlotPath)?.transform;
                int baseCount = 0;
                int additionCount = 0;
                foreach (KeyValuePair<int, long> property in properties.OrderBy(value => value.Key))
                {
                    OfficialPropDefinition definition = host.Astrological.Property(property.Key);
                    if (definition == null || property.Value == 0) continue;
                    bool isBase = definition.Catalog == (int)OfficialPropCatalogType.Base;
                    Transform parent = isBase ? baseSlot : additionSlot;
                    if (parent == null) continue;
                    HomeUiRenderer rowRenderer = Renderer();
                    rowRenderer.Create("character_prop_row", parent);
                    rowRenderer.SetText("/PlayerPropItem1/Key", definition.Name ?? string.Empty);
                    rowRenderer.SetText("/PlayerPropItem1/Text", OfficialPropFormat.Signed(definition, property.Value));
                    astrologicalPropertyRows.Add(rowRenderer);
                    if (isBase) baseCount++; else additionCount++;
                }
                renderer.SetActive("/AstrologicalDestinyDetailPropsDialog/BgDialog1/Content/NonePropGo",
                    baseCount + additionCount == 0);
                renderer.SetActive(baseSlotPath, baseCount > 0);
                renderer.SetActive(additionSlotPath, additionCount > 0);
                RectTransform content = renderer.Find<RectTransform>(
                    "/AstrologicalDestinyDetailPropsDialog/BgDialog1/Content/OverallContent/Scroll/Viewprot/Content");
                if (content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            });
        }

        private void ShowAstrologicalRules()
        {
            ShowAstrologicalOverlay("astrological_rule", null);
        }

        private void ShowAstrologicalOverlay(string panelKey, Action<HomeUiRenderer> configure)
        {
            CloseAstrologicalOverlay();
            if (activeDialog == null) return;
            astrologicalOverlayRoot = new GameObject("AstrologicalOverlay", typeof(RectTransform));
            astrologicalOverlayRoot.transform.SetParent(activeDialog.transform, false);
            RectTransform rootRect = (RectTransform)astrologicalOverlayRoot.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdrop.transform.SetParent(astrologicalOverlayRoot.transform, false);
            RectTransform backdropRect = (RectTransform)backdrop.transform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            backdrop.GetComponent<Button>().onClick.AddListener(CloseAstrologicalOverlay);

            astrologicalOverlayRenderer = Renderer();
            astrologicalOverlayRenderer.Create(panelKey, astrologicalOverlayRoot.transform);
            configure?.Invoke(astrologicalOverlayRenderer);
        }

        private void CloseAstrologicalOverlay()
        {
            if (astrologicalOverlayRoot != null) UnityEngine.Object.Destroy(astrologicalOverlayRoot);
            astrologicalOverlayRoot = null;
            if (astrologicalOverlayRenderer != null) owned.Remove(astrologicalOverlayRenderer);
            astrologicalOverlayRenderer = null;
            foreach (HomeUiRenderer row in astrologicalPropertyRows) owned.Remove(row);
            astrologicalPropertyRows.Clear();
        }

        private void AddAstrologicalHoldEvents(GameObject target)
        {
            if (target == null) return;
            EventTrigger trigger = target.GetComponent<EventTrigger>();
            if (trigger == null) trigger = target.AddComponent<EventTrigger>();
            trigger.triggers ??= new List<EventTrigger.Entry>();
            AddAstrologicalTrigger(trigger, EventTriggerType.PointerDown,
                _ => { host.BeginAstrologicalCharge(); });
            AddAstrologicalTrigger(trigger, EventTriggerType.PointerUp,
                _ => { host.EndAstrologicalCharge(); });
        }

        private static void AddAstrologicalTrigger(EventTrigger trigger, EventTriggerType type,
            Action<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data => action(data));
            trigger.triggers.Add(entry);
        }

        private void ClearAstrologicalRows()
        {
            foreach (AstrologicalRow row in astrologicalRows)
            {
                if (row.Rect != null) UnityEngine.Object.Destroy(row.Rect.gameObject);
                owned.Remove(row.Renderer);
            }
            astrologicalRows.Clear();
            if (astrologicalScroll != null)
                astrologicalScroll.onValueChanged.RemoveListener(OnAstrologicalScrollChanged);
            astrologicalRenderer = null;
            astrologicalScroll = null;
            astrologicalListContent = null;
            CloseAstrologicalOverlay();
        }
    }
}

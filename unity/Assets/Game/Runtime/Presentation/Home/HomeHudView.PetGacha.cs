using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private const string PetShellPrefix = "/PetGachaPanel/BgPanel1/Content/MiddleGo/";
        private const string PetDetailPrefix = "/GachaDetailDialog/BgDialog1/Content/";
        private readonly List<PetAchievementRow> petVirtualRows = new List<PetAchievementRow>();
        private ScrollRect petVirtualScroll;
        private RectTransform petVirtualContent;
        private HomeUiNode petVirtualTemplate;
        private int petVirtualGroup;
        private int petVirtualTotal;
        private int petVirtualFirstVisible = -1;
        private float petVirtualRowHeight;
        private HomeUiRenderer petScoreRenderer;
        private readonly Queue<int> petNewPetQueue = new Queue<int>();
        private GameObject petNewPetLayer;
        private GameObject petSkillEntryLayer;
        private GameObject petPerformanceLayer;
        private Button petNewPetBackdrop;
        private Button petNewPetCloseButton;
        private float petNewPetCloseAt;

        private sealed class PetAchievementRow
        {
            public HomeUiRenderer Renderer;
            public GameObject Root;
        }

        private void ShowPetConfirm(string title, string message, bool offerReminderSuppression,
            Action<bool> confirm)
        {
            petHundredSuppressChoice = false;
            HomeUiRenderer renderer = CreatePetLayer("pet_gacha_confirm", out petConfirmLayer,
                () => ClosePetLayer(ref petConfirmLayer));
            renderer.SetText("/ConfirmDialog/BgDialog1/BgImg/BgTitle/TitleText", title);
            renderer.SetText("/ConfirmDialog/BgDialog1/Content/TextContent/ContentTxt", message);
            renderer.SetText("/ConfirmDialog/BgDialog1/Content/TextContent/HintTxt", string.Empty);
            Bind(renderer, "/ConfirmDialog/BgDialog1/Content/DialogButtonGroup/ButtonsGo/CancelBtn",
                () => ClosePetLayer(ref petConfirmLayer));
            Bind(renderer, "/ConfirmDialog/BgDialog1/Content/DialogButtonGroup/ButtonsGo/ConfirmBtn", () =>
            {
                bool suppress = offerReminderSuppression && petHundredSuppressChoice;
                ClosePetLayer(ref petConfirmLayer);
                confirm?.Invoke(suppress);
            });
            if (offerReminderSuppression) AddPetReminderToggle(renderer);
            BindAllNamed(renderer, "CloseBtn", () => ClosePetLayer(ref petConfirmLayer));
        }

        private void AddPetReminderToggle(HomeUiRenderer renderer)
        {
            Transform parent = renderer.Find("/ConfirmDialog/BgDialog1/Content/AdditonContent")?.transform;
            if (parent == null) return;
            parent.gameObject.SetActive(true);
            var row = new GameObject("HundredDrawReminderToggle", typeof(RectTransform), typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = (RectTransform)row.transform;
            rowRect.sizeDelta = new Vector2(520f, 48f);
            LayoutElement rowElement = row.GetComponent<LayoutElement>();
            rowElement.preferredHeight = 48f;
            rowElement.flexibleWidth = 1f;
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;

            var check = new GameObject("Check", typeof(RectTransform), typeof(Image), typeof(Toggle),
                typeof(LayoutElement));
            check.transform.SetParent(row.transform, false);
            RectTransform checkRect = (RectTransform)check.transform;
            checkRect.sizeDelta = new Vector2(38f, 38f);
            LayoutElement checkElement = check.GetComponent<LayoutElement>();
            checkElement.preferredWidth = 38f;
            checkElement.preferredHeight = 38f;
            Image background = check.GetComponent<Image>();
            Sprite normal = catalog.FindSprite("BtnCheck_Nor-3b5c5a72eb.png");
            Sprite selected = catalog.FindSprite("BtnCheck_Sel-cbe9e7c698.png");
            background.sprite = normal;
            background.preserveAspect = true;
            background.raycastTarget = true;
            var markObject = new GameObject("Selected", typeof(RectTransform), typeof(Image));
            markObject.transform.SetParent(check.transform, false);
            RectTransform markRect = (RectTransform)markObject.transform;
            markRect.anchorMin = Vector2.zero;
            markRect.anchorMax = Vector2.one;
            markRect.offsetMin = Vector2.zero;
            markRect.offsetMax = Vector2.zero;
            Image mark = markObject.GetComponent<Image>();
            mark.sprite = selected;
            mark.preserveAspect = true;
            mark.raycastTarget = false;
            Toggle toggle = check.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = mark;
            toggle.isOn = false;
            toggle.onValueChanged.AddListener(value => petHundredSuppressChoice = value);

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI),
                typeof(LayoutElement));
            textObject.transform.SetParent(row.transform, false);
            LayoutElement textElement = textObject.GetComponent<LayoutElement>();
            textElement.preferredWidth = 390f;
            textElement.preferredHeight = 42f;
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.font = catalog.Font;
            text.fontSize = 23f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.text = "今日不再提示百次结缘确认";
            text.raycastTarget = false;
        }

        private HomeUiRenderer CreatePetLayer(string panelKey, out GameObject layer, Action close,
            float backdropAlpha = 0.58f)
        {
            Transform parent = activeDialog == null ? canvas.transform : activeDialog.transform;
            layer = new GameObject("PetGachaLayer_" + panelKey, typeof(RectTransform), typeof(Image), typeof(Button));
            layer.transform.SetParent(parent, false);
            RectTransform rootRect = (RectTransform)layer.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Image backdrop = layer.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, Mathf.Clamp01(backdropAlpha));
            backdrop.raycastTarget = true;
            Button blocker = layer.GetComponent<Button>();
            blocker.transition = Selectable.Transition.None;
            blocker.onClick.AddListener(() => close?.Invoke());

            var renderer = new HomeUiRenderer(catalog);
            GameObject panel = renderer.Create(panelKey, layer.transform, null);
            RectTransform panelRect = (RectTransform)panel.transform;
            if (StretchesAcrossParent(catalog.Panel(panelKey)?.root))
            {
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                panelRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;
            }
            layer.transform.SetAsLastSibling();
            return renderer;
        }

        private void PopulatePetGachaResult(HomeUiRenderer renderer,
            IReadOnlyList<OfflinePetGachaAward> awards, int count)
        {
            const string middle = "/GachaResultPanel/BgPanel1/Content/MiddleGo/";
            renderer.SetActive(middle + "OneGrid", count == 1);
            renderer.SetActive(middle + "TenGrid", count == 10);
            renderer.SetActive(middle + "HundredGrid", count == 100);
            if (awards == null || awards.Count != count)
                throw new InvalidOperationException("结缘结果数量与抽取次数不一致。");

            HomeUiPanel panel = catalog.Panel("pet_gacha_result_item");
            if (panel?.root == null)
                throw new InvalidOperationException("官方结缘结果卡模板未载入。");
            HomeUiNode iconTemplate = FindPetUiNode(panel.root, "ItemAwardIconGodGacha");
            if (iconTemplate == null)
                throw new InvalidOperationException("官方结缘结果卡缺少道具图标绑定。");

            if (count == 1)
            {
                GameObject position = renderer.Find(middle + "OneGrid/Pos1");
                if (position != null) CreatePetResultIcon(iconTemplate, position.transform, awards[0]);
                return;
            }

            if (count == 10)
            {
                for (int index = 0; index < awards.Count; index++)
                {
                    GameObject slot = renderer.Find(middle + "TenGrid/Slot_" + index);
                    if (slot == null) throw new InvalidOperationException("官方十连结果格缺失。");
                    CreatePetResultIcon(iconTemplate, slot.transform, awards[index]);
                }
                return;
            }

            GameObject content = renderer.Find(middle + "HundredGrid/View/Content");
            if (content == null) throw new InvalidOperationException("官方百连结果列表缺失。");
            RectTransform contentRect = content.transform as RectTransform;
            for (int index = 0; index < awards.Count; index++)
            {
                HomeUiRenderer cardRenderer = new HomeUiRenderer(catalog);
                GameObject card = cardRenderer.CreateNode(panel.root, content.transform);
                card.name = "PetGachaResult_" + index.ToString("D3", CultureInfo.InvariantCulture);
                FillPetResultIcon(cardRenderer, awards[index]);
            }
            if (contentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        private void CreatePetResultIcon(HomeUiNode template, Transform parent,
            OfflinePetGachaAward award)
        {
            HomeUiRenderer rowRenderer = new HomeUiRenderer(catalog);
            GameObject iconRoot = rowRenderer.CreateNode(template, parent);
            RectTransform iconRect = iconRoot.transform as RectTransform;
            RectTransform parentRect = parent as RectTransform;
            if (iconRect != null && parentRect != null)
            {
                float parentWidth = Mathf.Max(parentRect.rect.width, parentRect.sizeDelta.x);
                if (parentWidth > 0f && iconRect.sizeDelta.x > 0f)
                    iconRect.localScale = Vector3.one * Mathf.Min(1f, parentWidth / iconRect.sizeDelta.x);
            }
            FillPetResultIcon(rowRenderer, award);
        }

        private void FillPetResultIcon(HomeUiRenderer renderer, OfflinePetGachaAward award)
        {
            PetGachaItem item = award == null ? null : host.PetGachaCatalog.Item(award.ItemId);
            Sprite sprite = item == null ? null : catalog.FindSprite(item.IconFile);
            if (sprite == null) throw new InvalidOperationException("结缘奖励缺少官方图标。");

            const string iconPath = "/PetGachaResultItem/Content/ItemAwardIconGodGacha";
            GameObject iconRoot = renderer.Find(iconPath);
            GameObject anchor = renderer.Find(iconPath + "/Anchor");
            if (iconRoot == null || anchor == null) return;

            var imageObject = new GameObject("RewardIcon", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(anchor.transform, false);
            RectTransform imageRect = (RectTransform)imageObject.transform;
            imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = new Vector2(0f, 12f);
            imageRect.sizeDelta = new Vector2(136f, 136f);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            TMP_Text style = renderer.Find<TMP_Text>(iconPath + "/AgioObj/AgiotagePiece/CountText");
            CreatePetResultLabel(iconRoot.transform, "ItemName", item.Name, style,
                new Vector2(4f, 28f), TextAlignmentOptions.Bottom);

            bool converted = award.ConvertedItemId > 0 && award.ConvertedCount > 0;
            GameObject agioObject = renderer.Find(iconPath + "/AgioObj");
            if (agioObject != null) agioObject.SetActive(converted);
            if (converted)
            {
                PetGachaItem convertedItem = host.PetGachaCatalog.Item(award.ConvertedItemId);
                Sprite convertedSprite = convertedItem == null ? null : catalog.FindSprite(convertedItem.IconFile);
                if (convertedSprite == null) throw new InvalidOperationException("重复幻兽缺少官方碎片图标。");
                renderer.SetSprite(iconPath + "/AgioObj/AgiotagePiece/Icon/Mask/IconImage", sprite);
                renderer.SetText(iconPath + "/AgioObj/AgiotagePiece/CountText", award.Count.ToString(CultureInfo.InvariantCulture));
                renderer.SetSprite(iconPath + "/AgioObj/RecyclePiece/IconImage", convertedSprite);
                renderer.SetText(iconPath + "/AgioObj/RecyclePiece/CountText", award.ConvertedCount.ToString(CultureInfo.InvariantCulture));
                renderer.Find(iconPath + "/AgioObj/AgiotagePiece")?.SetActive(true);
                renderer.Find(iconPath + "/AgioObj/RecyclePiece")?.SetActive(true);
            }
            else if (award.Count > 1)
            {
                CreatePetResultLabel(iconRoot.transform, "ItemCount",
                    "×" + award.Count.ToString(CultureInfo.InvariantCulture), style,
                    new Vector2(72f, 28f), TextAlignmentOptions.BottomRight);
            }
            anchor.transform.SetAsLastSibling();
            if (converted && agioObject != null) agioObject.transform.SetAsLastSibling();
        }

        private void CreatePetResultLabel(Transform parent, string name, string value, TMP_Text style,
            Vector2 size, TextAlignmentOptions alignment)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)labelObject.transform;
            bool count = name == "ItemCount";
            rect.anchorMin = count ? Vector2.one : new Vector2(0.5f, 0f);
            rect.anchorMax = count ? Vector2.one : new Vector2(0.5f, 0f);
            rect.pivot = count ? Vector2.one : new Vector2(0.5f, 0f);
            rect.anchoredPosition = count ? new Vector2(-8f, -4f) : Vector2.zero;
            rect.sizeDelta = size;
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.font = style == null ? catalog.Font : style.font;
            if (style != null) label.fontSharedMaterial = style.fontSharedMaterial;
            label.fontSize = count ? 30f : 22f;
            label.enableAutoSizing = !count;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 22f;
            label.color = Color.white;
            label.alignment = alignment;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.text = value ?? string.Empty;
            label.raycastTarget = false;
        }

        private static HomeUiNode FindPetUiNode(HomeUiNode node, string name)
        {
            if (node == null) return null;
            if (node.name == name) return node;
            foreach (HomeUiNode child in node.children ?? Array.Empty<HomeUiNode>())
            {
                HomeUiNode found = FindPetUiNode(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void ClosePetLayer(ref GameObject layer)
        {
            if (layer != null) UnityEngine.Object.Destroy(layer);
            layer = null;
        }

        private void QueueNewPetIntroductions(IReadOnlyList<OfflinePetGachaAward> awards)
        {
            petNewPetQueue.Clear();
            if (awards == null) return;
            foreach (OfflinePetGachaAward award in awards)
                if (award != null && award.IsPet && award.IsNew && award.Count > 0)
                    petNewPetQueue.Enqueue(award.ItemId);
        }

        private void ShowNextPetIntroduction()
        {
            ClosePetLayer(ref petSkillEntryLayer);
            ClosePetLayer(ref petNewPetLayer);
            petNewPetBackdrop = null;
            petNewPetCloseButton = null;
            petNewPetCloseAt = 0f;
            if (petNewPetQueue.Count == 0) return;

            int petId = petNewPetQueue.Dequeue();
            PetGachaItem item = host.PetGachaCatalog.Item(petId);
            PetGachaPetProfile profile = host.PetGachaCatalog.PetProfile(petId);
            GameObject prefab = profile == null ? null : host.PetGachaAssets.NativeModel(petId, profile.UIModel);
            if (item == null || profile == null || prefab == null)
                throw new InvalidOperationException("新获得幻兽缺少固定版配置或原生模型：" + petId);

            HomeUiRenderer renderer = CreatePetLayer("pet_gacha_new_pet", out petNewPetLayer,
                ShowNextPetIntroduction);
            renderer.SetText("/PetGetNewPanel/BgPanel1/Content/MiddleGo/Content/NameGo/NameText", item.Name);
            renderer.SetSprite("/PetGetNewPanel/BgPanel1/Content/MiddleGo/Content/NameGo/NameText/TypeImg",
                catalog.FindSprite(profile.TypeIconFile));

            const string qualityRoot = "/PetGetNewPanel/BgPanel1/Content/MiddleGo/Content/QualityLight/";
            foreach (string quality in new[] { "White", "Blue", "Purple", "Orange" })
                renderer.SetActive(qualityRoot + "GachaQualityLight" + quality, false);
            string qualityName = item.Quality switch
            {
                4 => "Orange",
                3 => "Purple",
                2 => "Blue",
                _ => "White"
            };
            renderer.SetActive(qualityRoot + "GachaQualityLight" + qualityName, true);

            Transform appearance = renderer.Find(
                "/PetGetNewPanel/BgPanel1/Content/MiddleGo/Content/PetSlot/PetAppearance")?.transform;
            if (appearance == null)
                throw new InvalidOperationException("官方新幻兽面板缺少模型挂点。");
            GameObject model = UnityEngine.Object.Instantiate(prefab, appearance, false);
            model.name = "PetGachaPreview_" + petId.ToString(CultureInfo.InvariantCulture);
            model.transform.localScale = Vector3.one * profile.ModelScale;
            Vector3 position = model.transform.localPosition;
            position.y = profile.ModelOffsetY;
            model.transform.localPosition = position;
            Animator animator = model.GetComponentInChildren<Animator>(true);
            if (animator != null) animator.Play("idle_01", 0, 0f);

            for (int index = 0; index < 3; index++)
            {
                string skillPath = "/PetGetNewPanel/BgPanel1/Content/MiddleGo/Content/SkilIs/ItemIconSkill"
                    + (index == 0 ? string.Empty : " (" + index.ToString(CultureInfo.InvariantCulture) + ")");
                int skillId = profile.SupportSkillIds != null && index < profile.SupportSkillIds.Length
                    ? profile.SupportSkillIds[index] : 0;
                PetGachaItem skill = skillId == 0 ? null : host.PetGachaCatalog.Item(skillId);
                renderer.SetActive(skillPath, skill != null);
                if (skill == null) continue;
                renderer.SetSprite(skillPath + "/Content/mask/icon", catalog.FindSprite(skill.IconFile));
                renderer.SetText(skillPath + "/Content/TopTextGo/Text", skill.Name);
                renderer.SetText(skillPath + "/Content/BgCountText/CountText", string.Empty);
                renderer.SetActive(skillPath + "/Content/QualityImg/SelAnimGo", false);
                Button skillButton = renderer.Find<Button>(skillPath);
                if (skillButton == null)
                    throw new InvalidOperationException("官方新幻兽面板缺少技能选择按钮。");
                int selectedSkillId = skillId;
                int selectedIndex = index;
                skillButton.onClick.AddListener(() => SelectPetIntroductionSkill(
                    renderer, profile, selectedSkillId, selectedIndex));
            }

            SelectPetIntroductionSkill(renderer, profile, profile.SupportSkillIds[0], 0);

            BindAllNamed(renderer, "CloseBtn", ShowNextPetIntroduction);
            petNewPetCloseButton = renderer.Find<Button>(
                "/PetGetNewPanel/BgPanel1/Content/MiddleGo/CloseBtn");
            petNewPetBackdrop = petNewPetLayer.GetComponent<Button>();
            if (petNewPetCloseButton == null || petNewPetBackdrop == null)
                throw new InvalidOperationException("官方新幻兽面板缺少关闭输入绑定。");
            petNewPetCloseButton.interactable = false;
            petNewPetBackdrop.interactable = false;
            petNewPetCloseAt = Time.unscaledTime + 0.5f;
        }

        private void TickPetIntroduction()
        {
            if (petNewPetCloseAt <= 0f || Time.unscaledTime < petNewPetCloseAt) return;
            petNewPetCloseAt = 0f;
            if (petNewPetCloseButton != null) petNewPetCloseButton.interactable = true;
            if (petNewPetBackdrop != null) petNewPetBackdrop.interactable = true;
        }

        private void ClearPetIntroduction()
        {
            ClosePetLayer(ref petSkillEntryLayer);
            ClosePetLayer(ref petNewPetLayer);
            petNewPetQueue.Clear();
            petNewPetBackdrop = null;
            petNewPetCloseButton = null;
            petNewPetCloseAt = 0f;
        }

        private void SelectPetIntroductionSkill(HomeUiRenderer renderer, PetGachaPetProfile profile,
            int skillId, int selectedIndex)
        {
            const string skillRoot = "/PetGetNewPanel/BgPanel1/Content/MiddleGo/Content/SkilIs/ItemIconSkill";
            for (int index = 0; index < 3; index++)
            {
                string path = skillRoot + (index == 0 ? string.Empty
                    : " (" + index.ToString(CultureInfo.InvariantCulture) + ")");
                renderer.SetActive(path + "/Content/QualityImg/SelAnimGo",
                    index == selectedIndex && index < profile.SupportSkillIds.Length);
            }

            PetGachaItem skill = host.PetGachaCatalog.Item(skillId);
            if (skill == null) throw new InvalidOperationException("官方支援技能配置缺失：" + skillId);
            const string descriptionPath = "/PetGetNewPanel/BgPanel1/Content/MiddleGo/Content/DescGo/DescText";
            TMP_Text description = renderer.Find<TMP_Text>(descriptionPath);
            if (description == null) throw new InvalidOperationException("官方新幻兽面板缺少技能说明。");
            description.text = skill.FuncDesc;
            description.raycastTarget = skill.HyperlinkEntryIds != null && skill.HyperlinkEntryIds.Length > 0;
            PetGachaDescriptionLinkHandler linkHandler = description.GetComponent<PetGachaDescriptionLinkHandler>();
            if (linkHandler == null) linkHandler = description.gameObject.AddComponent<PetGachaDescriptionLinkHandler>();
            linkHandler.Configure(description, skill.HyperlinkEntryIds, ShowPetSkillEntryDetail);
        }

        private void ShowPetSkillEntryDetail(int entryId, Vector2 clickPosition)
        {
            PetGachaEntryDetail entry = host.PetGachaCatalog.EntryDetail(entryId);
            if (entry == null) throw new InvalidOperationException("官方技能词条配置缺失：" + entryId);
            ClosePetLayer(ref petSkillEntryLayer);
            HomeUiRenderer dialog = CreatePetLayer("pet_gacha_entry_dialog", out petSkillEntryLayer,
                () => ClosePetLayer(ref petSkillEntryLayer), 0f);
            const string gridPath = "/EntryDetailDialog/Content (1)/ScrollRect/Viewport/Grid";
            Transform grid = dialog.Find(gridPath)?.transform;
            HomeUiNode rowTemplate = catalog.Panel("pet_gacha_entry_row")?.root;
            if (grid == null || rowTemplate == null)
                throw new InvalidOperationException("官方词条详情面板缺少列表或行模板。");

            HomeUiRenderer row = new HomeUiRenderer(catalog);
            row.CreateNode(rowTemplate, grid);
            row.SetText("/EntryDetailItem/TitleGo/NameTxt", entry.Name);
            row.SetText("/EntryDetailItem/DescTxt", entry.Description);
            bool hasIcon = !string.IsNullOrEmpty(entry.IconFile);
            bool hasBackground = !string.IsNullOrEmpty(entry.BackgroundFile);
            row.SetActive("/EntryDetailItem/TitleGo/IconGo", hasIcon);
            row.SetActive("/EntryDetailItem/TitleGo/IconGo/BgImg", hasBackground);
            row.SetActive("/EntryDetailItem/SplitBlank", true);
            row.SetActive("/EntryDetailItem/SplitBlank/TitleSplitGo", true);
            row.SetActive("/EntryDetailItem/SplitGo", false);
            row.SetText("/EntryDetailItem/TitleGo/IconGo/StackCountTxt", string.Empty);
            if (hasIcon) row.SetSprite("/EntryDetailItem/TitleGo/IconGo/IconImg", catalog.FindSprite(entry.IconFile));
            if (hasBackground) row.SetSprite("/EntryDetailItem/TitleGo/IconGo/BgImg", catalog.FindSprite(entry.BackgroundFile));
            PositionPetEntryPopup(dialog, clickPosition);
        }

        private void PositionPetEntryPopup(HomeUiRenderer dialog, Vector2 screenPosition)
        {
            GameObject dialogRoot = dialog.Find("/EntryDetailDialog");
            GameObject popupObject = dialog.Find("/EntryDetailDialog/Content (1)");
            RectTransform rootRect = dialogRoot == null ? null : dialogRoot.GetComponent<RectTransform>();
            RectTransform popup = popupObject == null ? null : popupObject.GetComponent<RectTransform>();
            if (rootRect == null || popup == null || canvas == null) return;

            // Match EntryDetailDialog and UIUtils.SetRectPopPosInScreen.
            Canvas.ForceUpdateCanvases();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenPosition,
                    canvas.worldCamera, out Vector2 position)) return;

            popup.anchorMin = popup.anchorMax = new Vector2(0.5f, 0.5f);
            Vector2 pivot = new Vector2(position.x > 0f ? 1f : 0f, position.y > 0f ? 1f : 0f);
            popup.pivot = pivot;
            position.y += position.y > 0f ? -10f : 10f;

            Vector2 screenSize = canvas.pixelRect.size / canvas.scaleFactor;
            Vector2 halfSize = screenSize * 0.5f;
            Vector2 popupSize = popup.rect.size;
            position.x = pivot.x > 0f
                ? Mathf.Clamp(position.x, -halfSize.x + popupSize.x, halfSize.x)
                : Mathf.Clamp(position.x, -halfSize.x, halfSize.x - popupSize.x);
            position.y = pivot.y > 0f
                ? Mathf.Clamp(position.y, -halfSize.y + popupSize.y, halfSize.y)
                : Mathf.Clamp(position.y, -halfSize.y, halfSize.y - popupSize.y);
            popup.anchoredPosition = position;
        }

        private void ClosePetResult()
        {
            ClosePetLayer(ref petResultLayer);
            ShowNextPetIntroduction();
        }

        private void PlayPetGachaResultPerformance(IReadOnlyList<OfflinePetGachaAward> awards, int count)
        {
            if (awards == null || awards.Count != count || (count != 1 && count != 10 && count != 100))
                throw new ArgumentException("结缘演出奖励数量与抽取次数不一致。", nameof(awards));

            ClosePetLayer(ref petPerformanceLayer);
            HomeUiRenderer renderer = CreatePetLayer("pet_gacha_stage", out petPerformanceLayer, null);
            const string skipPath = "/PetGachaSubPanel/PetGachaResultPerformer/SkipBtn";
            Button skip = renderer.Find<Button>(skipPath);
            if (skip == null)
                throw new InvalidOperationException("官方结缘演出缺少跳过输入绑定。");
            if (count == 1) skip.gameObject.SetActive(false);
            else skip.interactable = false;

            PetGachaResultPerformancePlayer player = petPerformanceLayer
                .AddComponent<PetGachaResultPerformancePlayer>();
            player.Begin(host, catalog, renderer, awards, count, count == 1 ? null : skip, () =>
            {
                ClosePetLayer(ref petPerformanceLayer);
                ShowPetGachaResultPage(awards, count);
            });
        }

        private void ShowPetGachaDetails()
        {
            petHistoryPage = 0;
            petDetailRenderer = CreatePetLayer("pet_gacha_detail", out petDetailLayer,
                () => ClosePetLayer(ref petDetailLayer));
            BindPetDetailTabs();
            ShowPetProbabilityPage();
            BindAllNamed(petDetailRenderer, "CloseBtn", () => ClosePetLayer(ref petDetailLayer));
        }

        private void BindPetDetailTabs()
        {
            BindPetTab(petDetailRenderer, "/GachaDetailDialog/BgDialog1/Content/TabGroup_14/TabGroup/PreviewTabItem",
                ShowPetProbabilityPage);
            BindPetTab(petDetailRenderer, "/GachaDetailDialog/BgDialog1/Content/TabGroup_14/TabGroup/HistoryTabItem",
                () => { petHistoryPage = 0; ShowPetHistoryPage(); });
        }

        private void BindPetTab(HomeUiRenderer renderer, string path, Action action)
        {
            Button button = renderer.Find<Button>(path);
            if (button == null) button = renderer.Find<Button>(path + "/Raycast");
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => action?.Invoke());
                return;
            }
            Toggle toggle = renderer.Find<Toggle>(path);
            if (toggle != null)
            {
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener(value => { if (value) action?.Invoke(); });
            }
        }

        private void ShowPetProbabilityPage()
        {
            if (petDetailRenderer == null || petDetailLayer == null) return;
            petDetailRenderer.SetActive(PetDetailPrefix + "RewardPreviewScroll", true);
            petDetailRenderer.SetActive(PetDetailPrefix + "RewardHistoryScroll", false);
            petDetailRenderer.SetActive("/GachaDetailDialog/BgDialog1/Content/TabGroup_14/TabGroup/PreviewTabItem/Selected", true);
            petDetailRenderer.SetActive("/GachaDetailDialog/BgDialog1/Content/TabGroup_14/TabGroup/HistoryTabItem/Selected", false);
            Transform content = petDetailRenderer.Find(PetDetailPrefix + "RewardPreviewScroll/RewardPreviewViewport/RewardPreviewGo")?.transform;
            if (content == null) return;
            HomeUiNode source = host.Catalog.Panel("pet_gacha_detail")?.root;
            HomeUiNode headerTemplate = FindUiNode(source, "DayGachaRuleItem");
            HomeUiNode rowTemplate = FindUiNode(source, "AwardRateRuleItem");
            if (headerTemplate == null || rowTemplate == null) return;
            foreach (string templateName in new[] { "DayGachaRuleItem", "GachaRuleItem", "AwardRateRuleItem" })
                foreach (Transform child in content)
                    if (child.name == templateName) child.gameObject.SetActive(false);

            EnsurePetVerticalList(content);
            HomeUiRenderer header = CreatePetDetailRow(headerTemplate, content, true);
            header.SetTextUnderPath("DayGachaRuleItem", "RuleTitleItem/NumText", "01");
            header.SetTextUnderPath("DayGachaRuleItem", "RuleTitleItem/TitleText", "基础概率");
            header.SetTextUnderPath("DayGachaRuleItem", "RuleTextItem/Desc",
                "固定版基础概率按原奖励权重计算；前10次使用新手奖池，之后使用常规奖池。橙色幻兽包内候选权重相同。");

            AddPetRuleProbabilityRows(content, rowTemplate, host.PetGachaCatalog.Rule(20401), "前10次结缘");
            AddPetRuleProbabilityRows(content, rowTemplate, host.PetGachaCatalog.Rule(20400), "常规结缘");

            HomeUiRenderer pityHeader = CreatePetDetailRow(headerTemplate, content, true);
            pityHeader.SetTextUnderPath("DayGachaRuleItem", "RuleTitleItem/NumText", "02");
            pityHeader.SetTextUnderPath("DayGachaRuleItem", "RuleTitleItem/TitleText", "官方综合概率");
            pityHeader.SetTextUnderPath("DayGachaRuleItem", "RuleTextItem/Desc",
                "以下数值来自固定版结缘详情概率表，与基础奖池权重分开显示。");
            foreach (PetGachaPityProbability value in host.PetGachaCatalog.PityProbabilities
                .Where(row => row.GachaType == host.PetGachaCatalog.Gacha.GachaType))
            {
                string category = PetPityCategory(value.Quality, value.GroupType);
                HomeUiRenderer row = CreatePetDetailRow(rowTemplate, content, false);
                row.SetTextUnderPath("AwardRateRuleItem", "RuleTitleItem/NumText", "");
                row.SetTextUnderPath("AwardRateRuleItem", "RuleTitleItem/TitleText", category);
                row.SetTextUnderPath("AwardRateRuleItem", "RuleTextItem/Desc", "固定版详情概率");
                row.SetTextUnderPath("RuleRateInfoItemGacha", "RateContent/TitleGo/TitleText", category);
                row.SetTextUnderPath("RuleRateInfoItemGacha", "RateContent/TitleGo/BaseProbilityText", "");
                row.SetTextUnderPath("RuleRateInfoItemGacha", "RateContent/TitleGo/PityProbilityText",
                    FormatPercent(value.PityProbability / 100d));
                Transform rate = FindNamedTransform(row.Find(rowTemplate.path)?.transform, "RuleRateInfoItemGacha");
                if (rate != null) rate.gameObject.SetActive(true);
            }
        }

        private void AddPetRuleProbabilityRows(Transform content, HomeUiNode template,
            PetGachaRewardRule rule, string label)
        {
            if (rule == null) return;
            long weightTotal = rule.Weights.Where(value => value > 0).Sum(value => (long)value);
            for (int index = 0; index < rule.ItemIds.Length; index++)
            {
                PetGachaItem directItem = host.PetGachaCatalog.Item(rule.ItemIds[index]);
                PetGachaRandomReward package = host.PetGachaCatalog.RandomReward(rule.ItemIds[index]);
                double basePercent = weightTotal <= 0 ? 0d : 100d * rule.Weights[index] / weightTotal;
                string title = package == null ? directItem?.Name ?? rule.ItemIds[index].ToString(CultureInfo.InvariantCulture)
                    : PetRewardCategory(package.Id);
                string details = package == null
                    ? "数量 " + Math.Max(1, rule.Counts[index]).ToString("0.##", CultureInfo.InvariantCulture)
                    : PetCandidateProbabilityText(package, basePercent);
                HomeUiRenderer row = CreatePetDetailRow(template, content, false);
                row.SetTextUnderPath("AwardRateRuleItem", "RuleTitleItem/NumText", "");
                row.SetTextUnderPath("AwardRateRuleItem", "RuleTitleItem/TitleText", title);
                row.SetTextUnderPath("AwardRateRuleItem", "RuleTextItem/Desc", label + "：" + details);
                row.SetTextUnderPath("RuleRateInfoItemGacha", "RateContent/TitleGo/TitleText", title);
                row.SetTextUnderPath("RuleRateInfoItemGacha", "RateContent/TitleGo/BaseProbilityText",
                    FormatPercent(basePercent));
                string pity = package == null ? string.Empty : OfficialDisplayedProbability(package.Id);
                row.SetTextUnderPath("RuleRateInfoItemGacha", "RateContent/TitleGo/PityProbilityText", pity);
                Transform rowRoot = row.Find(template.path)?.transform;
                Transform rate = FindNamedTransform(rowRoot, "RuleRateInfoItemGacha");
                if (rate != null) rate.gameObject.SetActive(true);
                Transform iconGrid = FindNamedTransform(rowRoot, "Grid");
                if (iconGrid != null && package != null)
                {
                    EnsurePetGrid(iconGrid);
                    foreach (int itemId in package.ItemIds)
                    {
                        PetGachaItem item = host.PetGachaCatalog.Item(itemId);
                        if (item != null) AddPetAwardIcon(iconGrid, item, package.MinCount, 48f);
                    }
                }
            }
        }

        private string PetCandidateProbabilityText(PetGachaRandomReward package, double categoryPercent)
        {
            long total = package.Weights.Where(value => value > 0).Sum(value => (long)value);
            var candidates = new List<string>();
            for (int index = 0; index < package.ItemIds.Length; index++)
            {
                PetGachaItem item = host.PetGachaCatalog.Item(package.ItemIds[index]);
                if (item == null) continue;
                double inner = total <= 0 ? 0d : (double)package.Weights[index] / total;
                candidates.Add(item.Name + " " + FormatPercent(inner * 100d)
                    + "（本奖池 " + FormatPercent(categoryPercent * inner) + "）");
            }
            return "类别权重 " + FormatPercent(categoryPercent) + "；包内候选按配置权重抽取："
                + string.Join("、", candidates);
        }

        private HomeUiRenderer CreatePetDetailRow(HomeUiNode template, Transform content, bool section)
        {
            HomeUiRenderer row = new HomeUiRenderer(catalog);
            GameObject gameObject = row.CreateNode(template, content);
            gameObject.SetActive(true);
            if (!section)
            {
                Transform rate = FindNamedTransform(gameObject.transform, "RuleRateInfoItemGacha");
                if (rate != null) rate.gameObject.SetActive(true);
            }
            SetListRowPosition(gameObject.transform as RectTransform, content, content.childCount, content.childCount - 1);
            return row;
        }

        private static void EnsurePetVerticalList(Transform content)
        {
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private string OfficialDisplayedProbability(int rewardPackageId)
        {
            int quality;
            int groupType;
            switch (rewardPackageId)
            {
                case 53102: quality = 4; groupType = 2; break;
                case 53100: quality = 3; groupType = 2; break;
                case 53201: quality = 4; groupType = 3; break;
                case 53200: quality = 3; groupType = 3; break;
                case 53323: quality = 2; groupType = 4; break;
                case 53324: quality = 3; groupType = 4; break;
                default: return string.Empty;
            }
            PetGachaPityProbability value = host.PetGachaCatalog.PityProbabilities.FirstOrDefault(row =>
                row.GachaType == host.PetGachaCatalog.Gacha.GachaType
                && row.Quality == quality && row.GroupType == groupType);
            return value == null ? string.Empty : FormatPercent(value.PityProbability / 100d);
        }

        private static string PetPityCategory(int quality, int groupType)
        {
            string group = groupType switch
            {
                2 => "整只幻兽",
                3 => "幻兽碎片",
                4 => "能力食材",
                9 => "其他奖励",
                _ => "奖励类别 " + groupType.ToString(CultureInfo.InvariantCulture)
            };
            return QualityText(quality) + " · " + group;
        }

        private static string PetRewardCategory(int rewardPackageId) => rewardPackageId switch
        {
            53102 => "传说幻兽整只",
            53100 => "史诗幻兽整只",
            53201 => "传说幻兽碎片",
            53200 => "史诗幻兽碎片",
            53323 => "精良能力食材",
            53324 => "史诗能力食材",
            _ => "随机奖励 " + rewardPackageId.ToString(CultureInfo.InvariantCulture)
        };

        private void ShowPetHistoryPage()
        {
            if (petDetailRenderer == null || petDetailLayer == null) return;
            petDetailRenderer.SetActive(PetDetailPrefix + "RewardPreviewScroll", false);
            petDetailRenderer.SetActive(PetDetailPrefix + "RewardHistoryScroll", true);
            petDetailRenderer.SetActive("/GachaDetailDialog/BgDialog1/Content/TabGroup_14/TabGroup/PreviewTabItem/Selected", false);
            petDetailRenderer.SetActive("/GachaDetailDialog/BgDialog1/Content/TabGroup_14/TabGroup/HistoryTabItem/Selected", true);
            Transform list = petDetailRenderer.Find(PetDetailPrefix
                + "RewardHistoryScroll/RewardHistoryViewport/RewardHistoryGo/RecordListRuleItem/RuleFormScrollListItem/ScrollView/Viewport/ListDataGo")?.transform;
            HomeUiNode source = host.Catalog.Panel("pet_gacha_detail")?.root;
            HomeUiNode rowTemplate = FindUiNode(source, "RuleFormDataItem");
            if (list == null || rowTemplate == null) return;
            foreach (Transform child in list) UnityEngine.Object.Destroy(child.gameObject);
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            long cutoff = Math.Max(0, now - host.PetGachaCatalog.Settings.HistoryDays * 86400L);
            int totalRecords = host.PetGacha.State.History.Count(value => value.UnixSeconds >= cutoff);
            int pageSize = 10;
            int totalPages = Math.Max(1, (totalRecords + pageSize - 1) / pageSize);
            petHistoryPage = Mathf.Clamp(petHistoryPage, 0, totalPages - 1);
            IReadOnlyList<OfflinePetGachaDrawRecord> records = host.PetGacha.History(now, petHistoryPage, pageSize);
            EnsurePetVerticalList(list);
            for (int index = 0; index < records.Count; index++)
            {
                OfflinePetGachaDrawRecord record = records[index];
                OfflinePetGachaAward award = record.Awards.FirstOrDefault();
                HomeUiRenderer row = new HomeUiRenderer(catalog);
                GameObject root = row.CreateNode(rowTemplate, list);
                root.SetActive(true);
                row.SetTextByName("DataText1", award == null ? string.Empty : QualityText(award.Quality));
                row.SetTextByName("DataText2", award == null ? string.Empty : PetAwardSummary(record.Awards));
                row.SetTextByName("DataText3", DateTimeOffset.FromUnixTimeSeconds(record.UnixSeconds)
                    .LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                SetListRowPosition(root.transform as RectTransform, list, records.Count, index);
            }
            petDetailRenderer.SetTextByName("CurPageText", (petHistoryPage + 1).ToString(CultureInfo.InvariantCulture));
            petDetailRenderer.SetTextByName("TotalPageText ", totalPages.ToString(CultureInfo.InvariantCulture));
            Bind(petDetailRenderer, PetDetailPrefix
                + "RewardHistoryScroll/RewardHistoryViewport/RewardHistoryGo/TurnPageUI/PrevBtn", () =>
            {
                if (petHistoryPage <= 0) return;
                petHistoryPage--;
                ShowPetHistoryPage();
            });
            Bind(petDetailRenderer, PetDetailPrefix
                + "RewardHistoryScroll/RewardHistoryViewport/RewardHistoryGo/TurnPageUI/NextBtn", () =>
            {
                if (petHistoryPage + 1 >= totalPages) return;
                petHistoryPage++;
                ShowPetHistoryPage();
            });
        }

        private void ShowPetScoreRewards()
        {
            HomeUiRenderer renderer = CreatePetLayer("pet_gacha_score", out petScoreLayer,
                ClosePetScoreRewards);
            renderer.SetActive("/ScoreRewardDialog/BgDialog1/Content", true);
            renderer.SetTextByName("TitleText", "结缘积分奖励");
            renderer.SetTextByName("RuleText", "每次结缘获得1积分。当前累计 "
                + host.PetGacha.State.Score.ToString(CultureInfo.InvariantCulture) + " 积分。");
            Transform content = renderer.Find("/ScoreRewardDialog/BgDialog1/Content/RewardItemContent/Scroll View/Viewport/RewardItemSlot")?.transform;
            Transform scrollNode = renderer.Find("/ScoreRewardDialog/BgDialog1/Content/RewardItemContent/Scroll View")?.transform;
            HomeUiNode source = host.Catalog.Panel("pet_gacha_score_item")?.root;
            if (content != null && scrollNode != null && source != null)
            {
                HomeUiNode template = source;
                ScrollRect scroll = scrollNode.GetComponent<ScrollRect>();
                if (scroll == null) scroll = scrollNode.gameObject.AddComponent<ScrollRect>();
                petScoreRenderer = renderer;
                PreparePetVirtualList(scroll, content, template, 16);
            }
            BindAllNamed(renderer, "CloseBtn", ClosePetScoreRewards);
        }

        private void ClosePetScoreRewards()
        {
            ClearPetVirtualList();
            ClosePetLayer(ref petScoreLayer);
            petScoreRenderer = null;
        }

        private void RefreshPetCountAchievements()
        {
            if (petGachaPanel == null || host.PetGacha == null) return;
            Transform panel = FindNamedTransform(petGachaPanel.transform, "AchievementPanel");
            Transform scrollNode = panel == null ? null : FindNamedTransform(panel, "ScrollRect");
            Transform content = panel == null ? null : FindNamedTransform(panel, "Grid");
            HomeUiNode source = host.Catalog.Panel("pet_exchange_shell")?.root;
            HomeUiNode template = FindUiNode(source, "AchievementPanelItem");
            if (scrollNode == null || content == null || template == null) return;
            ScrollRect scroll = scrollNode.GetComponent<ScrollRect>();
            if (scroll == null) scroll = scrollNode.gameObject.AddComponent<ScrollRect>();
            if (petVirtualScroll == scroll && petVirtualGroup == 15 && petVirtualContent == content)
            {
                RefreshPetVirtualRows();
                return;
            }
            PreparePetVirtualList(scroll, content, template, 15);
        }

        private void PreparePetVirtualList(ScrollRect scroll, Transform content, HomeUiNode template, int group)
        {
            if (scroll == null || content == null || template == null) return;
            if (petVirtualScroll != null) petVirtualScroll.onValueChanged.RemoveListener(OnPetVirtualScroll);
            foreach (PetAchievementRow row in petVirtualRows)
                if (row.Root != null) UnityEngine.Object.Destroy(row.Root);
            petVirtualRows.Clear();
            petVirtualScroll = scroll;
            petVirtualContent = content as RectTransform;
            petVirtualTemplate = template;
            petVirtualGroup = group;
            petVirtualTotal = host.PetGacha.AchievementCount(group);
            petVirtualFirstVisible = -1;
            petVirtualRowHeight = Mathf.Max(80f, template.size_delta != null && template.size_delta.Length > 1
                ? Mathf.Abs(template.size_delta[1]) : 180f);
            RectTransform contentRect = petVirtualContent;
            if (contentRect == null) return;
            LayoutGroup layout = content.GetComponent<LayoutGroup>();
            if (layout != null) layout.enabled = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            foreach (Transform child in content) UnityEngine.Object.Destroy(child.gameObject);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, petVirtualTotal * petVirtualRowHeight);
            scroll.content = contentRect;
            scroll.vertical = true;
            scroll.horizontal = false;
            if (scroll.viewport == null)
                scroll.viewport = FindNamedTransform(scroll.transform, "ViewPort") as RectTransform
                    ?? FindNamedTransform(scroll.transform, "Viewport") as RectTransform;
            RectTransform viewport = scroll.viewport;
            int visibleCount = viewport == null || viewport.rect.height <= 0f ? 8
                : Mathf.CeilToInt(viewport.rect.height / petVirtualRowHeight) + 3;
            int rowCount = Mathf.Clamp(visibleCount, 6, 20);
            for (int index = 0; index < rowCount; index++)
            {
                HomeUiRenderer rowRenderer = new HomeUiRenderer(catalog);
                GameObject rowRoot = rowRenderer.CreateNode(template, content, null);
                rowRoot.name = "PetAchievementVirtualRow_" + index.ToString(CultureInfo.InvariantCulture);
                rowRoot.SetActive(false);
                petVirtualRows.Add(new PetAchievementRow { Renderer = rowRenderer, Root = rowRoot });
            }
            scroll.onValueChanged.AddListener(OnPetVirtualScroll);
            scroll.verticalNormalizedPosition = 1f;
            RefreshPetVirtualRows();
        }

        private void ClearPetVirtualList()
        {
            if (petVirtualScroll != null) petVirtualScroll.onValueChanged.RemoveListener(OnPetVirtualScroll);
            foreach (PetAchievementRow row in petVirtualRows)
                if (row.Root != null) UnityEngine.Object.Destroy(row.Root);
            petVirtualRows.Clear();
            petVirtualScroll = null;
            petVirtualContent = null;
            petVirtualTemplate = null;
            petVirtualTotal = 0;
            petVirtualGroup = 0;
            petVirtualFirstVisible = -1;
        }

        private void OnPetVirtualScroll(Vector2 _)
        {
            RefreshPetVirtualRows();
        }

        private void RefreshPetVirtualRows()
        {
            if (petVirtualScroll == null || petVirtualContent == null || petVirtualTemplate == null
                || petVirtualRows.Count == 0 || host.PetGacha == null) return;
            int first = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, petVirtualContent.anchoredPosition.y)
                / Mathf.Max(1f, petVirtualRowHeight)), 0, Math.Max(0, petVirtualTotal - petVirtualRows.Count));
            if (first == petVirtualFirstVisible) return;
            petVirtualFirstVisible = first;
            for (int poolIndex = 0; poolIndex < petVirtualRows.Count; poolIndex++)
            {
                int entryIndex = first + poolIndex;
                PetAchievementRow row = petVirtualRows[poolIndex];
                if (entryIndex >= petVirtualTotal)
                {
                    row.Root.SetActive(false);
                    continue;
                }
                RectTransform rowRect = row.Root.transform as RectTransform;
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -entryIndex * petVirtualRowHeight);
                rowRect.sizeDelta = new Vector2(0f, petVirtualRowHeight);
                row.Root.SetActive(true);
                PetGachaAchievementEntry entry = host.PetGacha.Achievements(petVirtualGroup, entryIndex, 1)
                    .FirstOrDefault();
                FillPetAchievementRow(row, entry);
            }
        }

        private void FillPetAchievementRow(PetAchievementRow row, PetGachaAchievementEntry entry)
        {
            if (row == null || row.Renderer == null || row.Root == null || entry == null) return;
            HomeUiRenderer renderer = row.Renderer;
            bool score = petVirtualGroup == 16;
            renderer.SetTextByName(score ? "Titletext" : "TitleTxt", score
                ? "累计结缘积分达到 " + entry.Target
                : "累计结缘 " + entry.Target + " 次");
            renderer.SetTextByName(score ? "text" : "ProgressTxt",
                entry.CurrentValue.ToString(CultureInfo.InvariantCulture) + "/" + entry.Target.ToString(CultureInfo.InvariantCulture));
            Slider slider = row.Root.GetComponentInChildren<Slider>(true);
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 100f;
                slider.value = entry.Target <= 0 ? 100f : Mathf.Clamp01((float)entry.CurrentValue / entry.Target) * 100f;
            }
            Transform grid = FindNamedTransform(row.Root.transform, "AwardGrid");
            if (grid != null)
            {
                foreach (Transform child in grid) UnityEngine.Object.Destroy(child.gameObject);
                EnsurePetGrid(grid);
                foreach (PetGachaAmount reward in entry.Rewards ?? Array.Empty<PetGachaAmount>())
                {
                    PetGachaItem item = host.PetGachaCatalog.Item(reward.ItemId);
                    if (item != null) AddPetAwardIcon(grid, item, reward.Count, 58f);
                }
            }

            bool claimable = entry.IsReady && !entry.IsClaimed;
            string receiveName = score ? "GetAwardBtn" : "ReceiveBtn";
            string unfinishedName = score ? "UnFinishedBtn" : "UncompletedGo";
            if (score)
            {
                SetActiveInRow(row.Root.transform, "GetAwardBtn", claimable || entry.IsClaimed);
                SetActiveInRow(row.Root.transform, "UnFinishedBtn", !entry.IsReady && !entry.IsClaimed);
            }
            else
            {
                SetActiveInRow(row.Root.transform, "BtnGroup/ReceiveBtn", claimable);
                SetActiveInRow(row.Root.transform, "BtnGroup/UncompletedGo", !entry.IsReady && !entry.IsClaimed);
                SetActiveInRow(row.Root.transform, "BtnGroup/CommonSeal", entry.IsClaimed);
            }
            Transform receiveTransform = FindNamedTransform(row.Root.transform, receiveName);
            Button receive = receiveTransform == null ? null : receiveTransform.GetComponent<Button>();
            if (receive != null)
            {
                receive.onClick.RemoveAllListeners();
                receive.interactable = claimable;
                if (entry.IsClaimed)
                {
                    TMP_Text buttonText = FindNamedTransform(row.Root.transform, "Text")?.GetComponent<TMP_Text>();
                    if (buttonText != null) buttonText.text = "已领取";
                }
                else if (claimable) receive.onClick.AddListener(() => ClaimPetAchievement(entry.Group, entry.Index));
            }
            Transform unfinishedTransform = FindNamedTransform(row.Root.transform, unfinishedName);
            Button uncompleted = unfinishedTransform == null ? null : unfinishedTransform.GetComponent<Button>();
            if (uncompleted != null) uncompleted.interactable = false;
        }

        private void ClaimPetAchievement(int group, int index)
        {
            PetGachaResult result = host.PetGacha.ClaimAchievement(group, index,
                PetGachaReceipt("achievement-" + group.ToString(CultureInfo.InvariantCulture) + "-"
                    + index.ToString(CultureInfo.InvariantCulture)), DateTimeOffset.Now.ToUnixTimeSeconds());
            if (result == null || !result.IsApplied)
            {
                Notify(string.IsNullOrEmpty(result?.Message) ? PetGachaError(result?.Code ?? PetGachaResultCode.NotReady) : result.Message);
                return;
            }
            host.RefreshCounts();
            Notify("奖励已领取");
            petVirtualFirstVisible = -1;
            RefreshPetVirtualRows();
            if (group == 16 && petScoreLayer != null)
                petScoreRenderer?.SetTextByName("RuleText", "每次结缘获得1积分。当前累计 "
                    + host.PetGacha.State.Score.ToString(CultureInfo.InvariantCulture) + " 积分。");
        }

        private void RefreshPetShop()
        {
            if (petGachaPanel == null || host.PetGacha == null) return;
            Transform grid = FindNamedTransform(petGachaPanel.transform, "GoodsGrid");
            Transform viewport = grid == null ? null : FindNamedTransform(grid, "ViewPort");
            Transform content = viewport == null ? null : FindNamedTransform(viewport, "Content");
            if (content == null) content = grid;
            if (content == null) return;
            foreach (Transform child in content) UnityEngine.Object.Destroy(child.gameObject);
            HomeUiNode itemTemplate = host.Catalog.Panel("pet_exchange_goods")?.root;
            HomeUiNode iconTemplate = host.Catalog.Panel("pet_exchange_icon")?.root;
            if (itemTemplate == null || iconTemplate == null) return;
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            IReadOnlyList<PetShopGoodsEntry> goods = host.PetGacha.ShopGoods(now);
            GridLayoutGroup gridLayout = content.GetComponent<GridLayoutGroup>();
            if (gridLayout == null) gridLayout = content.gameObject.AddComponent<GridLayoutGroup>();
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int index = 0; index < goods.Count; index++)
            {
                PetShopGoodsEntry entry = goods[index];
                HomeUiRenderer row = new HomeUiRenderer(catalog);
                GameObject rowRoot = row.CreateNode(itemTemplate, content);
                rowRoot.SetActive(true);
                rowRoot.name = "PetShopGoods_" + entry.GoodsId.ToString(CultureInfo.InvariantCulture);
                Transform slot = FindNamedTransform(rowRoot.transform, "ItemInfoSlot");
                if (slot != null)
                {
                    row.CreateNode(iconTemplate, slot, null);
                    row.SetSprite("/ShopGoodsItemInfoCommon/QualityBgImg",
                        catalog.FindSprite(host.PetGachaCatalog.QualityBackground(entry.Quality)));
                    row.SetSprite("/ShopGoodsItemInfoCommon/ItemImg", catalog.FindSprite(entry.IconFile));
                    row.SetText("/ShopGoodsItemInfoCommon/GoodsNameText", entry.Name);
                    row.SetText("/ShopGoodsItemInfoCommon/CountInfoGo/CountText", "×" + entry.ItemCount);
                }
                row.SetText("/ShopGoodsItem/Content/ImgBg/Inventory", entry.Remaining < 0
                    ? string.Empty : "剩余 " + entry.Remaining.ToString(CultureInfo.InvariantCulture));
                row.SetText("/ShopGoodsItem/Content/ImgBg/BuyBtn/Content/Text/PresentPriceText",
                    entry.Price.ToString(CultureInfo.InvariantCulture));
                PetGachaItem currency = host.PetGachaCatalog.Item(entry.CurrencyItemId);
                if (currency != null)
                    row.SetSprite("/ShopGoodsItem/Content/ImgBg/BuyBtn/Content/Text/MoneyImg",
                        catalog.FindSprite(currency.IconFile));
                row.SetActive("/ShopGoodsItem/Content/ImgBg/PriceOffBg/ImgDark", false);
                TMP_Text priceText = row.Find<TMP_Text>("/ShopGoodsItem/Content/ImgBg/BuyBtn/Content/Text/PresentPriceText");
                if (priceText != null) priceText.color = new Color(0.14f, 0.14f, 0.14f, 1f);
                bool soldOut = entry.Remaining == 0;
                row.SetActive("/ShopGoodsItem/Content/ImgBg/SoldoutGo", soldOut);
                row.SetActive("/ShopGoodsItem/Content/ImgBg/CannotBuyMask", soldOut
                    || host.Profile.InventoryCount(entry.CurrencyItemId) < entry.Price);
                Button buy = row.Find<Button>("/ShopGoodsItem/Content/ImgBg/BuyBtn");
                if (buy != null)
                {
                    buy.interactable = !soldOut && host.Profile.InventoryCount(entry.CurrencyItemId) >= entry.Price;
                    buy.onClick.RemoveAllListeners();
                    buy.onClick.AddListener(() => RequestPetShopBuy(entry));
                }
                Button details = row.Find<Button>("/ShopGoodsItem/Content/ImgBg/ItemInfoBtn");
                if (details != null)
                {
                    details.onClick.RemoveAllListeners();
                    details.onClick.AddListener(() => Notify(entry.Name + " × " + entry.ItemCount));
                }
                (rowRoot.transform as RectTransform).localScale = Vector3.one;
            }
            UpdatePetShopCountdown(DateTime.Now);
            petGachaRenderer?.SetActive(PetShellPrefix + "ShopContent/ShopContent/ShopContentGroup/EmptyTip", goods.Count == 0);
        }

        private void RequestPetShopBuy(PetShopGoodsEntry entry)
        {
            if (entry.RequiresConfirmation)
            {
                ShowPetConfirm("购买确认", "购买 " + entry.Name + " × " + entry.ItemCount + "？", false,
                    _ => BuyPetShopGoods(entry));
                return;
            }
            BuyPetShopGoods(entry);
        }

        private void BuyPetShopGoods(PetShopGoodsEntry entry)
        {
            PetGachaResult result = host.PetGacha.BuyShopGoods(entry.GoodsId, 1,
                PetGachaReceipt("shop-buy-" + entry.GoodsId.ToString(CultureInfo.InvariantCulture)),
                DateTimeOffset.Now.ToUnixTimeSeconds(), true);
            if (result == null || !(result.IsApplied || result.Code == PetGachaResultCode.Unchanged))
            {
                Notify(string.IsNullOrEmpty(result?.Message) ? "购买未完成" : result.Message);
                return;
            }
            host.RefreshCounts();
            RefreshPetShop();
            Notify("购买完成");
        }

        private void UpdatePetShopCountdown(DateTime now)
        {
            int daysFromMonday = ((int)now.DayOfWeek + 6) % 7;
            DateTime next = now.Date.AddDays(-daysFromMonday).AddHours(8);
            if (now >= next) next = next.AddDays(7);
            TimeSpan left = next - now;
            petGachaRenderer?.SetText(PetShellPrefix + "ShopContent/TopGo/RefreshGo/CountDownText",
                string.Format(CultureInfo.InvariantCulture, "{0}天 {1:00}:{2:00}:{3:00}",
                    (int)left.TotalDays, left.Hours, left.Minutes, left.Seconds));
        }

        private void AddPetAwardIcon(Transform parent, PetGachaItem item, long count, float size)
        {
            var root = new GameObject("Award_" + item.ClassId.ToString(CultureInfo.InvariantCulture),
                typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(size, size);
            Image image = root.GetComponent<Image>();
            image.sprite = catalog.FindSprite(item.IconFile);
            image.preserveAspect = true;
            image.raycastTarget = false;
            var countObject = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            countObject.transform.SetParent(root.transform, false);
            RectTransform countRect = (RectTransform)countObject.transform;
            countRect.anchorMin = new Vector2(0f, 0f);
            countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(0.5f, 0f);
            countRect.anchoredPosition = new Vector2(0f, -2f);
            countRect.sizeDelta = new Vector2(size + 8f, Mathf.Max(18f, size * 0.36f));
            TMP_Text text = countObject.GetComponent<TMP_Text>();
            text.font = catalog.Font;
            text.fontSize = Mathf.Max(12f, size * 0.28f);
            text.alignment = TextAlignmentOptions.BottomRight;
            text.color = Color.white;
            text.outlineWidth = 0.16f;
            text.text = "×" + count.ToString(CultureInfo.InvariantCulture);
            text.raycastTarget = false;
        }

        private static void EnsurePetGrid(Transform grid)
        {
            GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
            if (layout != null) return;
            layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.Flexible;
            layout.childAlignment = TextAnchor.MiddleCenter;
        }

        private static string FormatPercent(double value) => value.ToString("0.####", CultureInfo.InvariantCulture) + "%";

        private static string PetGachaReceipt(string action)
            => "pet-gacha:" + action + ":" + Guid.NewGuid().ToString("N");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Home;

namespace Zjcs.Presentation.Home
{
    internal sealed class HomeSkinScreenController
    {
        private const string PanelPrefix = "/HomeSkinPanel/BgPanel1/Content/BottomGo/";
        private const string MainTabsPath = PanelPrefix + "Top/MainTabGroup/TabGroup";
        private static readonly string[] MainTabNames = { "FurnitureTabItem", "ThemeTabItem", "EnvironmentTabItem" };
        private static readonly string[] ContentNames =
        {
            "HomeSkinFurnitureContent", "HomeSkinThemeContent", "HomeSkinEnvironmentContent"
        };
        private static readonly string[] TemplateNames =
        {
            "decor_furniture_tab", "decor_theme_tab", "decor_environment_tab"
        };

        private readonly HomeFeatureRuntime host;
        private readonly HomeHudView hud;
        private readonly HomeAssetCatalog catalog;
        private readonly HomeUiRenderer page;
        private readonly GameObject root;
        private int category;
        private int selectedType = -1;
        private bool closed;

        private sealed class SkinOption
        {
            public int ItemId;
            public int Order;
            public bool IsDefault;
            public string Name;
        }

        private sealed class SkinGroup
        {
            public int Type;
            public string Name;
            public int DefaultItemId;
            public List<SkinOption> Items;
        }

        public bool IsOpen => !closed && root != null;

        public HomeSkinScreenController(HomeFeatureRuntime host, HomeHudView hud, Transform canvas)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            catalog = host.Catalog;
            if (canvas == null) throw new InvalidOperationException("The home canvas is unavailable.");

            page = new HomeUiRenderer(catalog);
            root = page.Create("decor", canvas);
            root.name = "HomeSkinPanel";
            BindCloseButton();
            BindMainTabs();
            hud.SetStandardHomeHudVisible(false);
            SelectCategory(0);
        }

        private void BindCloseButton()
        {
            Button close = page.Find<Button>(PanelPrefix + "Bottom/CloseBtn");
            if (close == null) throw new InvalidOperationException("The official home skin close button is missing.");
            close.onClick.AddListener(Close);
        }

        private void BindMainTabs()
        {
            ToggleGroup group = page.Find<ToggleGroup>(MainTabsPath);
            if (group == null) throw new InvalidOperationException("The official home skin category tabs are missing.");
            for (int index = 0; index < MainTabNames.Length; index++)
            {
                int tabIndex = index;
                Toggle toggle = page.Find<Toggle>(MainTabsPath + "/" + MainTabNames[index]);
                if (toggle == null) throw new InvalidOperationException("The official home skin tab is missing: " + MainTabNames[index]);
                toggle.group = group;
                toggle.onValueChanged.AddListener(isOn => { if (isOn) SelectCategory(tabIndex); });
            }
        }

        private void SelectCategory(int nextCategory)
        {
            if (closed) return;
            category = Mathf.Clamp(nextCategory, 0, 2);
            Toggle selected = page.Find<Toggle>(MainTabsPath + "/" + MainTabNames[category]);
            if (selected != null && !selected.isOn) selected.SetIsOnWithoutNotify(true);

            for (int index = 0; index < ContentNames.Length; index++)
                page.SetActive(PanelPrefix + "Content/" + ContentNames[index], index == category);

            List<SkinGroup> groups = BuildGroups(category);
            if (groups.Count == 0) throw new InvalidOperationException("The official home skin rules have no entries for category " + category);
            selectedType = groups[0].Type;
            BuildSubTabs(groups);
            RenderItems(groups[0]);
        }

        private List<SkinGroup> BuildGroups(int skinCategory)
        {
            if (skinCategory == 0)
            {
                return (host.Rules.Data.furnitureSkins ?? Array.Empty<HomeFurnitureSkinRecord>())
                    .GroupBy(value => value.furnitureType)
                    .OrderBy(group => group.Key)
                    .Select(group => CreateGroup(group.Key, group.First().prefabName,
                        group.Select(value => new SkinOption
                        {
                            ItemId = value.skinItemId,
                            Order = value.order,
                            IsDefault = value.isDefault,
                            Name = host.ItemName(value.skinItemId)
                        })))
                    .Where(value => value != null)
                    .ToList();
            }
            if (skinCategory == 1)
            {
                return (host.Rules.Data.themeSkins ?? Array.Empty<HomeThemeSkinRecord>())
                    .GroupBy(value => value.themeType)
                    .OrderBy(group => group.Key)
                    .Select(group => CreateGroup(group.Key, group.First().prefabName,
                        group.Select(value => new SkinOption
                        {
                            ItemId = value.skinItemId,
                            Order = value.order,
                            IsDefault = value.isDefault,
                            Name = host.ItemName(value.skinItemId)
                        })))
                    .Where(value => value != null)
                    .ToList();
            }
            return (host.Rules.Data.environmentSkins ?? Array.Empty<HomeEnvironmentSkinRecord>())
                .GroupBy(value => value.environmentType)
                .OrderBy(group => group.Key)
                .Select(group => CreateGroup(group.Key, "Weather",
                    group.Select(value => new SkinOption
                    {
                        ItemId = value.skinItemId,
                        Order = value.order,
                        IsDefault = value.isDefault,
                        Name = host.ItemName(value.skinItemId)
                    })))
                .Where(value => value != null)
                .ToList();
        }

        private SkinGroup CreateGroup(int type, string prefabName, IEnumerable<SkinOption> source)
        {
            List<SkinOption> all = source.OrderBy(value => value.Order).ThenBy(value => value.ItemId).ToList();
            SkinOption defaultSkin = all.FirstOrDefault(value => value.IsDefault);
            if (defaultSkin == null) return null;
            int equipped = EquippedItemId(category, type, defaultSkin.ItemId);
            List<SkinOption> available = all.Where(value => value.IsDefault || value.ItemId == equipped
                || host.ItemCount(value.ItemId) > 0).ToList();
            string localizationKey = category switch
            {
                0 => "Efurniture." + prefabName,
                1 => "HomeThemeType." + prefabName,
                _ => "HomeEnvironmentType.Weather"
            };
            string displayName = catalog.LocalizedText(localizationKey, prefabName);
            return new SkinGroup
            {
                Type = type,
                Name = displayName,
                DefaultItemId = defaultSkin.ItemId,
                Items = available
            };
        }

        private int EquippedItemId(int skinCategory, int type, int defaultItemId)
        {
            OfflineHomeSkinSelection selection = host.Session.SkinOf(skinCategory, type);
            return selection == null ? defaultItemId : selection.SkinItemId;
        }

        private void BuildSubTabs(IReadOnlyList<SkinGroup> groups)
        {
            string contentPath = ContentPath(category) + "/SubTabGrop/ScrollRect/Viewport/Content";
            Transform content = RequireTransform(contentPath);
            ClearChildren(content);
            ToggleGroup group = content.GetComponent<ToggleGroup>();
            if (group == null) group = content.gameObject.AddComponent<ToggleGroup>();
            group.allowSwitchOff = false;

            foreach (SkinGroup skinGroup in groups)
            {
                HomeUiPanel template = catalog.Panel(TemplateNames[category]);
                if (template?.root == null) throw new InvalidOperationException("The official home skin sub-tab template is missing.");
                var renderer = new HomeUiRenderer(catalog);
                GameObject item = renderer.CreateNode(template.root, content);
                item.name = "HomeSkinSubTab_" + skinGroup.Type;
                renderer.SetTextByName("text", skinGroup.Name);
                Toggle toggle = item.GetComponent<Toggle>();
                if (toggle == null) throw new InvalidOperationException("The official home skin sub-tab has no Toggle component.");
                toggle.group = group;
                LayoutElement layout = item.GetComponent<LayoutElement>() ?? item.AddComponent<LayoutElement>();
                layout.preferredWidth = 214f;
                layout.preferredHeight = 93f;
                layout.flexibleWidth = 0f;
                int type = skinGroup.Type;
                toggle.SetIsOnWithoutNotify(type == selectedType);
                toggle.onValueChanged.AddListener(isOn =>
                {
                    if (!isOn) return;
                    selectedType = type;
                    RenderItems(skinGroup);
                });
            }
        }

        private void RenderItems(SkinGroup skinGroup)
        {
            string contentPath = ContentPath(category) + "/SkinItemLoopScrollRect/Viewport/Content";
            Transform content = RequireTransform(contentPath);
            ClearChildren(content);
            int equipped = EquippedItemId(category, skinGroup.Type, skinGroup.DefaultItemId);
            HomeUiPanel template = catalog.Panel("decor_item");
            if (template?.root == null) throw new InvalidOperationException("The official home skin card template is missing.");

            foreach (SkinOption option in skinGroup.Items)
            {
                var renderer = new HomeUiRenderer(catalog);
                GameObject card = renderer.CreateNode(template.root, content, (path, node) =>
                {
                    if (!path.EndsWith("/HomeSkinItemIcon", StringComparison.Ordinal)) return;
                    Button button = node.GetComponent<Button>();
                    if (button != null) button.onClick.AddListener(() => SelectSkin(skinGroup, option));
                });
                card.name = "HomeSkinItem_" + option.ItemId;
                RectTransform rect = (RectTransform)card.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(208f, 208f);
                LayoutElement layout = card.GetComponent<LayoutElement>() ?? card.AddComponent<LayoutElement>();
                layout.preferredWidth = 208f;
                layout.preferredHeight = 208f;
                layout.flexibleWidth = 0f;

                Sprite icon = catalog.FindDynamicSprite("item:" + option.ItemId);
                if (icon == null) icon = host.ItemIcon(option.ItemId);
                string iconPath = "/HomeSkinItem/HomeSkinItemIcon/Content/mask/icon";
                string fakeIconPath = "/HomeSkinItem/HomeSkinItemIcon/Content/mask/FakeIcon";
                renderer.SetSprite(iconPath, icon);
                renderer.SetActive(fakeIconPath, icon == null);
                if (icon == null) renderer.SetText(fakeIconPath, option.Name);
                renderer.SetText("/HomeSkinItem/HomeSkinItemIcon/Content/BottomText", option.Name);
                bool isEquipped = option.ItemId == equipped;
                renderer.SetActive("/HomeSkinItem/HomeSkinItemIcon/Content/QualityImg/SelAnimGo", isEquipped);
                renderer.SetActive("/HomeSkinItem/NewMark", false);
                if (isEquipped) AddWearingMark(renderer.Find("/HomeSkinItem/HomeSkinItemIcon/Content/ItemIconAttachment"));
            }
        }

        private void SelectSkin(SkinGroup skinGroup, SkinOption option)
        {
            if (category == 0 && !host.HasFurnitureForSkin(skinGroup.Type))
            {
                hud.ShowNotice(catalog.LocalizedText("ui_preview_furniture_skin_not_unlock", "该家具尚未解锁"));
                return;
            }
            if (option.ItemId != EquippedItemId(category, skinGroup.Type, skinGroup.DefaultItemId))
            {
                string receipt = "home:skin:" + host.ActiveProfileSlotId + ":" + category + ":"
                    + skinGroup.Type + ":" + option.ItemId + ":" + Guid.NewGuid().ToString("N");
                if (!host.HomeProgression.EquipSkin(host.Session, category, skinGroup.Type,
                    option.ItemId, host.ItemCount, receipt))
                {
                    hud.ShowNotice("该装潢尚未解锁或无法穿戴");
                    return;
                }
                host.Scene.ApplyHomeSkins(catalog.HomeSkins, host.Session.Skins, host.Rules);
            }
            RenderItems(skinGroup);
        }

        private void AddWearingMark(GameObject attachment)
        {
            HomeUiPanel template = catalog.Panel("decor_wearing");
            if (attachment == null || template?.root == null) return;
            var renderer = new HomeUiRenderer(catalog);
            GameObject mark = renderer.CreateNode(template.root, attachment.transform);
            RectTransform rect = (RectTransform)mark.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private string ContentPath(int skinCategory) => PanelPrefix + "Content/" + ContentNames[skinCategory];

        private Transform RequireTransform(string path)
        {
            GameObject value = page.Find(path);
            if (value == null) throw new InvalidOperationException("The official home skin page is missing " + path);
            return value.transform;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Transform child = parent.GetChild(index);
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            hud.HomeSkinClosed(this);
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}

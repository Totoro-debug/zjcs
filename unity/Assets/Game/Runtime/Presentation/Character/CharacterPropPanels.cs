using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Character
{
    /// <summary>
    /// Binds the original character property rows, the overall/detailed property dialog and the
    /// expandable per-source groups to the offline property pipeline.
    /// </summary>
    public sealed class CharacterPropPanels : MonoBehaviour
    {
        private const string SummaryButtonPath =
            "/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotProp";
        private const string SummarySlotPath = SummaryButtonPath + "/SlotPropItems";
        private const string CharacterScreen = "/CharacterPanel";

        private OfficialCharacterAssetCatalog assets;
        private OfficialCharacterCatalog catalog;
        private Func<CharacterPropSnapshot> evaluate;
        private CharacterPanelNodeFactory factory;
        private Dictionary<string, object> rowTemplate;
        private Dictionary<string, object> groupTemplate;
        private OfficialCharacterPanelNode dialogTemplate;
        private Dictionary<string, object> dialogBindings;
        private Dictionary<string, object> overview;
        private GameObject dialog;
        private GameObject detailDialog;
        private RectTransform summarySlot;
        private readonly List<GameObject> summaryRows = new List<GameObject>();

        public bool HasDialog => dialog != null;
        public long CombatRating { get; private set; }
        public CharacterPropSnapshot Snapshot { get; private set; }

        public void Initialize(OfficialCharacterAssetCatalog assetCatalog, OfficialCharacterCatalog characterCatalog,
            Func<CharacterPropSnapshot> snapshotProvider)
        {
            assets = assetCatalog ?? throw new ArgumentNullException(nameof(assetCatalog));
            catalog = characterCatalog ?? throw new ArgumentNullException(nameof(characterCatalog));
            evaluate = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            factory = new CharacterPanelNodeFactory(assets);
            overview = CharacterModelPackage.AsObject(OfficialEquipmentJson.Parse(assets.CharacterOverview.text),
                "character overview");
            Dictionary<string, object> panels = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(assets.CharacterPanels.text), "character panels");
            Dictionary<string, object> screens = CharacterModelPackage.AsObject(panels["screens"], "screens");
            rowTemplate = CharacterModelPackage.AsObject(screens["character_prop_row"], "character_prop_row");
            groupTemplate = CharacterModelPackage.AsObject(screens["character_prop_group"], "character_prop_group");
            dialogTemplate = CharacterPanelNodeFactory.ReadRoot(
                CharacterModelPackage.AsObject(screens["character_props"], "character_props"));
            dialogBindings = panels.TryGetValue("bindings", out object bindings)
                && bindings is Dictionary<string, object> bindingMap ? bindingMap : new Dictionary<string, object>();
        }

        /// <summary>Rebuilds the summary rows shown on the character page.</summary>
        public void RefreshSummary(Transform characterRoot)
        {
            if (characterRoot == null || rowTemplate == null) return;
            if (summarySlot == null)
            {
                Transform slot = FindDeep(characterRoot, "SlotPropItems");
                if (slot == null) throw new FormatException("The original character page has no SlotPropItems slot.");
                summarySlot = (RectTransform)slot;
            }
            Snapshot = evaluate();
            CombatRating = Snapshot.CombatRating;
            foreach (GameObject row in summaryRows) if (row != null) Destroy(row);
            summaryRows.Clear();
            Dictionary<string, object> fields = CharacterModelPackage.AsObject(overview["row_fields"],
                "character row fields");
            string namePath = CharacterPanelNodeFactory.String(fields, "myPropTypeText");
            string valuePath = CharacterPanelNodeFactory.String(fields, "myPropValueText");
            string linePath = CharacterPanelNodeFactory.String(fields, "mLineIcon");
            IReadOnlyList<OfficialPropDefinition> rows = catalog.Props.ByCatalog((int)OfficialPropCatalogType.Main);
            int index = 0;
            bool hideLastLine = HideLastSummaryLine();
            foreach (OfficialPropDefinition definition in rows)
            {
                if (definition.PropType == 0) continue;
                long value = CharacterPropFormula.Raw(Snapshot.OutOfFightBattleProps, definition.PropType);
                long relative = definition.RelativeProp == 0 ? 0
                    : CharacterPropFormula.Raw(Snapshot.OutOfFightBattleProps, definition.RelativeProp);
                if (definition.HideWhenNoValue && value == 0 && relative == 0) continue;
                GameObject row = CreateRow(CharacterModelPackage.AsObject(overview["row"], "character row"),
                    summarySlot, namePath, valuePath, linePath, definition.Name, FormatValue(definition, value, relative));
                if (row == null) continue;
                AttachDetail(row, definition);
                summaryRows.Add(row);
                index++;
            }
            if (hideLastLine && summaryRows.Count > 0 && !string.IsNullOrEmpty(linePath))
            {
                Transform line = FindDeep(summaryRows[summaryRows.Count - 1].transform,
                    linePath.Substring(linePath.LastIndexOf('/') + 1));
                if (line != null) line.gameObject.SetActive(false);
            }
        }

        private bool HideLastSummaryLine()
        {
            Dictionary<string, object> bindings = CharacterModelPackage.AsObject(overview["bindings"],
                "character bindings");
            if (!bindings.TryGetValue("CharacterDetailPropsDialogOverallContent", out object raw)
                || !(raw is Dictionary<string, object> binding)
                || !binding.TryGetValue("fields", out object fieldRaw)
                || !(fieldRaw is Dictionary<string, object> fields))
                return false;
            return CharacterPanelNodeFactory.Boolean(fields, "mNeedHideLastLineImage", false);
        }

        /// <summary>Opens the original overall/detailed property dialog.</summary>
        public void OpenDialog(Transform parent, string titleKey)
        {
            if (dialog != null) return;
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            Snapshot = evaluate();
            CombatRating = Snapshot.CombatRating;
            var nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
            var overlay = new GameObject("CharacterDetailPropsDialog", typeof(RectTransform));
            overlay.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            dialog = overlay;
            var blocker = new GameObject("DialogBlocker", typeof(RectTransform), typeof(Image), typeof(Button));
            blocker.transform.SetParent(overlay.transform, false);
            RectTransform blockerRect = (RectTransform)blocker.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            blocker.GetComponent<Button>().onClick.AddListener(CloseDialog);

            var panel = new GameObject(dialogTemplate.name, typeof(RectTransform));
            panel.transform.SetParent(overlay.transform, false);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var tree = new Dictionary<string, RectTransform>(nodes);
            factory.Build(dialogTemplate, panelRect, tree, buttons);
            RectTransform overall = Find(tree, "OverallContent");
            RectTransform detailed = Find(tree, "DetailedContent");
            if (overall == null || detailed == null)
                throw new FormatException("The original property dialog has no overall or detailed content.");
            BuildOverall(overall, tree);
            BuildDetailed(detailed, tree);
            detailed.gameObject.SetActive(false);
            Button overallTab = FindButton(buttons, "/OverallTab");
            Button detailedTab = FindButton(buttons, "/DetailedTab");
            RectTransform overallSelected = Find(tree, "/OverallTab/Selected");
            RectTransform detailedSelected = Find(tree, "/DetailedTab/Selected");
            if (overallTab != null)
                overallTab.onClick.AddListener(() =>
                {
                    overall.gameObject.SetActive(true);
                    detailed.gameObject.SetActive(false);
                    if (overallSelected != null) overallSelected.gameObject.SetActive(true);
                    if (detailedSelected != null) detailedSelected.gameObject.SetActive(false);
                });
            if (detailedTab != null)
                detailedTab.onClick.AddListener(() =>
                {
                    overall.gameObject.SetActive(false);
                    detailed.gameObject.SetActive(true);
                    if (overallSelected != null) overallSelected.gameObject.SetActive(false);
                    if (detailedSelected != null) detailedSelected.gameObject.SetActive(true);
                });
            if (overallSelected != null) overallSelected.gameObject.SetActive(true);
            if (detailedSelected != null) detailedSelected.gameObject.SetActive(false);
            Button close = FindButton(buttons, "/CloseBtn");
            if (close != null) close.onClick.AddListener(CloseDialog);
        }

        public void CloseDialog()
        {
            if (dialog != null) Destroy(dialog);
            dialog = null;
        }

        public bool HandleEscape()
        {
            if (detailDialog != null)
            {
                CloseDetail();
                return true;
            }
            if (dialog == null) return false;
            CloseDialog();
            return true;
        }

        private void CloseDetail()
        {
            if (detailDialog != null) Destroy(detailDialog);
            detailDialog = null;
        }

        private void ShowPropDetail(OfficialPropDefinition definition)
        {
            if (definition == null) return;
            CloseDetail();
            Dictionary<string, object> panels = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(assets.CharacterPanels.text), "panels");
            Dictionary<string, object> screens = CharacterModelPackage.AsObject(panels["screens"], "screens");
            Dictionary<string, object> detailScreen = CharacterModelPackage.AsObject(screens["character_props"],
                "character_props");
            detailDialog = new GameObject("CharacterPropDetail", typeof(RectTransform));
            detailDialog.transform.SetParent(transform, false);
            RectTransform root = (RectTransform)detailDialog.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            var blocker = new GameObject("DetailBlocker", typeof(RectTransform), typeof(Image), typeof(Button));
            blocker.transform.SetParent(root, false);
            RectTransform blockerRect = (RectTransform)blocker.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            blocker.GetComponent<Button>().onClick.AddListener(CloseDetail);
            var panel = new GameObject("PropDetailDialog", typeof(RectTransform));
            panel.transform.SetParent(root, false);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
            factory.Build(CharacterPanelNodeFactory.ReadRoot(detailScreen), panelRect, nodes, buttons);
            RectTransform content = nodes.Values.FirstOrDefault(value => value.name == "OverallContent");
            Text title = nodes.Values.Select(value => value.GetComponent<Text>())
                .FirstOrDefault(text => text != null && text.name == "TitleText");
            if (title != null) title.text = definition.EntryName.Length > 0 ? definition.EntryName : definition.Name;
            if (content != null)
            {
                foreach (RectTransform child in content) child.gameObject.SetActive(false);
                Sprite icon = string.IsNullOrEmpty(definition.IconUri) ? null : assets.FindSprite(definition.IconUri);
                if (icon != null)
                {
                    var iconObject = new GameObject("PropDetailIcon", typeof(RectTransform), typeof(Image));
                    iconObject.transform.SetParent(content, false);
                    RectTransform iconRect = (RectTransform)iconObject.transform;
                    iconRect.anchorMin = new Vector2(0.5f, 0.86f);
                    iconRect.anchorMax = new Vector2(0.5f, 0.86f);
                    iconRect.sizeDelta = new Vector2(96f, 96f);
                    Image image = iconObject.GetComponent<Image>();
                    image.sprite = icon;
                    image.preserveAspect = true;
                }
                var body = new GameObject("PropDetailText", typeof(RectTransform), typeof(Text));
                body.transform.SetParent(content, false);
                RectTransform bodyRect = (RectTransform)body.transform;
                bodyRect.anchorMin = new Vector2(0.08f, 0.08f);
                bodyRect.anchorMax = new Vector2(0.92f, 0.78f);
                bodyRect.offsetMin = Vector2.zero;
                bodyRect.offsetMax = Vector2.zero;
                Text text = body.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = 25;
                text.color = Color.white;
                text.supportRichText = true;
                text.alignment = TextAnchor.UpperLeft;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.text = string.IsNullOrEmpty(definition.Description)
                    ? definition.Name : definition.Description;
            }
        }

        private void AttachDetail(GameObject row, OfficialPropDefinition definition)
        {
            if (row == null || definition == null) return;
            Image graphic = row.GetComponent<Image>();
            if (graphic == null)
            {
                graphic = row.AddComponent<Image>();
                graphic.color = new Color(1f, 1f, 1f, 0f);
            }
            Button button = row.GetComponent<Button>() ?? row.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.onClick.AddListener(() => ShowPropDetail(definition));
            foreach (Button child in row.GetComponentsInChildren<Button>(true))
                if (child != button) child.onClick.AddListener(() => ShowPropDetail(definition));
        }

        private void BuildOverall(RectTransform content, Dictionary<string, RectTransform> nodes)
        {
            Dictionary<string, object> bindings = Binding("CharacterDetailPropsDialogOverallContent");
            if (bindings == null) return;
            List<object> configs = bindings.TryGetValue("PropCatalogCfgList", out object raw)
                && raw is List<object> list ? list : new List<object>();
            foreach (object entry in configs)
            {
                if (!(entry is Dictionary<string, object> config)) continue;
                int catalogId = (int)CharacterPanelNodeFactory.Number(config, "PropCatalog", 0f);
                string slotPath = CharacterPanelNodeFactory.String(config, "PropSlot");
                string titlePath = CharacterPanelNodeFactory.String(config, "PropTitle");
                RectTransform slot = Find(nodes, slotPath);
                RectTransform title = string.IsNullOrEmpty(titlePath) ? null : Find(nodes, titlePath);
                if (slot == null) continue;
                int count = 0;
                foreach (OfficialPropDefinition definition in catalog.Props.ByCatalog(catalogId))
                {
                    if (definition.PropType == 0) continue;
                    long value = CharacterPropFormula.Raw(Snapshot.OutOfFightBattleProps, definition.PropType);
                    long relative = definition.RelativeProp == 0 ? 0
                        : CharacterPropFormula.Raw(Snapshot.OutOfFightBattleProps, definition.RelativeProp);
                    if (definition.HideWhenNoValue && value == 0 && relative == 0) continue;
                    GameObject row = CreateRow(rowTemplate, slot,
                        "/Key", "/Text", "/Button", definition.Name, FormatValue(definition, value, relative));
                    if (row == null) continue;
                    AttachDetail(row, definition);
                    count++;
                }
                if (title != null) title.gameObject.SetActive(count > 0);
                slot.gameObject.SetActive(count > 0);
            }
        }

        private void BuildDetailed(RectTransform content, Dictionary<string, RectTransform> nodes)
        {
            Dictionary<string, object> bindings = Binding("CharacterDetailPropDialogDetailPropItem");
            if (bindings == null) return;
            string slotPath = CharacterPanelNodeFactory.String(bindings, "mPropSlot");
            RectTransform slot = Find(nodes, slotPath);
            if (slot == null) return;
            IReadOnlyList<int> showCatalogs = CatalogIds(bindings);
            int count = 0;
            foreach (int category in Snapshot.DetailOrder)
            {
                if (!Snapshot.DetailProps.TryGetValue(category, out IReadOnlyDictionary<int, long> source)
                    || source.Count == 0) continue;
                var groupNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                var groupButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
                RectTransform group = factory.Build(CharacterPanelNodeFactory.ReadRoot(groupTemplate), slot, groupNodes, groupButtons);
                Text title = FindText(groupNodes, "/Key");
                if (title != null) title.text = catalog.Props.CategoryName(category);
                RectTransform detailSlot = Find(groupNodes, "/RolePowerDetail");
                if (detailSlot == null) continue;
                var props = new List<int>();
                foreach (KeyValuePair<int, long> entry in source)
                {
                    if (entry.Value == 0) continue;
                    if (catalog.Props.TryGetMainPropByRelativeProp(entry.Key, out int owner)
                        && source.ContainsKey(owner)) continue;
                    if (!showCatalogs.Contains(catalog.Props.Get(entry.Key).Catalog)) continue;
                    props.Add(entry.Key);
                }
                props.Sort();
                int rows = 0;
                foreach (int propType in props)
                {
                    OfficialPropDefinition definition = catalog.Props.Get(propType);
                    long value = source[propType];
                    long relative = definition.RelativeProp == 0 ? 0
                        : CharacterPropFormula.Raw(source, definition.RelativeProp);
                    GameObject row = CreateRow(rowTemplate, detailSlot,
                        "/Key", "/Text", "/Button", definition.Name, FormatValue(definition, value, relative));
                    if (row == null) continue;
                    Image background = row.transform.childCount > 0
                        ? row.transform.GetChild(0).GetComponent<Image>() : null;
                    if (background != null) background.enabled = rows % 2 == 0;
                    AttachDetail(row, definition);
                    rows++;
                }
                detailSlot.gameObject.SetActive(false);
                Button fold = FindButton(groupButtons, "/FoldExpandBtn");
                RectTransform arrow = Find(groupNodes, "/FoldExpandImg");
                if (fold != null)
                    fold.onClick.AddListener(() =>
                    {
                        bool open = !detailSlot.gameObject.activeSelf;
                        detailSlot.gameObject.SetActive(open);
                        if (arrow != null) arrow.localRotation = Quaternion.Euler(0f, 0f, open ? 180f : 0f);
                    });
                group.gameObject.SetActive(rows > 0);
                if (rows > 0) count++;
            }
            content.gameObject.SetActive(count > 0);
        }

        private static IReadOnlyList<int> CatalogIds(Dictionary<string, object> bindings)
        {
            List<int> result = new List<int>();
            if (bindings.TryGetValue("mPropCatalogList", out object raw) && raw is List<object> list)
                foreach (object entry in list) result.Add(Convert.ToInt32(entry, CultureInfo.InvariantCulture));
            return result;
        }

        private Dictionary<string, object> Binding(string name)
        {
            if (!dialogBindings.TryGetValue("dialog", out object raw)
                || !(raw is Dictionary<string, object> dialogMap)) return null;
            return dialogMap.TryGetValue(name, out object value) ? value as Dictionary<string, object> : null;
        }

        private GameObject CreateRow(Dictionary<string, object> template, RectTransform parent, string namePath,
            string valuePath, string linePath, string name, string value)
        {
            OfficialCharacterPanelNode node = CharacterPanelNodeFactory.ReadRoot(template);
            var nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
            RectTransform row = factory.Build(node, parent, nodes, buttons);
            Text nameText = FindText(nodes, namePath);
            Text valueText = FindText(nodes, valuePath);
            if (nameText == null || valueText == null)
            {
                Destroy(row.gameObject);
                throw new FormatException("Original property row template has no name or value text.");
            }
            nameText.text = name;
            valueText.text = value;
            if (!string.IsNullOrEmpty(linePath))
            {
                RectTransform line = Find(nodes, linePath);
                if (line != null) line.gameObject.SetActive(false);
            }
            return row.gameObject;
        }

        private string FormatValue(OfficialPropDefinition definition, long value, long relative)
        {
            string text = OfficialPropFormat.Value(definition, value);
            if (relative != 0)
            {
                OfficialPropDefinition relativeDefinition = catalog.Props.Get(definition.RelativeProp)
                    ?? definition;
                text += "+" + OfficialPropFormat.Value(relativeDefinition, relative);
            }
            return text;
        }

        private static RectTransform Find(Dictionary<string, RectTransform> nodes, string suffix)
        {
            if (string.IsNullOrEmpty(suffix)) return null;
            if (nodes.TryGetValue(suffix, out RectTransform exact)) return exact;
            return nodes.Where(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal))
                .Select(pair => pair.Value).FirstOrDefault();
        }

        private static Text FindText(Dictionary<string, RectTransform> nodes, string suffix)
        {
            RectTransform rect = Find(nodes, suffix);
            return rect == null ? null : rect.GetComponent<Text>();
        }

        private static Button FindButton(Dictionary<string, Button> buttons, string suffix)
        {
            if (buttons.TryGetValue(suffix, out Button exact)) return exact;
            return buttons.Where(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal))
                .Select(pair => pair.Value).FirstOrDefault();
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}

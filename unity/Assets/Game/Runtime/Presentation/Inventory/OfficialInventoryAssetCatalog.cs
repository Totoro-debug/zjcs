using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Offline.Equipment;

namespace Zjcs.Presentation.Inventory
{
    public sealed class OfficialInventoryTab
    {
        public string Path { get; internal set; }
        public string NameKey { get; internal set; }
        public IReadOnlyList<int> ItemTypes { get; internal set; }
    }

    [Serializable]
    public sealed class InventorySpriteBinding
    {
        public string sourcePath;
        public Sprite sprite;
    }

    [CreateAssetMenu(menuName = "Zjcs/Inventory/Official Inventory Asset Catalog")]
    public sealed class OfficialInventoryAssetCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "Inventory/OfficialInventoryAssetCatalog";
        private static readonly string[] QualityBackgroundFiles =
        {
            "ImgQuality_White-031a03cab0.png",
            "ImgQuality_White-031a03cab0.png",
            "ImgQuality_Blue-42b8cb9630.png",
            "ImgQuality_Purple-0b40a53021.png",
            "ImgQuality_Orange-78887320b0.png",
            "ImgQuality_Gold-5f018c42e0.png",
            "ImgQuality_Red-caa09e6943.png",
            "ImgQuality_Multicolour-93374ebc65.png"
        };

        [SerializeField] private TextAsset uiData;
        [SerializeField] private TextAsset sourceManifest;
        [SerializeField] private TextAsset itemSourceUiData;
        [SerializeField] private TextAsset itemSourceManifest;
        [SerializeField] private InventorySpriteBinding[] sprites = Array.Empty<InventorySpriteBinding>();

        private Dictionary<string, object> parsed;
        private Dictionary<string, object> parsedItemSources;
        private Dictionary<string, Sprite> spriteByName;
        private bool bindingsValidated;

        public static OfficialInventoryAssetCatalog LoadDefault()
        {
            OfficialInventoryAssetCatalog catalog = Resources.Load<OfficialInventoryAssetCatalog>(DefaultResourcePath);
            if (catalog == null) throw new InvalidOperationException("Official inventory assets are not installed.");
            catalog.ValidateBindings();
            return catalog;
        }

        public Sprite FindSprite(string sourceUri)
        {
            string name = Path.GetFileName(sourceUri ?? string.Empty);
            if (spriteByName == null)
            {
                spriteByName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (InventorySpriteBinding binding in sprites ?? Array.Empty<InventorySpriteBinding>())
                    if (binding != null && !string.IsNullOrEmpty(binding.sourcePath) && binding.sprite != null)
                        spriteByName[binding.sourcePath] = binding.sprite;
            }
            return spriteByName.TryGetValue(name, out Sprite sprite) ? sprite : null;
        }

        public Sprite FindItemSprite(int itemId)
        {
            Dictionary<string, object> icons = Object(Parsed()["icons"]);
            if (!icons.TryGetValue(itemId.ToString(System.Globalization.CultureInfo.InvariantCulture), out object raw)) return null;
            Dictionary<string, object> icon = Object(raw);
            return FindSprite(Text(icon, "file"));
        }

        public Sprite FindQualityBackground(int quality)
        {
            return quality >= 0 && quality < QualityBackgroundFiles.Length
                ? FindSprite(QualityBackgroundFiles[quality]) : null;
        }

        public IReadOnlyDictionary<int, int> FindRandomTreasurePieceRequirements()
        {
            Dictionary<string, object> tables = Object(Parsed()["tables"]);
            var requirements = new Dictionary<int, int>();
            if (!tables.TryGetValue("random_treasure_exchange", out object rawRows)
                || !(rawRows is List<object> rows))
                throw new FormatException("Official random treasure exchange requirements are missing.");
            foreach (object rawRow in rows)
            {
                Dictionary<string, object> row = Object(rawRow);
                int itemId = Convert.ToInt32(row["ItemId"], System.Globalization.CultureInfo.InvariantCulture);
                int requireCount = Convert.ToInt32(row["RequireCount"], System.Globalization.CultureInfo.InvariantCulture);
                if (itemId <= 0 || requireCount <= 0 || !requirements.TryAdd(itemId, requireCount))
                    throw new FormatException("Official random treasure exchange requirements are invalid.");
            }
            return requirements;
        }

        public string FindText(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            Dictionary<string, object> texts = Object(Parsed()["texts"]);
            return texts.TryGetValue(key, out object value)
                ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        }

        public string FindItemSourceText(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            Dictionary<string, object> texts = Object(ItemSourcesParsed()["texts"]);
            if (texts.TryGetValue(key, out object value))
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            if (key == "ItemSourceType.PreciousChestGacha") return "国度扭蛋获取";
            if (key == "ItemSourceType.ElementChestGacha") return "元素扭蛋获取";
            return FindText(key);
        }

        public Dictionary<string, object> LoadItemSourceTemplate()
        {
            ValidateBindings();
            return Object(Object(ItemSourcesParsed()["template"])["root"]);
        }

        public string FindItemTypeText(int itemType)
        {
            Dictionary<string, object> itemTypes = Object(Parsed()["item_types"]);
            string id = itemType.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return itemTypes.TryGetValue(id, out object key)
                ? FindText(Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture)) : string.Empty;
        }

        public string FindItemIconTemplateName(int itemId, int itemType)
        {
            Dictionary<string, object> templates = Object(Parsed()["icon_templates"]);
            if (templates.TryGetValue("id", out object rawIdTemplates)
                && Object(rawIdTemplates).TryGetValue(itemId.ToString(System.Globalization.CultureInfo.InvariantCulture), out object idTemplate))
                return Convert.ToString(idTemplate, System.Globalization.CultureInfo.InvariantCulture);
            if (templates.TryGetValue("type", out object rawTypeTemplates)
                && Object(rawTypeTemplates).TryGetValue(itemType.ToString(System.Globalization.CultureInfo.InvariantCulture), out object typeTemplate))
                return Convert.ToString(typeTemplate, System.Globalization.CultureInfo.InvariantCulture);
            Dictionary<string, object> fallback = Object(templates["default"]);
            return fallback.TryGetValue("0", out object defaultTemplate)
                ? Convert.ToString(defaultTemplate, System.Globalization.CultureInfo.InvariantCulture)
                : string.Empty;
        }

        public static string GetMissingIconText(string localizedItemName)
        {
            if (string.IsNullOrEmpty(localizedItemName)) return string.Empty;
            int start = Math.Max(0, localizedItemName.Length - 4);
            return localizedItemName.Substring(start);
        }

        public IReadOnlyList<OfficialInventoryTab> LoadBagTabs()
        {
            Dictionary<string, object> template = LoadTemplate("bag");
            if (template == null || !template.TryGetValue("root", out object root))
                throw new FormatException("Official inventory panel root is missing.");
            var tabs = new List<OfficialInventoryTab>();
            CollectTabs(Object(root), tabs);
            if (tabs.Count == 0) throw new FormatException("Official inventory tab bindings are missing.");
            return tabs;
        }

        public Dictionary<string, object> LoadTemplate(string name)
        {
            ValidateBindings();
            Dictionary<string, object> templates = Object(Parsed()["templates"]);
            return templates.TryGetValue(name, out object raw) ? Object(raw) : null;
        }

        public Dictionary<string, object> LoadRowTemplate(string name)
        {
            ValidateBindings();
            Dictionary<string, object> templates = Object(Parsed()["templates"]);
            return templates.TryGetValue(name, out object raw) ? Object(raw) : null;
        }

        public Dictionary<string, object> LoadRowTemplate(string panelName, string componentPath)
        {
            ValidateBindings();
            Dictionary<string, object> rowGroups = Object(Parsed()["row_templates"]);
            if (!rowGroups.TryGetValue(panelName, out object rawRows)) return null;
            Dictionary<string, object> rows = Object(rawRows);
            return rows.TryGetValue(componentPath, out object rowName)
                ? LoadRowTemplate(Convert.ToString(rowName, System.Globalization.CultureInfo.InvariantCulture))
                : null;
        }

        public Dictionary<string, object> LoadAttachmentTemplate(int attachmentId)
        {
            Dictionary<string, object> attachments = Object(Parsed()["attachments"]);
            if (!attachments.TryGetValue(attachmentId.ToString(System.Globalization.CultureInfo.InvariantCulture), out object raw))
                return null;
            return LoadTemplate(Text(Object(raw), "template"));
        }

        public int FindAttachmentLayer(int attachmentId)
        {
            Dictionary<string, object> attachments = Object(Parsed()["attachments"]);
            if (!attachments.TryGetValue(attachmentId.ToString(System.Globalization.CultureInfo.InvariantCulture), out object raw))
                throw new FormatException("Official inventory icon attachment is missing: " + attachmentId);
            Dictionary<string, object> attachment = Object(raw);
            return attachment.TryGetValue("layer", out object layer)
                ? Convert.ToInt32(layer, System.Globalization.CultureInfo.InvariantCulture) : 0;
        }

        public void ValidateBindings()
        {
            if (bindingsValidated) return;
            if (uiData == null || sourceManifest == null || itemSourceUiData == null
                || itemSourceManifest == null || sprites == null || sprites.Length < 1500)
                throw new InvalidOperationException("Official inventory data and sprite bindings are incomplete.");
            Dictionary<string, object> root = Parsed();
            Dictionary<string, object> itemSourceManifestData = Object(OfficialEquipmentJson.Parse(itemSourceManifest.text));
            Dictionary<string, object> itemSourceTemplate = Object(Object(ItemSourcesParsed()["template"])["root"]);
            if (Text(root, "version") != "854_156383"
                || Object(root["templates"]).Count < 20
                || Object(root["row_templates"]).Count < 20
                || Text(itemSourceManifestData, "version") != "854_156383"
                || Text(itemSourceTemplate, "name") != "ItemSourceItem")
                throw new FormatException("Official inventory UI metadata is incomplete or from an unsupported manifest.");
            foreach (InventorySpriteBinding binding in sprites)
                if (binding == null || string.IsNullOrEmpty(binding.sourcePath) || binding.sprite == null)
                    throw new InvalidOperationException("An official inventory sprite binding is missing.");
            for (int quality = 0; quality < 8; quality++)
                if (FindQualityBackground(quality) == null)
                    throw new InvalidOperationException("An official inventory quality background is not bound: " + quality);

            Dictionary<string, object> templates = Object(root["templates"]);
            foreach (object rawTemplate in templates.Values)
            {
                Dictionary<string, object> template = Object(rawTemplate);
                if (template.TryGetValue("root", out object rawTemplateRoot))
                    ValidateTreeSprites(Object(rawTemplateRoot));
            }
            Dictionary<string, object> attachments = Object(root["attachments"]);
            foreach (object rawAttachment in attachments.Values)
            {
                string templateName = Text(Object(rawAttachment), "template");
                if (!templates.TryGetValue(templateName, out object attachmentTemplate)
                    || !Object(attachmentTemplate).TryGetValue("root", out object attachmentRoot))
                    throw new FormatException("Official inventory attachment template is missing: " + templateName);
                ValidateTreeSprites(Object(attachmentRoot));
            }
            ValidateTreeSprites(itemSourceTemplate);
            Dictionary<string, object> icons = Object(root["icons"]);
            foreach (object rawIcon in icons.Values)
            {
                Dictionary<string, object> icon = Object(rawIcon);
                if (FindSprite(Text(icon, "file")) == null)
                    throw new InvalidOperationException("An official inventory item sprite is not bound: " + Text(icon, "file"));
            }
            bindingsValidated = true;
        }

        private Dictionary<string, object> Parsed()
        {
            if (parsed == null) parsed = Object(OfficialEquipmentJson.Parse(uiData.text));
            return parsed;
        }

        private Dictionary<string, object> ItemSourcesParsed()
        {
            if (parsedItemSources == null)
                parsedItemSources = Object(OfficialEquipmentJson.Parse(itemSourceUiData.text));
            return parsedItemSources;
        }

        private static Dictionary<string, object> Object(object value) =>
            value as Dictionary<string, object> ?? throw new FormatException("Official inventory JSON object is missing.");

        private static string Text(Dictionary<string, object> value, string key) =>
            value.TryGetValue(key, out object raw) ? Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture) : string.Empty;

        private static void CollectTabs(Dictionary<string, object> node, List<OfficialInventoryTab> tabs)
        {
            if (node.TryGetValue("original_bindings", out object rawBindings)
                && rawBindings is Dictionary<string, object> bindings
                && bindings.TryGetValue("ItemTypesToggle", out object rawToggle)
                && bindings.TryGetValue("PlayerBagPanelTabName", out object rawTab))
            {
                Dictionary<string, object> toggle = Object(rawToggle);
                Dictionary<string, object> tab = Object(rawTab);
                if (toggle.TryGetValue("myTypes", out object rawTypes) && rawTypes is List<object> values)
                {
                    var types = new List<int>(values.Count);
                    foreach (object value in values)
                        types.Add(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
                    tabs.Add(new OfficialInventoryTab
                    {
                        Path = Text(node, "path"),
                        NameKey = Text(tab, "NameKey"),
                        ItemTypes = types
                    });
                }
            }
            if (node.TryGetValue("children", out object children) && children is List<object> childNodes)
                foreach (object child in childNodes)
                    if (child is Dictionary<string, object> childNode) CollectTabs(childNode, tabs);
        }

        private void ValidateTreeSprites(Dictionary<string, object> node)
        {
            if (node.TryGetValue("image", out object rawImage) && rawImage is Dictionary<string, object> image
                && image.TryGetValue("sprite", out object rawSprite) && rawSprite is Dictionary<string, object> sprite
                && FindSprite(Text(sprite, "file")) == null)
                throw new InvalidOperationException("An official inventory UI sprite is not bound: " + Text(sprite, "file"));
            if (node.TryGetValue("children", out object rawChildren) && rawChildren is List<object> children)
                foreach (object rawChild in children)
                    if (rawChild is Dictionary<string, object> child) ValidateTreeSprites(child);
        }
    }
}

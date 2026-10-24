using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.CharacterCreation
{
    public sealed class CharacterCreationOptionInfo
    {
        public string Id { get; internal set; }
        public int SkinId { get; internal set; }
        public int SkinType { get; internal set; }
        public string Part { get; internal set; }
        public string SkinName { get; internal set; }
        public string Name { get; internal set; }
        public int Weight { get; internal set; }
    }

    public sealed class CharacterCreationSkinInfo
    {
        public int Id { get; internal set; }
        public int Type { get; internal set; }
        public string Name { get; internal set; }
        public bool HasDye { get; internal set; }
    }

    [Serializable]
    public sealed class CharacterCreationAudioClipBinding
    {
        public string eventName;
        public AudioClip clip;
    }

    [Serializable]
    public sealed class CharacterCreationSkinIconBinding
    {
        public string skinName;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class CharacterCreationAppearanceGroupIconBinding
    {
        public int groupId;
        public int sex;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class CharacterCreationAppearanceGroupSkin
    {
        public int appearance_id;
        public int skin_id;
        public int skin_type;
        public string skin_name;
    }

    [Serializable]
    public sealed class CharacterCreationAppearanceGroupInfo
    {
        public int group_id;
        public string sex;
        public int item_id;
        public string localized_label;
        public int condition_id;
        public int[] appearance_ids;
        public CharacterCreationAppearanceGroupSkin[] appearance_skins;
    }

    [Serializable]
    internal sealed class CharacterCreationAppearanceGroupManifest
    {
        public string manifest;
        public CharacterCreationAppearanceGroupInfo[] groups;
    }

    [CreateAssetMenu(menuName = "Zjcs/Character Creation Asset Catalog")]
    public sealed class CharacterCreationAssetCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "CharacterCreation/OfficialCharacterCreationAssetCatalog";
        private static readonly string[] AppearanceFields =
        {
            "hairstyle_id", "eyebrow_id", "eye_id", "mouth_id", "makeup_id", "feature_id"
        };

        [SerializeField] private TextAsset creationPerformance;
        [SerializeField] private TextAsset appearanceUi;
        [SerializeField] private TextAsset appearanceOptions;
        [SerializeField] private TextAsset wardrobeRules;
        [SerializeField] private TextAsset wardrobeUi;
        [SerializeField] private TextAsset wardrobeItems;
        [SerializeField] private TextAsset sourceManifest;
        [SerializeField] private TextAsset createPlayerUiSourceManifest;
        [SerializeField] private TextAsset confirmDialogSourceManifest;
        [SerializeField] private TextAsset appearanceConfigRules;
        [SerializeField] private GameObject createPlayerPanelPrefab;
        [SerializeField] private GameObject createPlayerDiyContentPrefab;
        [SerializeField] private GameObject confirmDialogPrefab;
        [SerializeField] private string[] creationPageTitles;
        public string CreationPageTitle(int step) => creationPageTitles[step];
        [SerializeField] private Shader nativeCreationShader;
        public Shader NativeCreationShader => nativeCreationShader;
        [SerializeField] private GameObject maleNativeModelPrefab;
        [SerializeField] private GameObject femaleNativeModelPrefab;
        [SerializeField] private UnityEngine.Object officialFont;
        [SerializeField] private CharacterCreationAudioClipBinding[] audioBindings;
        [SerializeField] private TextAsset skinIconSourceManifest;
        [SerializeField] private CharacterCreationSkinIconBinding[] skinIcons;
        [SerializeField] private TextAsset appearanceGroupIconSourceManifest;
        [SerializeField] private CharacterCreationAppearanceGroupIconBinding[] appearanceGroupIcons;

        [NonSerialized] private CharacterAppearanceOptions parsedAppearanceOptions;
        [NonSerialized] private CharacterCreationAppearanceRules parsedAppearanceRules;
        [NonSerialized] private Dictionary<string, Dictionary<int, int>> defaultSkinsByProfession;
        [NonSerialized] private Dictionary<string, Sprite> skinIconsByName;
        [NonSerialized] private Dictionary<int, List<CharacterCreationAppearanceGroupInfo>> appearanceGroupsBySex;

        public TextAsset CreationPerformance => creationPerformance;
        public TextAsset AppearanceUi => appearanceUi;
        public TextAsset AppearanceOptions => appearanceOptions;
        public TextAsset WardrobeRules => wardrobeRules;
        public TextAsset WardrobeUi => wardrobeUi;
        public TextAsset WardrobeItems => wardrobeItems;
        public TextAsset SourceManifest => sourceManifest;
        public TextAsset CreatePlayerUiSourceManifest => createPlayerUiSourceManifest;
        public TextAsset ConfirmDialogSourceManifest => confirmDialogSourceManifest;
        public TextAsset AppearanceConfigRules => appearanceConfigRules;
        public GameObject CreatePlayerPanelPrefab => createPlayerPanelPrefab;
        public GameObject CreatePlayerDiyContentPrefab => createPlayerDiyContentPrefab;
        public GameObject ConfirmDialogPrefab => confirmDialogPrefab;
        public GameObject MaleNativeModelPrefab => maleNativeModelPrefab;
        public GameObject FemaleNativeModelPrefab => femaleNativeModelPrefab;
        public UnityEngine.Object OfficialFont => officialFont;
        public TextAsset AppearanceGroupIconSourceManifest => appearanceGroupIconSourceManifest;

        public Sprite FindSkinIcon(string skinName)
        {
            return GetSkinIconIndex().TryGetValue(skinName ?? string.Empty, out Sprite sprite) ? sprite : null;
        }

        public Sprite FindAppearanceGroupIcon(int groupId, int sex)
        {
            foreach (CharacterCreationAppearanceGroupIconBinding binding
                in appearanceGroupIcons ?? Array.Empty<CharacterCreationAppearanceGroupIconBinding>())
                if (binding != null && binding.groupId == groupId && binding.sex == sex)
                    return binding.sprite;
            return null;
        }

        public IReadOnlyList<CharacterCreationAppearanceGroupInfo> GetAppearanceGroups(int sex)
        {
            if (sex != 0 && sex != 1) throw new ArgumentOutOfRangeException(nameof(sex));
            if (appearanceGroupsBySex == null)
                appearanceGroupsBySex = ParseAppearanceGroups(appearanceGroupIconSourceManifest);
            return appearanceGroupsBySex[sex];
        }

        public int DefaultAppearanceGroupId(int sex)
        {
            if (sex != 0 && sex != 1) throw new ArgumentOutOfRangeException(nameof(sex));
            int expectedId = sex == 0 ? 1 : 11;
            foreach (CharacterCreationAppearanceGroupInfo group in GetAppearanceGroups(sex))
                if (group.group_id == expectedId && group.condition_id == 0) return group.group_id;
            throw new InvalidOperationException("The official default appearance group is missing for sex " + sex + ".");
        }

        public bool TryGetAppearanceGroup(int sex, int groupId, out CharacterCreationAppearanceGroupInfo group)
        {
            foreach (CharacterCreationAppearanceGroupInfo candidate in GetAppearanceGroups(sex))
                if (candidate.group_id == groupId)
                {
                    group = candidate;
                    return true;
                }
            group = null;
            return false;
        }

        public bool IsSelectableAppearanceGroup(int sex, int groupId)
        {
            return TryGetAppearanceGroup(sex, groupId, out CharacterCreationAppearanceGroupInfo group)
                && group.condition_id == 0;
        }

        public AudioClip FindAudio(string eventName)
        {
            foreach (CharacterCreationAudioClipBinding binding in audioBindings ?? Array.Empty<CharacterCreationAudioClipBinding>())
                if (binding != null && binding.eventName == eventName) return binding.clip;
            return null;
        }

        public IReadOnlyList<CharacterCreationOptionInfo> GetAppearanceOptions(string bodyType, string field)
        {
            if (parsedAppearanceOptions == null)
                parsedAppearanceOptions = CharacterAppearanceOptions.Parse(appearanceOptions);
            return parsedAppearanceOptions.GetOptions(bodyType, field);
        }

        public bool TryGetAppearanceOption(string bodyType, string field, string optionId,
            out CharacterCreationOptionInfo option)
        {
            if (parsedAppearanceOptions == null)
                parsedAppearanceOptions = CharacterAppearanceOptions.Parse(appearanceOptions);
            return parsedAppearanceOptions.TryGetOption(bodyType, field, optionId, out option);
        }

        public bool TryGetSkinInfo(int skinId, out CharacterCreationSkinInfo info)
        {
            if (parsedAppearanceOptions == null)
                parsedAppearanceOptions = CharacterAppearanceOptions.Parse(appearanceOptions);
            return parsedAppearanceOptions.TryGetSkinInfo(skinId, out info);
        }

        public IReadOnlyList<CharacterCreationSkinInfo> GetDefaultSkinInfos(int sex, int profession)
        {
            var result = new List<CharacterCreationSkinInfo>();
            for (int skinType = 0; skinType <= 12; skinType++)
            {
                if (TryGetDefaultSkinId(sex, profession, skinType, out int skinId)
                    && TryGetSkinInfo(skinId, out CharacterCreationSkinInfo info))
                    result.Add(info);
            }
            return result;
        }

        public bool TryGetDefaultSkinId(int sex, int profession, int skinType, out int skinId)
        {
            ValidateBindings();
            if (defaultSkinsByProfession == null)
                defaultSkinsByProfession = ReadDefaultSkins(wardrobeRules.text);
            string professionKey = sex.ToString(CultureInfo.InvariantCulture) + ":"
                + profession.ToString(CultureInfo.InvariantCulture);
            if (defaultSkinsByProfession.TryGetValue(professionKey, out Dictionary<int, int> skins)
                && skins.TryGetValue(skinType, out skinId)) return true;

            skinId = 0;
            return false;
        }

        public bool TryGetDefaultAppearanceOption(string bodyType, int sex, int profession, string field,
            out CharacterCreationOptionInfo option)
        {
            IReadOnlyList<CharacterCreationOptionInfo> options = GetAppearanceOptions(bodyType, field);
            if (options.Count == 0)
            {
                option = null;
                return false;
            }

            int skinType = options[0].SkinType;
            if (options.Any(value => value.SkinType != skinType)
                || !TryGetDefaultSkinId(sex, profession, skinType, out int skinId))
            {
                option = null;
                return false;
            }

            option = options.FirstOrDefault(value => value.SkinId == skinId);
            return option != null;
        }

        public bool HasAppearanceOption(string bodyType, string field, string optionId)
        {
            if (parsedAppearanceOptions == null)
                parsedAppearanceOptions = CharacterAppearanceOptions.Parse(appearanceOptions);
            return parsedAppearanceOptions.Contains(bodyType, field, optionId);
        }

        public bool TryGetSkinRule(int skinType, out CharacterCreationSkinTypeRule rule)
        {
            if (parsedAppearanceRules == null)
                parsedAppearanceRules = CharacterCreationAppearanceRules.Parse(appearanceConfigRules);
            return parsedAppearanceRules.TryGet(skinType, out rule);
        }

        public static CharacterCreationAssetCatalog LoadDefault()
        {
            CharacterCreationAssetCatalog catalog = Resources.Load<CharacterCreationAssetCatalog>(DefaultResourcePath);
            if (catalog == null) throw new InvalidOperationException("Official character creation assets are not installed.");
            catalog.ValidateBindings();
            return catalog;
        }

        private void OnValidate()
        {
            skinIconsByName = null;
            appearanceGroupsBySex = null;
        }

        public void ValidateBindings()
        {
            if (creationPerformance == null || appearanceUi == null || appearanceOptions == null
                || wardrobeRules == null || wardrobeUi == null || wardrobeItems == null || sourceManifest == null
                || createPlayerUiSourceManifest == null || confirmDialogSourceManifest == null
                || appearanceConfigRules == null || createPlayerPanelPrefab == null
                || createPlayerDiyContentPrefab == null || confirmDialogPrefab == null
                || officialFont == null
                || audioBindings == null || audioBindings.Length != 4
                || skinIconSourceManifest == null || skinIcons == null || skinIcons.Length == 0
                || appearanceGroupIconSourceManifest == null || appearanceGroupIcons == null
                || appearanceGroupIcons.Length != 12)
                throw new InvalidOperationException("Official character creation assets are not bound.");
            var audioEvents = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterCreationAudioClipBinding binding in audioBindings)
                if (binding == null || string.IsNullOrEmpty(binding.eventName) || binding.clip == null
                    || !audioEvents.Add(binding.eventName))
                    throw new InvalidOperationException("An official character creation audio binding is missing or duplicated.");
            if (parsedAppearanceOptions == null)
                parsedAppearanceOptions = CharacterAppearanceOptions.Parse(appearanceOptions);
            if (parsedAppearanceRules == null)
                parsedAppearanceRules = CharacterCreationAppearanceRules.Parse(appearanceConfigRules);

            Dictionary<string, Sprite> icons = GetSkinIconIndex();
            var groupIconKeys = new HashSet<int>();
            foreach (CharacterCreationAppearanceGroupIconBinding binding in appearanceGroupIcons)
            {
                if (binding == null || binding.sprite == null
                    || (binding.sex != 0 && binding.sex != 1)
                    || !IsExpectedAppearanceGroup(binding.groupId, binding.sex)
                    || !groupIconKeys.Add(binding.groupId))
                    throw new InvalidOperationException("An official appearance group icon binding is invalid or duplicated.");
            }
            appearanceGroupsBySex = ParseAppearanceGroups(appearanceGroupIconSourceManifest);
            foreach (KeyValuePair<int, List<CharacterCreationAppearanceGroupInfo>> entry in appearanceGroupsBySex)
                foreach (CharacterCreationAppearanceGroupInfo group in entry.Value)
                {
                    if (FindAppearanceGroupIcon(group.group_id, entry.Key) == null
                        || group.appearance_skins == null || group.appearance_skins.Length == 0
                        || group.appearance_ids == null || group.appearance_ids.Length != group.appearance_skins.Length)
                        throw new InvalidOperationException("An official appearance group is missing its icon or skin data: "
                            + group.group_id);
                    foreach (CharacterCreationAppearanceGroupSkin skin in group.appearance_skins)
                        if (skin == null || skin.appearance_id <= 0 || skin.skin_id <= 0 || skin.skin_type < 0
                            || string.IsNullOrEmpty(skin.skin_name)
                            || !TryGetSkinInfo(skin.skin_id, out CharacterCreationSkinInfo skinInfo)
                            || skinInfo.Name != skin.skin_name)
                            throw new InvalidOperationException("Official appearance group skin data is invalid: " + group.group_id);
                }
            foreach (string bodyType in new[] { "male", "female" })
                foreach (string field in AppearanceFields)
                    foreach (CharacterCreationOptionInfo option in GetAppearanceOptions(bodyType, field))
                        if (!icons.ContainsKey(option.SkinName))
                            throw new InvalidOperationException("Official player skin icon is missing: " + option.SkinName);
        }

        private static bool IsExpectedAppearanceGroup(int groupId, int sex)
        {
            return groupId >= 1 && groupId <= 6 && sex == 0
                || groupId >= 11 && groupId <= 16 && sex == 1;
        }

        private static Dictionary<int, List<CharacterCreationAppearanceGroupInfo>> ParseAppearanceGroups(TextAsset source)
        {
            if (source == null) throw new InvalidOperationException("Official appearance group data is not bound.");
            CharacterCreationAppearanceGroupManifest data = JsonUtility.FromJson<CharacterCreationAppearanceGroupManifest>(source.text);
            if (data == null || data.manifest != "854_156383" || data.groups == null || data.groups.Length != 12)
                throw new FormatException("Official appearance group source data is invalid.");
            var groupsBySex = new Dictionary<int, List<CharacterCreationAppearanceGroupInfo>>
            {
                { 0, new List<CharacterCreationAppearanceGroupInfo>(6) },
                { 1, new List<CharacterCreationAppearanceGroupInfo>(6) }
            };
            var ids = new HashSet<int>();
            foreach (CharacterCreationAppearanceGroupInfo group in data.groups)
            {
                int sex = group != null && group.sex == "male" ? 0
                    : group != null && group.sex == "female" ? 1 : -1;
                if (group == null || sex < 0 || !IsExpectedAppearanceGroup(group.group_id, sex)
                    || group.item_id <= 0 || string.IsNullOrEmpty(group.localized_label)
                    || group.condition_id < 0 || !ids.Add(group.group_id))
                    throw new FormatException("Official appearance group source record is invalid or duplicated.");
                groupsBySex[sex].Add(group);
            }
            foreach (List<CharacterCreationAppearanceGroupInfo> groups in groupsBySex.Values)
                groups.Sort((left, right) => left.group_id.CompareTo(right.group_id));
            return groupsBySex;
        }

        private Dictionary<string, Sprite> GetSkinIconIndex()
        {
            if (skinIconsByName != null) return skinIconsByName;
            skinIconsByName = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            foreach (CharacterCreationSkinIconBinding binding in skinIcons ?? Array.Empty<CharacterCreationSkinIconBinding>())
            {
                if (binding == null || string.IsNullOrEmpty(binding.skinName) || binding.sprite == null
                    || !skinIconsByName.TryAdd(binding.skinName, binding.sprite))
                    throw new InvalidOperationException("An official player skin icon binding is invalid or duplicated.");
            }
            return skinIconsByName;
        }

        private static Dictionary<string, object> Object(object value, string context)
        {
            if (value is Dictionary<string, object> result) return result;
            throw new FormatException(context + " must be an object.");
        }

        private static Dictionary<string, Dictionary<int, int>> ReadDefaultSkins(string json)
        {
            Dictionary<string, object> root = Object(OfficialEquipmentJson.Parse(json), "wardrobe rules");
            Dictionary<string, object> defaults = Object(root.TryGetValue("defaults", out object value)
                ? value : null, "wardrobe rules.defaults");
            var result = new Dictionary<string, Dictionary<int, int>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> entry in defaults)
            {
                Dictionary<string, object> skins = Object(entry.Value, "default appearance");
                var byType = new Dictionary<int, int>();
                foreach (KeyValuePair<string, object> skin in skins)
                {
                    int type = int.Parse(skin.Key, CultureInfo.InvariantCulture);
                    string idText = Convert.ToString(skin.Value, CultureInfo.InvariantCulture);
                    if (!int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                        throw new FormatException("Official default appearance skin ID is not an integer: "
                            + entry.Key + "/" + skin.Key);
                    if (type < 0 || id <= 0 || !byType.TryAdd(type, id))
                        throw new FormatException("Official default appearance contains an invalid skin.");
                }
                result.Add(entry.Key, byType);
            }
            return result;
        }
    }

    [Serializable]
    internal sealed class CharacterCreationAppearanceRulesData
    {
        public string manifest;
        public CharacterCreationSkinTypeRule[] skin_types;
    }

    [Serializable]
    public sealed class CharacterCreationSkinTypeRule
    {
        public int type;
        public bool base_skin;
        public bool can_normal_set;
    }

    internal sealed class CharacterCreationAppearanceRules
    {
        private readonly Dictionary<int, CharacterCreationSkinTypeRule> byType;

        private CharacterCreationAppearanceRules(Dictionary<int, CharacterCreationSkinTypeRule> byType)
        {
            this.byType = byType;
        }

        public bool TryGet(int skinType, out CharacterCreationSkinTypeRule rule)
        {
            return byType.TryGetValue(skinType, out rule);
        }

        public static CharacterCreationAppearanceRules Parse(TextAsset source)
        {
            if (source == null) throw new InvalidOperationException("Official AppearanceConfig rules are not bound.");
            CharacterCreationAppearanceRulesData data = JsonUtility.FromJson<CharacterCreationAppearanceRulesData>(source.text);
            if (data == null || data.manifest != "854_156383" || data.skin_types == null || data.skin_types.Length == 0)
                throw new FormatException("Official AppearanceConfig rules are invalid.");
            var byType = new Dictionary<int, CharacterCreationSkinTypeRule>();
            foreach (CharacterCreationSkinTypeRule rule in data.skin_types)
            {
                if (rule == null || rule.type < 0 || !byType.TryAdd(rule.type, rule))
                    throw new FormatException("Official AppearanceConfig skin type is duplicated or invalid.");
            }
            return new CharacterCreationAppearanceRules(byType);
        }
    }

    [Serializable]
    internal sealed class CharacterAppearanceOptions
    {
        public CharacterAppearanceBodies bodies;
        [NonSerialized] private Dictionary<int, int> skinTypes;
        [NonSerialized] private Dictionary<int, string> skinNames;
        [NonSerialized] private Dictionary<int, CharacterCreationSkinInfo> skinInfos;

        public static CharacterAppearanceOptions Parse(TextAsset source)
        {
            if (source == null) throw new InvalidOperationException("Official appearance options are not bound.");
            CharacterAppearanceOptions options = JsonUtility.FromJson<CharacterAppearanceOptions>(source.text);
            if (options == null || options.bodies == null
                || options.bodies.male == null || options.bodies.female == null)
                throw new InvalidOperationException("Official appearance options are invalid.");
            options.skinInfos = ReadSkinInfos(source.text);
            options.skinTypes = options.skinInfos.ToDictionary(pair => pair.Key, pair => pair.Value.Type);
            options.skinNames = options.skinInfos.ToDictionary(pair => pair.Key, pair => pair.Value.Name);
            options.bodies.male.Validate("male", options.skinTypes, options.skinNames);
            options.bodies.female.Validate("female", options.skinTypes, options.skinNames);
            return options;
        }

        public bool TryGetSkinInfo(int skinId, out CharacterCreationSkinInfo info)
        {
            return skinInfos.TryGetValue(skinId, out info);
        }

        public bool Contains(string bodyType, string field, string optionId)
        {
            if (string.IsNullOrEmpty(optionId)) return false;
            CharacterAppearanceBodyOptions body = bodyType == "male" ? bodies.male
                : bodyType == "female" ? bodies.female : null;
            if (body == null) return false;
            return body.Contains(field, optionId);
        }

        public IReadOnlyList<CharacterCreationOptionInfo> GetOptions(string bodyType, string field)
        {
            CharacterAppearanceBodyOptions body = Body(bodyType);
            return body == null ? Array.Empty<CharacterCreationOptionInfo>() : body.GetOptions(field);
        }

        public bool TryGetOption(string bodyType, string field, string optionId,
            out CharacterCreationOptionInfo option)
        {
            CharacterAppearanceBodyOptions body = Body(bodyType);
            if (body != null) return body.TryGetOption(field, optionId, out option);
            option = null;
            return false;
        }

        private CharacterAppearanceBodyOptions Body(string bodyType)
        {
            return bodyType == "male" ? bodies.male : bodyType == "female" ? bodies.female : null;
        }

        private static Dictionary<int, CharacterCreationSkinInfo> ReadSkinInfos(string json)
        {
            Dictionary<string, object> root = Object(OfficialEquipmentJson.Parse(json), "appearance options");
            Dictionary<string, object> skins = Object(root["skins"], "skins");
            var result = new Dictionary<int, CharacterCreationSkinInfo>();
            foreach (KeyValuePair<string, object> entry in skins)
            {
                if (!int.TryParse(entry.Key, NumberStyles.None, CultureInfo.InvariantCulture, out int id))
                    throw new FormatException("Official appearance skin id is not an invariant integer: '" + entry.Key + "'.");
                Dictionary<string, object> skin = Object(entry.Value, "appearance skin");
                int type = Convert.ToInt32(skin["SkinType"], CultureInfo.InvariantCulture);
                string name = Convert.ToString(skin["SkinName"], CultureInfo.InvariantCulture);
                bool hasDye = Convert.ToBoolean(skin["HasDye"], CultureInfo.InvariantCulture);
                if (id <= 0 || type < 0 || string.IsNullOrEmpty(name)
                    || !result.TryAdd(id, new CharacterCreationSkinInfo
                    { Id = id, Type = type, Name = name, HasDye = hasDye }))
                    throw new FormatException("Official appearance skin metadata is invalid or duplicated.");
            }
            return result;
        }

        private static Dictionary<string, object> Object(object value, string context)
        {
            if (value is Dictionary<string, object> result) return result;
            throw new FormatException(context + " must be an object.");
        }
    }

    [Serializable]
    internal sealed class CharacterAppearanceBodies
    {
        public CharacterAppearanceBodyOptions male;
        public CharacterAppearanceBodyOptions female;
    }

    [Serializable]
    internal sealed class CharacterAppearanceOption
    {
        public string id;
        public int skin_id;
        public string part;
        public string name;
        public int weight;
        public string[] bodies;
    }

    [Serializable]
    internal sealed class CharacterAppearanceBodyOptions
    {
        public CharacterAppearanceOption[] hairstyle_id;
        public CharacterAppearanceOption[] eyebrow_id;
        public CharacterAppearanceOption[] eye_id;
        public CharacterAppearanceOption[] mouth_id;
        public CharacterAppearanceOption[] makeup_id;
        public CharacterAppearanceOption[] feature_id;

        [NonSerialized] private Dictionary<string, Dictionary<string, CharacterCreationOptionInfo>> optionsByField;
        [NonSerialized] private Dictionary<string, List<CharacterCreationOptionInfo>> orderedOptionsByField;

        public void Validate(string bodyType, IReadOnlyDictionary<int, int> skinTypes,
            IReadOnlyDictionary<int, string> skinNames)
        {
            optionsByField = new Dictionary<string, Dictionary<string, CharacterCreationOptionInfo>>(StringComparer.Ordinal);
            orderedOptionsByField = new Dictionary<string, List<CharacterCreationOptionInfo>>(StringComparer.Ordinal);
            Add(bodyType, "hairstyle_id", hairstyle_id, skinTypes, skinNames);
            Add(bodyType, "eyebrow_id", eyebrow_id, skinTypes, skinNames);
            Add(bodyType, "eye_id", eye_id, skinTypes, skinNames);
            Add(bodyType, "mouth_id", mouth_id, skinTypes, skinNames);
            Add(bodyType, "makeup_id", makeup_id, skinTypes, skinNames);
            Add(bodyType, "feature_id", feature_id, skinTypes, skinNames);
        }

        public bool Contains(string field, string optionId)
        {
            return TryGetOption(field, optionId, out _);
        }

        public IReadOnlyList<CharacterCreationOptionInfo> GetOptions(string field)
        {
            return orderedOptionsByField != null && orderedOptionsByField.TryGetValue(field, out List<CharacterCreationOptionInfo> options)
                ? options : Array.Empty<CharacterCreationOptionInfo>();
        }

        public bool TryGetOption(string field, string optionId, out CharacterCreationOptionInfo option)
        {
            if (optionsByField != null && optionsByField.TryGetValue(field, out Dictionary<string, CharacterCreationOptionInfo> options)
                && options.TryGetValue(optionId ?? string.Empty, out option)) return true;
            option = null;
            return false;
        }

        private void Add(string bodyType, string field, CharacterAppearanceOption[] options,
            IReadOnlyDictionary<int, int> skinTypes, IReadOnlyDictionary<int, string> skinNames)
        {
            if (options == null || options.Length == 0)
                throw new InvalidOperationException("Official appearance options are missing: " + bodyType + "/" + field);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var byId = new Dictionary<string, CharacterCreationOptionInfo>(StringComparer.Ordinal);
            var ordered = new List<CharacterCreationOptionInfo>(options.Length);
            foreach (CharacterAppearanceOption option in options)
            {
                if (option == null || string.IsNullOrEmpty(option.id) || option.bodies == null
                    || option.skin_id <= 0 || string.IsNullOrEmpty(option.part)
                    || option.weight < 0 || Array.IndexOf(option.bodies, bodyType) < 0
                    || !ids.Add(option.id) || !skinTypes.TryGetValue(option.skin_id, out int skinType)
                    || !skinNames.TryGetValue(option.skin_id, out string skinName))
                    throw new InvalidOperationException("Official appearance option is invalid: " + bodyType + "/" + field);
                var info = new CharacterCreationOptionInfo
                {
                    Id = option.id,
                    SkinId = option.skin_id,
                    SkinType = skinType,
                    Part = option.part,
                    SkinName = skinName,
                    Name = option.name ?? string.Empty,
                    Weight = option.weight
                };
                byId.Add(option.id, info);
                ordered.Add(info);
            }
            optionsByField.Add(field, byId);
            orderedOptionsByField.Add(field, ordered);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;

namespace Zjcs.Offline.CharacterCreation
{
    public sealed class CharacterCreationDyeChoice
    {
        public int ColorId { get; internal set; }
        public int Hue { get; internal set; }
        public int Saturation { get; internal set; }
        public int Value { get; internal set; }
        public int RandomWeight { get; internal set; }
        public string Hex { get; internal set; }
    }

    public sealed class CharacterCreationOfflineService
    {
        public const int SwordInitialWeaponType = 200;
        public const int WandInitialWeaponType = 201;
        public const int DefaultProfession = 1;
        private const int ChineseRegion = 0;
        private static readonly string[] AppearanceFields =
        {
            "hairstyle_id", "eyebrow_id", "eye_id", "mouth_id", "makeup_id", "feature_id"
        };

        private readonly ProfileSlotCatalog slots;
        private readonly CharacterCreationDraftStore drafts;
        private readonly CharacterCreationAssetCatalog creationAssets;
        private readonly OfficialCharacterCatalog characterCatalog;

        public CharacterCreationOfflineService(ProfileSlotCatalog slots,
            CharacterCreationAssetCatalog creationAssets, OfficialCharacterAssetCatalog characterAssets)
        {
            this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
            this.creationAssets = creationAssets ?? throw new ArgumentNullException(nameof(creationAssets));
            this.creationAssets.ValidateBindings();
            drafts = new CharacterCreationDraftStore(slots);
            characterCatalog = (characterAssets ?? throw new ArgumentNullException(nameof(characterAssets))).Load();
        }

        public static CharacterCreationOfflineService OpenDefault(string persistentDataPath)
        {
            return new CharacterCreationOfflineService(new ProfileSlotCatalog(persistentDataPath),
                CharacterCreationAssetCatalog.LoadDefault(), OfficialCharacterAssetCatalog.LoadDefault());
        }

        public IReadOnlyList<string> GetSlotIds() => slots.GetSlotIds();

        public string CreateSlot()
        {
            string slotId = slots.CreateSlot();
            drafts.Save(slotId, CreateDefaultDraft());
            return slotId;
        }

        public CharacterCreationDraft OpenDraft(string slotId)
        {
            CharacterCreationDraftReadResult result = drafts.Open(slotId);
            if (result.Status == SaveReadStatus.Corrupt)
                throw new InvalidOperationException("创角草稿损坏。正式存档未修改，请选择其他存档或重新创建草稿。");
            if (result.Draft != null) return result.Draft;

            CharacterCreationDraft draft = CreateDefaultDraft();
            drafts.Save(slotId, draft);
            return draft;
        }

        public void SaveDraft(string slotId, CharacterCreationDraft draft)
        {
            drafts.Save(slotId, draft);
        }

        public SaveReadStatus GetDraftStatus(string slotId)
        {
            return drafts.Open(slotId).Status;
        }

        public CharacterCreationDraft PreserveCorruptDraftAndRestart(string slotId)
        {
            if (HasCharacter(slotId))
                throw new InvalidOperationException("已有角色的存档不能重置创角草稿。");

            drafts.PreserveCorruptAndReset(slotId);
            CharacterCreationDraft draft = CreateDefaultDraft();
            drafts.Save(slotId, draft);
            return draft;
        }

        public bool HasCharacter(string slotId)
        {
            if (!slots.Contains(slotId))
                throw new DirectoryNotFoundException("The selected profile slot does not exist.");
            OfflineMainlineProgressSession session = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            return session.Save.Player.CharacterCreated;
        }

        public int GetInitialWeaponType(string slotId)
        {
            if (!slots.Contains(slotId))
                throw new DirectoryNotFoundException("The selected profile slot does not exist.");
            return OfflineMainlineProgressSession.OpenCharacter(slots, slotId).Save.Player.InitialWeaponType;
        }

        public string GetCharacterName(string slotId)
        {
            if (!slots.Contains(slotId))
                throw new DirectoryNotFoundException("The selected profile slot does not exist.");
            return OfflineMainlineProgressSession.OpenCharacter(slots, slotId).Save.Player.CharacterName;
        }

        public bool TrySetAppearanceOption(CharacterAppearanceDraft appearance, string field, string optionId,
            out string error)
        {
            error = string.Empty;
            if (appearance == null || !creationAssets.TryGetAppearanceOption(appearance.body_type, field, optionId,
                out CharacterCreationOptionInfo option))
                return Fail("所选外观不在官方选项中。", out error);
            SetOption(appearance, field, option.Id);
            return true;
        }

        public int[] GetDyeHsvRange(int type)
        {
            Dictionary<string, object> dye = characterCatalog.Rows("character_skin_dye")
                .FirstOrDefault(row => ReadInt(row, "DyeType") == type);
            if (dye == null || !(dye.TryGetValue("HSVRangeList", out object raw) && raw is List<object> values)
                || values.Count != 6)
                throw new InvalidOperationException("Official dye HSV range is missing: " + type);
            return values.Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)).ToArray();
        }

        public IReadOnlyList<CharacterCreationDyeChoice> GetFixedDyeChoices(int type)
        {
            Dictionary<string, object> dye = characterCatalog.Rows("character_skin_dye")
                .FirstOrDefault(row => ReadInt(row, "DyeType") == type);
            if (dye == null || !(dye.TryGetValue("FixedColorIdList", out object raw) && raw is List<object> ids))
                throw new InvalidOperationException("Official fixed dye colors are missing: " + type);
            if (!(dye.TryGetValue("RandomWeightList", out object rawWeights) && rawWeights is List<object> weights)
                || weights.Count != ids.Count)
                throw new InvalidOperationException("Official fixed dye weights are missing or mismatched: " + type);
            var choices = new List<CharacterCreationDyeChoice>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                int id = Convert.ToInt32(ids[i], CultureInfo.InvariantCulture);
                Dictionary<string, object> color = characterCatalog.Rows("character_skin_color")
                    .FirstOrDefault(row => ReadInt(row, "Id") == id);
                if (color == null) continue;
                int hue = ReadInt(color, "H");
                int saturation = ReadInt(color, "S");
                int value = ReadInt(color, "V");
                choices.Add(new CharacterCreationDyeChoice
                {
                    ColorId = id, Hue = hue, Saturation = saturation, Value = value,
                    RandomWeight = Convert.ToInt32(weights[i], CultureInfo.InvariantCulture),
                    Hex = HsvHex(hue, saturation, value)
                });
            }
            return choices;
        }

        public Vector4[] GetDyeHsvChanges(CharacterAppearanceDraft appearance, int skinType)
        {
            if (appearance == null) throw new ArgumentNullException(nameof(appearance));
            var channels = new Vector4[3];
            foreach (Dictionary<string, object> dye in characterCatalog.Rows("character_skin_dye"))
            {
                int dyeType = ReadInt(dye, "DyeType");
                if (!dye.TryGetValue("DyeSkinTypeInfoList", out object rawTargets)
                    || !(rawTargets is List<object> targets))
                    throw new InvalidOperationException("Official dye skin targets are missing: " + dyeType);

                string hex = dyeType == 1 ? appearance.skin_color
                    : dyeType == 2 ? appearance.hair_color
                    : dyeType == 3 ? appearance.eye_color : null;
                if (hex == null) continue;

                foreach (object rawTarget in targets)
                {
                    if (!(rawTarget is Dictionary<string, object> target))
                        throw new InvalidOperationException("Official dye skin target is invalid: " + dyeType);
                    if (ReadInt(target, "SkinType") != skinType) continue;

                    int channel = ReadInt(target, "Channel");
                    if (channel < 1 || channel > channels.Length)
                        throw new InvalidOperationException("Official dye channel is outside the native shader range: " + channel);
                    int valueOffset = ReadInt(target, "VOffset");
                    channels[channel - 1] = GetDyeHsvChange(dyeType, hex, valueOffset);
                }
            }
            return channels;
        }

        private Vector4 GetDyeHsvChange(int dyeType, string hex, int valueOffset)
        {
            if (!ColorUtility.TryParseHtmlString("#" + hex, out Color selected))
                throw new InvalidOperationException("The saved official dye color is invalid: " + dyeType);
            Color.RGBToHSV(selected, out float hue, out float saturation, out float value);
            int h = Mathf.RoundToInt(hue * 360f) % 360;
            int s = Mathf.RoundToInt(saturation * 100f);
            int v = Mathf.RoundToInt(value * 100f) + valueOffset;

            Dictionary<string, object> dye = characterCatalog.Rows("character_skin_dye")
                .FirstOrDefault(row => ReadInt(row, "DyeType") == dyeType);
            if (dye == null) throw new InvalidOperationException("Official dye base color is missing: " + dyeType);
            Color baseColor = HsvToLinearColor(ReadInt(dye, "BaseH"), ReadInt(dye, "BaseS"), ReadInt(dye, "BaseV"));
            Color targetColor = HsvToLinearColor(h, s, v);
            Color.RGBToHSV(baseColor, out float baseHue, out float baseSaturation, out float baseValue);
            Color.RGBToHSV(targetColor, out float targetHue, out float targetSaturation, out float targetValue);
            if (baseSaturation == 0f || baseValue == 0f)
                throw new InvalidOperationException("Official dye base HSV cannot be zero: " + dyeType);
            return new Vector4(targetHue - baseHue,
                (targetSaturation - baseSaturation) / baseSaturation,
                (targetValue - baseValue) / baseValue, 0f);
        }

        private static Color HsvToLinearColor(int hue, int saturation, int value)
        {
            return Color.HSVToRGB(Mathf.Clamp(hue / 360f, 0f, 1f),
                Mathf.Clamp01(saturation / 100f), Mathf.Clamp01(value / 100f)).linear;
        }

        public bool TryResetDraftAppearance(CharacterAppearanceDraft appearance, bool random, out string error)
        {
            error = string.Empty;
            if (appearance == null || (appearance.body_type != "male" && appearance.body_type != "female"))
                return Fail("角色体型无效，无法恢复官方外观。", out error);

            int sex = appearance.body_type == "female" ? 1 : 0;
            var selectedOptions = new Dictionary<string, CharacterCreationOptionInfo>(StringComparer.Ordinal);
            foreach (string field in AppearanceFields)
            {
                IReadOnlyList<CharacterCreationOptionInfo> options = creationAssets.GetAppearanceOptions(appearance.body_type, field);
                if (options.Count == 0 || !creationAssets.TryGetSkinRule(options[0].SkinType,
                    out CharacterCreationSkinTypeRule rule) || !rule.base_skin)
                    return Fail("官方创角部件配置不完整：" + field, out error);

                CharacterCreationOptionInfo selected;
                if (random && rule.can_normal_set)
                    selected = WeightedOption(options);
                else if (!creationAssets.TryGetDefaultAppearanceOption(appearance.body_type, sex,
                    DefaultProfession, field, out selected))
                    return Fail("官方职业默认外观缺失：" + field, out error);

                selectedOptions.Add(field, selected);
            }

            var selectedDyes = new Dictionary<int, CharacterCreationDyeChoice>();
            for (int type = 1; type <= 3; type++)
            {
                IReadOnlyList<CharacterCreationDyeChoice> choices = GetFixedDyeChoices(type);
                if (choices.Count == 0) return Fail("官方染色选项缺失：" + type, out error);
                selectedDyes.Add(type, random ? WeightedDye(choices) : choices[0]);
            }

            foreach (KeyValuePair<string, CharacterCreationOptionInfo> pair in selectedOptions)
                SetOption(appearance, pair.Key, pair.Value.Id);
            foreach (KeyValuePair<int, CharacterCreationDyeChoice> pair in selectedDyes)
            {
                CharacterCreationDyeChoice dye = pair.Value;
                if (!TrySetDraftDye(appearance, pair.Key, dye.ColorId, dye.Hue,
                    dye.Saturation, dye.Value, out error)) return false;
            }
            return true;
        }

        public bool TrySetDraftDye(CharacterAppearanceDraft appearance, int type,
            int colorId, int hue, int saturation, int value, out string error)
        {
            error = string.Empty;
            if (appearance == null || !characterCatalog.IsValidDye(type, colorId, hue, saturation, value))
                return Fail("所选颜色超出官方染色范围。", out error);
            string hex = HsvHex(hue, saturation, value);
            switch (type)
            {
                case 1: appearance.skin_color = hex; break;
                case 2: appearance.hair_color = hex; break;
                case 3: appearance.eye_color = hex; break;
                default: return Fail("染色类型无效。", out error);
            }
            return true;
        }

        public int GetOfficialNameMaximum()
        {
            Dictionary<string, object> settings = characterCatalog.Rows("game_settings").FirstOrDefault();
            if (settings == null) throw new InvalidOperationException("Official name rules are missing.");
            return ReadNameMaximum(settings);
        }

        public int CountOfficialNameCharacters(string name) => OfficialCharacterNameRules.Count(name);

        public bool TryCreateCharacter(string slotId, CharacterCreationDraft draft, out string error)
        {
            error = string.Empty;
            if (!slots.Contains(slotId) || draft == null || !CharacterCreationDraftStore.IsValidJson(JsonUtility.ToJson(draft)))
                return Fail("角色草稿无效，请重新选择外观。", out error);
            if (!IsLegalNameForProfile(draft.name))
                return Fail("名字为空、超出官方字数上限，或包含不允许的字符。", out error);

            int sex = draft.body_type == "female" ? 1 : 0;
            CharacterAppearanceDraft appearance = sex == 0 ? draft.bodies.male : draft.bodies.female;
            if (!TryResolveAppearance(appearance, out List<OfflineAppearanceSkin> wornSkins, out error))
                return false;

            IReadOnlyDictionary<int, int> defaults = characterCatalog.DefaultSkins(sex, DefaultProfession);
            if (defaults.Count == 0)
                return Fail("官方角色初始外观缺失，无法创建角色。", out error);

            if (!TryResolveDyes(appearance, out List<OfflineAppearanceDye> dyes, out error))
                return false;

            OfflineMainlineProgressSession session = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            if (session.Save.Player.CharacterCreated)
                return Fail("该存档已经创建角色。", out error);
            drafts.Save(slotId, draft);
            if (session.Save.CreatedAtUnixSeconds == 0)
                session.Save.CreatedAtUnixSeconds = slots.GetCreatedAtUnixSeconds(slotId);

            ProgressionResult result = session.ApplyCharacterMutation(player =>
            {
                player.CharacterName = draft.name;
                player.CharacterCreated = true;
                player.InitialWeaponType = 0;
                player.Wardrobe.Sex = sex;
                player.Wardrobe.Profession = DefaultProfession;
                player.Wardrobe.BaseSkins = defaults.OrderBy(pair => pair.Key)
                    .Select(pair => new OfflineAppearanceSkin { Type = pair.Key, Id = pair.Value }).ToList();
                player.Wardrobe.WornSkins = wornSkins;
                player.Wardrobe.Dyes = dyes;
            }, "character-create:" + slotId);

            if (result.Status != ProgressionResultStatus.Applied)
                return Fail(string.IsNullOrEmpty(result.Message) ? "角色存档未能保存。" : result.Message, out error);

            return true;
        }

        public bool TrySetInitialWeapon(string slotId, int weaponType, out string error)
        {
            error = string.Empty;
            if (!slots.Contains(slotId))
                return Fail("所选存档不存在。", out error);
            if (weaponType != SwordInitialWeaponType && weaponType != WandInitialWeaponType)
                return Fail("初始武器类型无效。", out error);

            OfflineMainlineProgressSession session = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            if (!session.Save.Player.CharacterCreated)
                return Fail("请先完成角色创建。", out error);
            if (session.Save.Player.InitialWeaponType != 0)
            {
                if (session.Save.Player.InitialWeaponType == weaponType) return true;
                return Fail("初始武器已经选择，不能重复更改。", out error);
            }

            ProgressionResult result = session.ApplyCharacterMutation(
                player =>
                {
                    player.InitialWeaponType = weaponType;
                    OfflineSkillProgression.EnsureInitialSkill(player.Skills,
                        characterCatalog.InitialActiveSkill(weaponType == WandInitialWeaponType));
                }, "initial-weapon:" + slotId);
            if (result.Status != ProgressionResultStatus.Applied)
                return Fail(string.IsNullOrEmpty(result.Message) ? "初始武器没有保存。" : result.Message, out error);

            drafts.Clear(slotId);
            return true;
        }

        public static bool IsLegalName(string name)
        {
            return IsLegalName(name, 12);
        }

        public bool IsLegalNameForProfile(string name)
        {
            Dictionary<string, object> settings = characterCatalog.Rows("game_settings").FirstOrDefault();
            if (settings == null) throw new InvalidOperationException("Official name rules are missing.");
            int maximum = ReadNameMaximum(settings);
            return IsLegalName(name, maximum);
        }

        private CharacterCreationDraft CreateDefaultDraft()
        {
            IReadOnlyDictionary<int, OfflineAppearanceDye> defaultDyes = characterCatalog.DefaultDyes();
            return new CharacterCreationDraft
            {
                version = 4,
                page = "body",
                body_type = "male",
                tab = "整体",
                field = "skin_color",
                name = string.Empty,
                bodies = new CharacterCreationDraftBodies
                {
                    male = CreateDefaultAppearance("male", 0, defaultDyes),
                    female = CreateDefaultAppearance("female", 1, defaultDyes)
                }
            };
        }

        private static CharacterCreationOptionInfo WeightedOption(IReadOnlyList<CharacterCreationOptionInfo> options)
        {
            long total = options.Sum(option => (long)Math.Max(0, option.Weight));
            if (total <= 0) return options[UnityEngine.Random.Range(0, options.Count)];
            int roll = UnityEngine.Random.Range(0, checked((int)total));
            foreach (CharacterCreationOptionInfo option in options)
            {
                roll -= Math.Max(0, option.Weight);
                if (roll < 0) return option;
            }
            throw new InvalidOperationException("Official random appearance weights are invalid.");
        }

        private static CharacterCreationDyeChoice WeightedDye(IReadOnlyList<CharacterCreationDyeChoice> choices)
        {
            long total = choices.Sum(choice => (long)Math.Max(0, choice.RandomWeight));
            if (total <= 0) return choices[UnityEngine.Random.Range(0, choices.Count)];
            int roll = UnityEngine.Random.Range(0, checked((int)total));
            foreach (CharacterCreationDyeChoice choice in choices)
            {
                roll -= Math.Max(0, choice.RandomWeight);
                if (roll < 0) return choice;
            }
            throw new InvalidOperationException("Official random dye weights are invalid.");
        }

        private CharacterAppearanceDraft CreateDefaultAppearance(string body, int sex,
            IReadOnlyDictionary<int, OfflineAppearanceDye> defaultDyes)
        {
            var appearance = new CharacterAppearanceDraft { body_type = body };
            foreach (string field in AppearanceFields)
            {
                if (!creationAssets.TryGetDefaultAppearanceOption(body, sex, DefaultProfession, field,
                    out CharacterCreationOptionInfo option))
                    throw new InvalidOperationException("Official default appearance is missing: " + body + "/" + field);
                SetOption(appearance, field, option.Id);
            }
            appearance.skin_color = DyeHex(defaultDyes, 1);
            appearance.hair_color = DyeHex(defaultDyes, 2);
            appearance.eye_color = DyeHex(defaultDyes, 3);
            return appearance;
        }

        private bool TryResolveAppearance(CharacterAppearanceDraft appearance,
            out List<OfflineAppearanceSkin> skins, out string error)
        {
            error = string.Empty;
            skins = new List<OfflineAppearanceSkin>(AppearanceFields.Length);
            var types = new HashSet<int>();
            foreach (string field in AppearanceFields)
            {
                if (!creationAssets.TryGetAppearanceOption(appearance.body_type, field,
                    GetOption(appearance, field), out CharacterCreationOptionInfo option)
                    || !types.Add(option.SkinType))
                    return Fail("所选官方外观部件无效。", out error);
                skins.Add(new OfflineAppearanceSkin { Type = option.SkinType, Id = option.SkinId });
            }
            return true;
        }

        private bool TryResolveDyes(CharacterAppearanceDraft appearance,
            out List<OfflineAppearanceDye> dyes, out string error)
        {
            error = string.Empty;
            dyes = new List<OfflineAppearanceDye>(3);
            if (!TryResolveDye(1, appearance.skin_color, out OfflineAppearanceDye skin)
                || !TryResolveDye(2, appearance.hair_color, out OfflineAppearanceDye hair)
                || !TryResolveDye(3, appearance.eye_color, out OfflineAppearanceDye eyes))
                return Fail("所选染色超出官方 HSV 配置范围。", out error);
            dyes.Add(skin);
            dyes.Add(hair);
            dyes.Add(eyes);
            return true;
        }

        private bool TryResolveDye(int type, string hex, out OfflineAppearanceDye dye)
        {
            dye = null;
            if (!ColorUtility.TryParseHtmlString("#" + hex, out Color selected)) return false;

            foreach (Dictionary<string, object> color in characterCatalog.Rows("character_skin_color"))
            {
                int id = Integer(color, "Id");
                int hue = Integer(color, "H");
                int saturation = Integer(color, "S");
                int value = Integer(color, "V");
                if (!string.Equals(HsvHex(hue, saturation, value), hex, StringComparison.OrdinalIgnoreCase)
                    || !IsFixedColor(type, id)) continue;
                if (!characterCatalog.IsValidDye(type, id, hue, saturation, value)) return false;
                dye = new OfflineAppearanceDye
                {
                    Type = type, ColorId = id, Hue = hue, Saturation = saturation, Value = value
                };
                return true;
            }

            Color.RGBToHSV(selected, out float h, out float s, out float v);
            int customHue = Mathf.RoundToInt(h * 360f) % 360;
            int customSaturation = Mathf.RoundToInt(s * 100f);
            int customValue = Mathf.RoundToInt(v * 100f);
            if (!characterCatalog.IsValidDye(type, 0, customHue, customSaturation, customValue)) return false;
            dye = new OfflineAppearanceDye
            {
                Type = type, ColorId = 0, Hue = customHue, Saturation = customSaturation, Value = customValue
            };
            return true;
        }

        private bool IsFixedColor(int dyeType, int colorId)
        {
            Dictionary<string, object> row = characterCatalog.Rows("character_skin_dye")
                .FirstOrDefault(value => Integer(value, "DyeType") == dyeType);
            if (row == null || !(row.TryGetValue("FixedColorIdList", out object raw) && raw is List<object> ids))
                return false;
            return ids.Any(value => Convert.ToInt32(value, CultureInfo.InvariantCulture) == colorId);
        }

        private static string DyeHex(IReadOnlyDictionary<int, OfflineAppearanceDye> dyes, int type)
        {
            if (!dyes.TryGetValue(type, out OfflineAppearanceDye dye))
                throw new InvalidOperationException("Official default dye is missing: " + type);
            return HsvHex(dye.Hue, dye.Saturation, dye.Value);
        }

        private static string HsvHex(int hue, int saturation, int value)
        {
            Color color = Color.HSVToRGB((hue % 360) / 360f, saturation / 100f, value / 100f);
            return ColorUtility.ToHtmlStringRGB(color);
        }

        private static int ReadInt(Dictionary<string, object> row, string key)
        {
            if (!row.TryGetValue(key, out object value))
                throw new FormatException("Official character rule field is missing: " + key);
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static bool IsLegalName(string name, int maximum)
        {
            return !string.IsNullOrEmpty(name) && OfficialCharacterNameRules.Count(name) <= maximum
                && !Regex.IsMatch(name, "[#<> \\u3000]");
        }

        private static int ReadNameMaximum(Dictionary<string, object> settings)
        {
            if (settings.TryGetValue("NameCharacterMaxCountDict", out object raw)
                && raw is Dictionary<string, object> byRegion
                && byRegion.TryGetValue(ChineseRegion.ToString(CultureInfo.InvariantCulture), out object localized))
                return Convert.ToInt32(localized, CultureInfo.InvariantCulture);
            if (settings.TryGetValue("NameCharacterDefaultMaxCount", out object fallback))
                return Convert.ToInt32(fallback, CultureInfo.InvariantCulture);
            throw new FormatException("Official name maximum length is missing.");
        }

        private static int Integer(IReadOnlyDictionary<string, object> row, string key)
        {
            return Convert.ToInt32(row[key], CultureInfo.InvariantCulture);
        }

        private static string GetOption(CharacterAppearanceDraft appearance, string field)
        {
            switch (field)
            {
                case "hairstyle_id": return appearance.hairstyle_id;
                case "eyebrow_id": return appearance.eyebrow_id;
                case "eye_id": return appearance.eye_id;
                case "mouth_id": return appearance.mouth_id;
                case "makeup_id": return appearance.makeup_id;
                case "feature_id": return appearance.feature_id;
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        }

        private static void SetOption(CharacterAppearanceDraft appearance, string field, string value)
        {
            switch (field)
            {
                case "hairstyle_id": appearance.hairstyle_id = value; break;
                case "eyebrow_id": appearance.eyebrow_id = value; break;
                case "eye_id": appearance.eye_id = value; break;
                case "mouth_id": appearance.mouth_id = value; break;
                case "makeup_id": appearance.makeup_id = value; break;
                case "feature_id": appearance.feature_id = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }

    internal static class OfficialCharacterNameRules
    {
        public static int Count(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            int count = 0;
            TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(value);
            while (elements.MoveNext())
            {
                string element = elements.GetTextElement();
                int codePoint = element.Length == 1 ? element[0] : char.ConvertToUtf32(element, 0);
                count += IsDoubleByte(codePoint) ? 2 : 1;
            }
            return count;
        }

        private static bool IsDoubleByte(int codePoint)
        {
            if (codePoint < 128) return false;
            return IsCjk(codePoint) || (codePoint >= 8192 && codePoint <= 8303)
                || (codePoint >= 9312 && codePoint <= 9471);
        }

        private static bool IsCjk(int value)
        {
            return (value >= 4352 && value <= 4607) || (value >= 11904 && value <= 12543)
                || (value >= 12592 && value <= 12687) || (value >= 12736 && value <= 12799)
                || (value >= 12800 && value <= 13311) || (value >= 13312 && value <= 19903)
                || (value >= 19968 && value <= 40959) || (value >= 43360 && value <= 43391)
                || (value >= 44032 && value <= 55295) || (value >= 63744 && value <= 64255)
                || (value >= 65040 && value <= 65055) || (value >= 65072 && value <= 65103)
                || (value >= 65280 && value <= 65519) || (value >= 131072 && value <= 192093)
                || (value >= 194560 && value <= 195103) || (value >= 196608 && value <= 205743);
        }
    }
}

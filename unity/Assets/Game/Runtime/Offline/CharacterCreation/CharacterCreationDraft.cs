using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Zjcs.Offline.State;

namespace Zjcs.Offline.CharacterCreation
{
    [Serializable]
    public sealed class CharacterAppearanceDraft
    {
        public string body_type;
        public string skin_color;
        public string hairstyle_id;
        public string hair_color;
        public string eyebrow_id;
        public string eye_id;
        public string eye_color;
        public string mouth_id;
        public string makeup_id;
        public string feature_id;
        public int appearance_group_id;
    }

    [Serializable]
    public sealed class CharacterCreationDraft
    {
        public int version;
        public string page;
        public string body_type;
        public string tab;
        public string field;
        public string name;
        public CharacterCreationDraftBodies bodies;
    }

    [Serializable]
    public sealed class CharacterCreationDraftBodies
    {
        public CharacterAppearanceDraft male;
        public CharacterAppearanceDraft female;
    }

    public sealed class CharacterCreationDraftReadResult
    {
        public SaveReadStatus Status { get; }
        public CharacterCreationDraft Draft { get; }

        internal CharacterCreationDraftReadResult(SaveReadStatus status, CharacterCreationDraft draft)
        {
            Status = status;
            Draft = draft;
        }
    }

    public sealed class CharacterCreationDraftStore
    {
        private const int CurrentVersion = 4;
        private readonly ProfileSlotCatalog slots;

        public CharacterCreationDraftStore(ProfileSlotCatalog slots)
        {
            this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
        }

        public CharacterCreationDraftReadResult Open(string slotId)
        {
            if (!slots.Contains(slotId))
                throw new DirectoryNotFoundException("The selected profile slot does not exist.");

            SaveReadResult result = LocalJsonSaveFile.Read(slots.GetDraftPath(slotId), IsValidJson);
            CharacterCreationDraft draft = result.Json == null
                ? null
                : JsonUtility.FromJson<CharacterCreationDraft>(result.Json);
            return new CharacterCreationDraftReadResult(result.Status, draft);
        }

        public void Save(string slotId, CharacterCreationDraft draft)
        {
            if (!slots.Contains(slotId))
                throw new DirectoryNotFoundException("The selected profile slot does not exist.");
            if (draft == null || !IsValid(draft))
                throw new InvalidDataException("The character creation draft is incomplete or has an unsupported version.");

            LocalJsonSaveFile.Write(slots.GetDraftPath(slotId), JsonUtility.ToJson(draft), IsValidJson);
        }

        public void Clear(string slotId)
        {
            if (!slots.Contains(slotId))
                throw new DirectoryNotFoundException("The selected profile slot does not exist.");

            string path = slots.GetDraftPath(slotId);
            foreach (string suffix in new[] { string.Empty, ".tmp", ".bak" })
            {
                string candidate = path + suffix;
                if (File.Exists(candidate)) File.Delete(candidate);
            }
        }

        public void PreserveCorruptAndReset(string slotId)
        {
            if (!slots.Contains(slotId))
                throw new DirectoryNotFoundException("The selected profile slot does not exist.");

            string path = slots.GetDraftPath(slotId);
            if (LocalJsonSaveFile.Read(path, IsValidJson).Status != SaveReadStatus.Corrupt)
                throw new InvalidOperationException("The character draft is no longer corrupt.");

            string suffix = ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture);
            bool preserved = false;
            foreach (string candidateSuffix in new[] { string.Empty, ".bak", ".unreadable", ".tmp" })
            {
                string candidate = path + candidateSuffix;
                if (!File.Exists(candidate)) continue;

                int collision = 0;
                string preservedPath;
                do
                {
                    string collisionSuffix = collision == 0 ? string.Empty
                        : "-" + collision.ToString(CultureInfo.InvariantCulture);
                    preservedPath = path + suffix + collisionSuffix + candidateSuffix;
                    collision++;
                } while (File.Exists(preservedPath));
                File.Move(candidate, preservedPath);
                preserved = true;
            }

            if (!preserved)
                throw new InvalidOperationException("The corrupt character draft could not be preserved.");
        }

        public static bool IsValidJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 65536) return false;
            try
            {
                if (!CharacterCreationJson.HasOnlyKnownDraftFields(json)) return false;
                return IsValid(JsonUtility.FromJson<CharacterCreationDraft>(json));
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValid(CharacterCreationDraft draft)
        {
            if (draft == null || draft.version != CurrentVersion
                || (draft.page != "body" && draft.page != "appearance" && draft.page != "name")
                || !IsBody(draft.body_type)
                || (draft.tab != "整体" && draft.tab != "头发" && draft.tab != "脸部" && draft.tab != "特征")
                || draft.name == null || draft.name.Length > 64
                || draft.bodies == null
                || !IsAppearance(draft.bodies.male, "male")
                || !IsAppearance(draft.bodies.female, "female"))
                return false;

            CharacterCreationAssetCatalog catalog;
            try
            {
                catalog = CharacterCreationAssetCatalog.LoadDefault();
            }
            catch
            {
                return false;
            }

            if (!HasValidOptions(catalog, draft.bodies.male) || !HasValidOptions(catalog, draft.bodies.female))
                return false;

            return IsFieldInTab(draft.tab, draft.field);
        }

        private static bool HasValidOptions(CharacterCreationAssetCatalog catalog, CharacterAppearanceDraft appearance)
        {
            int sex = appearance.body_type == "female" ? 1 : 0;
            return (appearance.appearance_group_id == 0
                    || catalog.IsSelectableAppearanceGroup(sex, appearance.appearance_group_id))
                && catalog.HasAppearanceOption(appearance.body_type, "hairstyle_id", appearance.hairstyle_id)
                && catalog.HasAppearanceOption(appearance.body_type, "eyebrow_id", appearance.eyebrow_id)
                && catalog.HasAppearanceOption(appearance.body_type, "eye_id", appearance.eye_id)
                && catalog.HasAppearanceOption(appearance.body_type, "mouth_id", appearance.mouth_id)
                && catalog.HasAppearanceOption(appearance.body_type, "makeup_id", appearance.makeup_id)
                && catalog.HasAppearanceOption(appearance.body_type, "feature_id", appearance.feature_id);
        }

        private static bool IsAppearance(CharacterAppearanceDraft appearance, string body)
        {
            return appearance != null && appearance.body_type == body
                && IsColor(appearance.skin_color) && IsColor(appearance.hair_color) && IsColor(appearance.eye_color)
                && !string.IsNullOrEmpty(appearance.hairstyle_id)
                && !string.IsNullOrEmpty(appearance.eyebrow_id)
                && !string.IsNullOrEmpty(appearance.eye_id)
                && !string.IsNullOrEmpty(appearance.mouth_id)
                && !string.IsNullOrEmpty(appearance.makeup_id)
                && !string.IsNullOrEmpty(appearance.feature_id);
        }

        private static bool IsColor(string value)
        {
            if (value == null || value.Length != 6) return false;
            for (int i = 0; i < value.Length; i++)
                if (!Uri.IsHexDigit(value[i])) return false;
            return true;
        }

        private static bool IsBody(string value)
        {
            return value == "male" || value == "female";
        }

        private static bool IsFieldInTab(string tab, string field)
        {
            switch (tab)
            {
                case "整体": return field == "skin_color";
                case "头发": return field == "hairstyle_id" || field == "hair_color";
                case "脸部": return field == "eyebrow_id" || field == "eye_id" || field == "eye_color" || field == "mouth_id" || field == "makeup_id";
                case "特征": return field == "feature_id";
                default: return false;
            }
        }
    }

    public static class CharacterCreationJson
    {
        private static readonly HashSet<string> DraftFields = Set(
            "version", "page", "body_type", "tab", "field", "name", "bodies");
        private static readonly HashSet<string> BodiesFields = Set("male", "female");
        private static readonly HashSet<string> AppearanceFields = Set(
            "body_type", "skin_color", "hairstyle_id", "hair_color", "eyebrow_id",
            "eye_id", "eye_color", "mouth_id", "makeup_id", "feature_id", "appearance_group_id");

        public static object Parse(string json)
        {
            return new Parser(json).Parse();
        }

        public static bool HasOnlyKnownDraftFields(string json)
        {
            object root = Parse(json);
            Dictionary<string, object> draft = root as Dictionary<string, object>;
            if (!HasOnly(draft, DraftFields) || !draft.TryGetValue("bodies", out object rawBodies)) return false;
            Dictionary<string, object> bodies = rawBodies as Dictionary<string, object>;
            if (!HasOnly(bodies, BodiesFields)) return false;
            return bodies.TryGetValue("male", out object male) && HasOnly(male as Dictionary<string, object>, AppearanceFields)
                && bodies.TryGetValue("female", out object female) && HasOnly(female as Dictionary<string, object>, AppearanceFields);
        }

        private static bool HasOnly(Dictionary<string, object> value, HashSet<string> allowed)
        {
            if (value == null) return false;
            foreach (string key in value.Keys)
                if (!allowed.Contains(key)) return false;
            return true;
        }

        private static HashSet<string> Set(params string[] values)
        {
            return new HashSet<string>(values, StringComparer.Ordinal);
        }

        private sealed class Parser
        {
            private readonly string text;
            private int position;

            public Parser(string text) { this.text = text; }

            public object Parse()
            {
                object value = Value(0);
                Space();
                if (position != text.Length) throw Error();
                return value;
            }

            private object Value(int depth)
            {
                Space();
                if (depth > 64 || position >= text.Length) throw Error();
                char ch = text[position];
                if (ch == '{') return ObjectValue(depth + 1);
                if (ch == '[') return ArrayValue(depth + 1);
                if (ch == '"') return StringValue();
                if (ch == 't') { Literal("true"); return true; }
                if (ch == 'f') { Literal("false"); return false; }
                if (ch == 'n') { Literal("null"); return null; }
                return NumberValue();
            }

            private Dictionary<string, object> ObjectValue(int depth)
            {
                position++;
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                Space();
                if (Take('}')) return result;
                while (true)
                {
                    Space();
                    if (position >= text.Length || text[position] != '"') throw Error();
                    string key = StringValue();
                    Space();
                    Expect(':');
                    if (result.ContainsKey(key)) throw Error();
                    result.Add(key, Value(depth));
                    Space();
                    if (Take('}')) return result;
                    Expect(',');
                }
            }

            private List<object> ArrayValue(int depth)
            {
                position++;
                var result = new List<object>();
                Space();
                if (Take(']')) return result;
                while (true)
                {
                    result.Add(Value(depth));
                    Space();
                    if (Take(']')) return result;
                    Expect(',');
                }
            }

            private string StringValue()
            {
                Expect('"');
                var result = new StringBuilder();
                while (position < text.Length)
                {
                    char ch = text[position++];
                    if (ch == '"') return result.ToString();
                    if (ch < 0x20) throw Error();
                    if (ch != '\\') { result.Append(ch); continue; }
                    if (position >= text.Length) throw Error();
                    char escaped = text[position++];
                    switch (escaped)
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u':
                            if (position + 4 > text.Length) throw Error();
                            result.Append((char)int.Parse(text.Substring(position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            position += 4;
                            break;
                        default: throw Error();
                    }
                }
                throw Error();
            }

            private double NumberValue()
            {
                int start = position;
                Take('-');
                if (Take('0'))
                {
                    if (IsDigit(position)) throw Error();
                }
                else
                {
                    Digits(required: true);
                }
                if (Take('.')) Digits(required: true);
                if (Take('e') || Take('E'))
                {
                    if (!Take('+')) Take('-');
                    Digits(required: true);
                }
                if (start == position) throw Error();
                return double.Parse(text.Substring(start, position - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            private void Digits(bool required)
            {
                int start = position;
                while (IsDigit(position)) position++;
                if (required && start == position) throw Error();
            }

            private bool IsDigit(int index)
            {
                return index < text.Length && text[index] >= '0' && text[index] <= '9';
            }

            private void Literal(string value)
            {
                if (position + value.Length > text.Length
                    || string.CompareOrdinal(text, position, value, 0, value.Length) != 0) throw Error();
                position += value.Length;
            }

            private void Space()
            {
                while (position < text.Length && (text[position] == ' ' || text[position] == '\t'
                    || text[position] == '\r' || text[position] == '\n')) position++;
            }

            private bool Take(char value)
            {
                if (position >= text.Length || text[position] != value) return false;
                position++;
                return true;
            }

            private void Expect(char value)
            {
                if (!Take(value)) throw Error();
            }

            private FormatException Error() { return new FormatException("Invalid character creation JSON at offset " + position); }
        }
    }
}

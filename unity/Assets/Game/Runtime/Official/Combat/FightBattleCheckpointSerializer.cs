using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Versioned persistence for <see cref="FightBattleCheckpoint"/>. It serializes the public
    /// serializable fields of the checkpoint graph without Unity, so the rules assembly can round
    /// trip a running battle and a later entry can restore it with
    /// <see cref="FightBattleExecutor.Restore"/>.
    /// </summary>
    public static class FightBattleCheckpointSerializer
    {
        public const int Version = 1;

        public static string Save(FightBattleCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            var builder = new StringBuilder(1024);
            builder.Append("{\"version\":").Append(Version).Append(",\"checkpoint\":");
            WriteValue(builder, checkpoint, typeof(FightBattleCheckpoint));
            builder.Append('}');
            return builder.ToString();
        }

        public static FightBattleCheckpoint Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Fight checkpoint payload is empty.", nameof(json));
            Dictionary<string, object> root = FightOfficialCombatCatalog.ParseObject(json, "checkpoint");
            object rawVersion;
            if (!root.TryGetValue("version", out rawVersion)
                || Convert.ToInt32(rawVersion, CultureInfo.InvariantCulture) != Version)
                throw new FormatException("Fight checkpoint payload version is unsupported.");
            object rawCheckpoint;
            if (!root.TryGetValue("checkpoint", out rawCheckpoint) || rawCheckpoint == null)
                throw new FormatException("Fight checkpoint payload has no checkpoint body.");
            var checkpoint = new FightBattleCheckpoint();
            Populate(checkpoint, rawCheckpoint as Dictionary<string, object>);
            FightBattleCheckpointRules.Validate(checkpoint, true);
            return checkpoint;
        }

        private static void WriteValue(StringBuilder builder, object value, Type type)
        {
            if (value == null) { builder.Append("null"); return; }
            if (type == typeof(string)) { WriteString(builder, (string)value); return; }
            if (type == typeof(bool)) { builder.Append((bool)value ? "true" : "false"); return; }
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }
            if (type == typeof(float))
            {
                builder.Append(((float)value).ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (type == typeof(double))
            {
                builder.Append(((double)value).ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (type.IsEnum)
            {
                builder.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (value is IEnumerable list && type != typeof(string))
            {
                builder.Append('[');
                bool first = true;
                foreach (object item in list)
                {
                    if (!first) builder.Append(',');
                    first = false;
                    WriteValue(builder, item, item == null ? typeof(object) : item.GetType());
                }
                builder.Append(']');
                return;
            }

            builder.Append('{');
            bool wrote = false;
            foreach (FieldInfo field in SerializableFields(type))
            {
                object fieldValue = field.GetValue(value);
                if (fieldValue == null) continue;
                if (wrote) builder.Append(',');
                wrote = true;
                WriteString(builder, field.Name);
                builder.Append(':');
                WriteValue(builder, fieldValue, field.FieldType);
            }
            builder.Append('}');
        }

        private static void Populate(object target, Dictionary<string, object> values)
        {
            if (target == null || values == null) return;
            foreach (FieldInfo field in SerializableFields(target.GetType()))
            {
                object raw;
                if (!values.TryGetValue(field.Name, out raw)) continue;
                object converted = ConvertValue(raw, field.FieldType);
                if (converted == null && field.FieldType.IsValueType) continue;
                field.SetValue(target, converted);
            }
        }

        private static object ConvertValue(object raw, Type type)
        {
            if (raw == null) return null;
            if (type == typeof(string)) return Convert.ToString(raw, CultureInfo.InvariantCulture);
            if (type == typeof(bool)) return Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
            if (type == typeof(int)) return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            if (type == typeof(long)) return Convert.ToInt64(raw, CultureInfo.InvariantCulture);
            if (type == typeof(short)) return Convert.ToInt16(raw, CultureInfo.InvariantCulture);
            if (type == typeof(byte)) return Convert.ToByte(raw, CultureInfo.InvariantCulture);
            if (type == typeof(float)) return Convert.ToSingle(raw, CultureInfo.InvariantCulture);
            if (type == typeof(double)) return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            if (type.IsEnum) return Enum.ToObject(type, Convert.ToInt64(raw, CultureInfo.InvariantCulture));

            var list = raw as List<object>;
            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                var array = Array.CreateInstance(elementType, list == null ? 0 : list.Count);
                for (int i = 0; i < array.Length; i++) array.SetValue(ConvertValue(list[i], elementType), i);
                return array;
            }
            if (typeof(IList).IsAssignableFrom(type) && type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elementType = type.GetGenericArguments()[0];
                var instance = (IList)Activator.CreateInstance(type);
                if (list != null)
                    foreach (object item in list) instance.Add(ConvertValue(item, elementType));
                return instance;
            }
            var map = raw as Dictionary<string, object>;
            if (map == null) return null;
            object nested = Activator.CreateInstance(type);
            Populate(nested, map);
            return nested;
        }

        private static IEnumerable<FieldInfo> SerializableFields(Type type)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.IsStatic || field.IsInitOnly) continue;
                if (field.IsDefined(typeof(NonSerializedAttribute), false)) continue;
                yield return field;
            }
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char ch in value)
            {
                switch (ch)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (ch < 0x20)
                            builder.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(ch);
                        break;
                }
            }
            builder.Append('"');
        }
    }
}

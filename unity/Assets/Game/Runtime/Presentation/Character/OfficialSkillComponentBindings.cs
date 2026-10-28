using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Character
{
    // Restore serialized fields of the migrated original UI components. The
    // extracted prefab provides every reference and managed-reference state.
    internal static class OfficialSkillComponentBindings
    {
        internal static void Apply(object instance, Dictionary<string, object> fields,
            Dictionary<string, RectTransform> nodes)
        {
            Dictionary<string, object> references = fields.TryGetValue("references", out object raw)
                ? Object(raw) : null;
            Fill(instance, fields, nodes, references);
        }

        private static Dictionary<string, object> Object(object raw) => CharacterModelPackage.AsObject(raw, "original skill component fields");

        private static void Fill(object target, Dictionary<string, object> fields,
            Dictionary<string, RectTransform> nodes, Dictionary<string, object> references)
        {
            foreach (var pair in fields)
            {
                FieldInfo field = Field(target.GetType(), pair.Key);
                if (field == null || field.IsInitOnly) continue;
                field.SetValue(target, ConvertField(field.FieldType, pair.Value, nodes, references));
            }
        }

        private static FieldInfo Field(Type type, string key)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }

        private static object ConvertField(Type type, object raw, Dictionary<string, RectTransform> nodes,
            Dictionary<string, object> references)
        {
            if (raw == null) return null;
            if (type.IsInstanceOfType(raw)) return raw;
            if (type == typeof(string)) return Convert.ToString(raw, CultureInfo.InvariantCulture);
            if (type.IsEnum) return Enum.ToObject(type, Convert.ToInt32(raw, CultureInfo.InvariantCulture));
            if (type.IsPrimitive) return Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
            if (type.IsArray)
            {
                var values = (List<object>)raw;
                var array = Array.CreateInstance(type.GetElementType(), values.Count);
                for (int i = 0; i < values.Count; i++) array.SetValue(ConvertField(type.GetElementType(), values[i], nodes, references), i);
                return array;
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                string path = Convert.ToString(raw, CultureInfo.InvariantCulture);
                if (!nodes.TryGetValue(path, out var node)) throw new InvalidOperationException("Original skill component reference is absent: " + path);
                if (type == typeof(GameObject)) return node.gameObject;
                if (type == typeof(Transform) || type == typeof(RectTransform)) return node;
                Component component = node.GetComponent(type);
                if (component == null) throw new InvalidOperationException("Original skill component is absent: " + path + " / " + type);
                return component;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)Activator.CreateInstance(type);
                Type itemType = type.GetGenericArguments()[0];
                foreach (object value in (List<object>)raw) list.Add(ConvertField(itemType, value, nodes, references));
                return list;
            }
            var data = Object(raw);
            // Unity serializes Rect as x/y/width/height, while its backing
            // fields are m_XMin/m_YMin/m_Width/m_Height. Field reflection alone
            // would silently collapse the original RawImage UV to a point.
            if (type == typeof(Rect)) return new Rect(Convert.ToSingle(data["x"], CultureInfo.InvariantCulture),
                Convert.ToSingle(data["y"], CultureInfo.InvariantCulture), Convert.ToSingle(data["width"], CultureInfo.InvariantCulture),
                Convert.ToSingle(data["height"], CultureInfo.InvariantCulture));
            if (type == typeof(RectOffset)) return new RectOffset(Convert.ToInt32(data["m_Left"]),
                Convert.ToInt32(data["m_Right"]), Convert.ToInt32(data["m_Top"]), Convert.ToInt32(data["m_Bottom"]));
            if (type == typeof(IWorldStateConfig))
            {
                if (references == null) throw new InvalidOperationException("Original skill managed reference registry is absent.");
                string rid = Convert.ToString(data["rid"], CultureInfo.InvariantCulture);
                var record = ((List<object>)references["RefIds"]).Select(Object)
                    .First(row => Convert.ToString(row["rid"], CultureInfo.InvariantCulture) == rid);
                string name = Convert.ToString(Object(record["type"])["class"]);
                type = name == "StateGameObjectConfig" ? typeof(WorldStateGameObjectConfig)
                    : name == "StateUIColorConfig" ? typeof(WorldStateUIColorConfig)
                    : throw new InvalidOperationException("Unmigrated original skill state configuration: " + name);
                data = Object(record["data"]);
            }
            object instance = Activator.CreateInstance(type, true);
            Fill(instance, data, nodes, references);
            return instance;
        }
    }
}

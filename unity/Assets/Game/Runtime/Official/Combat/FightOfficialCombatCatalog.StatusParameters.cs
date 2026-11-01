using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        // Resolve only properties actually read through the original component accessors.
        // Copies are per status instance; the shared official configuration stays immutable.
        private static Dictionary<string, object> ResolveStatusParameters(
            Dictionary<string, object> components, IList<FightStatusReplacementValue> values)
        {
            Dictionary<string, object> result = null;
            foreach (var entry in components)
            {
                string fields = StatusParameterFields(entry.Key);
                if (fields == null || entry.Value == null) continue;
                var component = Object(entry.Value, entry.Key);
                object raw;
                if (!component.TryGetValue("StatusReplacePropertyValues", out raw) || raw == null) continue;
                Dictionary<string, object> resolved = null;
                var remaining = new List<object>();
                var visited = new HashSet<string>(StringComparer.Ordinal);
                foreach (object item in Array(raw, "status replacement properties"))
                {
                    var mapping = Object(item, "status replacement property");
                    string name = StringField(mapping, "PropertyName");
                    char type = StatusParameterType(fields, name);
                    if (type == '\0') { remaining.Add(item); continue; }
                    if (resolved == null) resolved = new Dictionary<string, object>(component);
                    // FightStatusUtilsHandler returns the first matching name, even if its
                    // special property is absent. Later duplicate mappings cannot override it.
                    if (!visited.Add(name) || values == null) continue;
                    int id = Int(mapping, "SpecialParamProp");
                    foreach (var value in values)
                        if (value != null && value.PropertyId == id)
                        {
                            resolved[name] = ConvertStatusParameter(value, type);
                            break;
                        }
                }
                if (resolved == null) continue;
                resolved["StatusReplacePropertyValues"] = remaining;
                if (result == null) result = new Dictionary<string, object>(components);
                result[entry.Key] = resolved;
            }
            return result ?? components;
        }

        private static char StatusParameterType(string fields, string name)
        {
            foreach (string field in fields.Split(' '))
                if (field.Substring(1) == name) return field[0];
            return '\0';
        }

        // FightStatusBaseComponent.GetPropertyReplaceValue overloads, including the
        // original int narrowing when an integer accessor receives a fixed-point float.
        private static object ConvertStatusParameter(FightStatusReplacementValue value, char type)
        {
            switch (type)
            {
                case 'f':
                    if (value.IsFloat) return (float)(value.RawValue / 10000d);
                    return (float)unchecked((int)value.RawValue);
                case 'i':
                    return value.IsFloat ? (int)(value.RawValue / 10000d) : unchecked((int)value.RawValue);
                case 'l':
                    if (value.IsFloat) return (long)(int)(value.RawValue / 10000d);
                    return value.RawValue;
                default: throw new InvalidOperationException("Unknown original status parameter type.");
            }
        }
    }
}

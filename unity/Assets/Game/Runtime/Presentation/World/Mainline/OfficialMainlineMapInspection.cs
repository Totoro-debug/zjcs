#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZJCS_MAINLINE_MODULE_VALIDATION
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Zjcs.Presentation.World
{
    /// <summary>Inspection for existing regression fixtures across module boundaries.</summary>
    public static class OfficialMainlineMapInspection
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.Public | BindingFlags.NonPublic;

        private static IEnumerable<object> Targets(OfficialMainlineMapRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            yield return runtime;
            foreach (FieldInfo field in typeof(OfficialMainlineMapRuntime).GetFields(Members))
                if (field.FieldType.DeclaringType == typeof(OfficialMainlineMapRuntime)
                    && field.FieldType.Name.EndsWith("Module", StringComparison.Ordinal))
                {
                    object module = field.GetValue(runtime);
                    if (module != null) yield return module;
                }
        }

        public static object Get(OfficialMainlineMapRuntime runtime, string name)
        {
            foreach (object target in Targets(runtime))
            {
                FieldInfo field = target.GetType().GetField(name, Members);
                if (field != null) return field.GetValue(target);
                PropertyInfo property = target.GetType().GetProperty(name, Members);
                if (property != null) return property.GetValue(target);
            }
            throw new MissingMemberException(typeof(OfficialMainlineMapRuntime).FullName, name);
        }

        public static void Set(OfficialMainlineMapRuntime runtime, string name, object value)
        {
            foreach (object target in Targets(runtime))
            {
                FieldInfo field = target.GetType().GetField(name, Members);
                if (field != null) { field.SetValue(target, value); return; }
            }
            throw new MissingFieldException(typeof(OfficialMainlineMapRuntime).FullName, name);
        }

        public static object Call(OfficialMainlineMapRuntime runtime, string name, params object[] arguments)
        {
            foreach (object target in Targets(runtime))
            {
                MethodInfo method = target.GetType().GetMethod(name, Members);
                if (method != null) return method.Invoke(target, arguments);
            }
            throw new MissingMethodException(typeof(OfficialMainlineMapRuntime).FullName, name);
        }
    }
}
#endif

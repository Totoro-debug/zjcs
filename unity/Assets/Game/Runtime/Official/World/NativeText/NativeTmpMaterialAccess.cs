using System;
using System.Reflection;
using TMPro;

namespace Zjcs.Official.World.NativeText
{
    // Original TMPEffect directly accesses TMP_Text.m_isMaterialDirty (ARM64
    // offset 0x140). Preserve that raw field access across the assembly boundary;
    // calling SetMaterialDirty here would register a different graphics update.
    internal static class NativeTmpMaterialAccess
    {
        private static readonly FieldInfo Dirty = typeof(TMP_Text).GetField("m_isMaterialDirty", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(TMP_Text).FullName, "m_isMaterialDirty");
        public static bool IsDirty(TMP_Text text) => (bool)Dirty.GetValue(text);
        public static void SetDirty(TMP_Text text) => Dirty.SetValue(text, true);
    }
}

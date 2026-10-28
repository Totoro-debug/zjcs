using System.Text;
using UnityEngine;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Preserve original diagnostic calls without the online logging backend.
    internal struct MiniMapSettingsErrorHandler
    {
        private StringBuilder text;
        public MiniMapSettingsErrorHandler(int literals, int formatted, out bool enabled)
        { text = new StringBuilder(literals); enabled = true; }
        public void AppendLiteral(string value) => text.Append(value);
        public void AppendFormatted<T>(T value, int alignment = 0, string format = null, string expression = null) => text.Append(value);
        public override string ToString() => text.ToString();
    }
    internal static class MiniMapSettingsDiagnostic
    {
        public static void Error(ref MiniMapSettingsErrorHandler message, object context, string member, string file, int line)
        { Debug.LogError(message.ToString()); }
    }
}

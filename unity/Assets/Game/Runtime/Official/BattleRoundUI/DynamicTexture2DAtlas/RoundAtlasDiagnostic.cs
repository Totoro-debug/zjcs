using System.Globalization;
using System.Text;
using UnityEngine;

namespace Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas
{
    // Retains the decompiled client's diagnostics without its online logging backend.
    internal struct RoundAtlasDiagnostic
    {
        private readonly StringBuilder text;
        public RoundAtlasDiagnostic(int literals, int formatted, out bool enabled)
        {
            text = new StringBuilder(literals);
            enabled = true;
        }
        public void AppendLiteral(string value) => text.Append(value);
        public void AppendFormatted<T>(T value, int alignment = 0, string format = null, string expression = null)
        {
            string rendered = value is System.IFormattable formattable
                ? formattable.ToString(format, CultureInfo.InvariantCulture) : value?.ToString();
            text.Append(rendered);
        }
        public override string ToString() => text.ToString();
    }

    internal static class Log
    {
        public static void Error(ref RoundAtlasDiagnostic message, object context, string member, string file, int line)
            => Debug.LogError(message.ToString());
        public static void Warn(ref RoundAtlasDiagnostic message, object context, string member, string file, int line)
            => Debug.LogWarning(message.ToString());
    }
}

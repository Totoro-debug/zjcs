using System.Text;
using UnityEngine;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Preserve original diagnostic calls without the online logging backend.
    internal struct MiniMapResourcePointErrorHandler
    {
        private StringBuilder text;
        public MiniMapResourcePointErrorHandler(int literals, int formatted, out bool enabled)
        { text = new StringBuilder(literals); enabled = true; }
        public void AppendLiteral(string value) => text.Append(value);
        public void AppendFormatted<T>(T value, int alignment = 0, string format = null, string expression = null) => text.Append(value);
        public override string ToString() => text.ToString();
    }
    internal static class MiniMapResourcePointDiagnostic
    {
        public static void Error(System.Exception exception, ref MiniMapResourcePointErrorHandler message, object context, string member, string file, int line) { Debug.LogError(message.ToString()+"\n"+exception); }
        public static void Error(ref MiniMapResourcePointErrorHandler message, object context, string member, string file, int line)
        { Debug.LogError(message.ToString()); }
    }
}

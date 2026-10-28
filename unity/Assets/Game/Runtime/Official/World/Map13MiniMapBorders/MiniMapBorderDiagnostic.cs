using System.Text;
namespace Zjcs.Official.World.Map13MiniMap
{
    internal struct MiniMapBorderDebugHandler
    {
        private StringBuilder text;
        public MiniMapBorderDebugHandler(int literals,int formatted,out bool enabled){text=new StringBuilder(literals);enabled=true;}
        public void AppendLiteral(string value)=>text.Append(value);
        public void AppendFormatted<T>(T value,int alignment=0,string format=null,string expression=null)=>text.Append(value);
        public override string ToString()=>text.ToString();
    }
    internal static class MiniMapBorderDiagnostic
    {
        public static void Debug(ref MiniMapBorderDebugHandler message,object context,string member,string file,int line)=>UnityEngine.Debug.Log(message.ToString());
    }
}

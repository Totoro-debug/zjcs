using System.Text;
namespace Zjcs.Official.World.Map13MiniMap
{
    internal struct MiniMapDescriptionErrorHandler
    {
        private StringBuilder text;public MiniMapDescriptionErrorHandler(int literals,int formatted,out bool enabled){text=new StringBuilder(literals);enabled=true;}
        public void AppendLiteral(string value)=>text.Append(value);public void AppendFormatted<T>(T value,int alignment=0,string format=null,string expression=null)=>text.Append(value);
        public override string ToString()=>text.ToString();
    }
    internal static class MiniMapDescriptionDiagnostic { public static void Error(ref MiniMapDescriptionErrorHandler message,object context,string member,string file,int line)=>UnityEngine.Debug.LogError(message.ToString()); }
}

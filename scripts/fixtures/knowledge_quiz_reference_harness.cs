// Engine/config adapters for running the unchanged pinned question parser.
using System;
using System.Collections.Generic;
using System.Linq;
using Framework;
namespace Luban { public class ByteBuf {} }
namespace GConfig {
 public class ParserBase { public void PostParse(){} }
}
namespace Common {
 public abstract class LubanDoubleKeyCsvParser<T,A,B> : GConfig.ParserBase {
  public T[] Items=Array.Empty<T>();
  public new virtual void PostParse(){}
  public T[] GetAll(){return Items;}
  public T Get(A first,B second){return Items.FirstOrDefault(x=>Equals(typeof(T).GetField("ActivityId").GetValue(x),first)&&Equals(typeof(T).GetField("Id").GetValue(x),second));}
  protected abstract Dictionary<A,Dictionary<B,T>> LoadLubanTable(Luban.ByteBuf buf);
 }
 public class ActivityKnowledgeQuizQuestionInfo {
  public int ActivityId,Id,Weight; public bool Enabled;
  public KnowledgeQuizDifficultyType Difficulty; public RegionCodeEnum Region; public KnowledgeQuizOptionType CorrectOption;
 }
 public class TbActivityKnowledgeQuizQuestionInfo {
  public IReadOnlyDictionary<(int,int),ActivityKnowledgeQuizQuestionInfo> DataMapUnion;
  public TbActivityKnowledgeQuizQuestionInfo(Luban.ByteBuf buf){throw new NotSupportedException();}
 }
 public static class RandomUtils {
  public static IRandom Default;
  public static int Range(int start,int end,IRandom rng=null){return (rng??Default).Range(start,end);}
 }
}
namespace Framework.Logging.InterpolatedStringHandler {
 public struct GLoggerErrorInterpolatedStringHandler {
  public GLoggerErrorInterpolatedStringHandler(int a,int b,out bool enabled){enabled=false;}
  public void AppendLiteral(string value){}
  public void AppendFormatted<T>(T value,int alignment=0,string format=null,string expression=null){}
 }
}
namespace Framework.Logging {
 public static class Log {
  // Empty and exhausted pools deliberately exercise the original error paths.
  public static void Error(ref InterpolatedStringHandler.GLoggerErrorInterpolatedStringHandler m,object c,string name,string file,int line){}
 }
}

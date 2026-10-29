// Dependency adapters; original fixed point, parser and score methods compile unchanged.
using System;
using System.Collections.Generic;
namespace UnityEngine { public class SerializeField : Attribute {} }
namespace Luban { public class ByteBuf {} }
namespace GConfig { public class ParserBase { public virtual void PostParse() {} } }
namespace Common {
 public abstract class LubanCsvParser<T> : GConfig.ParserBase {
  public T[] configs;
  protected abstract T[] LoadLubanTable(Luban.ByteBuf buf);
 }
 public class ActivityMinigameJumpScoreRuleInfo {
  public int ActivityId,StageID,LevelID;
  public int[] HeightRange;
  public float ScorePerHeightUnit;
 }
 public class TbActivityMinigameJumpScoreRuleInfo {
  public List<ActivityMinigameJumpScoreRuleInfo> DataList=new List<ActivityMinigameJumpScoreRuleInfo>();
  public TbActivityMinigameJumpScoreRuleInfo(Luban.ByteBuf buf) {}
 }
}

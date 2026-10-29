// Engine/config adapters; the tested ingredient and visitor methods stay unchanged.
using System;
using System.Collections.Generic;
using Framework;
public class ActivityData { public DateTime StartDate; }
public class ActivityDataManager {
 public static ActivityDataManager Instance=new ActivityDataManager();
 public ActivityData Data=new ActivityData();
 public bool TryGetActivity(Common.ActivityType type,int id,out ActivityData data){data=Data;return true;}
}
public class TimeManager {
 public static TimeManager Instance=new TimeManager(); public DateTime Now;
 public DateTime GetDateTime(){return Now;}
}
public static class MeManager { public static Common.Player MainPlayer=new Common.Player(); }
namespace Common {
 public enum ActivityType { FestivalMinigame }
 public enum ActivityMiniGameType { IceCream }
 public class ActivityMiniGameIceCreamInfo {
  public int ActivityId,Order,AllVisitorCount,QueueVisitorCount;
  public float UnlockHour;
  public List<int> Npcs,IceCreamFormulas,Weights;
  public WeightRandomSelector<int> IceCreamWeightRandomSelector;
 }
 public class IceCreamFormulaInfo { public int Id,IceCanister,OrnamentType,Score;public List<int> IceCreams; }
 public class FormulaParser {
  public Dictionary<int,IceCreamFormulaInfo> Items=new Dictionary<int,IceCreamFormulaInfo>();
  public IceCreamFormulaInfo Get(int id){return Items[id];}
 }
 public class StageParser {
  public List<ActivityMiniGameIceCreamInfo> Items=new List<ActivityMiniGameIceCreamInfo>();
  public ActivityMiniGameIceCreamInfo Get(int activity,int order){return Items.Find(x=>x.ActivityId==activity&&x.Order==order);}
 }
 public class Parsers {
  public static Parsers Instance=new Parsers();
  public FormulaParser IceCreamFormulaInfoParser=new FormulaParser();
  public StageParser ActivityMiniGameIceCreamInfoParser=new StageParser();
 }
 public class PlayerActivityMiniGameIceCreamVisitorDataWrap {
  public int NPCId,Score; public List<int> IceCreams=new List<int>();
  public static PlayerActivityMiniGameIceCreamVisitorDataWrap CreateNew(){return new PlayerActivityMiniGameIceCreamVisitorDataWrap();}
 }
 public class PlayerActivityMiniGameVisitorDatasWrap : Dictionary<int,PlayerActivityMiniGameIceCreamVisitorDataWrap> {
  public static PlayerActivityMiniGameVisitorDatasWrap CreateNew(){return new PlayerActivityMiniGameVisitorDatasWrap();}
 }
 public class PlayerActivityMiniGameIceCreamStageDataWrap {
  public int SuccessCount;public bool IceCreamStagePassed;
  public PlayerActivityMiniGameVisitorDatasWrap VisitorDatas;
  public static PlayerActivityMiniGameIceCreamStageDataWrap CreateNew(){return new PlayerActivityMiniGameIceCreamStageDataWrap();}
 }
 public class PlayerActivityMiniGameIceCreamStageDatasWrap : Dictionary<int,PlayerActivityMiniGameIceCreamStageDataWrap> {
  public static PlayerActivityMiniGameIceCreamStageDatasWrap CreateNew(){return new PlayerActivityMiniGameIceCreamStageDatasWrap();}
 }
 public class PlayerActivityMiniGameIceCreamDataWrap {
  public PlayerActivityMiniGameIceCreamStageDatasWrap IceCreamStageDatas;
  public static PlayerActivityMiniGameIceCreamDataWrap CreateNew(){return new PlayerActivityMiniGameIceCreamDataWrap();}
 }
 public class PlayerActivityMiniGameDataWrap { public Dictionary<ActivityMiniGameType,object> PartDatas=new Dictionary<ActivityMiniGameType,object>(); }
 public class Player {
  private PlayerActivityMiniGameDataWrap data=new PlayerActivityMiniGameDataWrap();
  public PlayerActivityMiniGameDataWrap GetOrCreateActivityMiniGameData(int id){return data;}
 }
 public static class RandomUtils {
  public static IRandom Default;
  public static int Range(int start,int end,IRandom random=null){return (random??Default).Range(start,end);}
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
  public static void Error(ref InterpolatedStringHandler.GLoggerErrorInterpolatedStringHandler m,object c,string name,string file,int line){throw new InvalidOperationException(name);}
 }
}

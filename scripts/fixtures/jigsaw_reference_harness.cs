// Engine/config adapters only. Original puzzle, parser and UI conditions compile unchanged.
using System;
using System.Collections.Generic;
using System.Linq;
namespace Luban { public class ByteBuf {} }
namespace GConfig { public class ParserBase { public void PostParse(){} } }
namespace Common {
 public abstract class LubanCsvParser<T> : GConfig.ParserBase {
  public T[] Items=Array.Empty<T>(); public T[] Get(){return Items;}
  public new virtual void PostParse(){}
  protected abstract T[] LoadLubanTable(Luban.ByteBuf buf);
 }
 public class ActivityMinigameJigsawInfo {
  public int Id,Level,Order,RowNum,ColumnNum; public float UnlockHour;
  public List<int> InitialLayout; public ActivityMinigameJigsawInfo LastInfo;
 }
 public class TbActivityMinigameJigsawInfo {
  public List<ActivityMinigameJigsawInfo> DataList;
  public TbActivityMinigameJigsawInfo(Luban.ByteBuf buf){throw new NotSupportedException();}
 }
 public static class DecompiledPairAdapter {
  public static void Deconstruct<K,V>(this KeyValuePair<K,V> pair,ref K key,ref V value){key=pair.Key;value=pair.Value;}
 }
 public class PlayerAwardedRecordsSubValueSubValueWrap : HashSet<int> {
  public static PlayerAwardedRecordsSubValueSubValueWrap CreateNew(){return new PlayerAwardedRecordsSubValueSubValueWrap();}
 }
 public class PlayerAwardedRecordsSubValueWrap : Dictionary<int,PlayerAwardedRecordsSubValueSubValueWrap> {
  public static PlayerAwardedRecordsSubValueWrap CreateNew(){return new PlayerAwardedRecordsSubValueWrap();}
 }
 public class PlayerAwardedRecordsWrap : Dictionary<int,PlayerAwardedRecordsSubValueWrap> {}
 public class PlayerJigsawtDataWrap { public PlayerAwardedRecordsWrap AwardedRecords=new PlayerAwardedRecordsWrap(); }
 public class Player { public PlayerJigsawtDataWrap JigsawtData=new PlayerJigsawtDataWrap(); }
 public class JigsawFastCompleteSetting { public List<int> AllowedDifficulties; }
 public class Settings { public JigsawFastCompleteSetting JigsawFastCompleteSetting=new JigsawFastCompleteSetting(); }
 public class Parsers {
  public static Parsers Instance=new Parsers(); public static Settings GameSettings=new Settings();
  public ActivityMinigameJigsawInfoParser ActivityMinigameJigsawInfoParser=new ActivityMinigameJigsawInfoParser();
 }
 public enum ActivityType { FestivalMinigame }
}
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

// Standalone adapters for executing the unchanged pinned Match3Game sources.
// Only engine services/config loading are replaced; board, matching, RNG,
// animation-event data and StageScoreHandler are compiled from original files.
using System;
using System.Collections.Generic;
using Common;
using Match3Game;
public class Parsers : Common.Parsers {}

namespace Framework {
 public class GAction {
  Action action; public void SafeInvoke(){action?.Invoke();}
  public static GAction operator +(GAction a,Action b){a.action+=b;return a;}
  public static GAction operator -(GAction a,Action b){a.action-=b;return a;}
 }
 public class GAction<T> {
  Action<T> action; public void SafeInvoke(T param1){action?.Invoke(param1);}
  public static GAction<T> operator +(GAction<T> a,Action<T> b){a.action+=b;return a;}
  public static GAction<T> operator -(GAction<T> a,Action<T> b){a.action-=b;return a;}
 }
 public class GAction<T,U> {
  Action<T,U> action; public void SafeInvoke(T param1,U param2){action?.Invoke(param1,param2);}
  public static GAction<T,U> operator +(GAction<T,U> a,Action<T,U> b){a.action+=b;return a;}
  public static GAction<T,U> operator -(GAction<T,U> a,Action<T,U> b){a.action-=b;return a;}
 }
 public class GAction<T,U,V> {
  Action<T,U,V> action; public void SafeInvoke(T param1,U param2,V param3){action?.Invoke(param1,param2,param3);}
  public static GAction<T,U,V> operator +(GAction<T,U,V> a,Action<T,U,V> b){a.action+=b;return a;}
  public static GAction<T,U,V> operator -(GAction<T,U,V> a,Action<T,U,V> b){a.action-=b;return a;}
 }
}
namespace Framework.Logging.InterpolatedStringHandler {
 public struct GLoggerErrorInterpolatedStringHandler {
  public GLoggerErrorInterpolatedStringHandler(int a,int b,out bool enabled){enabled=false;}
  public void AppendLiteral(string value){} public void AppendFormatted<T>(T value,int alignment=0,string format=null,string expression=null){}
 }
 public struct GLoggerInfoInterpolatedStringHandler {
  public GLoggerInfoInterpolatedStringHandler(int a,int b,out bool enabled){enabled=false;}
  public void AppendLiteral(string value){} public void AppendFormatted<T>(T value,int alignment=0,string format=null,string expression=null){}
 }
}
namespace Framework.Logging {
 public static class Log {
  public static void Error(ref InterpolatedStringHandler.GLoggerErrorInterpolatedStringHandler m,object c,string name,string file,int line){throw new InvalidOperationException(name+" at "+line);}
  public static void Info(ref InterpolatedStringHandler.GLoggerInfoInterpolatedStringHandler m,object c,string name,string file,int line){}
 }
}
namespace Common {
 public struct Pos2d : IEquatable<Pos2d> {
  public int X,Y; public Pos2d(int x,int y){X=x;Y=y;}
  public static int Distance(Pos2d a,Pos2d b){return Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y);}
  public static bool operator ==(Pos2d a,Pos2d b){return a.X==b.X&&a.Y==b.Y;}
  public static bool operator !=(Pos2d a,Pos2d b){return !(a==b);}
  public bool Equals(Pos2d b){return this==b;} public override bool Equals(object b){return b is Pos2d p&&this==p;}
  public override int GetHashCode(){return 31*X.GetHashCode()+Y.GetHashCode();}
  public override string ToString(){return X+","+Y;}
 }
 public static class JsonUtils {public static string ToJson(object value){return "";}}
 public static class RandomUtils {
  public static int Range(int a,int b,Framework.IRandom rng){return rng.Range(a,b);}
  public static T RandomOneInList<T>(List<T> values,Framework.IRandom rng){return values[rng.Range(0,values.Count)];}
 }
 public class Match3GameItemInfo {
  public int ItemId,Score,LossId; public Match3ItemType ItemType;
  public List<Match3ItemSkillType> SpecialSkills;
 }
 public class Match3GameStageInfo {
  public int StageId,Step,StageParam,MinMatchCount; public int[] GridSize;
  public List<int> ItemGroup,ItemSpawnWeights;
  public Dictionary<string,Match3ItemSkillType> InitBoardSkillDict;
 }
 public class Match3SkillScoreConfig {public long Score;public float Rate;}
 public class ActivityMatch3GameSetting {
  public int MaxChainCheckTimes,CreateBoardTryTimes,CrossBoomMinMatchCount,BoomDiameter,ColorMinMatchCount,LineMinMatchCount,ComboScoreBaseCount;
  public float CrossScoreRate,ComboScoreRate,StepConvertRate;
  public List<Match3ItemSkillType> SkillHitPriority=new List<Match3ItemSkillType>();
  public Dictionary<Match3ItemSkillType,Match3SkillScoreConfig> SkillScoreDict=new Dictionary<Match3ItemSkillType,Match3SkillScoreConfig>();
 }
 public class Match3GameItemInfoParser {
  public Dictionary<int,Match3GameItemInfo> Items=new Dictionary<int,Match3GameItemInfo>();
  public Match3GameItemInfo Get(int id){return Items[id];}
  public int GetMaxHp(int id){int hp=1;var seen=new HashSet<int>();while(Items[id].LossId>0&&seen.Add(id)){id=Items[id].LossId;hp++;}return hp;}
 }
 public class Parsers {
  public static Parsers Instance=new Parsers();public static Settings GameSettings=new Settings();
  public Match3GameItemInfoParser Match3GameItemInfoParser=new Match3GameItemInfoParser();
  public class Settings {public ActivityMatch3GameSetting ActivityMatch3GameSetting;}
 }
}
namespace Match3Game {
 public interface IStageDataProvider {int ComboTimes{get;}}
 public interface IGameDataProvider {IGameBoardDataProvider GameBoardDataProvider{get;}}
}
public class Match3Reference : IStageDataProvider,IGameDataProvider {
 public GameBoard Board;public StageScoreHandler Score;
 public IGameBoardDataProvider GameBoardDataProvider=>Board;public int ComboTimes=>Score.ComboTimes;
 public Match3Reference(Match3GameStageInfo stage,long seed){Board=new GameBoard(this,stage,seed);Score=new StageScoreHandler(this,null,null);Score.Init();Board.Init();}
 public int[][] Snapshot(){var result=new List<int[]>();foreach(var item in Board.Grid)result.Add(new[]{item.Pos.X,item.Pos.Y,item.ItemInfo.ItemId,item.Hp,(int)item.SkillType});return result.ToArray();}
 public object State(){return new {board=Snapshot(),score=Score.Score,combos=Score.MaxComboTimes};}
 public object Next(){
  for(int y=0;y<Board.Grid.GetLength(1);y++)for(int x=0;x<Board.Grid.GetLength(0);x++)foreach(var axis in new[]{new Pos2d(1,0),new Pos2d(0,1)}){
   var from=new Pos2d(x,y);var to=new Pos2d(x+axis.X,y+axis.Y);
   if(Board.SwapItemAndTryMatch(from,to))return new {from=new[]{from.X,from.Y},to=new[]{to.X,to.Y},result=State()};
  }
  throw new InvalidOperationException("No native valid move");
 }
 public object Special(int left,int right){
  Board.GetItem(0,0).SetSkillType((Match3ItemSkillType)left);Board.GetItem(1,0).SetSkillType((Match3ItemSkillType)right);
  var initial=Snapshot();bool ok=Board.SwapItemAndTryMatch(new Pos2d(0,0),new Pos2d(1,0));
  if(!ok)throw new InvalidOperationException("Native special swap failed");
  return new {initial=initial,result=State()};
 }
}

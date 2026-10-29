// Engine/config adapters only. The generator inserts verbatim pinned methods.
using System;
using System.Collections.Generic;
using Framework;
using Common;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace UnityEngine {
 public class Object {}
 public class Component:Object {
  public T GetComponent<T>() where T:class{return null;}
  public T GetComponentInChildren<T>(bool includeInactive) where T:class{return null;}
 }
 public class Transform:Component {}
 public class Sprite:Object {}
 public class Animation:Object {public void Play(string name){}}
 public static class Time {public static float deltaTime;}
 public static class Mathf {
  public static int RoundToInt(float value){return (int)Math.Round(value,MidpointRounding.ToEven);}
  public static int Max(int a,int b){return Math.Max(a,b);}
  public static int Min(params int[] values){int r=int.MaxValue;foreach(int v in values)r=Math.Min(r,v);return r;}
 }
}
namespace UnityEngine.UI { public class Image:Component {public Sprite sprite;} }
public enum EUIInstantiateFlag { AutoActive }
public static class UIUtils {public static T Instantiate<T>(T prefab,Transform parent,EUIInstantiateFlag flag) where T:new(){return new T();}}
public class StateSwitcher<T> {public void SetState(T state){}}
public class BBQSkewerFoodIngredient {public void Initialize(int food,object loader){} public void SetCookStatus(BBQCookState state){}}
public class BBQMainGameContent:Object {public bool IsGameplayPaused;}
public sealed class NativeSkewer {
 public List<Transform> mFoodMountPoints=new List<Transform>();
 public Image mFoodIconPrefab=new Image();public Animation mFoodMountAnimation;
 public string[] mFoodMountAnimationClipNames;
 public List<int> mFoodIds=new List<int>();public List<BBQSkewerFoodIngredient> mFoodIngredients=new List<BBQSkewerFoodIngredient>();
 public BBQMainGameContent mMainContent=new BBQMainGameContent();public object mResourceLoader;
 public StateSwitcher<BBQCookState> mCookStateSwitcher=new StateSwitcher<BBQCookState>();
 public StateSwitcher<BBQSkewerFlowState> mFlowStateSwitcher=new StateSwitcher<BBQSkewerFlowState>();
 public BBQCookState mCookState;public BBQSkewerFlowState mFlowState;public int mMaxFoodCount;
 public float mTotalCookSeconds,mBurntTimeRate,mGrillElapsed,mPostCookedElapsed;public bool mCookTimerPaused;
 public event Action<BBQCookState> CookStateChanged;
 public void Frame(float delta,bool paused,bool dragPaused){Time.deltaTime=delta;mMainContent.IsGameplayPaused=paused;mCookTimerPaused=dragPaused;Update();}
 /*PINNED_SKEWER_METHODS*/
}
public interface IBBQGameFlowHost {BBQGameSession Session{get;} BBQNpcSlotsView NpcSlotsView{get;}}
public class BBQNpcSlotsView:Object {public List<BBQNpcSlot> NpcSlots=new List<BBQNpcSlot>();}
public class NativeHost:IBBQGameFlowHost {
 public BBQGameSession Session{get;set;}public BBQNpcSlotsView NpcSlotsView{get;set;}
}
public sealed class NativeNormal {
 public static bool Exhausted(IBBQGameFlowHost host){return AreNormalLevelVisitorsExhausted(host);}
 /*PINNED_NORMAL_METHODS*/
}
public sealed class NativeEndless { /*PINNED_ENDLESS_METHODS*/ }
public sealed class BBQNpcSlot:Object {
 public int NpcId;public bool mVisitorReady;public float mPatienceRemaining,mPatienceTotal;
 public BBQMainGameContent mFlowHost=new BBQMainGameContent();public Action<BBQNpcSlot> mOnPatienceExpired;
 public int Expired;private void UpdatePatienceVisual(){}
 public void Frame(float delta,bool paused){Time.deltaTime=delta;mFlowHost.IsGameplayPaused=paused;Update();}
 public void Start(int npc,float wait){NpcId=npc;mVisitorReady=false;mPatienceTotal=wait>0?wait:-1;mPatienceRemaining=mPatienceTotal;mOnPatienceExpired=s=>Expired++;}
 /*PINNED_VISITOR_METHODS*/
}
namespace Common {
 public class ActivityMiniGameBBQFormulaInfo {public int Id,Score,WaitTime;public List<int> Foods;}
 public class ActivityMiniGameBBQFoodInfo {public int Id,Time;}
 public class ActivityMiniGameBBQLevelInfo {
  public int ActivityId,Order,AllVisitorCount,PassScore;public List<int> Npcs,BBQFormulas,Weights;
  public WeightRandomSelector<int> BBQFormulasWeightRandomSelector;
 }
 public class FormulaParser {public Dictionary<int,ActivityMiniGameBBQFormulaInfo> Items=new Dictionary<int,ActivityMiniGameBBQFormulaInfo>();public ActivityMiniGameBBQFormulaInfo Get(int id){Items.TryGetValue(id,out var result);return result;}}
 public class FoodParser {public Dictionary<int,ActivityMiniGameBBQFoodInfo> Items=new Dictionary<int,ActivityMiniGameBBQFoodInfo>();public ActivityMiniGameBBQFoodInfo Get(int id){Items.TryGetValue(id,out var result);return result;}}
 public class BBQSetting {public float BurntScoreRate;}
 public class Settings {public BBQSetting BBQSetting=new BBQSetting();}
 public class Parsers {
  public static Parsers Instance=new Parsers();public static Settings GameSettings=new Settings();
  public FormulaParser ActivityMiniGameBBQFormulaInfoParser=new FormulaParser();public FoodParser ActivityMiniGameBBQFoodInfoParser=new FoodParser();
 }
 public static class RandomUtils {
  public static IRandom Default;
  public static int Range(int start,int end,IRandom random=null){return (random??Default).Range(start,end);}
  public static T RandomOneInList<T>(List<T> list,IRandom random=null){return list[(random??Default).Range(0,list.Count)];}
 }
}
namespace Framework.Logging.InterpolatedStringHandler {
 public struct GLoggerErrorInterpolatedStringHandler {
  public GLoggerErrorInterpolatedStringHandler(int a,int b,out bool enabled){enabled=false;}
  public void AppendLiteral(string value){}public void AppendFormatted<T>(T value,int alignment=0,string format=null,string expression=null){}
 }
}
namespace Framework.Logging {
 public static class Log {public static void Error(ref InterpolatedStringHandler.GLoggerErrorInterpolatedStringHandler m,object c,string name,string file,int line){throw new InvalidOperationException(name);}}
}

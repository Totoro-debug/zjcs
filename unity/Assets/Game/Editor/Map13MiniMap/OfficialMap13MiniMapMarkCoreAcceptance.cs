using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
using Object=UnityEngine.Object;
using MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
using FullScreenAction=Zjcs.Official.StoryPlayables.GAction;

namespace Zjcs.Editor.Map13MiniMap
{
    public class Map13MarkFixture:MiniMapMarkContext
    {
        internal MiniMapSettings Settings;internal bool Root=true;internal int Current=13;internal Object MoveSource;internal Vector3 World;
        public override bool HasMapRoot=>Root;public override Object GetMoveAgent(IMiniMapCharacterController controller)=>MoveSource;public override Vector3 GetCharacterWorldPosition(IMiniMapCharacterController controller)=>World;
        public override MiniMapSettings.MiniMapSetting MiniMapSetting=>Settings.GetMinimapSetting(2);
        public override int MapClassId=>13;public override int CurrentMapClassId=>Current;public override bool HasMainPlayer=>true;public override IMapViewRangeHandler ViewRangeHandler=>null;
        public override MiniMapKeepSizeHandler SizingHandler=>null;public override FullScreenAction HideFullScreenAction{get;}=new FullScreenAction();public override FullScreenAction ShowFullScreenAction{get;}=new FullScreenAction();public override bool IsFullScreen=>false;
        public override bool HasGridVisibilityAgent=>false;public override MiniMapVisibilityAgent VisibilityAgent=>null;public override int CharacterViewRange=>5;public override Pos2d CharacterGridPos=>Pos2d.Zero;
        public override IMap13LabelServices LabelServices=>throw new NotSupportedException();
        public override event Action<IMapViewRangeHandler> OnVisibleRangeUpdate;
        public override event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate;
        public override event Action<Pos2d,bool,bool> OnPosVisibilityUpdate;
        public override IEnumerable<Pos2d> ForEachMapRegionActivedAllGrid(int map,int region)=>throw new NotSupportedException();
        public override bool TryGetMapLiteData(int map,out MapRootEntityLiteSingleInfo info){info=Lite;return true;}
        public override IMiniMapGridMover GetSpawnGridMover(int map)=>throw new NotSupportedException();public override IReadOnlyDictionary<int,MiniMapRegionInfo> GetMapRegionDict(int map)=>throw new NotSupportedException();public override MiniMapRegionInfo GetRegion(int map,int region)=>throw new NotSupportedException();public override int AreaToRegion(int map,int area)=>throw new NotSupportedException();public override int QueryRegion(Pos2d pos)=>throw new NotSupportedException();public override List<int> GetWorldMapProgresses(int map)=>throw new NotSupportedException();public override MiniMapRegionDisplayInfo GetRegionDisplay(int map,int region)=>throw new NotSupportedException();public override object QueryCommonGridGround(int map)=>throw new NotSupportedException();public override object QuerySceneContainer(Component component)=>throw new NotSupportedException();
        public override MiniMapRegionDescItem InstantiateDescription(MiniMapRegionDescItem prefab,Transform parent)=>throw new NotSupportedException();public override GameObject InstantiateDisplay(GameObject prefab,Transform parent)=>Object.Instantiate(prefab,parent,false);public override IMiniMapDescriptionAssetLease<GameObject> LoadPrefab(string path){Loads++;return new Lease<GameObject>(Icon,()=>Disposed++);}public override List<MapRegionStatus> GetMapStatusListInTeam(int map)=>throw new NotSupportedException();public override bool IsRangeInViewRange(Pos2d min,Pos2d max)=>throw new NotSupportedException();public override IMiniMapRegionAssetLease LoadAssetByGuid(string guid)=>throw new NotSupportedException();public override GameObject InstantiateRegion(GameObject prefab,Transform parent,bool world)=>throw new NotSupportedException();
    
        internal readonly List<string> Trace=new List<string>();internal readonly List<int> Errors=new List<int>();internal readonly List<GameObject> Windows=new List<GameObject>();internal int Loads,Disposed;internal GameObject Icon;internal Sprite Background;internal MiniMapMarkDisplayInfo BackgroundInfo=new MiniMapMarkDisplayInfo{MapBgPath="OriginalBackground"};internal MiniMapMarkDialogBase LastDialog;internal Func<Type,MiniMapMarkDialogBase> Factory;internal IMiniMapCharacterActionGroupHandle ActionGroup;internal bool Ground=true,Partition=true;internal Vector3 Reserved;
        internal MapRootEntityLiteSingleInfo Lite=new MapRootEntityLiteSingleInfo{MapClassId=13,MapGroundSize=new Pos2d(240,240),MapGroundOriginPos=new Vector3(-110.5f,-145.5f,0)};
        public override IMiniMapCharacterActionGroupHandle CharacterActionGroup=>ActionGroup;public override bool HasMapGroundRoot=>Ground;public override bool HasPartitionController=>Partition;public override void ReservePartition(Vector3 position){Reserved=position;Trace.Add("reserve");}
        public override string GetAreaTag(int map,Pos2d position){if(map!=13)throw new InvalidDataException();return "OriginalAreaTag";}public override MiniMapMarkDisplayInfo GetMapUIDisplay(int map,string tag){if(map!=13||tag!="OriginalAreaTag")throw new InvalidDataException();return BackgroundInfo;}
        public override IMiniMapDescriptionAssetLease<Sprite> LoadSprite(string path){if(path!="OriginalBackground")throw new InvalidDataException();Loads++;return new Lease<Sprite>(Background,()=>Disposed++);}
        public override void Popup(Type type,Action<MiniMapMarkDialogBase> callback){Trace.Add("popup:"+type.Name);LastDialog=Factory(type);Windows.Add(LastDialog.gameObject);callback(LastDialog);}
        public override void CloseWindow(GameObject window){Trace.Add("close");}public override void ShowError(int code){Errors.Add(code);}public override void FireToCloseFullScreen(){Trace.Add("fullscreen");}
        private sealed class Lease<T>:IMiniMapDescriptionAssetLease<T>{public T Result{get;}private readonly Action disposed;public Lease(T value,Action action){Result=value;disposed=action;}public void Dispose()=>disposed();}
    }

    public sealed class Map13MarkSupportProbe:MiniMapMarkSupportBase
    {
        internal string Id;internal List<string> Trace;
        public override void Init(MiniMapMarkUIBase ui){Trace.Add("init:"+Id+":"+ui.Region);if(ui.MiniMap==null||ui.Mark==null||ui.transform.localPosition!=ui.Mark.LocalPosition)throw new InvalidDataException("Support initialization order");}
        public override void Release(MiniMapMarkUIBase ui){Trace.Add("release:"+Id);}
    }
    public sealed class Map13MarkBlockProbe:MonoBehaviour,IMiniMapWayPointBlockHandler
    {internal bool Allow=true;internal int Calls;public bool CanUse(){Calls++;return Allow;}}
    public sealed class Map13MarkIconProbe:MonoBehaviour,IMapEntityDialogIconHandler
    {internal object Entity;public void Init(object entity){Entity=entity;}public void Init(MapEntityDialogIconLiteEntityData data){Entity=data;}}
    public static class OfficialMap13MiniMapMarkCoreAcceptance
    {
        private const string Key="Zjcs.Map13.MiniMapMarkCore.Running";private static IEnumerator routine;private static int errors;
        private static string Workspace=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        private sealed class Data:MiniMapMarkBase
        {
            internal bool Possible=true;internal string Icon="OriginalIcon";internal readonly object Entity=new object();internal List<string> Trace;internal int IconHandlers;
            public Data(){MapClassId=13;Region=38;GridPos=new Pos2d(111,146);LocalPosition=new Vector3(.5f,.5f,0);}
            public override MiniMapMarkDataType DataType=>MiniMapMarkDataType.Entity;public override bool EntityActive=>false;public override List<Pos2d> BodyRange{get;}=new List<Pos2d>{Pos2d.Zero};public override int GetEntityState()=>0;public override string GetMarkNameKey()=>"region_13_38";public override MiniMapMarkTextKeyArg GetMarkDescTextArgKey()=>new MiniMapMarkTextKeyArg("desc",1,2);public override string GetDialogIconPrefabPath()=>Icon;public override void InitDialogIconHandler(IMapEntityDialogIconHandler[] handlers){foreach(var handler in handlers){handler.Init(Entity);IconHandlers++;}}public override bool InteractPossible()=>Possible;public override void OnClick(){Trace.Add("click");}
        }
        private sealed class Custom:MiniMapCustomMark{public void Set(Pos2d point){GridPos=point;}}
        private sealed class ActionGroup:IMiniMapCharacterActionGroupHandle
        {internal Action Pending;internal int Calls;public UniTask ConfirmInterruptAndContinueAsync(Action callback){Pending=callback;Calls++;return UniTask.CompletedTask;}}
        [Serializable] private sealed class Proof{public string editor;public int text_cases,supports,ui_types,icon_handlers,asset_leases,button_clicks;public bool original_text_value,original_support_order,original_release_state,waypoint_block_order,original_error_2116,original_icon_reset,deferred_action_order,entry_reservation,button_unsubscribes,asset_disposal;}
        private static void Require(bool value,string detail){if(!value)throw new InvalidDataException(detail);}
        private static void Observe(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange value){if(value==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Runtime errors: "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private static void Set(object target,string name,object value){var type=target.GetType();while(type!=null){var field=type.GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.DeclaredOnly);if(field!=null){field.SetValue(target,value);return;}type=type.BaseType;}throw new MissingFieldException(name);}
        private static T UI<T>(Map13MarkFixture map,List<string> trace) where T:MiniMapMarkUIBase
        {var go=new GameObject(typeof(T).Name);go.SetActive(false);var ui=go.AddComponent<T>();foreach(string id in new[]{"A","B"}){var probe=go.AddComponent<Map13MarkSupportProbe>();probe.Id=id;probe.Trace=trace;}if(typeof(T)==typeof(MiniMapWayPointMarkUI)){go.AddComponent<Map13MarkBlockProbe>();go.AddComponent<Map13MarkBlockProbe>();}go.SetActive(true);return ui;}
        private static MiniMapMarkDialogBase Dialog(Map13MarkFixture map,Type type)
        {
            var go=new GameObject("OriginalDialogBehaviorFixture");go.SetActive(false);var dialog=(MiniMapMarkDialogBase)go.AddComponent(type);if(dialog is MiniMapFunctionMarkDialog function){var button=new GameObject("ButtonInputFixture",typeof(RectTransform),typeof(Button));button.transform.SetParent(go.transform,false);function.Btn=button.GetComponent<Button>();}
            var preview=new GameObject("IconSlotFixture",typeof(RectTransform));preview.transform.SetParent(go.transform,false);var old=new GameObject("OldIconFixture");old.transform.SetParent(preview.transform,false);dialog.myIconSlot=new MapDialogIconPreviewSlot();Set(dialog.myIconSlot,"mPreviewSlot",preview.transform);var bg=new GameObject("BackgroundReferenceFixture",typeof(RectTransform),typeof(Image));bg.transform.SetParent(go.transform,false);Set(dialog,"mBgIcon",bg.GetComponent<Image>());go.SetActive(true);return dialog;
        }
        private static IEnumerator Verify()
        {
            var savedFormatter=MiniMapMarkLanguage.Formatter;MiniMapMarkLanguage.Formatter=(key,args)=>key+":"+string.Join(",",args??Array.Empty<object>());int textCases=0;
            foreach(string key in new[]{"","a","region_13_38"})foreach(object[] args in new[]{null,Array.Empty<object>(),new object[]{1,"x"},new object[]{2,"x"}}){var a=new MiniMapMarkTextKeyArg(key,args);var b=new MiniMapMarkTextKeyArg(key,args==null?null:(object[])args.Clone());Require(a.Equals(b)&&a.GetHashCode()==b.GetHashCode(),"Original text key structural equality/hash");Require(a.ToString()==key+":"+string.Join(",",args??Array.Empty<object>()),"Original formatting port");textCases++;}
            Require(MiniMapMarkTextKeyArg.sEmpty.IsEmpty&&default(MiniMapMarkTextKeyArg).IsValid&&!new MiniMapMarkTextKeyArg("a",1).Equals(new MiniMapMarkTextKeyArg("a",2)),"Original empty identity/default validity and unequal values");var custom=new Custom();custom.Set(new Pos2d(3,4));var body=custom.BodyRange;custom.Set(new Pos2d(8,9));Require(custom.DataType==MiniMapMarkDataType.Entity&&!custom.EntityActive&&custom.InteractPossible()&&ReferenceEquals(body,custom.BodyRange)&&body.Single()==new Pos2d(3,4),"Original custom data type and cached absolute body range");
            var host=new GameObject("OriginalMarkCoreFixture");var map=host.AddComponent<Map13MarkFixture>();map.Icon=new GameObject("DialogIconFixture",typeof(RectTransform),typeof(Map13MarkIconProbe));map.Icon.SetActive(false);map.Icon.transform.localPosition=new Vector3(3,4,5);map.Icon.transform.localScale=new Vector3(2,3,4);map.Icon.transform.localRotation=Quaternion.Euler(0,0,40);map.Background=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Official/World/Map/13/NativeMiniMapCharacter/Image/MapWorld/MiniMap_Atlas/C_MiniMap_Me.asset");Require(map.Background,"Verified native original Sprite reference");map.Factory=type=>Dialog(map,type);var data=new Data{Trace=map.Trace};var supportTrace=new List<string>();var only=UI<MiniMapOnlyMarkUI>(map,supportTrace);MiniMapMarkBase oldMark=null;int updates=0;only.OnMarkUpdate+=old=>{oldMark=old;updates++;Require(only.Mark==(updates==1?data:null),"Original Mark assignment precedes event");};only.Init(map,data);Require(oldMark==null&&updates==1&&supportTrace.SequenceEqual(new[]{"init:A:38","init:B:38"})&&only.Region==38&&only.GridPos==data.GridPos&&ReferenceEquals(only.BodyRange,data.BodyRange),"Original support ordering and data aliases");only.OpenDialog();Require(map.Windows.Count==0&&!only.CanInteractive&&only.MarkType==MiniMapMarkType.MarkOnly,"Original noninteractive marker");only.OnGameObjectPoolReset();Require(updates==1,"Original pool reset no-op");only.OnGameObjectPoolClear();Require(updates==2&&oldMark==data&&only.Mark==null&&only.MiniMap==null&&only.Region==38&&only.transform.localPosition==Vector3.zero&&supportTrace.Skip(2).SequenceEqual(new[]{"release:A","release:B"}),"Original release retains region and resets mark/map/position");
            var display=UI<MiniMapDisplayMarkUI>(map,new List<string>());display.Init(map,data);display.OpenDialog();Require(map.LastDialog.GetType()==typeof(MiniMapDisplayMarkDialog)&&display.CanInteractive&&display.MarkType==MiniMapMarkType.Display,"Original display popup");yield return null;Require(map.LastDialog.myIconSlot.PreviewSlot.childCount==1,"Original child destruction completes");var icon=map.LastDialog.myIconSlot.PreviewSlot.GetChild(0);Require(icon.localPosition==Vector3.zero&&icon.localRotation==Quaternion.identity&&icon.localScale==Vector3.one&&((RectTransform)icon).anchoredPosition==Vector2.zero&&ReferenceEquals(icon.GetComponent<Map13MarkIconProbe>().Entity,data.Entity),"Original icon reset/centering and intact entity reference");
            var waypoint=UI<MiniMapWayPointMarkUI>(map,new List<string>());waypoint.Init(map,data);data.Possible=false;waypoint.OpenDialog();Require(map.Errors.SequenceEqual(new[]{2116}),"Original waypoint unavailable error");var blocks=waypoint.GetComponents<Map13MarkBlockProbe>();data.Possible=true;blocks[0].Allow=false;int windows=map.Windows.Count;waypoint.OpenDialog();Require(blocks[0].Calls==1&&blocks[1].Calls==0&&map.Windows.Count==windows,"Original block handlers short circuit in order");blocks[0].Allow=true;blocks[1].Allow=false;waypoint.OpenDialog();Require(blocks[0].Calls==2&&blocks[1].Calls==1&&map.Windows.Count==windows,"Original second block");blocks[1].Allow=true;waypoint.OpenDialog();Require(map.LastDialog.GetType()==typeof(MiniMapWayPointMarkDialog)&&waypoint.MarkType==MiniMapMarkType.WayPoint,"Original waypoint dialog");
            var events=new GameObject("CoreButtonEventFixture",typeof(EventSystem)).GetComponent<EventSystem>();var function=(MiniMapFunctionMarkDialog)map.LastDialog;map.Trace.Clear();ExecuteEvents.Execute(function.Btn.gameObject,new PointerEventData(events){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);Require(map.Trace.SequenceEqual(new[]{"click","close","fullscreen"}),"Original immediate action/close/fullscreen order");int clicks=1;
            var action=new ActionGroup();map.ActionGroup=action;map.Trace.Clear();ExecuteEvents.Execute(function.Btn.gameObject,new PointerEventData(events){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);Require(action.Calls==1&&action.Pending!=null&&map.Trace.Count==0,"Original action confirmation defers mutation");action.Pending();Require(map.Trace.SequenceEqual(new[]{"click","close","fullscreen"}),"Original continuation order");clicks++;
            var entry=UI<MiniMapEntryMarkUI>(map,new List<string>());entry.Init(map,data);data.Possible=false;entry.OpenDialog();Require(map.Errors.SequenceEqual(new[]{2116,2116}),"Original entry unavailable error");data.Possible=true;entry.OpenDialog();Require(map.LastDialog.GetType()==typeof(MiniMapEntryMarkDialog)&&entry.MarkType==MiniMapMarkType.Entry&&map.Reserved==map.Lite.GridToPos(data.GridPos),"Original entry partition reservation");map.Ground=false;map.Reserved=new Vector3(99,99,99);entry.OpenDialog();Require(map.Reserved==new Vector3(99,99,99),"Original absent ground skips reservation");
            map.BackgroundInfo=null;data.Icon=null;int before=map.Loads;entry.OpenDialog();Require(map.Loads==before,"Original absent icon/background avoids resource loads");var button=function.Btn;var buttonParent=button.transform;buttonParent.SetParent(null,false);Object.Destroy(function.gameObject);yield return null;map.Trace.Clear();ExecuteEvents.Execute(button.gameObject,new PointerEventData(events){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);Require(map.Trace.Count==0,"Original function destroy removes Button listener");
            foreach(var window in map.Windows)if(window)Object.Destroy(window);Object.Destroy(only.gameObject);Object.Destroy(display.gameObject);Object.Destroy(waypoint.gameObject);Object.Destroy(entry.gameObject);Object.Destroy(map.Icon);Object.Destroy(button.gameObject);Object.Destroy(events.gameObject);yield return null;Require(map.Loads==map.Disposed&&map.Loads>0,"Original all icon/background leases released");int leases=map.Loads,handlers=data.IconHandlers;Object.Destroy(host);MiniMapMarkLanguage.Formatter=savedFormatter;yield return null;
            File.WriteAllText(Path.Combine(Workspace,"minimap-mark-core-rules.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,text_cases=textCases,supports=2,ui_types=4,icon_handlers=handlers,asset_leases=leases,button_clicks=clicks,original_text_value=true,original_support_order=true,original_release_state=true,waypoint_block_order=true,original_error_2116=true,original_icon_reset=true,deferred_action_order=true,entry_reservation=true,button_unsubscribes=true,asset_disposal=true},true));Debug.Log("MAP13_MINIMAP_MARK_CORE_PASS");
        }
    }

}

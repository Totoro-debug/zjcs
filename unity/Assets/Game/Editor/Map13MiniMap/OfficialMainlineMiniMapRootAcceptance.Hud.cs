using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.World.MainlineMiniMap;
using Zjcs.Offline.World.NativeLabelResources;
using Zjcs.Offline.World.NativeMap13Labels;
using Object=UnityEngine.Object;
using Status=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
using Pos2d=Zjcs.Official.World.NativeTerrain.Common.Pos2d;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
    public sealed class MiniMapHudSimulatedMouse : BaseInput
    {
        public Vector2 Position;public bool Down,Up,Held;
        public override bool mousePresent=>true;
        public override Vector2 mousePosition=>Position;
        public override bool GetMouseButtonDown(int button)=>button==0&&Down;
        public override bool GetMouseButtonUp(int button)=>button==0&&Up;
        public override bool GetMouseButton(int button)=>button==0&&Held;
        public override float GetAxisRaw(string axis)=>0;
        public override bool GetButtonDown(string button)=>false;
    }

        private sealed class HudLanguage : ILanguageTypeService
        {
            public LanguageType CurrentLanguage{get;set;}=LanguageType.zh_CN;
            public bool IsShowLanguageKey{get;set;} public bool IsShowMissingKeyText{get;set;}
            public event Action<LanguageType> OnLanguageChanged;
        }
        private sealed class HudState : MiniMapHudContext,IMiniMapHudFullScreenPanel
        {
            internal MiniMapManagedContext Source;internal ILocalizationTextService Text;
            internal int Current=13,PopupCount,InitializedMap,BranchPopups,Error;
            internal Action<IMiniMapHudFullScreenPanel> Deferred;
            internal PublicMapLineStatus Line=PublicMapLineStatus.Free;
            internal readonly List<long> Lines=new List<long>{1};
            internal readonly Dictionary<int,List<Status>> Regions=new Dictionary<int,List<Status>>{{13,new List<Status>{Status.Private}}};
            internal readonly Dictionary<long,MiniMapHudFightData> Fights=new Dictionary<long,MiniMapHudFightData>();
            public override MiniMapManagedContext Root=>Source;
            public override int CurrentMapClassId=>Current;
            public override string GetMapGoddessHead(int map)=>null;
            public override string FormatText(string key,object[] args)=>Text.GetTextFormated(key,LanguageType.zh_CN,args);
            public override void PopupFullScreen(Action<IMiniMapHudFullScreenPanel> callback){PopupCount++;Deferred=callback;}
            public void Init(int map)=>InitializedMap=map;
            public override void PopupMapNotify(Action<IMiniMapHudNotify> callback)=>throw new NotSupportedException();
            public override MiniMapHudMapPartInfo GetMapPartInfo(int map)=>new MiniMapHudMapPartInfo{ClassId=map,SupportPublicRegion=true};
            public override bool HasMainPlayer=>true;
            public override Dictionary<long,MiniMapHudFightData> CurrentGames=>Fights;
            public override Dictionary<int,List<Status>> MapRegionStatusDict=>Regions;
            public override bool TryGetPublicContainerId(out long id){id=42;return true;}
            public override PublicMapLineStatus PublicMapLineStatus=>Line;
            public override List<long> GroupMapIdOrderList=>Lines;
            public override int GetMapLineIndex()=>2;
            // Explicit test colors; production must bind the original GlobalColors asset.
            public override MiniMapHudChannelColors ChannelColors{get;}=new MiniMapHudChannelColors{ServiceLoadFree=Color.green,ServiceLoadBusy=Color.yellow,ServiceLoadFull=Color.red,ServiceLoadOverLoad=Color.magenta};
            public override event Action<int,int,Status,Status> OnPlayerMapStatusUpdate;
            public override event Action<PublicMapLineStatus> OnPublicMapLineStatusChanged;
            public override event Action<List<long>> OnGroupMapIdOrderListChanged;
            internal int Subscribers=>(OnPlayerMapStatusUpdate?.GetInvocationList().Length??0)+(OnPublicMapLineStatusChanged?.GetInvocationList().Length??0)+(OnGroupMapIdOrderListChanged?.GetInvocationList().Length??0);
            internal void Public(){Regions[13][0]=Status.Public;OnPlayerMapStatusUpdate?.Invoke(13,0,Status.Private,Status.Public);}
            internal void AddLine(){Lines.Add(2);OnGroupMapIdOrderListChanged?.Invoke(Lines);}
            internal void Busy(){Line=PublicMapLineStatus.Busy;OnPublicMapLineStatusChanged?.Invoke(PublicMapLineStatus.Free);}
            public override void ShowError(int code)=>Error=code;
            public override void PopupPublicMapBranchLineDialog()=>BranchPopups++;
        }
        private static T HudField<T>(Object instance,string field) where T:Object=>new SerializedObject(instance).FindProperty(field).objectReferenceValue as T;
        private static void SaveHudImage(Camera camera,RenderTexture target,string name)
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();RenderTexture.active=previous;
            File.WriteAllBytes(Path.Combine(Workspace,name),image.EncodeToPNG());Object.Destroy(image);
        }
        private static IEnumerator ClickHud(Button button,MiniMapHudSimulatedMouse mouse,EventSystem events,Camera camera)
        {
            Canvas.ForceUpdateCanvases();var buttonCanvas=button.GetComponentInParent<Canvas>().rootCanvas;var eventCamera=buttonCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:camera;mouse.Position=RectTransformUtility.WorldToScreenPoint(eventCamera,button.GetComponent<RectTransform>().TransformPoint(button.GetComponent<RectTransform>().rect.center));
            var hits=new List<RaycastResult>();events.RaycastAll(new PointerEventData(events){position=mouse.Position},hits);
            Require(hits.Any(h=>h.gameObject.GetComponentInParent<Button>()==button),"Original button receives simulated pointer raycast: "+button.name+" at "+mouse.Position+" hits "+string.Join(",",hits.Select(h=>h.gameObject.name)));
            mouse.Down=true;mouse.Held=true;int frame=Time.frameCount;while(Time.frameCount<frame+2)yield return null;
            mouse.Down=false;mouse.Held=false;mouse.Up=true;frame=Time.frameCount;while(Time.frameCount<frame+2)yield return null;mouse.Up=false;yield return null;
        }
        private static IEnumerator VerifyHud(MainlineMiniMap module,MainlineNativeMiniMapRoot root,MainlineMiniMapRootStateFixture state,DescriptionLabels labels,GridData grid,Queries queries,IMiniMapCharacterManager characters)
        {
            const string native="Assets/Official/World/Shared/NativeMiniMapHudCurrent/";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(native+"Resources/World/Native/mini-map-hud.prefab");Require(prefab,"Complete original shared HUD");
            Require(!PrefabUtility.HasManagedReferencesWithMissingTypes(prefab),"HUD managed reference bindings");
            foreach(var transform in prefab.GetComponentsInChildren<Transform>(true))Require(transform.GetComponents<MonoBehaviour>().All(c=>c),"Every original HUD script imported");
            foreach(var component in prefab.GetComponentsInChildren<Component>(true)){var property=new SerializedObject(component).GetIterator();while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference)Require(property.objectReferenceValue||property.objectReferenceInstanceIDValue==0,"HUD original reference: "+property.propertyPath);}
            var language=new HudLanguage();var ui=new MultiLanguageUIService(language);
            var texts=new LocalizationBinaryTextService(language,null,new ILanguageTextDataProvider[]{new LaunchLanguageBinaryTextDataProvider("Assets/Launch/")},new LocalLanguageFiles(Application.streamingAssetsPath));texts.AddDataProvider(new GameLanguageBinaryTextDataProvider("Assets/Config/"));
            var fonts=new Map13LabelFontInstance(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic.asset"));
            var groups=AssetDatabase.LoadAssetAtPath<LanguageGroupConfig>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:LanguageGroupConfig",new[]{native.TrimEnd('/')}).Single()));
            Require(groups.GetLanguageGroupByType(LanguageType.zh_CN)==LanguageGroup.Zh,"Original Chinese language-group config");
            var hudState=new HudState{Source=state,Text=texts};int sounds=0;
            var camera=new GameObject("HudFixtureCamera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.transform.position=new Vector3(0,0,-100);camera.cullingMask=1<<5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.2f,.2f,.2f);
            var target=new RenderTexture(720,1280,24);target.Create();camera.targetTexture=target;
            var canvas=new GameObject("OriginalHudFixtureSlot",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));var c=canvas.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;
            queries.RegionAt=pos=>state.QueryRegion(pos);Require(state.Current==13&&root.Character.enabled&&root.Character.CharacterMe.activeSelf,"Original current-map character branch starts active");
            var hudBindings=new MainlineMiniMapHudBindings{State=hudState,LanguageUI=ui,Language=language,Text=texts,Fonts=fonts,LanguageGroups=groups,PlaySound=(clip,emitter)=>{Require(clip&&(clip.name=="Main_Cursor_Normal"||clip.name=="Main_Tag"),"Original decoded click event");sounds++;}};
            module.AttachHud(prefab,canvas.transform,hudBindings);
            for(int i=0;i<8;i++)yield return null;
            var small=canvas.GetComponentInChildren<MiniMapMaskedUI>(true);var area=small.GetComponent<AreaInfoUI>();var branch=small.GetComponentInChildren<PublicMapBranchLineBtn>(true);var content=HudField<GameObject>(branch,"mContent");
            Require(HudField<RawImage>(small,"mRawImage").texture==module.Texture,"Original HUD binds owned minimap texture");
            Require(grid.Receivers.Count==1&&hudState.Subscribers==1&&!content.activeSelf,"Local single-line HUD retains only the original region subscription");
            Require(branch.Context.GroupMapIdOrderList.SequenceEqual(new long[]{42})&&branch.Context.GetMapLineIndex()==1,"One local channel uses the actual public container identity");
            branch.Context.GroupMapIdOrderList.Add(999);Require(branch.Context.GroupMapIdOrderList.Count==1,"Callers cannot mutate the local channel count");
            int region=state.QueryRegion(grid.Position);Require(area.MyRegionInfoText.Text.Contains(labels.Text("region_13_"+region)),"Original region localization");
            labels.SetProgress(region,Math.Max(1,labels.Data.maxProgress[region]/2));string progress=(labels.Data.CalcWorldMapRegionProgress(13,region,labels.Progress[region])/100).ToString();Require(area.MyRegionProgress.Text.Contains(progress),"HUD uses original region exploration formula");
            string before=area.MyRegionInfoText.Text;var previous=grid.Position;grid.Move(new Pos2d(previous.X+1,previous.Y));Require(area.MyRegionInfoText.Text!=before,"HUD position event refreshes formatted coordinates");grid.Move(previous);
            hudState.Public();Require(!content.activeSelf&&branch.Context.MapRegionStatusDict[13][0]==Status.Public,"One-line hide retains original public-region state");hudState.AddLine();yield return null;Require(!content.activeSelf,"Underlying channel lists cannot reveal the single-player button");hudState.Busy();Require(HudField<Image>(branch,"mSwitchImage").color==Color.green,"The sole local channel remains free");
            SaveHudImage(camera,target,"native-minimap-single-channel-hud.png");
            // The remaining channel checks exercise the untouched original
            // button explicitly with its old multi-channel test adapter.
            branch.gameObject.SetActive(false);branch.Context=hudState;branch.gameObject.SetActive(true);yield return null;Require(content.activeSelf,"Original multi-channel compatibility fixture");Require(HudField<Image>(branch,"mSwitchImage").color==Color.yellow,"Original status refresh reads current state");
            var input=new GameObject("HudSimulatedInput",typeof(EventSystem),typeof(StandaloneInputModule));var events=input.GetComponent<EventSystem>();var mouse=input.AddComponent<MiniMapHudSimulatedMouse>();input.GetComponent<StandaloneInputModule>().inputOverride=mouse;yield return null;
            var click=ClickHud(small.MiniMapBtn,mouse,events,camera);while(click.MoveNext())yield return click.Current;Require(hudState.PopupCount==1&&sounds==1,"Simulated original minimap button dispatch and sound");
            hudState.Current=14;hudState.Deferred(hudState);Require(hudState.InitializedMap==14,"Original popup callback reads map identity when initialized");hudState.Current=13;
            var button=HudField<Button>(branch,"mButton");hudState.Fights[1]=new MiniMapHudFightData{PlayerMapId=42};click=ClickHud(button,mouse,events,camera);while(click.MoveNext())yield return click.Current;Require(hudState.Error==4131&&hudState.BranchPopups==0,"Original public-map combat rejection");
            hudState.Fights.Clear();click=ClickHud(button,mouse,events,camera);while(click.MoveNext())yield return click.Current;Require(hudState.BranchPopups==1&&sounds==3,"Simulated original channel button popup");
            camera.Render();var old=RenderTexture.active;RenderTexture.active=module.Texture;var mapImage=new Texture2D(module.Texture.width,module.Texture.height,TextureFormat.RGBA32,false);mapImage.ReadPixels(new Rect(0,0,mapImage.width,mapImage.height),0,0);mapImage.Apply();Require(mapImage.GetPixels32().Count(p=>p.r>50&&p.g>60&&p.b<180)>30,"Original character renderer draws regional geometry into owned HUD texture");Object.Destroy(mapImage);
            RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(Workspace,"native-minimap-hud.png"),image.EncodeToPNG());Object.Destroy(image);
            module.SetActive(false);Require(grid.Receivers.Count==0&&!small.gameObject.activeInHierarchy,"Public module disable hides HUD and releases grid receiver");module.SetActive(true);for(int i=0;i<3;i++)yield return null;Require(grid.Receivers.Count==1&&small.gameObject.activeInHierarchy,"Public module re-enable restores one HUD receiver");
            var fullscreen=VerifyPanel(module,root,state,labels,grid,characters,hudBindings,c,camera,target,mouse,events);while(fullscreen.MoveNext())yield return fullscreen.Current;
            module.Dispose();yield return null;Require(!small&&grid.Receivers.Count==0&&hudState.Subscribers==0,"Public module destruction owns HUD and releases all local subscriptions");
            Object.Destroy(input);Object.Destroy(canvas);
            queries.RegionAt=null;Object.Destroy(camera.gameObject);Object.Destroy(target);fonts.Dispose();texts.Dispose();ui.Dispose();
            Debug.Log("MAINLINE_MINIMAP_NATIVE_HUD_PASS");
        }
    }
}

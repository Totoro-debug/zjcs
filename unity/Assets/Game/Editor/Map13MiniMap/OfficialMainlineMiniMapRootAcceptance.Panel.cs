using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.Map13MiniMapWindows;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Offline.World.MainlineMiniMap;
using Object=UnityEngine.Object;
using Pos2d=Zjcs.Official.World.NativeTerrain.Common.Pos2d;
using PopupWindow=Zjcs.Official.World.Map13MiniMapWindows.PopupWindow;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        private sealed class PanelLease : IMiniMapDescriptionAssetLease<Sprite>
        { public PanelLease(Sprite sprite){Result=sprite;} public Sprite Result{get;private set;} public void Dispose(){Result=null;} }
        private sealed class PanelState : MiniMapFullScreenContext,IMap13PopupWindowHost
        {
            internal MiniMapPanelResources Catalog;internal MainlineMiniMap Owner;internal GameObject Hud;
            internal int Hides,Shows,Closes;
            public override int CurrentMapClassId=>13;
            public override MiniMapPanelMapInfo[] GetAllMapInfos()=>Catalog.Maps;
            public override MiniMapPanelMapInfo GetMapInfo(int map)=>Catalog.Maps.Single(r=>r.ClassId==map);
            // Only the current map is visited in this fixture. This does not
            // choose the pending production policy for foreign-map entities.
            public override bool IsWorldMapVisited(int map)=>map==13;
            public override long GetTick()=>DateTime.UtcNow.Ticks;
            public override double CalcElapsedSecond(long tick)=>(DateTime.UtcNow.Ticks-tick)/(double)TimeSpan.TicksPerSecond;
            public override void HideWorldUI(){Hides++;Hud.SetActive(false);}
            public override void ShowWorldUI(){Shows++;Hud.SetActive(true);}
            public override void InstantiateMiniMapAsync(string path,Transform parent,Action<MainlineMiniMapRoot> callback)=>throw new NotSupportedException("Foreign-map requests are outside this fixture");
            public override void ReleaseMiniMap(MainlineMiniMapRoot map)=>throw new NotSupportedException();
            public override IMiniMapDescriptionAssetLease<Sprite> LoadSprite(string path)=>new PanelLease(Catalog.Icons[path]);
            public override void CloseWindow(GameObject window){Closes++;Owner.CloseFullScreen();}
            public override void CloseLastWindow()=>CloseWindow(null);
            public GAction<GameObject> OnWindowsOpening{get;}=new GAction<GameObject>();
            public GAction<GameObject> OnWindowOpened{get;}=new GAction<GameObject>();
            public GAction<GameObject> OnWindowClosing{get;}=new GAction<GameObject>();
            public GAction<GameObject> OnWindowClosed{get;}=new GAction<GameObject>();
            public void Close(GameObject window,bool instant)=>CloseWindow(window);
        }
        private sealed class PanelDirection : MiniMapDirectionContext,IMiniMapDirectionGround
        {
            internal PanelState State;internal IMiniMapCharacterManager Manager;internal GridData Grid;
            public override MiniMapFullScreenContext FullScreen=>State;
            public override bool HasMapRoot=>true;
            public override bool HasMainPlayer=>true;
            public override IMiniMapCharacterManager Characters=>Manager;
            public override IMiniMapDirectionGround Ground=>this;
            public override event Action OnInitialized;
            public override event Action<MiniMapDirectionPlayerItemData,long,object> OnItemCountUpdate;
            public override Vector3 GetWorldPosition(IMiniMapCharacterController controller)=>Grid.GridToPosition(Grid.Position);
            public Vector3 GridToPos(Pos2d position)=>Grid.GridToPosition(position);
            public override bool TryGetItemGuiderData(out MiniMapDirectionData data){data=null;return false;}
            public override MiniMapDirectionItemInfo GetItemInfo(int item)=>throw new NotSupportedException();
            public override MiniMapRegionInfo GetRegionInfoByDropItemId(int item)=>throw new NotSupportedException();
            public override void DestroyChildren(Transform slot){foreach(Transform child in slot)Object.Destroy(child.gameObject);}
            public override void CreateItemIconWithoutText(int item,Transform slot)=>throw new NotSupportedException();
        }
        private sealed class PanelMarkQueries : IMiniMapMarkDataQueries
        {
            internal MiniMapPanelResources Catalog;
            public MiniMapMarkMapInfo GetMapInfo(int map)=>new MiniMapMarkMapInfo{MapGroupId=Catalog.Maps.Single(r=>r.ClassId==map).MapGroupId};
            public int GridToArea(int map,Pos2d position)=>throw new NotSupportedException();
            public string GetAreaTag(int map,int area)=>throw new NotSupportedException();
            public Zjcs.Official.World.Map13Entities.EntityMapUIDisplayInfo GetEntityMapUIDisplay(int entity,int group,int map,string area,string tag)=>throw new NotSupportedException();
            public MiniMapMarkExtendInfo GetEntityExtend(int entity)=>throw new NotSupportedException();
            public MiniMapMarkLoEntityInfo GetContainerEntityInfo(int map,int entity)=>throw new NotSupportedException();
            public string GetEntityTag(int map,int entity)=>throw new NotSupportedException();
            public IMiniMapMarkEntity GetCommonPrefab(int entity)=>throw new NotSupportedException();
            public Vector3 MapGridToPos(int map,Pos2d position)=>throw new NotSupportedException();
            public MiniMapMarkTransportTargetInfo GetMapTransportTargetInfo(int map,int entity)=>throw new NotSupportedException();
            public bool RegionIsActived(int map,int region)=>throw new NotSupportedException();
            public int QueryRegion(Pos2d position)=>throw new NotSupportedException();
            public int CurrentMeMapId=>13;public bool HasMainPlayer=>true;public bool HasMainMapContainer=>true;public int MainMapClassId=>13;
            public int GridToRegion(int map,Pos2d position)=>throw new NotSupportedException();
            public Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus GetMapRegionStatus(int map,int region)=>throw new NotSupportedException();
            public UniTask<bool> DoTransfer(IMiniMapMarkEntity entity)=>throw new NotSupportedException();
            public void DoEnterMap(int map,int entity)=>throw new NotSupportedException();
        }
        private sealed class PanelFilters : IMiniMapMarkFilterServices,IMiniMapFilterPersistentState
        {
            internal MiniMapPanelResources Catalog;internal DescriptionLabels Labels;internal MainlineMiniMapHudBindings UI;
            internal readonly List<Action<OnGlobalPointerDownResult>> Hooks=new List<Action<OnGlobalPointerDownResult>>();
            internal int Writes;
            public MiniMapMarkUIFilterSetting State{get;}=new MiniMapMarkUIFilterSetting();
            public UniTask WriteStateAsync(){Writes++;return UniTask.CompletedTask;}
            public MiniMapFilterMarkInfo[] GetAllMarkInfos()=>Catalog.Marks.Select(r=>r.GetInfo()).ToArray();
            public GameObjectPool<T> CreatePool<T>(Transform root,GameObject prefab) where T:Component=>new GameObjectPool<T>(root,(slot,_,active)=>
            {
                var inactive=new GameObject("OriginalPanelPoolBindings");inactive.SetActive(false);
                var item=Object.Instantiate(prefab,inactive.transform,false);Labels.Bind(item);
                foreach(var text in item.GetComponentsInChildren<MultiLanguageText>(true))
                {text.GetComponent<TMPro.TMP_Text>().font=UI.Fonts.GetMultiLanguageFontAsset().FontAsset;text.BindLanguageServices(UI.LanguageUI,UI.Language);text.LocalizationTextService=UI.Text;text.MultiLanguageFontService=UI.Fonts;}
                foreach(var sound in item.GetComponentsInChildren<Zjcs.Official.WorldUI.WorldButtonSound>(true))sound.Post=UI.PlaySound;
                foreach(var sound in item.GetComponentsInChildren<MiniMapPanelClickSound>(true))sound.Post=UI.PlaySound;
                item.transform.SetParent(slot,false);item.SetActive(active);Object.Destroy(inactive);return item;
            },prefab.name);
            public IMiniMapDescriptionAssetLease<Sprite> LoadSprite(string path)=>new PanelLease(Catalog.Icons[path]);
            public IEnumerable<RaycastResult> ForeachRaycastResults(Vector2 position)
            {var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);return hits;}
            public void HookOnInput(Action<OnGlobalPointerDownResult> receiver)=>Hooks.Add(receiver);
            public void UnhookOnInput(Action<OnGlobalPointerDownResult> receiver)=>Hooks.Remove(receiver);
        }
        // Deliberately no main-player portrait in this window fixture. Every
        // original portrait widget stays bound; live capture has separate tests.
        private sealed class PanelPlayer : IProfessionPlayerInfoBindings,ICharacterPortraitManager
        {
            public IProfessionInfoPlayer MainPlayer=>null;public bool ManagerAvailable=>true;
            public SafeAction OnPlayerUpdate{get;}=new SafeAction();
            public IProfessionImageResourceLoader Resources=>throw new NotSupportedException();
            public PlayerInfoData BuildData(IProfessionInfoPlayer player)=>throw new NotSupportedException();
            public PlayerInfoData BuildLiteData(object info,IProfessionInfoTarget target)=>throw new NotSupportedException();
            public object BuildBriefInfo(IProfessionInfoPlayer player,bool online)=>throw new NotSupportedException();
            public object CreateNameArg(string name,bool translate)=>throw new NotSupportedException();
            public object CompactNumber(long value)=>throw new NotSupportedException();
            public string SubRankKey(int rank)=>throw new NotSupportedException();
            public string ProfessionNameKey(int profession)=>throw new NotSupportedException();
            public string GetFullBodyGuid(int profession,Sex sex)=>throw new NotSupportedException();
            public string FirstProfessionIcon(int profession)=>throw new NotSupportedException();
            public string ThirdProfessionIcon(int profession)=>throw new NotSupportedException();
            public ProfessionInfoText NameWithPlanes(object name,long server)=>throw new NotSupportedException();
            public bool HasHonorTitle(object appearance)=>throw new NotSupportedException();
            public ProfessionInfoNpcDefinition GetNpc(int id)=>throw new NotSupportedException();
            public GameObject InstantiateKeepScale(GameObject prefab,Transform parent)=>throw new NotSupportedException();
            public void OverrideNpcInitialAnimation(GameObject model,int npc)=>throw new NotSupportedException();
            public TeamTargetCatalog? TeamCatalog(Component component)=>throw new NotSupportedException();
            public ProfessionInfoRobotInvite RobotInvite(Component component)=>throw new NotSupportedException();
            public ProfessionInfoRobotInvite DefaultRobotInvite()=>throw new NotSupportedException();
            public bool CanJointTogether(int target,int main)=>throw new NotSupportedException();
            public int DuelSkillGroup(long player,long target)=>throw new NotSupportedException();
            public void Popup(Component owner,Action<IProfessionPlayerInfoDetailPanel> initialized)=>throw new NotSupportedException();
            public CharacterPortraitCache GetCharacterPortrait(ICharacterPortraitRequestor requestor)=>throw new NotSupportedException();
            public void ClearAllCharacterPortraitPersistenceData()=>throw new NotSupportedException();
        }
        private static IEnumerator VerifyPanel(MainlineMiniMap module,MainlineNativeMiniMapRoot root,MainlineMiniMapRootStateFixture source,DescriptionLabels labels,GridData grid,IMiniMapCharacterManager characters,
            MainlineMiniMapHudBindings ui,Canvas canvas,Camera camera,RenderTexture target,MiniMapHudSimulatedMouse mouse,EventSystem events)
        {
            const string native="Assets/Official/World/Shared/NativeMiniMapPanelCurrent/";
            Require(Screen.width==720&&Screen.height==1280,"Portrait Game View matches original safe-area dimensions");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(native+"Resources/World/Native/mini-map-panel.prefab");
            var catalog=Resources.Load<MiniMapPanelResources>("World/Native/mini-map-panel-data");
            Require(prefab&&catalog&&catalog.Maps.Length==373&&catalog.Marks.Length==201,"Complete original fullscreen hierarchy and selected tables");
            var scaler=canvas.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=(CanvasScaler.ScaleMode)catalog.ScaleMode;scaler.referenceResolution=catalog.ReferenceResolution;scaler.screenMatchMode=(CanvasScaler.ScreenMatchMode)catalog.ScreenMatchMode;scaler.matchWidthOrHeight=catalog.MatchWidthOrHeight;scaler.referencePixelsPerUnit=catalog.ReferencePixelsPerUnit;
            Require(catalog.Maps.Single(r=>r.ClassId==13).MiniMapSwitchTabGroup==1&&catalog.Maps.Single(r=>r.ClassId==16).MiniMapSwitchTabGroup==16,"Original groups differ across supported maps");
            source.Queries=new PanelMarkQueries{Catalog=catalog};
            foreach(var transform in prefab.GetComponentsInChildren<Transform>(true))Require(transform.GetComponents<MonoBehaviour>().All(c=>c),"Every fullscreen original script imported");
            Require(!PrefabUtility.HasManagedReferencesWithMissingTypes(prefab),"Fullscreen managed reference bindings");
            foreach(var component in prefab.GetComponentsInChildren<Component>(true)){var property=new SerializedObject(component).GetIterator();while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference)Require(property.objectReferenceValue||property.objectReferenceInstanceIDValue==0,"Fullscreen original reference: "+property.propertyPath);}
            var hud=canvas.GetComponentInChildren<MiniMapMaskedUI>(true).gameObject;
            var state=new PanelState{Catalog=catalog,Owner=module,Hud=hud};var player=new PanelPlayer();
            var filters=new PanelFilters{Catalog=catalog,Labels=labels,UI=ui};
            var bindings=new MainlineMiniMapPanelBindings{State=state,Direction=new PanelDirection{State=state,Grid=grid,Manager=characters},UI=ui,Filters=filters,FilterState=filters,Player=player,Portraits=player,Windows=state};
            // The original shared renderer is an URP overlay camera. World UI
            // uses an overlay canvas above the world-camera stack.
            var renderCamera=new GameObject("OriginalMinimapStackFixture",typeof(Camera)).GetComponent<Camera>();renderCamera.tag="MainCamera";renderCamera.orthographic=true;renderCamera.cullingMask=0;renderCamera.clearFlags=CameraClearFlags.SolidColor;renderCamera.backgroundColor=Color.black;
            var baseData=renderCamera.GetUniversalAdditionalCameraData();
            camera.enabled=false;camera.tag="Untagged";canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            baseData.cameraStack.Add(root.NonMaskableManager.NonMaskableCamera);
            module.OpenFullScreen(prefab,canvas.transform,bindings);for(int i=0;i<12;i++)yield return null;
            var panel=canvas.GetComponentInChildren<MiniMapFullScreenPanel>();
            Require(panel&&panel.CurrentMapClassId==13&&panel.ScrollRect.totalCount==1&&!hud.activeSelf&&state.Hides==1,"Public module opens original window and enumerates visited maps in the original group");
            Require(panel.GetComponent<Zjcs.Official.World.Map13MiniMapWindows.PopupWindow>().State==Zjcs.Official.World.Map13MiniMapWindows.PopupWindow.EState.Show,"Public module uses original popup Show lifecycle");
            Require(grid.Receivers.Count==1,"Original fullscreen player guider replaces hidden HUD subscription");
            var captureFrame=CapturePanelFrame(target,"native-minimap-panel-open.png");while(captureFrame.MoveNext())yield return captureFrame.Current;
            var originalMapTexture=panel.CameraController.GetRenderTexture();var mapPrevious=RenderTexture.active;RenderTexture.active=originalMapTexture;
            var mapImage=new Texture2D(originalMapTexture.width,originalMapTexture.height,TextureFormat.RGBA32,false);mapImage.ReadPixels(new Rect(0,0,mapImage.width,mapImage.height),0,0);mapImage.Apply();RenderTexture.active=mapPrevious;
            Require(mapImage.GetPixels32().Count(p=>p.r>50&&p.g>60&&p.b<180)>100,"Original fullscreen camera renders actual explored geometry");Object.Destroy(mapImage);
            float beforeZoom=panel.CameraController.MiniMapCamera.orthographicSize;
            var sliderRect=panel.Slider.GetComponent<RectTransform>();var point=sliderRect.rect.center;
            if(panel.Slider.direction==Slider.Direction.LeftToRight||panel.Slider.direction==Slider.Direction.RightToLeft)point.x=Mathf.Lerp(sliderRect.rect.xMin,sliderRect.rect.xMax,.8f);
            else point.y=Mathf.Lerp(sliderRect.rect.yMin,sliderRect.rect.yMax,.8f);
            mouse.Position=RectTransformUtility.WorldToScreenPoint(null,sliderRect.TransformPoint(point));
            var sliderHits=new List<RaycastResult>();events.RaycastAll(new PointerEventData(events){position=mouse.Position},sliderHits);
            Require(sliderHits.Any(h=>h.gameObject.GetComponentInParent<Slider>()==panel.Slider),"Original zoom slider receives simulated mouse raycast");
            mouse.Down=mouse.Held=true;int frame=Time.frameCount;while(Time.frameCount<frame+2)yield return null;
            mouse.Down=mouse.Held=false;mouse.Up=true;frame=Time.frameCount;while(Time.frameCount<frame+2)yield return null;mouse.Up=false;
            Require(!Mathf.Approximately(beforeZoom,panel.CameraController.MiniMapCamera.orthographicSize)&&Mathf.Abs(panel.Slider.value-panel.CameraController.GetZoomRate())<.001f,"Simulated slider click updates original camera and keeps zoom synchronized");
            var filter=panel.GetComponentInChildren<MiniMapMarkUIFilter>(true);
            var click=ClickHud(HudField<Button>(filter,"mEntryBtn"),mouse,events,camera);while(click.MoveNext())yield return click.Current;
            var filterObject=HudField<GameObject>(filter,"mFilter");Require(filterObject.activeSelf,"Simulated pointer opens original mark filter");
            for(int i=0;i<8;i++)yield return null;
            captureFrame=CapturePanelFrame(target,"native-minimap-filter-open.png");while(captureFrame.MoveNext())yield return captureFrame.Current;
            int writes=filters.Writes;click=ClickHud(HudField<Button>(filter,"mButton"),mouse,events,camera);while(click.MoveNext())yield return click.Current;
            Require(filters.Writes==writes+1,"Original filter selection reaches local persistence port");
            captureFrame=CapturePanelFrame(target,"native-minimap-fullscreen.png");while(captureFrame.MoveNext())yield return captureFrame.Current;
            var close=panel.GetComponentsInChildren<MiniMapPanelCloseWindowBtn>(true).First(c=>c.gameObject.activeInHierarchy).myButton;
            click=ClickHud(close,mouse,events,camera);while(click.MoveNext())yield return click.Current;yield return null;
            Require(!panel&&hud.activeSelf&&state.Shows==1&&state.Closes==1&&filters.Hooks.Count==0&&grid.Receivers.Count==1,"Simulated original close restores HUD and releases fullscreen subscriptions");
            module.OpenFullScreen(prefab,canvas.transform,bindings);for(int i=0;i<4;i++)yield return null;
            module.CloseFullScreen();yield return null;Require(filters.Hooks.Count==0&&state.Shows==2,"Same public module reopens and owns the fullscreen lifecycle");
            baseData.cameraStack.Clear();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;camera.enabled=true;camera.tag="MainCamera";Object.Destroy(renderCamera.gameObject);
            Debug.Log("MAINLINE_MINIMAP_NATIVE_PANEL_PASS");
        }
        public sealed class MiniMapPanelFrameCapture : MonoBehaviour
        {
            internal bool Done;internal Texture2D Image;
            internal void Begin()=>StartCoroutine(Capture());
            private IEnumerator Capture(){yield return new WaitForEndOfFrame();try{Image=ScreenCapture.CaptureScreenshotAsTexture();}finally{Done=true;}}
        }
        private static IEnumerator CapturePanelFrame(RenderTexture target,string name)
        {
            float shaderDeadline=Time.realtimeSinceStartup+60;
            while(ShaderUtil.anythingCompiling){Require(Time.realtimeSinceStartup<shaderDeadline,"Original UI shader compilation timeout");yield return null;}
            var runner=new GameObject("EndOfFrameMinimapCapture").AddComponent<MiniMapPanelFrameCapture>();runner.Begin();float deadline=Time.realtimeSinceStartup+15;
            while(!runner.Done){Require(Time.realtimeSinceStartup<deadline,"Actual Game View frame capture timeout");yield return null;}
            var image=runner.Image;Require(image&&image.width==target.width&&image.height==target.height,"Actual portrait Game View screenshot: "+(image?image.width+"x"+image.height:"missing"));
            int fallbackPixels=image.GetPixels32().Count(pixel=>pixel.r<5&&pixel.g>245&&pixel.b>245);
            int pixelCount=image.width*image.height;
            File.WriteAllBytes(Path.Combine(Workspace,name),image.EncodeToPNG());Object.Destroy(image);
            Require(fallbackPixels<pixelCount/100,"Original portrait capture contains a pending-shader cyan overlay");
            Object.Destroy(runner.gameObject);
        }
    }
}

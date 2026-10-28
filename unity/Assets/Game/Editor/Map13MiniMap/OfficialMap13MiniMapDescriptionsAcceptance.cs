using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeLabelResources;
using Zjcs.Offline.World.NativeMap13Labels;
using Object=UnityEngine.Object;
using Status=Zjcs.Official.World.NativeMap13Labels.MapRegionStatus;
using CommonStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
using DescriptionUI=Zjcs.Official.World.Map13MiniMap.MapRegionDescItemUI;
using FullScreenAction=Zjcs.Official.StoryPlayables.GAction;

namespace Zjcs.Editor.Map13MiniMap
{
    public sealed class Map13DescriptionFixture:MiniMapDescriptionContext
    {
        internal DescriptionLabels Labels;internal MapRootEntityLiteSingleInfo Lite;
        internal Dictionary<int,MiniMapRegionInfo> Regions;internal Dictionary<int,int> AreaRegions;
        internal MiniMapSettings Settings;internal bool Full,Agent=true;internal int Loads,Disposed;
        internal IMiniMapGridMover Mover=new MoverFixture();
        private FullScreenAction shown=new FullScreenAction(),hidden=new FullScreenAction();
        public override IMap13LabelServices LabelServices=>Labels;
        public override MiniMapSettings.MiniMapSetting MiniMapSetting=>Settings.GetMinimapSetting(2);
        public override FullScreenAction ShowFullScreenAction=>shown;
        public override FullScreenAction HideFullScreenAction=>hidden;
        public override MiniMapKeepSizeHandler SizingHandler=>GetComponent<MiniMapKeepSizeHandler>();
        public override bool IsFullScreen=>Full;
        public override bool HasGridVisibilityAgent=>Agent;
        public override int MapClassId=>13;public override int CurrentMapClassId=>13;public override bool HasMainPlayer=>true;
        public override IMapViewRangeHandler ViewRangeHandler=>null;
        public override event Action<IMapViewRangeHandler> OnVisibleRangeUpdate;
        public override event Action<int,int,CommonStatus,CommonStatus> OnMapStatusUpdate;
        public override event Action<Pos2d,bool,bool> OnPosVisibilityUpdate;
        public override bool TryGetMapLiteData(int map,out MapRootEntityLiteSingleInfo info){info=Lite;return map==13;}
        public override IMiniMapGridMover GetSpawnGridMover(int map)=>Mover;
        public override IReadOnlyDictionary<int,MiniMapRegionInfo> GetMapRegionDict(int map)=>Regions;
        public override MiniMapRegionInfo GetRegion(int map,int region)=>Regions[region];
        public override int AreaToRegion(int map,int area)=>AreaRegions.TryGetValue(area,out var region)?region:-1;
        public override int QueryRegion(Pos2d pos)=>AreaToRegion(13,Lite.PosDataArray[pos.Y][pos.X].Area);
        public override List<int> GetWorldMapProgresses(int map)=>Labels.Progress.ToList();
        public override MiniMapRegionDisplayInfo GetRegionDisplay(int map,int region)=>region==38?new MiniMapRegionDisplayInfo{CenterOffsetX=0,CenterOffsetY=0,HighlightScaleX=1.559999942779541f,HighlightScaleY=1.559999942779541f,NameFormat="<color=#FFE50E>{0}</color>",LinePrefabPath="Map/Common/MiniMap/RegionDisplay/MinimapLineForVillage",HighlightPrefabPath="Map/Common/MiniMap/RegionDisplay/MinimapHighlightForVillage"}:null;
        public override object QueryCommonGridGround(int map)=>Lite;
        public override object QuerySceneContainer(Component component)=>gameObject;
        public override MiniMapRegionDescItem InstantiateDescription(MiniMapRegionDescItem prefab,Transform parent)
        {
            var staging=new GameObject("IsolatedOriginalDescriptionInjection");staging.SetActive(false);var instance=Object.Instantiate(prefab,staging.transform,false);Labels.Bind(instance.gameObject);foreach(var ui in instance.GetComponentsInChildren<DescriptionUI>(true))ui.Services=Labels;instance.transform.SetParent(parent,false);instance.gameObject.SetActive(true);Object.Destroy(staging);return instance;
        }
        public override GameObject InstantiateDisplay(GameObject prefab,Transform parent)=>Object.Instantiate(prefab,parent,false);
        public override IMiniMapDescriptionAssetLease<GameObject> LoadPrefab(string path){Loads++;var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OfficialMap13MiniMapDescriptionsAcceptance.Native+"Prefabs/"+path+".prefab");if(!prefab)throw new InvalidDataException(path);return new Lease(prefab,()=>Disposed++);}
        public override List<CommonStatus> GetMapStatusListInTeam(int map)=>throw new NotSupportedException();
        public override bool IsRangeInViewRange(Pos2d min,Pos2d max)=>throw new NotSupportedException();
        public override IMiniMapRegionAssetLease LoadAssetByGuid(string guid)=>throw new NotSupportedException();
        public override GameObject InstantiateRegion(GameObject prefab,Transform parent,bool world)=>throw new NotSupportedException();
        internal void Visibility(Pos2d pos,bool value)=>OnPosVisibilityUpdate?.Invoke(pos,!value,value);
        internal void FullScreen(bool value){Full=value;if(value)shown.Invoke();else hidden.Invoke();}
        private sealed class Lease:IMiniMapDescriptionAssetLease<GameObject>{public GameObject Result{get;private set;}private Action dispose;public Lease(GameObject value,Action callback){Result=value;dispose=callback;}public void Dispose(){Result=null;dispose?.Invoke();dispose=null;}}
        private sealed class MoverFixture:IMiniMapGridMover{public bool Walkable(GroundType type)=>(int)type>0;}
    }
    internal sealed class DescriptionLabels:IMap13LabelServices,IMap13LabelPlayer,ILanguageTypeService,IDisposable
    {
        private MultiLanguageUIService ui;private Map13LabelFontInstance font;private LocalizationBinaryTextService texts;
        public int[] Progress=new int[106];public Status[] Statuses=Enumerable.Repeat(Status.Private,106).ToArray();
        public DescriptionLabels()
        {
            Data=JsonUtility.FromJson<Map13LabelData>(Resources.Load<TextAsset>("Map13/Native/region-label-data").text);Colors=Resources.Load<CombatRatingConfig>("Map13/Native/combat-rating-config");font=new Map13LabelFontInstance(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic.asset"));
            texts=new LocalizationBinaryTextService(this,null,new ILanguageTextDataProvider[]{new LaunchLanguageBinaryTextDataProvider("Assets/Launch/")},new LocalLanguageFiles(Application.streamingAssetsPath));texts.AddDataProvider(new GameLanguageBinaryTextDataProvider("Assets/Config/"));LanguageManager.Initialize(texts);ui=new MultiLanguageUIService(this);
        }
        public void Bind(GameObject instance)=>Map13LabelBinding.Bind(instance,this,ui,this,texts,font);
        public string Text(string key)=>texts.GetText(key,LanguageType.zh_CN);
        public int MapClassId=>13;public object Map=>this;public bool IsTeamMember=>false;public IMap13LabelPlayer Player=>this;
        public Map13LabelData Data{get;}public CombatRatingConfig Colors{get;}public DateTime Now=>DateTime.Now;
        public event Action Initialized;public int QueryRegion(Vector3 pos)=>Data.QueryRegion(pos);public MapRegionInfo GetRegion(int map,int region)=>Data.regions[region];public double CalcRemainSecond(DateTime end)=>(end-Now).TotalSeconds;
        public int Level=>100;public SubRank SubRank=>(SubRank)8;public long CombatRating=>100000000;
        public event Action<int> OnLevelUpdate;public event Action<Rank> OnPlanesRankUnlock;public event Action<long,long> OnCombatRatingChanged;public event Action<int,List<int>> OnWorldMapProgressUpdate;public event Action<int,int,Status,Status> OnMapStatusUpdate;public event Action<long> OnDebugTimeOffset;
        public bool RegionIsActived(int map,int region)=>region>=0&&region<106&&Statuses[region]==Status.Public;
        public Status GetMapRegionStatus(int map,int region)=>region>=0&&region<106?Statuses[region]:Status.None;
        public int GetWorldMapRegionProgress(int map,int region)=>region>=0&&region<106?Progress[region]:0;
        public void SetProgress(int region,int value){Progress[region]=value;OnWorldMapProgressUpdate?.Invoke(13,Progress.ToList());}
        public void SetStatus(int region,Status value){var previous=Statuses[region];Statuses[region]=value;OnMapStatusUpdate?.Invoke(13,region,previous,value);}
        public void SetPublic(bool value){for(int i=0;i<106;i++){var previous=Statuses[i];Statuses[i]=value?Status.Public:Status.Private;OnMapStatusUpdate?.Invoke(13,i,previous,Statuses[i]);}}
        public LanguageType CurrentLanguage{get=>LanguageType.zh_CN;set{if(value!=LanguageType.zh_CN)throw new NotSupportedException();}}
        public bool IsShowLanguageKey{get;set;}public bool IsShowMissingKeyText{get;set;}public event Action<LanguageType> OnLanguageChanged;
        public void Dispose(){if(OnMapStatusUpdate!=null||OnWorldMapProgressUpdate!=null||OnCombatRatingChanged!=null)throw new InvalidDataException("Native UI retained player subscriptions");ui.Dispose();texts.Dispose();font.Dispose();}
    }
    public static class OfficialMap13MiniMapDescriptionsAcceptance
    {
        internal const string Native="Assets/Official/World/Map/13/NativeMiniMapDescriptions/";
        private const string Key="Zjcs.Map13.MiniMapDescriptions.Running";
        private static IEnumerator routine;private static int errors;
        [Serializable] private sealed class Fixture{public int width,height;public float[] origin;public Cell[] cells;public Area[] areas;public RegionRow[] regions;}
        [Serializable] private sealed class Cell{public int area,ground;}
        [Serializable] private sealed class Area{public int x,y,count;}
        [Serializable] private sealed class RegionRow{public int RegionTag,Region;public List<int> Areas;}
        [Serializable] private sealed class Source{public SpriteProof[] sprites;}
        [Serializable] private sealed class SpriteProof{public string path;public XY[] vertices,uv;public int[] triangles;public XY pivot;public float ppu;public RectProof rect;}
        [Serializable] private sealed class XY{public float x,y;}
        [Serializable] private sealed class RectProof{public float x,y,width,height;}
        [Serializable] private sealed class Proof{public string editor;public int regions,prefabs,sprites,centers,visibility_cells,unlocked_by_visibility,line_points,asset_loads,disposed,rendered_pixels,border_paths,border_segments;public bool original_native_prefabs,junction_gate,progress_unlock,public_status,progress_formula,full_screen_highlight,reentry,subscriptions_released,original_line_pool;}
        private static string Workspace=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        private static void Require(bool condition,string detail){if(!condition)throw new InvalidDataException(detail);}
        private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(value);
        private static void Observe(string condition,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Runtime errors: "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private static IEnumerator Verify()
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{Native.TrimEnd('/')}))
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));Require(asset&&!PrefabUtility.HasManagedReferencesWithMissingTypes(asset),"Original managed references");foreach(var t in asset.GetComponentsInChildren<Transform>(true))Require(t.GetComponents<MonoBehaviour>().All(c=>c),"Every original script: "+t.name);
                foreach(var component in asset.GetComponentsInChildren<Component>(true)){var property=new SerializedObject(component).GetIterator();while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference)Require(property.objectReferenceValue||property.objectReferenceInstanceIDValue==0,"Native original reference: "+asset.name+":"+property.propertyPath);}
            }
            var source=JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"),"assets/reference/official-map/map13-native-minimap-descriptions-source-manifest.json")));
            foreach(var proof in source.sprites){var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(proof.path.Substring(6));Require(sprite&&sprite.vertices.Length==proof.vertices.Length&&sprite.triangles.Length==proof.triangles.Length,"Original Sprite geometry");Require(sprite.rect==new Rect(proof.rect.x,proof.rect.y,proof.rect.width,proof.rect.height)&&sprite.pixelsPerUnit==proof.ppu&&Vector2.Distance(sprite.pivot,new Vector2(proof.pivot.x,proof.pivot.y))<.0001f,"Original logical rect/pivot/PPU");for(int i=0;i<proof.vertices.Length;i++){Require(Vector2.Distance(sprite.vertices[i],new Vector2(proof.vertices[i].x,proof.vertices[i].y))<.0001f,"Original Sprite vertex");Require(Vector2.Distance(sprite.uv[i],new Vector2(proof.uv[i].x,proof.uv[i].y))<.0001f,"Original Atlas UV");}Require(sprite.triangles.Select(i=>(int)i).SequenceEqual(proof.triangles),"Original indices");}
            var input=JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Workspace,"minimap-description-fixture.json")));var host=new GameObject("OriginalMiniMapDescriptionFixture");host.SetActive(false);var map=host.AddComponent<Map13DescriptionFixture>();var sizing=host.AddComponent<MiniMapKeepSizeHandler>();var labels=new DescriptionLabels();map.Labels=labels;map.Settings=AssetDatabase.LoadAssetAtPath<MiniMapSettings>("Assets/Official/World/Map/13/NativeMiniMapSettings/ScriptableConfig/Modules/MapCommon/MiniMapSettings.asset");
            var lite=new MapRootEntityLiteSingleInfo{MapClassId=13,MapGroundSize=new Pos2d(input.width,input.height),MapGroundOriginPos=new Vector3(input.origin[0],input.origin[1],input.origin[2]),PosDataArray=new MapRootEntityLiteGridInfo[input.height][],AreaInfos=input.areas.Select(a=>new MapRootEntityLiteAreaInfo{TotalPos=new Pos2d(a.x,a.y),TotalCount=a.count}).ToArray()};for(int y=0;y<input.height;y++){lite.PosDataArray[y]=new MapRootEntityLiteGridInfo[input.width];for(int x=0;x<input.width;x++){var c=input.cells[y*input.width+x];lite.PosDataArray[y][x]=new MapRootEntityLiteGridInfo{Area=c.area,GroundType=(GroundType)c.ground};}}map.Lite=lite;map.Regions=input.regions.ToDictionary(r=>r.Region,r=>new MiniMapRegionInfo{LabelInfo=labels.Data.regions[r.Region],Areas=r.Areas,RegionTag=(MapRegionType)r.RegionTag});map.AreaRegions=map.Regions.Values.SelectMany(r=>r.Areas.Select(a=>new{a,r.Region})).ToDictionary(v=>v.a,v=>v.Region);
            var manager=host.AddComponent<MiniMapRegionDescManager>();manager.MiniMap=map;manager.DescSlot=new GameObject("OriginalDescriptionSlot").transform;manager.DescSlot.SetParent(host.transform,false);manager.RegionDescItemPrefabDict=new MonoDictionary<MapRegionType,MiniMapRegionDescItem>();manager.RegionDescItemPrefabDict.Add(MapRegionType.Main,AssetDatabase.LoadAssetAtPath<GameObject>(Native+"Prefabs/UI/Map/MiniMap/MiniMapMainRegionDescItem.prefab").GetComponent<MiniMapRegionDescItem>());manager.RegionDescItemPrefabDict.Add(MapRegionType.Branch,AssetDatabase.LoadAssetAtPath<GameObject>(Native+"Prefabs/UI/Map/MiniMap/MiniMapBranchRegionDescItem.prefab").GetComponent<MiniMapRegionDescItem>());host.SetActive(true);int inits=0;manager.OnRegionDescManagerInited+=()=>inits++;
            var borderHost=new GameObject("OriginalMiniMapRegionBoardLineManager");borderHost.SetActive(false);borderHost.transform.SetParent(host.transform,false);var border=borderHost.AddComponent<MiniMapRegionBoardLineManager>();var borderFields=new SerializedObject(border);borderFields.FindProperty("mMiniMap").objectReferenceValue=map;borderFields.FindProperty("mDescManager").objectReferenceValue=manager;borderFields.ApplyModifiedPropertiesWithoutUndo();borderHost.SetActive(true);
            var visibility=new MapGroundGridBoolData(input.height,input.width);manager.Init(visibility);yield return null;Require(manager.RegionDescItemDict.Count==106&&manager.RegionDescItemDict.Values.All(v=>!v.IsRegionUnlocked),"All source regions initially locked");int centers=0;
            foreach(var region in map.Regions.Values){int sx=0,sy=0,count=0;foreach(int area in region.Areas){sx+=lite.AreaInfos[area].TotalPos.X;sy+=lite.AreaInfos[area].TotalPos.Y;count+=lite.AreaInfos[area].TotalCount;}Require(manager.RegionDescItemDict[region.Region].transform.localPosition==lite.GridToPos(new Pos2d(sx/count,sy/count)),"Exact original integer center: "+region.Region);centers++;}
            labels.SetProgress(38,1);Require(manager.RegionDescItemDict[38].IsRegionUnlocked&&manager.RegionDescItemDict.Values.Count(v=>v.IsRegionUnlocked)==1,"Original progress unlocks only matching region");
            var villageUI=manager.RegionDescItemDict[38].GetComponentInChildren<DescriptionUI>(true);var highlight=Field<Transform>(villageUI,"mHighlightTransform");var line=Field<Transform>(villageUI,"mLineTransform");Require(highlight&&line&&!highlight.gameObject.activeSelf,"Original village assets loaded before full screen");Require(Vector3.Distance(highlight.localScale,new Vector3(1.559999942779541f,1.559999942779541f,1))<.00001f,"Original village highlight scale");map.FullScreen(true);Require(highlight.gameObject.activeSelf,"Original full screen shows village mask");map.FullScreen(false);Require(!highlight.gameObject.activeSelf,"Original hide removes village mask");map.FullScreen(true);Require(highlight.gameObject.activeSelf,"Original full-screen re-entry");
            var positions=map.GetMapRegionPosList(13);var contour=PrivateRegionUtility.GetGridsBorderPoints(positions[38]);Require(line.GetComponent<LineRendererDashedLine>()!=null,"Original village dashed-line implementation");
            var locked=manager.RegionDescItemDict.First(v=>v.Key>0&&!v.Value.IsRegionUnlocked&&positions[v.Key].Any(p=>manager.IsUsableJunctionPos(p.X,p.Y,v.Key)));var junction=positions[locked.Key].First(p=>manager.IsUsableJunctionPos(p.X,p.Y,locked.Key));map.Visibility(junction,false);Require(!locked.Value.IsRegionUnlocked,"Invisible junction remains locked");map.Visibility(junction,true);Require(locked.Value.IsRegionUnlocked,"Original visible junction unlocks matching region");
            visibility.BitArray.SetAll(true);labels.SetProgress(38,0);manager.Init(visibility);yield return null;var expected=new HashSet<int>();for(int y=0;y<input.height;y++)for(int x=0;x<input.width;x++){var cell=lite.PosDataArray[y][x];if(cell.Area<0||(int)cell.GroundType<=0)continue;int region=map.AreaToRegion(13,cell.Area);bool junctionFound=false;foreach(var delta in new[]{new Pos2d(0,1),new Pos2d(0,-1),new Pos2d(-1,0),new Pos2d(1,0)})if(lite.TryGetPosData(x+delta.X,y+delta.Y,out var adjacent)&&(int)adjacent.GroundType>0&&map.AreaToRegion(13,adjacent.Area)>=0&&map.AreaToRegion(13,adjacent.Area)!=region)junctionFound=true;if(region<=0||junctionFound)expected.Add(region);}
            Require(manager.RegionDescItemDict.Where(v=>v.Value.IsRegionUnlocked).Select(v=>v.Key).ToHashSet().SetEquals(expected),"All source cells obey original visible walkable/junction gating");int visibleCount=expected.Count;
            for(int i=0;i<106;i++)labels.SetProgress(i,Math.Max(1,labels.Data.maxProgress[i]/2));Require(manager.RegionDescItemDict.Values.All(v=>v.IsRegionUnlocked),"Progress event unlocks all matching source regions");
            foreach(var pair in manager.RegionDescItemDict){var desc=pair.Value.GetComponentInChildren<DescriptionUI>(true);var progress=Field<List<MultiLanguageText>>(desc,"mProgressTexts");string expectedText=(labels.Data.CalcWorldMapRegionProgress(13,pair.Key,labels.Progress[pair.Key])/100).ToString();Require(progress.All(t=>t.Text==expectedText||t.Text.Contains(expectedText)),"Original progress formula text: "+pair.Key);var names=Field<List<MultiLanguageText>>(desc,"mRegionNameTexts");Require(names.All(t=>t.Text.Contains(labels.Text("region_13_"+pair.Key))),"Actual original Chinese region name");}
            labels.SetPublic(true);Require(manager.RegionDescItemDict.Values.All(v=>v.RegionStatus==Status.Public),"Original status subscriptions");foreach(var item in manager.RegionDescItemDict.Values){var desc=item.GetComponentInChildren<DescriptionUI>(true);Require(!Field<GameObject>(desc,"mExtendSlot").activeSelf,"Original Public state hides extended info");Require(Field<List<MultiLanguageText>>(desc,"mProgressTexts").All(t=>t.Text=="0"||t.Text.Contains("0")),"Original activated progress overwrite is 1 before integer division");}labels.SetPublic(false);
            labels.SetPublic(true);labels.SetStatus(38,Status.Private);
            var refresh=typeof(MiniMapRegionBoardLineManager).GetMethod("DoRefresh",BindingFlags.Instance|BindingFlags.NonPublic);refresh.Invoke(border,null);yield return null;var borderLines=Field<List<LineRendererDashedLine>>(border,"mLineRenderers").ToArray();Require(borderLines.Length>0,"Original unlocked Private regions draw dashed borders");int borderSegments=0;var origin=lite.MapGroundOriginPos;var expectedEdges=new HashSet<string>();Func<Vector3,Vector3,string> edgeKey=(a,b)=>{string one=a.x.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+","+a.y.ToString("F1",System.Globalization.CultureInfo.InvariantCulture);string two=b.x.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+","+b.y.ToString("F1",System.Globalization.CultureInfo.InvariantCulture);return string.CompareOrdinal(one,two)<0?one+"/"+two:two+"/"+one;};
            foreach(var list in positions.Values)foreach(var pair in PrivateRegionUtility.GetGridsBorderLineDict(list))foreach(var segment in pair.Value.Values)expectedEdges.Add(edgeKey(new Vector3(segment.Item1.x,segment.Item1.y,0)+origin,new Vector3(segment.Item2.x,segment.Item2.y,0)+origin));
            foreach(var dashed in borderLines){var points=Field<Vector3[]>(dashed,"mPoints");Require(points.Length>1&&dashed.GetComponent<MeshFilter>().sharedMesh.vertexCount>0,"Actual original dashed mesh generated");for(int i=1;i<points.Length;i++){Require(expectedEdges.Contains(edgeKey(points[i-1],points[i])),"Original doubled-grid path maps exactly to a source boundary");borderSegments++;}}
            labels.SetPublic(true);refresh.Invoke(border,null);Require(Field<List<LineRendererDashedLine>>(border,"mLineRenderers").Count==0&&borderLines.All(l=>!l.gameObject.activeSelf),"Original pool releases all Public borders");labels.SetStatus(38,Status.Private);refresh.Invoke(border,null);Require(Field<List<LineRendererDashedLine>>(border,"mLineRenderers").Any(l=>borderLines.Contains(l)),"Original line pool reuses object identities");
            var grid=new GameObject("OriginalMap13RegionalTilemaps",typeof(Grid));foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Official/World/Map/13/NativeMiniMapRegions"}))Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)),grid.transform,false);
            var camera=new GameObject("OriginalRegionDescriptionDiagnosticCamera",typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.aspect=.75f;camera.orthographicSize=45;camera.transform.position=manager.RegionDescItemDict[38].transform.position+new Vector3(0,0,-250);camera.nearClipPlane=.3f;camera.farClipPlane=500;camera.cullingMask=(1<<16)|(1<<20);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.06f,.06f,1);var target=new RenderTexture(768,1024,24);camera.targetTexture=target;map.FullScreen(true);for(int i=0;i<10;i++)yield return null;
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(768,1024,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,768,1024),0,0);image.Apply(false,false);RenderTexture.active=previous;int pixels=image.GetPixels32().Count(p=>p.r>130&&p.g>90&&p.b<150);Require(pixels>100,"Actual native region labels and village render");File.WriteAllBytes(Path.Combine(Workspace,"minimap-descriptions-village.png"),image.EncodeToPNG());Object.Destroy(image);
            int loads=map.Loads;Object.Destroy(host);yield return null;Require(map.Disposed==loads,"All original display asset leases released");labels.Dispose();camera.targetTexture=null;Object.Destroy(target);Object.Destroy(camera.gameObject);Object.Destroy(grid);yield return null;
            File.WriteAllText(Path.Combine(Workspace,"minimap-descriptions-rules.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,regions=106,prefabs=4,sprites=5,centers=centers,visibility_cells=input.width*input.height,unlocked_by_visibility=visibleCount,line_points=contour.Count+1,asset_loads=loads,disposed=loads,rendered_pixels=pixels,border_paths=borderLines.Length,border_segments=borderSegments,original_line_pool=true,original_native_prefabs=true,junction_gate=true,progress_unlock=true,public_status=true,progress_formula=true,full_screen_highlight=true,reentry=inits==2,subscriptions_released=true},true));Debug.Log("MAP13_MINIMAP_DESCRIPTIONS_PASS");
        }
    }
}

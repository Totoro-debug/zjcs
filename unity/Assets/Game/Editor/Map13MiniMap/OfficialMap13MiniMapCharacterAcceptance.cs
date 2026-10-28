using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
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
    public sealed class Map13CharacterFixture:MiniMapCharacterContext
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
        public override bool TryGetMapLiteData(int map,out MapRootEntityLiteSingleInfo info){throw new NotSupportedException();}
        public override IMiniMapGridMover GetSpawnGridMover(int map)=>throw new NotSupportedException();public override IReadOnlyDictionary<int,MiniMapRegionInfo> GetMapRegionDict(int map)=>throw new NotSupportedException();public override MiniMapRegionInfo GetRegion(int map,int region)=>throw new NotSupportedException();public override int AreaToRegion(int map,int area)=>throw new NotSupportedException();public override int QueryRegion(Pos2d pos)=>throw new NotSupportedException();public override List<int> GetWorldMapProgresses(int map)=>throw new NotSupportedException();public override MiniMapRegionDisplayInfo GetRegionDisplay(int map,int region)=>throw new NotSupportedException();public override object QueryCommonGridGround(int map)=>throw new NotSupportedException();public override object QuerySceneContainer(Component component)=>throw new NotSupportedException();
        public override MiniMapRegionDescItem InstantiateDescription(MiniMapRegionDescItem prefab,Transform parent)=>throw new NotSupportedException();public override GameObject InstantiateDisplay(GameObject prefab,Transform parent)=>throw new NotSupportedException();public override IMiniMapDescriptionAssetLease<GameObject> LoadPrefab(string path)=>throw new NotSupportedException();public override List<MapRegionStatus> GetMapStatusListInTeam(int map)=>throw new NotSupportedException();public override bool IsRangeInViewRange(Pos2d min,Pos2d max)=>throw new NotSupportedException();public override IMiniMapRegionAssetLease LoadAssetByGuid(string guid)=>throw new NotSupportedException();public override GameObject InstantiateRegion(GameObject prefab,Transform parent,bool world)=>throw new NotSupportedException();
    }
    public static class OfficialMap13MiniMapCharacterAcceptance
    {
        private const string Key="Zjcs.Map13.MiniMapCharacter.Running",Native="Assets/Official/World/Map/13/NativeMiniMapCharacter/";
        private static IEnumerator routine;private static int errors;
        [Serializable] private sealed class Source{public SpriteProof[] sprites;}
        [Serializable] private sealed class SpriteProof{public string path;public XY[] vertices,uv;public int[] triangles;public float ppu;public XY pivot;}
        [Serializable] private sealed class XY{public float x,y;}
        [Serializable] private sealed class Proof{public string editor;public int original_objects,sprites,rendered_pixels,position_cases,animation_curves;public bool native_camera,native_animation,original_fixed_texture,original_fullscreen_gate,main_camera_gate,controller_replacement,destroyed_move_agent,reentry,subscriptions,original_texture_lifetime;}
        private sealed class Controller:IMiniMapCharacterController
        {
            public MyGridTransform MyGridTransform{get;}
            public Controller(Vector3 position){var go=new GameObject("OriginalClientGridController",typeof(MyEntity));MyGridTransform=go.AddComponent<MyGridTransform>();go.transform.localPosition=position;}
        }
        private sealed class Characters:IMiniMapCharacterManager
        {
            public IMiniMapCharacterController MyMeUiCharacter{get;private set;}
            public event Action<IMiniMapCharacterController,IMiniMapCharacterController> OnMeMapControllerUpdated;
            public int Subscribers=>OnMeMapControllerUpdated?.GetInvocationList().Length??0;
            public void Switch(IMiniMapCharacterController value){var previous=MyMeUiCharacter;MyMeUiCharacter=value;OnMeMapControllerUpdated?.Invoke(previous,value);}
        }
        private static string Workspace=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");private static string Repository=>Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        private static void Require(bool condition,string detail){if(!condition)throw new InvalidDataException(detail);}
        private static void Observe(string condition,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Runtime errors: "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private static Color32[] Pixels(RenderTexture target,string name=null)
        {
            var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply(false,false);RenderTexture.active=old;var pixels=image.GetPixels32();if(name!=null)File.WriteAllBytes(Path.Combine(Workspace,name),image.EncodeToPNG());Object.Destroy(image);return pixels;
        }
        private static void Clear(RenderTexture target){var old=RenderTexture.active;RenderTexture.active=target;GL.Clear(true,true,Color.magenta);RenderTexture.active=old;}
        private static IEnumerator Verify()
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
            var source=JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(Repository,"assets/reference/official-map/map13-native-minimap-character-source-manifest.json")));Require(source.sprites.Length==2,"Both original player marker Sprites");
            foreach(var p in source.sprites){var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(p.path.Substring(6));Require(sprite&&sprite.vertices.Length==p.vertices.Length&&sprite.triangles.Select(i=>(int)i).SequenceEqual(p.triangles)&&sprite.pixelsPerUnit==p.ppu,"Original Sprite indices/PPU");for(int i=0;i<p.vertices.Length;i++){Require(Vector2.Distance(sprite.vertices[i],new Vector2(p.vertices[i].x,p.vertices[i].y))<.0001f,"Original marker Sprite vertices");Require(Vector2.Distance(sprite.uv[i],new Vector2(p.uv[i].x,p.uv[i].y))<.0001f,"Original marker Atlas UVs");}Require(Vector2.Distance(sprite.pivot,new Vector2(p.pivot.x,p.pivot.y))<.0001f,"Original marker pivot");}
            var host=new GameObject("OriginalMinimapPlayerMarkerFixture");host.SetActive(false);var map=host.AddComponent<Map13CharacterFixture>();map.Settings=AssetDatabase.LoadAssetAtPath<MiniMapSettings>("Assets/Official/World/Map/13/NativeMiniMapSettings/ScriptableConfig/Modules/MapCommon/MiniMapSettings.asset");map.MoveSource=new GameObject("ReadOnlyMoveAgentPresenceInput");map.World=new Vector3(12.25f,-35.75f,99);
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Native+"Prefabs/Map/Map_13/MiniMap/MinimapCharacter.prefab"),host.transform,false);var manager=go.GetComponent<MiniMapCharacterManager>();var labels=new DescriptionLabels();manager.Services=labels;manager.MiniMap=map;var characters=new Characters();var first=new Controller(new Vector3(12.25f,-35.75f,-7));var second=new Controller(new Vector3(-21.5f,53.125f,2));characters.Switch(first);manager.MapMyCharacterManager=characters;Require(go.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"All native scripts bound");
            var camera=manager.UiCamera;Require(!camera.enabled&&camera.cullingMask==65536&&camera.nearClipPlane==-2&&camera.farClipPlane==3&&camera.transform.localPosition==new Vector3(0,0,-1),"Original native minimap UI camera fields");host.SetActive(true);yield return null;yield return null;Require(manager.MapActived&&characters.Subscribers==1,"Original manager initialization");Require(camera.orthographicSize==map.MiniMapSetting.MiniMapUICameraOrthographicSize&&go.transform.localPosition==new Vector3(map.World.x,map.World.y,0),"Original setting and player local XY/Z flatten");int positionCases=1;
            var target=manager.GetOrAddRenderTexture();Require(target.width==256&&target.height==256&&target.depth==0&&target.name=="MinimapRenderTextureName"&&camera.aspect==1&&ReferenceEquals(target,manager.GetOrAddRenderTexture()),"Exact original fixed render target and cache");
            var animation=go.GetComponentInChildren<Animation>();Require(animation&&animation.clip&&animation.clip.legacy&&animation.clip.name=="MinimapCharacter_Loop","Complete original looping animation");var clip=animation.clip;int curves=AnimationUtility.GetCurveBindings(clip).Length;Require(curves>0,"Actual original animation curves");animation.Play(clip.name);var state=animation[clip.name];state.normalizedTime=.1f;animation.Sample();var sampleA=animation.GetComponentsInChildren<Transform>().Select(t=>t.localScale).ToArray();state.normalizedTime=.6f;animation.Sample();var sampleB=animation.GetComponentsInChildren<Transform>().Select(t=>t.localScale).ToArray();Require(!sampleA.SequenceEqual(sampleB),"Original marker animation changes actual transforms");
            var main=new GameObject("OriginalMainCameraTrigger",typeof(Camera)).GetComponent<Camera>();main.tag="MainCamera";main.orthographic=true;main.cullingMask=0;main.transform.position=new Vector3(0,0,-250);main.backgroundColor=Color.black;main.clearFlags=CameraClearFlags.SolidColor;for(int i=0;i<6;i++)yield return null;main.Render();int pixels=Pixels(target,"minimap-character.png").Count(p=>p.a>0&&p.r+p.g+p.b>20);Require(pixels>20,"Actual original player marker URP callback renders");
            manager.OnShowFullScreenMiniMap(null);Clear(target);main.Render();Require(Pixels(target).All(p=>p.r==255&&p.g==0&&p.b==255),"Original full-screen gate leaves UI texture unchanged");manager.OnHideFullScreenMiniMap(null);main.Render();Require(Pixels(target).Any(p=>p.g>0||p.b!=255),"Original small-map gate resumes rendering");Clear(target);main.tag="Untagged";main.Render();Require(Pixels(target).All(p=>p.r==255&&p.g==0&&p.b==255),"Only MainCamera triggers original UI render");main.tag="MainCamera";
            for(int i=0;i<12;i++){map.World=new Vector3(-7+i*.375f,5-i*.625f,70);yield return null;Require(go.transform.localPosition==new Vector3(map.World.x,map.World.y,0),"Original world-position read while movement component exists");positionCases++;}
            var cameraTarget=new GameObject("OriginalRecenterTarget").transform;cameraTarget.position=new Vector3(0,0,-250);Require(manager.TrySetCameraPos(cameraTarget)&&cameraTarget.position==new Vector3(map.World.x,map.World.y,-250),"Original recenter preserves camera Z");positionCases++;
            manager.enabled=false;var previous=go.transform.localPosition;map.World=new Vector3(70,80,90);yield return null;Require(go.transform.localPosition==previous&&!manager.TrySetCameraPos(cameraTarget),"Disabled original marker does not move or recenter");manager.enabled=true;yield return null;Require(go.transform.localPosition==new Vector3(70,80,0)&&manager.GetOrAddRenderTexture()==target,"Original re-entry reuses target");positionCases++;
            characters.Switch(second);map.World=new Vector3(-21.5f,53.125f,8);yield return null;Require(go.transform.localPosition==new Vector3(-21.5f,53.125f,0),"Original replacement controller");positionCases++;
            Object.Destroy(map.MoveSource);yield return null;previous=go.transform.localPosition;map.World=new Vector3(900,900,9);yield return null;Require(go.transform.localPosition==previous,"Destroyed original movement component stops position polling");
            map.Root=false;manager.enabled=false;manager.enabled=true;yield return null;Require(!manager.enabled&&!go.activeSelf&&!manager.MapActived,"Original missing-map branch disables both manager and marker");
            Object.Destroy(host);Object.Destroy(main.gameObject);Object.Destroy(cameraTarget.gameObject);Object.Destroy(first.MyGridTransform.gameObject);Object.Destroy(second.MyGridTransform.gameObject);yield return null;Require(characters.Subscribers==0,"Original controller subscription released");Require(target,"Original manager leaves fixed target lifetime unchanged");labels.Dispose();Object.Destroy(target);yield return null;
            File.WriteAllText(Path.Combine(Workspace,"minimap-character-rules.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,original_objects=19,sprites=2,rendered_pixels=pixels,position_cases=positionCases,animation_curves=curves,native_camera=true,native_animation=true,original_fixed_texture=true,original_fullscreen_gate=true,main_camera_gate=true,controller_replacement=true,destroyed_move_agent=true,reentry=true,subscriptions=true,original_texture_lifetime=true},true));Debug.Log("MAP13_MINIMAP_CHARACTER_PASS");
        }
    }
}

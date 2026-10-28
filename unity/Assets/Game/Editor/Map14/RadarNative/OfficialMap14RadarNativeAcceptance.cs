using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityRadar;
using Zjcs.Official.EntityRadarCost;
using Zjcs.Official.EntityWorldInput;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.World.NativeMap14Labels;
using Zjcs.Offline.World.NativeMap14EntityAssets;
using Zjcs.Offline.World.NativeMap14RadarTips;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14RadarNativeAcceptance
    {
        private const string Key = "Map14.RadarNative";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            if (Environment.GetEnvironmentVariable("ZJCS_ISOLATED_ENTITY_VIEWS") != "1")
                Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            else Debug.Log("Explicit entity container assembly isolation; full ProjectTools.Validate is not executed.");
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            QualitySettings.renderPipeline = null; PlayerSettings.colorSpace = ColorSpace.Linear;
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 240);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            started = false; errors = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            Application.logMessageReceived -= Observe;
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string text, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Original entity container check timed out."); EditorApplication.Exit(1); return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original entity container check").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Steps());
            while (stack.Count > 0)
            {
                object value;
                try
                {
                    var top = stack.Peek(); if (!top.MoveNext()) { stack.Pop(); continue; }
                    value = top.Current;
                    if (value is IEnumerator nested && !(value is CustomYieldInstruction)) { stack.Push(nested); continue; }
                }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return value;
            }
            if (errors == 0)
            {
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map14 original StageCostTip prefab Play Mode passed: original hierarchy/scripts/font/effect/material/sprite/clip bindings, actual nonempty 1/10/100 cost text and original icon alignment, original fade curves and animation-event completion, resource disposal and localization teardown; fast-game/cost/combined/radar/award/input/component/index/container/loading/four-statue regression passed. Formal item-icon services/map/HUD/input/save and Windows Player remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string text)
        { if (!value) throw new InvalidDataException("Original entity container: " + text); }

        private static IEnumerator Steps()
        {
            var catalog=Resources.Load<NativeMap14RadarTipCatalog>("Map14/Native/radar-tips-catalog");
            Require(catalog && catalog.Manifest=="871_156598" && catalog.StageCostTip,"original radar catalog is absent");
            var spriteCatalog=Resources.Load<NativeMap14EntitySpriteCatalog>("Map14/Native/entity-sprite-catalog");
            using(var sprites=new NativeMap14EntitySpriteLibrary(spriteCatalog))
            using(var fonts=new Map14LabelFontInstance(catalog.StageCostTip.GetComponentInChildren<TextMeshPro>(true).font))
            {
                var language=new Language();
                using(var ui=new MultiLanguageUIService(language))
                using(var localization=new LocalizationBinaryTextService(language,null,Array.Empty<ILanguageTextDataProvider>(),null))
                {
                    var host=new GameObject("Original radar inactive service scope");host.SetActive(false);
                    var instance=UnityEngine.Object.Instantiate(catalog.StageCostTip,host.transform,false);
                    var tip=instance.GetComponent<MapControllerEntityRadarTipStageCost>();
                    Require(instance.GetComponentsInChildren<Transform>(true).Length==4 && instance.GetComponentsInChildren<MonoBehaviour>(true).All(v=>v),"original radar hierarchy has missing scripts");
                    Require(tip.Animation.GetClip(tip.ShowClip).legacy && tip.Animation.GetClip(tip.HideClip).legacy,"original legacy fade clips are absent");
                    var resources=new Presentation(sprites);
                    NativeMap14RadarTipBinding.Bind(instance,resources,resources,resources,language,localization,fonts,ui,sprites);
                    var poolHost=new GameObject("Original radar native tip pool");
                    var manager=poolHost.AddComponent<MapControllerEntityRadarTipManager>();
                    var previousFactory=StoryPlayableInstantiation.Resolve;
                    try
                    {
                        StoryPlayableInstantiation.Resolve=scope=>new PreparedFactory(instance);
                        Require(ReferenceEquals(manager.GetTip(host.transform,catalog.StageCostTip.GetComponent<MapControllerEntityRadarTipStageCost>()),tip),"original pool did not borrow the prepared native tip");
                    }
                    finally{StoryPlayableInstantiation.Resolve=previousFactory;}
                    var entityHost=new GameObject("Original radar body fixture");var entity=entityHost.AddComponent<MyEntity>();entity.Entity=new Body();
                    var cameraHost=new GameObject("Original radar fixture camera");var camera=cameraHost.AddComponent<Camera>();
                    camera.orthographic=true;camera.orthographicSize=.7f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.07f,.07f,1);
                    camera.nearClipPlane=.1f;camera.farClipPlane=200;
                    host.SetActive(true);yield return null;
                    var text=instance.GetComponentInChildren<TextMeshPro>(true);var icon=instance.transform.Find("Icon").GetComponent<SpriteRenderer>();
                    Require(text.font!=catalog.StageCostTip.GetComponentInChildren<TextMeshPro>(true).font && ReferenceEquals(text.font,fonts.Font),"dynamic font writes are not isolated");
                    foreach(long value in new long[]{1,10,100})
                    {
                        resources.Cost[325]=value;tip.Init(entity);tip.Show();yield return new WaitForSeconds(.35f);
                        text.ForceMeshUpdate(false,false);
                        Require(text.text==value.ToString() && text.textInfo.characterCount==value.ToString().Length,"original cost text/glyphs differ");
                        float expected=value==1?-.08f:-.13f;
                        Require(Mathf.Abs(icon.transform.localPosition.x-expected)<.000001f && icon.sprite==resources.Sprite,"original character-count icon position differs");
                        Require(text.color.a>.99f && icon.color.a>.99f,"original show fade did not reach full opacity");
                        camera.transform.position=new Vector3(instance.transform.position.x,instance.transform.position.y,-100);
                        Capture(camera,"radar-cost-native-"+value+".png",true);
                    }
                    int hidden=0;tip.Hide(false,value=>{hidden++;manager.RecycleTip(value);});Require(hidden==0,"animation hide completed synchronously");
                    float deadline=Time.realtimeSinceStartup+3;
                    while(hidden==0 && Time.realtimeSinceStartup<deadline)yield return null;
                    Require(hidden==1 && !instance.activeSelf && text.color.a<.01f && icon.color.a<.01f,"original hide animation/event/pool recycle failed");
                    Capture(camera,"radar-cost-native-hidden.png",false);
                    var reused=manager.GetTip(host.transform,catalog.StageCostTip.GetComponent<MapControllerEntityRadarTipStageCost>());
                    Require(ReferenceEquals(reused,tip) && instance.activeSelf,"original native tip pool did not reactivate/reuse the same component");
                    reused.Hide(true,value=>manager.RecycleTip(value));
                    Require(!instance.activeSelf,"immediate native tip recycling failed");
                    UnityEngine.Object.Destroy(poolHost);UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(entityHost);UnityEngine.Object.Destroy(cameraHost);
                    yield return null;
                    Require(resources.Disposed==3,"original sprite operations were not released on destruction");
                    var registered=(ICollection<MultiLanguageUI>)typeof(MultiLanguageUIService).GetField("mMultiLanguageUIs",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui);
                    Require(registered.Count==0,"original text retained a UI registration after destruction");
                }
                Require(language.Count==0,"language scope retained subscriptions");
            }
            yield return (IEnumerator)typeof(OfficialMap14RadarFastKillAcceptance).GetMethod("Steps",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        }
        private sealed class PreparedFactory:IStoryPlayableInstanceFactory
        {
            private GameObject prepared;
            public PreparedFactory(GameObject prepared){this.prepared=prepared;}
            public GameObject Instantiate(GameObject prefab,Transform parent,bool worldPositionStays)
            {
                Require(prepared && !prepared.activeInHierarchy,"native tip must be injected before activation");
                var result=prepared;prepared=null;result.name=prefab.name;result.transform.SetParent(parent,worldPositionStays);return result;
            }
        }
        private static void Capture(Camera camera,string name,bool visible)
        {
            var target=new RenderTexture(450,800,24);target.Create();var previous=RenderTexture.active;
            var image=new Texture2D(450,800,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,450,800),0,0);image.Apply();
                int count=image.GetPixels32().Count(p=>p.r>80||p.g>80||p.b>80);
                Require(visible?count>30:count<30,"actual radar frame visibility differs: "+count);
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),image.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.Destroy(image);target.Release();UnityEngine.Object.Destroy(target);}
        }
        private sealed class Presentation:IRadarStageCostQueries,IRadarItemIconQueries,IWorldInputResourceLoader
        {
            public readonly Dictionary<int,long> Cost=new Dictionary<int,long>{{325,1}};
            public Sprite Sprite;public int Disposed;
            public Presentation(NativeMap14EntitySpriteLibrary sprites){Sprite=sprites.ForRenderer("cab-23bef326149fbeb0f636881d59f9eecc:7631631214633643292");}
            public Dictionary<int,long> GetStageCost(ECViewEntityData entity)=>Cost;
            public string GetItemIcon(int id){Require(id==325,"fixture cost item differs");return "original-prefab-icon-fixture";}
            public IWorldInputSpriteOperation LoadSprite(string name){Require(name=="original-prefab-icon-fixture","fixture icon lookup differs");return new Operation(this);}
            private sealed class Operation:IWorldInputSpriteOperation
            {private readonly Presentation owner;public Operation(Presentation owner){this.owner=owner;}public Sprite Result=>owner.Sprite;public void Dispose(){owner.Disposed++;}}
        }
        private sealed class Language:ILanguageTypeService
        {
            private Action<LanguageType> changed;public int Count=>changed?.GetInvocationList().Length??0;
            public LanguageType CurrentLanguage{get;set;}=LanguageType.zh_CN;
            public bool IsShowLanguageKey{get;set;}public bool IsShowMissingKeyText{get;set;}
            public event Action<LanguageType> OnLanguageChanged{add{changed+=value;}remove{changed-=value;}}
        }
        private sealed class Body:ECViewEntityData,IRadarBodyInfo
        {
            public override int ClassId=>6570;public override EcEntityIdentifier Id=>new EcEntityIdentifier(14,1);
            public override T GetComponent<T>()=>this is T value?value:default;
            public Vector2 RealCenter=>Vector2.zero;public IEnumerable<RadarBodyPosInfo> BodyRangeInfos=>new[]{new RadarBodyPosInfo{Pos=new Pos2d(0,0)}};
        }
        private sealed class Runner:MonoBehaviour{}
    }
}

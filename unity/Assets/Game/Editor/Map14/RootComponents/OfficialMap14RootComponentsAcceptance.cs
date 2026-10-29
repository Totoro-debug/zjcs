using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Zjcs.Offline.World.NativeMap14ClientEntities;
using Zjcs.Offline.World.NativeMap14SharedEntities;
namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14RootComponentsAcceptance
    {
        private const string Key = "Map14.RootComponents";
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
                Debug.Log("Map14 original thin root components Play Mode passed: all eight original root components/info bound through the stable client composition, original MyEntity bind/start/end/remove/reset and rebinding, repeated binding identity, live info, root ground alias, and no fabricated components on an ordinary entity; buffered native event/stable view/factory/display/statue/radar/input/container regressions passed. Original root prefab/fog/input, formal map/player/UI/save/Windows Player remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string text)
        { if (!value) throw new InvalidDataException("Original entity container: " + text); }

        private static IEnumerator Steps()
        {
            var catalog=Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            using(var shared=new NativeMap14SharedAssemblies(catalog))
            using(var configuration=new NativeMap14SharedConfiguration(shared))
            using(var source=new NativeMap14SharedData(shared,configuration))
            {
                yield return source.LoadAll(false);
                using(var root=new NativeMap14SharedRoot(shared,configuration,source,14080))
                using(var data=new Zjcs.Offline.World.NativeMap14DisplayData.NativeMap14DisplayData(shared))
                {
                    var bindings=new Zjcs.Offline.World.NativeMap14ClientViewBindings.NativeMap14ClientViewBindings(shared,data);
                    using(var state=new NativeMap14ClientEntityState(shared,root,source,configuration,1,()=>false,bindComponents:bindings.Bind))
                    {
                        root.Start();var view=state.Wrap(root.Root);Require(ReferenceEquals(view,state.RootEntity),"stable original root identity differs");
                        var host=new GameObject("Typed original root component input fixture");host.SetActive(false);
                        try
                        {
                            var owner=host.AddComponent<Zjcs.Official.EntityLifecycle.MyEntity>();
                            var names=new[]{"MyMap","MyWorld","MyMapCharacterSpawner","MyGridGround","MyAreaAgent","MyEntityVisibilityAgent","MyAreaExploreSpawner","MyMapStoryEntitySpawner"};
                            var components=new List<Zjcs.Official.EntityLifecycle.MyComponent>();
                            foreach(var name in names)components.Add((Zjcs.Official.EntityLifecycle.MyComponent)host.AddComponent(typeof(Zjcs.Official.EntityRootComponents.MyMap).Assembly.GetType("Zjcs.Official.EntityRootComponents."+name,true)));
                            host.SetActive(true);owner.Bind(null,view,false);owner.MyStart();
                            Require(owner.myStarted&&owner.Components.Count==8,"original thin component startup differs");
                            var originalBindings=new List<object>();
                            foreach(var component in components)
                            {
                                var value=(Zjcs.Official.EntityRootComponents.IRootClientComponentData)component.GetEcComponent();Require(value!=null,"original client component did not bind: "+component.GetType());
                                var original=Component(shared,root.Root,value.Original.GetType().Name);Require(ReferenceEquals(original,value.Original),"original native component identity differs");
                                Require(ReferenceEquals(component.GetType().GetProperty("Info").GetValue(component),NativeMap14SharedPrefabs.Get(original,"_Info")),"original info identity differs");originalBindings.Add(value);
                            }
                            var ground=view.GetComponent<Zjcs.Official.EntityRootComponents.GridGroundComponentData>();
                            Require(ReferenceEquals(ground,view.GetComponent<Zjcs.Official.EntityLifecycle.EntityGridGroundData>()),"root ground consumption alias differs");
                            bindings.Bind(view);for(int i=0;i<components.Count;i++)Require(ReferenceEquals(originalBindings[i],components[i].GetEcComponent()),"root projection identity was replaced");
                            var map=view.GetComponent<Zjcs.Official.EntityRootComponents.MapComponentData>();var info=NativeMap14SharedPrefabs.Get(map.Original,"_Info");
                            NativeMap14SharedPrefabs.Set(map.Original,"_Info",null);Require(map.ComponentInfo==null,"root info was captured instead of read live");NativeMap14SharedPrefabs.Set(map.Original,"_Info",info);
                            owner.End(null);owner.Remove(null);owner.OnGameObjectPoolReset();Require(owner.Entity==null&&!owner.myStarted,"original root lifecycle reset differs");
                            owner.Bind(null,view,false);owner.MyStart();for(int i=0;i<components.Count;i++)Require(ReferenceEquals(originalBindings[i],components[i].GetEcComponent()),"original root lifecycle rebinding changed projection identity");
                            owner.End(null);owner.Remove(null);owner.OnGameObjectPoolReset();
                            var ordinary=state.Wrap(root.Instantiate(source.GetCommonPrefab(6570)));
                            Require(ordinary.GetComponent<Zjcs.Official.EntityRootComponents.MapComponentData>()==null&&ordinary.GetComponent<Zjcs.Official.EntityRootComponents.GridGroundComponentData>()==null,"root components were fabricated on an ordinary entity");
                        }
                        finally{UnityEngine.Object.Destroy(host);}
                    }
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14BufferedEventsAcceptance).GetMethod("Steps",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        }
        private static object Component(NativeMap14SharedAssemblies shared,object entity,string type)=>entity.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(entity,new object[]{shared.CommonType(type)});
        private sealed class Runner:MonoBehaviour{}
    }
}

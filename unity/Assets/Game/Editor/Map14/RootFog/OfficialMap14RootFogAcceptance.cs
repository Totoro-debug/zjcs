using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Zjcs.Official.MapRootFog;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap14RootFog;
using Zjcs.Offline.World.NativeMap14SharedEntities;
namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14RootFogAcceptance
    {
        private const string Key = "Map14.RootFog";
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
                Debug.Log("Map14 original root fog Play Mode passed: exact native root/ground/visibility, original fog subtree/assets and three Windows shaders, initialized cell and boundary masks, rendered dynamic meshes, original reveal/distance timing, immediate add/remove, camera movement and map-scene predicate, destruction; earlier root/container/display regressions passed. Root agent event lifecycle, formal map/player/UI/save and Windows Player remain pending.");
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
                using(var root=new NativeMap14SharedRoot(shared,configuration,source,14090))
                {
                    root.Start();var inputs=new NativeMap14RootFogInputs(shared,root.Root);
                    Require(ReferenceEquals(inputs.Ground.Original,root.Ground),"ground was reconstructed");
                    Require(inputs.Ground.Info.Size==new Pos2d(280,280),"official root size differs");
                    var locked=new Pos2d(140,140);bool found=false;
                    for(int x=80;x<200&&!found;x++)for(int y=80;y<200;y++)if(!inputs.Visibility.GetPosVisibility(x,y)){locked=new Pos2d(x,y);found=true;break;}
                    Require(found,"fixture needs an actual initially concealed cell");
                    var world=inputs.Ground.GridToPos(locked);
                    Require(inputs.Ground.PosToGrid(world)==locked&&inputs.Ground.GridToPos(locked.X,locked.Y)==world,"unchanged original coordinate methods differ");
                    var cameraGo=new GameObject("Original fog render fixture camera");var camera=cameraGo.AddComponent<Camera>();
                    camera.orthographic=true;camera.orthographicSize=8;camera.aspect=540f/960;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.25f,.3f);camera.transform.position=new Vector3(world.x,world.y,-10);
                    var inactive=new GameObject("Original fog inactive injection owner");inactive.SetActive(false);
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/World/Map/14/RootFog/Prefabs/RootFog.prefab");Require(prefab,"original fog subtree missing");
                    var instance=UnityEngine.Object.Instantiate(prefab,inactive.transform,false);var fog=instance.GetComponent<CloudController2>();Require(fog,"original controller script missing");
                    var platform=new Platform(camera);fog.Platform=platform;
                    try
                    {
                        Require(fog.DiffusionSpeed==5&&fog.CloudTemplate.sprite&&fog.CloudTemplate.sprite.rect.size==new Vector2(400,400),"original prefab values/sprite differ");
                        foreach(var material in new[]{fog.CloudMat,fog.CloudMaskMat,fog.DisappearMat})
                        {Require(material&&material.shader&&material.shader.isSupported&&!ShaderUtil.ShaderHasError(material.shader),"original Windows fog shader unavailable: "+material);}
                        inactive.SetActive(true);fog.Initialize(inputs.Visibility,inputs.Ground);
                        Require(fog.Contains(locked)&&fog.Contains(new Pos2d(-1,-1))&&!fog.Contains(new Pos2d(-3,-3)),"original initialization/boundary mask differs");
                        yield return null;yield return null;
                        var mesh=fog.Cloud.GetComponent<MeshFilter>().sharedMesh;Require(mesh.vertexCount>0,"original visible fog geometry missing");
                        Capture(camera,"root-fog-concealed.png");
                        var data=(System.Collections.IDictionary)Field(fog,"mFadingClouds");
                        Time.timeScale=0;fog.Reveal(locked,locked);Require(fog.Contains(locked)&&data.Count==1,"original reveal transition differs");
                        var fade=(CloudController2.DisappearData)data[locked];Require(Mathf.Abs(fade.EndTime-fade.StartTime-.2f)<.0001f&&fade.Center==(Vector2)world,"original reveal timing/center differs");
                        yield return null;yield return null;Require(((Mesh)Field(fog,"mFadingMesh")).vertexCount==4,"original fading quad missing");
                        Capture(camera,"root-fog-revealing.png");
                        Time.timeScale=1;yield return new WaitForSeconds(.3f);yield return null;Require(!fog.Contains(locked)&&data.Count==0,"original reveal did not expire");
                        var cells=new List<Pos2d>();for(int x=locked.X-4;x<=locked.X+4;x++)for(int y=locked.Y-6;y<=locked.Y+6;y++){var p=new Pos2d(x,y);cells.Add(p);fog.Remove(p);}
                        yield return null;yield return null;Capture(camera,"root-fog-cleared.png");
                        foreach(var p in cells)fog.Add(p);yield return null;Require(fog.Contains(locked),"original add did not restore fog");
                        var oldCamera=Field(fog,"mLastCamPos");platform.Active=false;camera.transform.position+=Vector3.right;yield return null;Require(Equals(oldCamera,Field(fog,"mLastCamPos")),"non-map scene updated fog");
                        platform.Active=true;yield return null;Require(!Equals(oldCamera,Field(fog,"mLastCamPos")),"map camera move did not update fog");
                        var render=(RenderTexture)Field(fog,"mRenderTexture");Require(render&&render.format==RenderTextureFormat.R8,"original half-size R8 mask target differs");
                    }
                    finally{Time.timeScale=1;UnityEngine.Object.Destroy(inactive);UnityEngine.Object.Destroy(cameraGo);}
                    yield return null;
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14RootComponentsAcceptance).GetMethod("Steps",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        }
        private static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        private static void Capture(Camera camera,string name)
        {
            var rt=RenderTexture.GetTemporary(540,960,24,RenderTextureFormat.ARGB32);var previous=camera.targetTexture;var active=RenderTexture.active;
            var texture=new Texture2D(540,960,TextureFormat.RGBA32,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,540,960),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),texture.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(texture);}
        }
        private sealed class Platform:IRootFogPlatform {public Camera MapCamera{get;}public bool Active=true;public bool IsMapScene=>Active;public Platform(Camera value){MapCamera=value;}}
        private sealed class Runner:MonoBehaviour{}
    }
}

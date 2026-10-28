using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.ProfessionUI;
using Object=UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeMeshBatchesValidation
    {
        [Serializable] private sealed class Payload { public string shader;public Settings[] settings; }
        [Serializable] private sealed class Settings { public bool useClipping,pmaVertexColors;public float zSpacing; }
        private const string Active="Zjcs.ProfessionNativeMeshBatches.Active";
        private static bool ready;
        private static readonly List<Object> owned=new List<Object>();
        static ProfessionNativeMeshBatchesValidation()
        {
            EditorApplication.update+=Update;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode)ready=true;};
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                SessionState.SetBool(Active,true);SessionState.SetFloat(Active+".Deadline",(float)(EditorApplication.timeSinceStartup+120));EditorApplication.EnterPlaymode();
            }
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static void Update()
        {
            if(!SessionState.GetBool(Active,false))return;
            try
            {
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",0))throw new TimeoutException("Draw batches fixture did not enter Play Mode.");
                if(!ready||!EditorApplication.isPlaying)return;
                var input=JsonUtility.FromJson<Payload>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("native-mesh-batches-validation.json")));
                Require(input.settings.Length==2&&input.settings.All(s=>s.useClipping&&!s.pmaVertexColors&&s.zSpacing==0),"both original profession graphic geometry parameters");
                var pose=new ProfessionNativeBonePose(new[]{new ProfessionNativeBonePose.Bone{name="root"},new ProfessionNativeBonePose.Bone{name="skinChild",parent=0,skinRequired=true}});
                var region=new ProfessionNativeAtlasRegion(1,1,0,0,1,1,0,0,1,1,0);
                ProfessionNativeRenderAttachment Quad(string page,float x=0,int bone=0)=>new ProfessionNativeRenderAttachment("draw-fixture",page,bone,region,2,2,x,0,1,1,0,1);
                ProfessionNativeClippingAttachment Clip(int end)=>new ProfessionNativeClippingAttachment("clip-fixture",0,2,new[]{-.5f,-.5f,.5f,-.5f,.5f,.5f,-.5f,.5f},4,end,3,1);
                var left=Quad("A");var right=Quad("A",3);var red=new Color32(255,0,0,255);var blue=new Color32(0,0,255,255);
                using(var batches=new ProfessionNativeMeshBatches(3))
                {
                    var layout=new[]{new ProfessionNativeMeshBatches.Slot(0,Clip(1),red),new ProfessionNativeMeshBatches.Slot(1,left,red),new ProfessionNativeMeshBatches.Slot(2,right,blue)};
                    batches.Build(pose,layout,input.settings[0].useClipping,input.settings[0].pmaVertexColors);
                    Require(batches.Batches.Count==1&&batches.Batches[0].Page=="A"&&batches.Batches[0].Blend==0,"contiguous equal-page material batch");
                    Mesh pooled=batches.Batches[0].Mesh;Near(5,Area(pooled),"inclusive clipped red plus unclipped blue area");
                    Require(pooled.colors32.Contains(red)&&pooled.colors32.Contains(blue),"original per-slot vertex colors");
                    Near(-.5f,pooled.bounds.min.x,"clipped left boundary");Near(4,pooled.bounds.max.x,"after-end unclipped right boundary");Capture(input.shader,pooled);
                    batches.Build(pose,layout,false);Near(8,Area(batches.Batches[0].Mesh),"original clipping-disabled parameter");
                    var empty=new[]{new ProfessionNativeMeshBatches.Slot(0,null,red),new ProfessionNativeMeshBatches.Slot(1,null,red),new ProfessionNativeMeshBatches.Slot(2,null,red)};
                    batches.Build(pose,empty);Require(batches.Batches.Count==0&&pooled.vertexCount==0,"stale pooled mesh cleared when no geometry remains");
                    batches.Build(pose,layout);Require(ReferenceEquals(pooled,batches.Batches[0].Mesh),"native mesh reused across frames");Near(5,Area(pooled),"reused mesh does not accumulate prior vertices");
                    var nullEnd=new[]{new ProfessionNativeMeshBatches.Slot(0,Clip(1),red),new ProfessionNativeMeshBatches.Slot(1,null,red),new ProfessionNativeMeshBatches.Slot(2,left,red)};
                    batches.Build(pose,nullEnd);Near(4,Area(batches.Batches[0].Mesh),"empty end slot ends clipping");
                    nullEnd[1]=new ProfessionNativeMeshBatches.Slot(1,left,red,active:false);batches.Build(pose,nullEnd);Near(4,Area(batches.Batches[0].Mesh),"inactive end slot ends clipping");
                    nullEnd[0]=new ProfessionNativeMeshBatches.Slot(0,Clip(0),red);batches.Build(pose,nullEnd);Near(1,Area(batches.Batches[0].Mesh),"end equal to start clips through final draw slot");
                    var reordered=new[]{new ProfessionNativeMeshBatches.Slot(1,null,red),new ProfessionNativeMeshBatches.Slot(0,Clip(1),red),new ProfessionNativeMeshBatches.Slot(2,left,red)};
                    batches.Build(pose,reordered);Near(1,Area(batches.Batches[0].Mesh),"original source slot indices follow supplied draw-order permutation");
                    var pages=new[]{new ProfessionNativeMeshBatches.Slot(0,left,red),new ProfessionNativeMeshBatches.Slot(1,Quad("B"),blue),new ProfessionNativeMeshBatches.Slot(2,left,red)};
                    batches.Build(pose,pages);Require(batches.Batches.Select(b=>b.Page).SequenceEqual(new[]{"A","B","A"}),"noncontiguous material pages retain draw order");
                    pages[1]=new ProfessionNativeMeshBatches.Slot(1,left,blue,blend:1);batches.Build(pose,pages);Require(batches.Batches.Select(b=>b.Blend).SequenceEqual(new[]{0,1,0}),"source blend modes split contiguous groups");
                    pages[1]=new ProfessionNativeMeshBatches.Slot(1,left,blue,breakAfter:true);batches.Build(pose,pages);Require(batches.Batches.Count==2,"explicit original separator boundary splits equal pages");
                    var semi=new Color32(200,100,50,128);pages[0]=new ProfessionNativeMeshBatches.Slot(0,left,semi);pages[1]=new ProfessionNativeMeshBatches.Slot(1,null,red);pages[2]=new ProfessionNativeMeshBatches.Slot(2,null,red);
                    batches.Build(pose,pages,pmaVertexColors:true);Require(batches.Batches[0].Mesh.colors32.All(c=>c.Equals(new Color32(100,50,25,128))),"explicit premultiplied-color mode");
                    pages[0]=new ProfessionNativeMeshBatches.Slot(0,Quad("A",0,1),red);batches.Build(pose,pages);Require(batches.Batches.Count==0,"inactive source skin bone contributes no mesh");
                    Reject(()=>batches.Build(pose,new[]{pages[0],pages[0],pages[2]}));
                    bool overlap=false;try{batches.Build(pose,new[]{new ProfessionNativeMeshBatches.Slot(0,Clip(2),red),new ProfessionNativeMeshBatches.Slot(1,Clip(2),red),pages[2]});}catch(NotSupportedException){overlap=true;}Require(overlap,"undefined overlapping clip ranges fail explicitly");
                    batches.Dispose();Require(batches.Batches.Count==0,"dispose releases active draw list");bool disposed=false;try{batches.Build(pose,layout);}catch(ObjectDisposedException){disposed=true;}Require(disposed,"disposed renderer cannot rebuild");
                }
                Debug.Log("Profession native draw batches passed in Play Mode and D3D: original clipping/PMA/zero-spacing parameters, inclusive/empty/inactive/reordered/self end slots, page/blend draw-order groups, explicit separators, vertex colors, inactive skin bones, pooled mesh clearing/reuse and disposal. Actual raster shows clipped red and unclipped blue geometry with transparent exterior. Geometry and white texture are fixtures; native actor constraints, source material assignment, parts and full profession UI remain unvalidated.");Finish(0);
            }
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static float Area(Mesh mesh)
        {
            var vertices=mesh.vertices;var indices=mesh.triangles;double area=0;
            for(int i=0;i<indices.Length;i+=3){Vector2 a=vertices[indices[i]],b=vertices[indices[i+1]],c=vertices[indices[i+2]];area+=Math.Abs(((double)b.x-a.x)*(c.y-a.y)-((double)b.y-a.y)*(c.x-a.x))*.5;}
            return (float)area;
        }
        private static void Capture(string shaderPath,Mesh mesh)
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);Require(shader&&shader.isSupported,"original UI shader binding");var material=Own(new Material(shader));material.mainTexture=Texture2D.whiteTexture;
            var target=RenderTexture.GetTemporary(256,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);RenderTexture previous=RenderTexture.active;
            try
            {
                using(var commands=new CommandBuffer())
                {
                    commands.SetRenderTarget(target);commands.ClearRenderTarget(true,true,Color.clear);commands.SetViewProjectionMatrices(Matrix4x4.identity,Matrix4x4.identity);
                    commands.DrawMesh(mesh,Matrix4x4.TRS(new Vector3(-.7f,0,0),Quaternion.identity,new Vector3(.4f,.8f,1)),material);Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active=target;var texture=Own(new Texture2D(256,128,TextureFormat.RGBA32,false,true));texture.ReadPixels(new Rect(0,0,256,128),0,0);texture.Apply();
                Color32 red=texture.GetPixel(38,64),blue=texture.GetPixel(192,64),cut=texture.GetPixel(38,110),after=texture.GetPixel(192,110),outside=texture.GetPixel(255,120);
                Require(red.r>250&&red.b<3&&blue.b>250&&blue.r<3&&cut.a==0&&after.a>250&&outside.a==0,"actual clipped and after-end original shader GPU output");
                File.WriteAllBytes(ProfessionValidationWorkspace.Output("native-mesh-batches-geometry.png"),texture.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        }
        private static void Near(float expected,float actual,string reason){Require(Mathf.Abs(expected-actual)<.0001f,reason);}
        private static void Reject(Action action){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Require(rejected,"invalid slot permutation rejected");}
        private static T Own<T>(T value)where T:Object{owned.Add(value);return value;}
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException("Native mesh batches validation failed: "+reason);}
        private static void Finish(int code){SessionState.SetBool(Active,false);foreach(Object o in owned)if(o)Object.DestroyImmediate(o);owned.Clear();EditorApplication.Exit(code);}
    }
}

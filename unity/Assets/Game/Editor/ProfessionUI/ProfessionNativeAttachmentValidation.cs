using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;
using Object=UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeAttachmentValidation
    {
        [Serializable] private sealed class Payload { public Body[] cases;public Gpu gpu; }
        [Serializable] private sealed class Body { public string sex;public ProfessionNativeBonePose.Bone[] bones;public int slotCount;public Region[] regions;public Attachment[] attachments; }
        [Serializable] private sealed class Region { public string page,name;public int[] size,bounds,offsets;public int rotate; }
        [Serializable] private sealed class Attachment
        {
            public string skin,key,name,kind;public int slot,slotBone,region,count,end;public float scale,width,height,x,y,scaleX,scaleY,rotation;
            public float[] vertices,uvs;public int[] triangles;public Vector2[] expected,expectedUvs;
        }
        [Serializable] private sealed class Gpu { public string sex,page,shader;public int attachment;public TextureRef[] textures; }
        [Serializable] private sealed class TextureRef { public string role,path,sha256; }
        private sealed class Slot : IProfessionNativeAttachmentSlot { public ProfessionNativeBonePose Pose{get;set;}public object Attachment{get;set;} }
        private const string Active="Zjcs.ProfessionNativeAttachment.Active";
        private static bool ready;
        private static readonly List<Object> owned=new List<Object>();
        static ProfessionNativeAttachmentValidation()
        {
            EditorApplication.update+=Update;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode)ready=true;};
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                SessionState.SetBool(Active,true);SessionState.SetFloat(Active+".Deadline",(float)(EditorApplication.timeSinceStartup+300));EditorApplication.EnterPlaymode();
            }
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static void Update()
        {
            if(!SessionState.GetBool(Active,false))return;
            try
            {
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",0))throw new TimeoutException("Native attachment fixture did not enter Play Mode.");
                if(!ready||!EditorApplication.isPlaying)return;
                var source=JsonUtility.FromJson<Payload>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("native-attachments-validation.json")));
                Require(source.cases.Length==2,"both original attachment sources");int count=0,points=0,indices=0;float maxPosition=0,maxUv=0;
                Mesh mesh=Own(new Mesh());
                foreach(Body body in source.cases)
                {
                    var pose=new ProfessionNativeBonePose(body.bones);
                    var atlas=body.regions.Select(r=>new ProfessionNativeAtlasRegion(r.size[0],r.size[1],r.bounds[0],r.bounds[1],r.bounds[2],r.bounds[3],r.offsets[0],r.offsets[1],r.offsets[2],r.offsets[3],r.rotate)).ToArray();
                    foreach(Attachment a in body.attachments)
                    {
                        var native=Create(a,body,atlas);Require(native.Name==a.name&&native.SlotBone==a.slotBone,"original attachment identity fields");
                        if(native is ProfessionNativeRenderAttachment render)
                        {
                            render.WriteMesh(pose,mesh,new Color32(255,255,255,255));Vector3[] positions=mesh.vertices;Vector2[] uv=mesh.uv;int[] triangles=mesh.triangles;
                            Require(render.Page==body.regions[a.region].page&&positions.Length==a.expected.Length&&render.IndexCount==triangles.Length,"original page and mesh sizes");
                            Require(triangles.SequenceEqual(a.kind=="mesh"?a.triangles:new[]{0,1,2,2,3,0}),"exact original triangle indices and winding");indices+=triangles.Length;
                            for(int i=0;i<positions.Length;i++)
                            {
                                Compare(positions[i],a.expected[i],ref maxPosition);Require(positions[i].z==0,"original zero z spacing");
                                CompareUv(uv[i],a.expectedUvs[i],ref maxUv);Require(mesh.colors32[i].Equals(new Color32(255,255,255,255)),"unmodified white source tint");
                            }
                        }
                        else if(native is ProfessionNativeBoundingAttachment bounds)
                        {
                            Require(bounds.WorldVerticesLength==a.count*2,"bounding vertex length");var output=new float[bounds.WorldVerticesLength];bounds.ComputeWorldVertices(new Slot{Pose=pose,Attachment=bounds},output);
                            for(int i=0;i<a.count;i++)Compare(new Vector2(output[2*i],output[2*i+1]),a.expected[i],ref maxPosition);
                        }
                        else if(native is ProfessionNativeClippingAttachment clip)
                        {
                            Require(clip.EndSlot==a.end&&clip.VertexCount==a.count&&!(native is IProfessionSkinClipBoundingBox),"separate clipping identity and end slot");
                            var output=new Vector2[clip.VertexCount];clip.ComputeVertices(pose,output);for(int i=0;i<output.Length;i++)Compare(output[i],a.expected[i],ref maxPosition);
                        }
                        else if(native is ProfessionNativePointAttachment point)Compare(point.ComputePosition(pose),a.expected[0],ref maxPosition);
                        else throw new InvalidOperationException("Unhandled native source attachment");
                        count++;points+=a.expected.Length;
                    }
                    if(body.sex==source.gpu.sex)
                    {
                        Attachment a=body.attachments[source.gpu.attachment];var render=(ProfessionNativeRenderAttachment)Create(a,body,atlas);
                        render.WriteMesh(pose,mesh,new Color32(255,255,255,255));Capture(source.gpu,a,mesh);
                    }
                }
                Debug.Log($"Profession native attachment objects passed: {count} attachments, {points} scaled vertices/points, {indices} exact triangle indices; max position difference {maxPosition:G9}, max UV difference {maxUv:G9}. Original typed bounding/clip/point identities, original source data scale and real Unity mesh attributes pass. Current original body atlas and dye texture render on D3D against independent reference geometry. Pose matrices are synthetic; actual constrained actor and profession UI are not validated.");Finish(0);
            }
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static ProfessionNativeAttachment Create(Attachment a,Body body,ProfessionNativeAtlasRegion[] atlas)
        {
            switch(a.kind)
            {
                case "region":return new ProfessionNativeRenderAttachment(a.name,body.regions[a.region].page,a.slotBone,atlas[a.region],a.width,a.height,a.x,a.y,a.scaleX,a.scaleY,a.rotation,a.scale);
                case "mesh":return new ProfessionNativeRenderAttachment(a.name,body.regions[a.region].page,a.slotBone,body.bones.Length,atlas[a.region],a.vertices,a.uvs,a.triangles,a.scale);
                case "boundingbox":return new ProfessionNativeBoundingAttachment(a.name,a.slotBone,body.bones.Length,a.vertices,a.count,a.scale);
                case "clipping":return new ProfessionNativeClippingAttachment(a.name,a.slotBone,body.bones.Length,a.vertices,a.count,a.end,body.slotCount,a.scale);
                case "point":return new ProfessionNativePointAttachment(a.name,a.slotBone,a.x,a.y,a.scale);
                default:throw new InvalidOperationException("Unknown original attachment type");
            }
        }
        private static void Compare(Vector2 value,Vector2 expected,ref float maximum)
        {
            maximum=Mathf.Max(maximum,Mathf.Abs(value.x-expected.x),Mathf.Abs(value.y-expected.y));
            Require(Mathf.Abs(value.x-expected.x)<=.000005f+.000007f*Mathf.Abs(expected.x)&&Mathf.Abs(value.y-expected.y)<=.000005f+.000007f*Mathf.Abs(expected.y),"independent scaled source vertex");
        }
        private static void CompareUv(Vector2 value,Vector2 expected,ref float maximum)
        {maximum=Mathf.Max(maximum,Mathf.Abs(value.x-expected.x),Mathf.Abs(value.y-expected.y));Require((value-expected).sqrMagnitude<=.000002f*.000002f,"independent original atlas UV");}
        private static void Capture(Gpu gpu,Attachment a,Mesh native)
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(gpu.shader);Require(shader&&shader.isSupported,"original Windows UI shader");var material=Own(new Material(shader));
            Require(gpu.textures.Select(t=>t.path).Distinct().Count()==gpu.textures.Length,"main and dye have independent payload paths");
            foreach(TextureRef r in gpu.textures)
            {
                using(var hash=SHA256.Create())Require(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath),r.path)))).Replace("-","").ToLowerInvariant()==r.sha256,"original texture artifact hash");
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(r.path);Require(texture,"original texture binding");material.SetTexture(r.role=="targetTextureReference"?"_MainTex":"_DyeTex",texture);
            }
            var reference=Own(new Mesh());reference.vertices=a.expected.Select(p=>new Vector3(p.x,p.y,0)).ToArray();reference.uv=a.expectedUvs;reference.colors32=Enumerable.Repeat(new Color32(255,255,255,255),a.expected.Length).ToArray();reference.triangles=a.triangles;reference.RecalculateBounds();
            Bounds b=reference.bounds;float extent=Mathf.Max(b.size.x,b.size.y)*.55f;Require(extent>0,"original nonempty mesh extent");
            Matrix4x4 fit=Matrix4x4.Scale(new Vector3(1/extent,1/extent,1))*Matrix4x4.Translate(-b.center);
            var actual=Raster(native,material,fit);var expected=Raster(reference,material,fit);Color32[] ap=actual.GetPixels32(),ep=expected.GetPixels32();int visible=0,colored=0,different=0;
            for(int i=0;i<ap.Length;i++)
            {
                if(ap[i].a>8)visible++;
                if(ap[i].r+ap[i].g+ap[i].b>30&&ap[i].a>8)colored++;
                if(Math.Abs(ap[i].r-ep[i].r)>2||Math.Abs(ap[i].g-ep[i].g)>2||Math.Abs(ap[i].b-ep[i].b)>2||Math.Abs(ap[i].a-ep[i].a)>2)different++;
            }
            Require(visible>30&&colored>30&&different<8,"actual original texture GPU raster versus independent reference geometry; visible="+visible+", colored="+colored+", differences="+different);
            File.WriteAllBytes(ProfessionValidationWorkspace.Output("native-attachment-geometry.png"),actual.EncodeToPNG());
            Debug.Log("Original attachment GPU geometry pixels: visible="+visible+", colored="+colored+", reference differences="+different+", renderer="+SystemInfo.graphicsDeviceType);
        }
        private static Texture2D Raster(Mesh mesh,Material material,Matrix4x4 fit)
        {
            var target=RenderTexture.GetTemporary(256,256,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);RenderTexture previous=RenderTexture.active;
            try
            {
                using(var commands=new CommandBuffer())
                {
                    commands.SetRenderTarget(target);commands.ClearRenderTarget(true,true,Color.clear);commands.SetViewProjectionMatrices(Matrix4x4.identity,Matrix4x4.identity);commands.DrawMesh(mesh,fit,material);Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active=target;var image=Own(new Texture2D(256,256,TextureFormat.RGBA32,false,true));image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();return image;
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        }
        private static T Own<T>(T value)where T:Object{owned.Add(value);return value;}
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException("Native attachment validation failed: "+reason);}
        private static void Finish(int code){SessionState.SetBool(Active,false);foreach(Object o in owned)if(o)Object.DestroyImmediate(o);owned.Clear();EditorApplication.Exit(code);}
    }
}

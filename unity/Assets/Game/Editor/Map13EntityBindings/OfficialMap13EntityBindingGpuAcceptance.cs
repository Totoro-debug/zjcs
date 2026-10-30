using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.World.NativeMap13EntityAssets;

namespace Zjcs.Editor.Map13EntityBindings
{
    [InitializeOnLoad]
    public static class OfficialMap13EntityBindingGpuAcceptance
    {
        private const string Pending = "Map13.EntityBindingGpu.Pending";
        private const string Failed = "Map13.EntityBindingGpu.Failed";
        private static bool running;
        static OfficialMap13EntityBindingGpuAcceptance()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.SetBool(Pending, false);
                EditorApplication.Exit(SessionState.GetBool(Failed, false) ? 1 : 0);
            };
        }
        public static void Run()
        {
            SessionState.SetBool(Pending, true); SessionState.SetBool(Failed, false);
            EditorApplication.isPlaying = true;
        }
        private static void Tick()
        {
            if (running || !SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            running = true;
            var asynchronous = ShaderUtil.allowAsyncCompilation;
            try { ShaderUtil.allowAsyncCompilation = false; Validate(); }
            catch (Exception error) { SessionState.SetBool(Failed, true); Debug.LogException(error); }
            finally { ShaderUtil.allowAsyncCompilation = asynchronous; EditorApplication.isPlaying = false; }
        }
        private static void Validate()
        {
            var first = Resources.Load<NativeMap13EntityShaderCatalog>("Map13/Native/entity-shader-catalog");
            if (first == null || first.manifest != "871_156598" || first.entries.Length != 21)
                throw new InvalidDataException("Serialized native shader catalog is missing.");
            Resources.UnloadAsset(first);
            var catalog = Resources.Load<NativeMap13EntityShaderCatalog>("Map13/Native/entity-shader-catalog");
            foreach (var entry in catalog.entries)
                if (catalog.Require(entry.sourceCab.ToUpperInvariant(), entry.sourcePathId) != entry.shader)
                    throw new InvalidDataException("Cold-reloaded native shader identity differs.");
            try { catalog.Require("missing", "1"); throw new InvalidDataException("Unbound shader was accepted."); }
            catch (System.Collections.Generic.KeyNotFoundException) { }
            var spine = new Material(catalog.entries.Single(e => e.originalName == "Universal Render Pipeline/2D/Spine/Skeleton").shader);
            var unlit = new Material(catalog.entries.Single(e => e.originalName == "Universal Render Pipeline/2D/Sprite-Unlit-Default").shader);
            var engine = new Material(catalog.entries.Single(e => e.originalName == "Hidden/InternalErrorShader").shader);
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-32,-32,0), new Vector3(32,-32,0), new Vector3(32,32,0), new Vector3(-32,32,0) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                triangles = new[] { 0,1,2,0,2,3 }
            };
            var texture = new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
            var target = new RenderTexture(128,128,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            var readback = new Texture2D(128,128,TextureFormat.RGBAFloat,false,true);
            target.Create();
            var oldLod = spine.shader.maximumLOD;
            try
            {
                Color texel = new Color(.8f,.5f,.25f,.75f), vertex = new Color(.15f,.3f,.45f,.6f);
                texture.SetPixel(0,0,texel); texture.Apply(); unlit.mainTexture = texture;
                mesh.colors = Enumerable.Repeat(vertex,4).ToArray();
                unlit.SetColor("_Color", Color.white); unlit.SetColor("_RendererColor", Color.white);
                Color unlitControl = vertex * texel;
                CheckSpriteRendererFallback(spine.shader, texel, false);
                CheckSpriteRendererFallback(spine.shader, texel, true);
                CheckQuadEither(Draw(mesh,unlit,0,target,readback),
                    new Color(unlitControl.r*unlitControl.a,unlitControl.g*unlitControl.a,
                        unlitControl.b*unlitControl.a,unlitControl.a*unlitControl.a),unlitControl.a,
                    "Diagnostic URP unlit control");
                mesh.colors = Enumerable.Repeat(Color.white,4).ToArray();
                CheckQuad(Draw(mesh,engine,0,target,readback),Color.magenta,"Native engine error pass");
                Save(readback,"engine-error.png");
                texture.SetPixel(0,0,Color.white);texture.Apply();
                unlit.SetColor("_Color",Color.white);unlit.SetColor("_RendererColor",Color.white);
                for (var pass=1;pass<3;pass++)
                {
                    CheckQuad(Draw(mesh,engine,pass,target,readback),Color.clear,"Native engine color mask pass "+pass);
                    var counts = new int[2];
                    for (var side=0;side<2;side++)
                    {
                        var pixels=Draw(mesh,engine,pass,target,readback,unlit,side==0 ? -1 : 1);
                        counts[side]=pixels.Count(pixel=>pixel.a>.5f);
                        CheckQuad(pixels,counts[side]==0 ? Color.clear : Color.white,"Native engine depth pass "+pass);
                        Save(readback,"engine-depth-"+pass+"-"+side+".png");
                    }
                    if (counts.Min()!=0 || counts.Max()!=4096)
                        throw new InvalidDataException("Native engine depth pass did not write depth: "+string.Join(",",counts));
                }
                Debug.Log("Map13 native shader binding Play Mode passed: 21 cold-reloaded serialized references, " +
                    "two original SpriteRenderer Spine variants and native engine output; all 16384 pixels per draw checked under .");
            }
            finally
            {
                spine.shader.maximumLOD = oldLod;
                UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(readback); target.Release(); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(spine); UnityEngine.Object.DestroyImmediate(unlit); UnityEngine.Object.DestroyImmediate(engine);
            }
        }
        private static void CheckSpriteRendererFallback(Shader shader, Color texel, bool straight)
        {
            var texture = new Texture2D(64,64,TextureFormat.RGBAFloat,false,true);
            var target = new RenderTexture(128,128,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            var readback = new Texture2D(128,128,TextureFormat.RGBAFloat,false,true);
            var material = new Material(shader);
            var sprite = Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f),1f);
            var rendererObject = new GameObject("Map13 shader SpriteRenderer check");
            var cameraObject = new GameObject("Map13 shader camera check");
            try
            {
                texture.SetPixels(Enumerable.Repeat(texel,64*64).ToArray()); texture.Apply();
                material.mainTexture = texture;
                rendererObject.layer = 31;
                var spriteRenderer = rendererObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                spriteRenderer.sharedMaterial = material;
                if (straight) material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                cameraObject.layer = 31;
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 64; camera.aspect = 1;
                camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear; camera.targetTexture = target;
                camera.transform.position = new Vector3(0,0,-10);
                target.Create(); camera.Render();
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0,0,128,128),0,0); readback.Apply();
                Color[] pixels = readback.GetPixels();
                Color sample = pixels[64*128+64];
                Color fallback = new Color(texel.r*texel.a,texel.g*texel.a,texel.b*texel.a,texel.a);
                if (!QuadMatches(pixels,texel) && !QuadMatches(pixels,fallback))
                    throw new InvalidDataException("Original Spine SpriteRenderer output is neither the native pass nor its official Sprite-Unlit fallback: " + sample);
                if (straight && !QuadMatches(pixels,fallback))
                    throw new InvalidDataException("Original straight-alpha Spine SpriteRenderer output is not premultiplied: " + sample);
                Debug.Log("Map13 original Spine SpriteRenderer GPU path passed straight=" + straight + ", center=" + sample);
                Save(readback,"spine-sprite-renderer-"+straight+".png");
            }
            finally
            {
                RenderTexture.active = null; target.Release();
                UnityEngine.Object.DestroyImmediate(rendererObject); UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(readback); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        private static bool QuadMatches(Color[] pixels,Color color)
        {
            for(var y=0;y<128;y++) for(var x=0;x<128;x++)
            {
                var expected=x>=32 && x<96 && y>=32 && y<96 ? color : Color.clear;
                var actual=pixels[y*128+x];
                for(var channel=0;channel<4;channel++)
                    if(float.IsNaN(actual[channel]) || Mathf.Abs(actual[channel]-expected[channel])>.003f) return false;
            }
            return true;
        }
        private static void CheckQuadEither(Color[] pixels,Color color,float alternateAlpha,string label)
        {
            if (QuadMatches(pixels,color) || QuadMatches(pixels,new Color(color.r,color.g,color.b,alternateAlpha))) return;
            throw new InvalidDataException(label+" differs from both original blend alpha variants.");
        }
        private static Color[] Draw(Mesh mesh,Material material,int pass,RenderTexture target,Texture2D readback,
            Material occluder=null,float occluderZ=0)
        {
            var previous = RenderTexture.active; var commands = new CommandBuffer();
            try
            {
                commands.SetRenderTarget(target); commands.SetViewport(new Rect(0,0,128,128));
                commands.ClearRenderTarget(true,true,Color.clear);
                commands.SetViewProjectionMatrices(Matrix4x4.identity,GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-64,64,-64,64,-10,10),true));
                commands.DrawMesh(mesh,Matrix4x4.identity,material,0,pass);
                if (occluder!=null) commands.DrawMesh(mesh,Matrix4x4.Translate(new Vector3(0,0,occluderZ)),occluder,0,0);
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,128,128),0,0);readback.Apply();return readback.GetPixels();
            }
            finally { RenderTexture.active=previous;commands.Release(); }
        }
        private static void CheckQuad(Color[] pixels,Color color,string label,bool diagnose=false)
        {
            var covered= pixels.Select((value,index)=>new {value,index}).Where(x=>x.value.a>.01f).ToArray();
            if(diagnose) Debug.Log("Map13 fallback draw "+label+" coverage="+covered.Length+
                (covered.Length==0?"":" bounds="+covered.Min(x=>x.index%128)+".."+covered.Max(x=>x.index%128)+","+
                covered.Min(x=>x.index/128)+".."+covered.Max(x=>x.index/128)+" sample="+covered[0].value));
            for(var y=0;y<128;y++) for(var x=0;x<128;x++)
            {
                var expected=x>=32 && x<96 && y>=32 && y<96 ? color : Color.clear;
                var actual=pixels[y*128+x];
                for(var channel=0;channel<4;channel++)
                    if(float.IsNaN(actual[channel]) || Mathf.Abs(actual[channel]-expected[channel])>.003f)
                        throw new InvalidDataException(label+" at "+x+","+y+": "+actual+" != "+expected);
            }
        }
        private static void Save(Texture2D texture,string name)
        {
            var folder=Path.GetFullPath("../captures");Directory.CreateDirectory(folder);
            var png=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false,true);
            try { png.SetPixels(texture.GetPixels());png.Apply();File.WriteAllBytes(Path.Combine(folder,name),png.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(png); }
        }
    }
}

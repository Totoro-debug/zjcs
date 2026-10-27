using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Editor.Map13EntityShaders
{
    [InitializeOnLoad]
    public static class OfficialMap13EntityShaderGpuAcceptance
    {
        private const string Pending = "Map13.EntityShaderGpu.Pending";
        private const string Failed = "Map13.EntityShaderGpu.Failed";
        private static bool running;
        private static readonly Color Texel = new Color(.8f, .5f, .25f, .75f);

        static OfficialMap13EntityShaderGpuAcceptance()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending, false))
                {
                    SessionState.SetBool(Pending, false);
                    EditorApplication.Exit(SessionState.GetBool(Failed, false) ? 1 : 0);
                }
            };
        }

        public static void Run()
        {
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Failed, false);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (running || !SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying
                || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            running = true;
            bool asynchronous = ShaderUtil.allowAsyncCompilation;
            try { ShaderUtil.allowAsyncCompilation = false; Validate(); }
            catch (Exception error) { SessionState.SetBool(Failed, true); Debug.LogException(error); }
            finally { ShaderUtil.allowAsyncCompilation = asynchronous; EditorApplication.isPlaying = false; }
        }

        private static void Validate()
        {
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-32,-32,0), new Vector3(32,-32,0), new Vector3(32,32,0), new Vector3(-32,32,0) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                triangles = new[] { 0,1,2,0,2,3 }
            };
            var texture = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            texture.SetPixel(0, 0, Texel); texture.Apply();
            var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create();
            var readback = new Texture2D(128, 128, TextureFormat.RGBAFloat, false, true);
            var spine = MaterialFor("SpineSkeleton_");
            var ui = MaterialFor("UIEffectAdditive_");
            var dye = MaterialFor("UniversalRenderPipelineSpineSkeletonDye2D_");
            try
            {
                spine.mainTexture = texture;
                Color vertex = new Color(.15f,.3f,.45f,.6f);
                mesh.colors = Enumerable.Repeat(vertex, 4).ToArray();
                foreach (bool straight in new[] { false, true })
                {
                    if (straight) spine.EnableKeyword("_STRAIGHT_ALPHA_INPUT"); else spine.DisableKeyword("_STRAIGHT_ALPHA_INPUT");
                    foreach (float flip in new[] { 1f, -1f })
                    {
                        Color[] pixels = Draw(mesh, spine, Matrix4x4.TRS(new Vector3(14,0,0), Quaternion.identity,
                            new Vector3(.75f*flip,.5f,1)), target, readback);
                        Save(readback,"spine-"+straight+"-"+flip+".png");
                        int[] covered=pixels.Select((color,index)=>new {color,index}).Where(item=>item.color.a>.01f).Select(item=>item.index).ToArray();
                        Debug.Log("Spine GPU coverage "+covered.Length+(covered.Length==0?"":
                            " x="+covered.Min(index=>index%128)+".."+covered.Max(index=>index%128)+" y="+covered.Min(index=>index/128)+".."+covered.Max(index=>index/128)));
                        Color expected = new Color(Polynomial(vertex.r/vertex.a)*vertex.a*Texel.r,
                            Polynomial(vertex.g/vertex.a)*vertex.a*Texel.g,
                            Polynomial(vertex.b/vertex.a)*vertex.a*Texel.b, vertex.a*Texel.a);
                        if (straight) { expected.r*=Texel.a; expected.g*=Texel.a; expected.b*=Texel.a; }
                        int lit = 0;
                        for (int y=0;y<128;y++) for (int x=0;x<128;x++)
                        {
                            bool inside = x>=54 && x<102 && y>=48 && y<80;
                            Near(pixels[y*128+x], inside ? expected : Color.clear, .003f, "Spine premultiplied color/transform at "+x+","+y);
                            if (pixels[y*128+x].a>.01f) lit++;
                        }
                        if (lit!=1536) throw new InvalidDataException("Native Spine transform coverage changed.");
                    }
                }
                mesh.colors = Enumerable.Repeat(new Color(.6f,.4f,.2f,.503f),4).ToArray();
                ui.mainTexture=texture;
                ui.SetColor("_Color",new Color(.5f,.75f,1,1));
                ui.SetVector("_TextureSampleAdd",new Vector4(.1f,.1f,.1f,0));
                ui.SetFloat("_UIVertexColorAlwaysGammaSpace",0);
                ui.SetFloat("_Brightness",.8f); ui.SetFloat("_Contrast",1);
                ui.SetVector("_ClipRect",new Vector4(-16,-16,16,16));
                ui.SetFloat("_UIMaskSoftnessX",4); ui.SetFloat("_UIMaskSoftnessY",8);
                ui.EnableKeyword("UNITY_UI_CLIP_RECT");
                Shader.SetGlobalFloat("unity_GUIZTestMode",(float)CompareFunction.Always);
                Color[] clipped=Draw(mesh,ui,Matrix4x4.identity,target,readback);
                float alpha=(float)Math.Round(.503f*255,MidpointRounding.ToEven)/255*.75f;
                for(int y=0;y<128;y++) for(int x=0;x<128;x++)
                {
                    float px=x+.5f-64,py=y+.5f-64;
                    float mask=Mathf.Clamp01((32-Mathf.Abs(2*px))*(.25f/1.5f))
                        *Mathf.Clamp01((32-Mathf.Abs(2*py))*(.25f/2.5f));
                    Color expected=new Color(.27f*.27f*.8f*alpha*mask,.18f*.18f*.8f*alpha*mask,
                        .07f*.07f*.8f*alpha*mask,alpha*mask);
                    Near(clipped[y*128+x],expected,.003f,"Original UI clipping/projection/alpha rounding");
                }
                Save(readback,"ui-clipping.png");
                mesh.colors=Enumerable.Repeat(Color.white,4).ToArray();
                dye.mainTexture=Texture2D.whiteTexture;
                dye.SetTexture("_DyeTex",Texture2D.blackTexture);
                dye.SetTexture("_MaskTex",Texture2D.whiteTexture);
                dye.SetFloat("_Light",0); dye.SetFloat("_Intensity",1);
                dye.EnableKeyword("_DITHER_FADE");
                Shader.SetGlobalFloat("_UseSceneLighting",0);
                Shader.SetGlobalFloat("_HDREmulationScale",1);
                foreach(float fade in new[]{0f,.5f,1f})
                {
                    dye.SetFloat("_DitherAlpha",fade);
                    Color[] pixels=Draw(mesh,dye,Matrix4x4.identity,target,readback);
                    int actual=pixels.Count(color=>color.a>.5f);
                    int expected=fade==0?0:fade==1?4096:2048;
                    if(actual!=expected) throw new InvalidDataException("Original 16-entry dither coverage: "+fade+" expected "+expected+", actual "+actual);
                    if(fade==.5f) Save(readback,"dither-half.png");
                }
                Debug.Log("Map13 entity shader Play Mode GPU checks passed: four Spine color/transform renders, all 16384 UI clipping pixels, and three original dither coverage states. Formal entities, mouse input and saves are not covered.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(spine); UnityEngine.Object.DestroyImmediate(ui); UnityEngine.Object.DestroyImmediate(dye);
                UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(readback);
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static float Polynomial(float value) => value*(value*(value*.305306017f+.682171106f)+.0125228781f);
        private static Material MaterialFor(string prefix)
        {
            string path=Directory.GetFiles("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders",prefix+"*.shader").Single();
            Shader shader=AssetDatabase.LoadAssetAtPath<Shader>(path.Replace('\\','/'));
            if(!shader) throw new InvalidDataException("Native shader missing: "+path);
            return new Material(shader);
        }
        private static Color[] Draw(Mesh mesh,Material material,Matrix4x4 model,RenderTexture target,Texture2D readback)
        {
            var commands=new CommandBuffer();
            var previous=RenderTexture.active;
            try
            {
                commands.SetRenderTarget(target);
                commands.SetViewport(new Rect(0,0,128,128));
                commands.ClearRenderTarget(true,true,Color.clear);
                commands.SetViewProjectionMatrices(Matrix4x4.identity,GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-64,64,-64,64,-10,10),true));
                commands.SetGlobalVector("_ScreenParams",new Vector4(128,128,1+1f/128,1+1f/128));
                commands.SetGlobalVector("_ScaledScreenParams",new Vector4(128,128,1+1f/128,1+1f/128));
                commands.DrawMesh(mesh,model,material,0,0);
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active=target;
                readback.ReadPixels(new Rect(0,0,128,128),0,0); readback.Apply();
                return readback.GetPixels();
            }
            finally { RenderTexture.active=previous; commands.Release(); }
        }
        private static void Near(Color actual,Color expected,float tolerance,string label)
        {
            for(int i=0;i<4;i++) if(float.IsNaN(actual[i]) || Mathf.Abs(actual[i]-expected[i])>tolerance)
                throw new InvalidDataException(label+": "+actual+" != "+expected);
        }
        private static void Save(Texture2D source,string name)
        {
            string output=Path.GetFullPath("../captures"); Directory.CreateDirectory(output);
            var png=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);
            try { png.SetPixels(source.GetPixels());png.Apply();File.WriteAllBytes(Path.Combine(output,name),png.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(png); }
        }
    }
}

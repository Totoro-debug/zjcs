using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionAppearanceMaterialValidation
    {
        private const string Active = "Zjcs.ProfessionMaterials.Active";
        private static bool ready;
        private static readonly List<Object> owned = new List<Object>();
        private static string unexpectedError;

        static ProfessionAppearanceMaterialValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 120));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Appearance material fixture did not enter Play Mode.");
                if (!ready || !EditorApplication.isPlaying) return;
                Application.logMessageReceived += Observe;
                try { CheckMaterials(); CheckMasks(); }
                finally { Application.logMessageReceived -= Observe; }
                Require(unexpectedError == null, unexpectedError);
                Debug.Log("Profession original appearance material operations passed: original dye cache/maximum/Clear semantics, main/dye texture and light/hair/effect reset, all stencil fields and alpha-clip branches, enabled parent masks and root sort-override stencil depth. Actual Direct3D output of the existing original UI shader has an opaque blue center and transparent corners; PNG written. Material/mesh fixtures are not a real character or profession-page validation.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void CheckMaterials()
        {
            Shader ui = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/SpineSkeletonGraphicDye_a2f0973c.shader");
            Shader world = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/UniversalRenderPipelineSpineSkeletonDye2D_ffd6b8c6.shader");
            Require(ui && world && ui.isSupported && world.isSupported, "original translated Windows shaders");
            Material material = Own(AppearanceMaterialUtils.CreateUIMaterial(ui));
            Material original = Own(new Material(ui));
            Texture2D texture = Own(new Texture2D(2, 2, TextureFormat.RGBA32, false, true));
            texture.SetPixels(new[] { Color.blue, Color.blue, Color.blue, Color.blue }); texture.Apply();
            AppearanceMaterialUtils.Clear();
            int channel = AppearanceMaterialUtils.GetDyePropertyId(3);
            Require(channel == Shader.PropertyToID("_DyeHsv3") && channel == AppearanceMaterialUtils.GetDyePropertyId(3), "original dye cache");
            AppearanceMaterialUtils.SetDye(material, 3, new Vector3(.2f, .3f, .4f));
            Require(material.GetVector(channel) == new Vector4(.2f, .3f, .4f, 0), "original Vector3 conversion");
            material.SetVector(AppearanceMaterialUtils.GetDyePropertyId(1), Vector4.one);
            material.SetTexture(AppearanceMaterialUtils.sDyeTexId, texture);
            AppearanceMaterialUtils.SetMaterialWithMainTexture(material, texture);
            Require(material.GetVector(channel) == Vector4.zero && material.GetVector(AppearanceMaterialUtils.GetDyePropertyId(1)) == Vector4.zero && material.mainTexture == texture && !material.GetTexture(AppearanceMaterialUtils.sDyeTexId), "reset all channels through maximum and dye texture");
            AppearanceMaterialUtils.SetDye(material, 2, Vector3.one);
            AppearanceMaterialUtils.Clear();
            AppearanceMaterialUtils.SetMaterialWithMainTexture(material, texture);
            Require(material.GetVector(AppearanceMaterialUtils.GetDyePropertyId(2)) == new Vector4(1, 1, 1, 0), "original Clear resets maximum only, retaining material dye and cached IDs");
            AppearanceMaterialUtils.SetDye(material, 3, Vector3.one);
            AppearanceMaterialUtils.SetMaterialWithMainTexture(material, texture);

            foreach (float operation in new[] { 0, .5f, 2 })
                foreach (float write in new[] { 0, 63 })
                {
                    AppearanceMaterialUtils.SetMaterialStencil(original, 5, operation, 3, 7, 31, write);
                    AppearanceMaterialUtils.InitUIMaterial(material, texture, original);
                    Require(material.GetFloat("_Stencil") == 5 && material.GetFloat("_StencilOp") == operation && material.GetFloat("_StencilComp") == 3 && material.GetFloat("_ColorMask") == 7 && material.GetFloat("_StencilReadMask") == 31 && material.GetFloat("_StencilWriteMask") == write, "original stencil fields");
                    bool alpha = (int)operation != 0 && write > 0;
                    // The source shader's translated programs do not declare
                    // this keyword. Unity retains the original operation in
                    // shaderKeywords while IsKeywordEnabled reports false.
                    Require(material.GetFloat("_UseUIAlphaClip") == (alpha ? 1 : 0) && material.shaderKeywords.Contains("UNITY_UI_ALPHACLIP") == alpha, "original cast-to-int alpha-clip field and retained keyword");
                }
            Material rendered = Own(new Material(world)); Material worldOriginal = Own(new Material(world));
            worldOriginal.mainTexture = texture;
            foreach (string keyword in new[] { "_DISSOLVE", "_HITEFFECT", "_FROZEN", "_FORDING" }) rendered.EnableKeyword(keyword);
            rendered.SetFloat("_LightAtten", 2); rendered.SetFloat("_EnableHairClip", 1); rendered.SetTexture("_DyeTex", texture);
            AppearanceMaterialUtils.SetDye(rendered, 1, Vector3.one);
            AppearanceMaterialUtils.ResetRenderMaterial(rendered, worldOriginal);
            Require(rendered.GetFloat("_LightAtten") == .565f && rendered.GetFloat("_EnableHairClip") == 0 && rendered.mainTexture == texture && !rendered.GetTexture("_DyeTex") && rendered.GetVector("_DyeHsv1") == Vector4.zero, "original render material reset");
            foreach (string keyword in new[] { "_DISSOLVE", "_HITEFFECT", "_FROZEN", "_FORDING" }) Require(!rendered.IsKeywordEnabled(keyword), "original effect keyword reset");
            AppearanceMaterialUtils.SetMaterialStencil(material, 0, 0, 8, 15, 255, 255);
            Capture(material);
            AppearanceMaterialUtils.Clear();
        }
        private static void CheckMasks()
        {
            var canvas = Own(new GameObject("MaterialMaskCanvas", typeof(RectTransform), typeof(Canvas)));
            var maskGo = Own(new GameObject("ParentMask", typeof(RectTransform), typeof(Image), typeof(Mask)));
            maskGo.transform.SetParent(canvas.transform, false);
            var child = Own(new GameObject("MaskSubject", typeof(RectTransform), typeof(Image), typeof(Mask)));
            child.transform.SetParent(maskGo.transform, false);
            Mask mask = maskGo.GetComponent<Mask>();
            Require(AppearanceMaterialUtils.IsMaskable(child.transform), "enabled parent mask");
            mask.enabled = false; Require(!AppearanceMaterialUtils.IsMaskable(child.transform), "own mask is not an ancestor");
            mask.enabled = true; mask.graphic.enabled = false; Require(!AppearanceMaterialUtils.IsMaskable(child.transform), "disabled parent graphic");
            mask.graphic.enabled = true;
            Require(AppearanceMaterialUtils.GetStencilValue(child.transform) == 1, "root canvas stencil depth");
            var nested = Own(new GameObject("NestedOverrideCanvas", typeof(RectTransform), typeof(Canvas)));
            nested.transform.SetParent(maskGo.transform, false); nested.GetComponent<Canvas>().overrideSorting = true;
            child.transform.SetParent(nested.transform, false);
            Require(AppearanceMaterialUtils.GetStencilValue(child.transform) == 0, "root sort override stops stencil depth");
        }
        private static void Capture(Material material)
        {
            Mesh mesh = Own(new Mesh());
            mesh.vertices = new[] { new Vector3(-.7f, -.7f, 0), new Vector3(-.7f, .7f, 0), new Vector3(.7f, .7f, 0), new Vector3(.7f, -.7f, 0) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            var target = RenderTexture.GetTemporary(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            try
            {
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    commands.DrawMesh(mesh, Matrix4x4.identity, material);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target;
                var texture = Own(new Texture2D(128, 128, TextureFormat.RGBA32, false, true));
                texture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); texture.Apply();
                Color32 center = texture.GetPixel(64, 64), corner = texture.GetPixel(0, 0);
                Require(center.b > 250 && center.r < 3 && center.g < 3 && center.a > 250 && corner.a == 0, "original UI shader GPU output");
                File.WriteAllBytes(ProfessionValidationWorkspace.Output("appearance-material-fixture.png"), texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        private static void Observe(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) unexpectedError = message; }
        private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            foreach (Object value in owned) if (value) Object.DestroyImmediate(value);
            EditorApplication.Exit(code);
        }
    }
}

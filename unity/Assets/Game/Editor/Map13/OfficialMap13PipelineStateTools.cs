using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Zjcs.Editor.Map13
{
    public sealed class OfficialMap13PipelineStateTools : IPreprocessShaders
    {
        private const string Key = "Zjcs.Map13.BaselineStateBundle";
        public int callbackOrder => 10000;
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (!SessionState.GetBool(Key, false)) return;
            // This bundle inspects ShaderLab state, not executable variant parity.
            while (data.Count > 1) data.RemoveAt(data.Count - 1);
        }
        [Serializable] private sealed class ShaderInput { public string baseline_path; }
        [Serializable] private sealed class Cases { public ShaderInput[] shaders; }
        public static void Run()
        {
            string root = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/") ? Path.GetFullPath("../../..") : Path.GetFullPath("..");
            var source = JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(root, "unity/Assets/Official/World/Map/13/Source/native-pipeline-baseline-cases.json")));
            string output = Path.Combine(root, ".tools/map13-native-cache-871_156598/baseline-shader-state-bundle");
            Directory.CreateDirectory(output);
            var previousPipeline = GraphicsSettings.defaultRenderPipeline;
            var previousQuality = QualitySettings.renderPipeline;
            var previousSettings = GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();
            var renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            var settings = (RenderPipelineGlobalSettings)ScriptableObject.CreateInstance(typeof(UniversalRenderPipeline).Assembly.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings", true));
            var settingsFields = new SerializedObject(settings);
            foreach (string field in new[] { "m_StripDebugVariants", "m_StripUnusedVariants", "m_StripUnusedPostProcessingVariants" })
                settingsFields.FindProperty(field).boolValue = false;
            settingsFields.ApplyModifiedPropertiesWithoutUndo();
            const string assets = "Assets/Map13BaselineProbe";
            if (AssetDatabase.IsValidFolder(assets)) throw new InvalidOperationException("Own shader state probe folder already exists.");
            AssetDatabase.CreateFolder("Assets", "Map13BaselineProbe");
            AssetDatabase.CreateAsset(renderer, assets + "/renderer.asset");
            AssetDatabase.CreateAsset(pipeline, assets + "/pipeline.asset");
            AssetDatabase.CreateAsset(settings, assets + "/settings.asset");
            GraphicsSettings.RegisterRenderPipelineSettings<UniversalRenderPipeline>(settings);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = null;
            SessionState.SetBool(Key, true);
            try
            {
                var result = BuildPipeline.BuildAssetBundles(output, new[] { new AssetBundleBuild { assetBundleName = "baseline-shaders",
                    assetNames = source.shaders.Select(s => s.baseline_path.Substring("unity/".Length)).ToArray() } },
                    BuildAssetBundleOptions.UncompressedAssetBundle | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
                if (!result) throw new BuildFailedException("Original engine shader-state probe bundle failed.");
                Debug.Log("Built engine shader-state inspection bundle. Executable equivalence is checked separately against APK GLES programs.");
            }
            finally
            {
                SessionState.SetBool(Key, false);
                GraphicsSettings.defaultRenderPipeline = previousPipeline;
                QualitySettings.renderPipeline = previousQuality;
                if (previousSettings) GraphicsSettings.RegisterRenderPipelineSettings<UniversalRenderPipeline>(previousSettings);
                else GraphicsSettings.UnregisterRenderPipelineSettings<UniversalRenderPipeline>();
                AssetDatabase.DeleteAsset(assets);
            }
        }
    }
}

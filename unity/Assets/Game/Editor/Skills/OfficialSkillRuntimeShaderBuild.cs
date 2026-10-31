using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Editor.Skills
{
    // Home and battle enter the original 2D pipeline after the Built-in startup.
    // URP's scene-only unused-variant scan cannot see those runtime scopes.
    public sealed class OfficialSkillRuntimeShaderBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static bool prepared;
        private static RenderPipelineAsset previousPipeline;
        private static UnityEngine.Object previousShaderSettings;
        private static bool previousStripUnused;
        private static readonly List<TMP_FontAsset> preservedFonts = new List<TMP_FontAsset>();
        private static readonly FieldInfo clearFontOnBuild = typeof(TMP_FontAsset).GetField(
            "m_ClearDynamicDataOnBuild", BindingFlags.Instance | BindingFlags.NonPublic);
        public int callbackOrder => -10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            Prepare();
        }

        public static void Prepare()
        {
            if (prepared) return;
            var binding = Resources.Load<ScriptableObject>("Combat/OfficialBattleRenderPipelineAssets");
            var pipeline = binding == null ? null : new SerializedObject(binding).FindProperty("Pipeline")?.objectReferenceValue as RenderPipelineAsset;
            if (pipeline == null)
                throw new BuildFailedException("The runtime 2D pipeline must be bound before building.");
            // Register before BuildPlayer gathers pipeline dependencies. Player
            // startup restores Built-in before rendering its first scene; Home
            // and battles then enter their original pipeline scopes as usual.
            previousPipeline = GraphicsSettings.defaultRenderPipeline;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            prepared = true;
            Type type = pipeline.GetType().Assembly.GetType(
                "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings", true);
            var settings = type.GetProperty("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetValue(null) as UnityEngine.Object;
            if (settings == null)
                throw new BuildFailedException("The runtime 2D pipeline's global shader settings are missing.");
            var serialized = new SerializedObject(settings);
            var strip = serialized.FindProperty("m_StripUnusedVariants");
            if (strip == null)
                throw new BuildFailedException("The pinned URP unused-variant setting is missing.");
            previousShaderSettings = settings;
            previousStripUnused = strip.boolValue;
            strip.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            PreserveNativeFontAtlases();
            Debug.Log("[Skills] Preserving shader variants for the original Home/battle runtime 2D pipeline.");
        }

        private static void PreserveNativeFontAtlases()
        {
            if (clearFontOnBuild == null)
                throw new BuildFailedException("The pinned TMP dynamic-font build setting is missing.");
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Official" }))
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic
                    || !(bool)clearFontOnBuild.GetValue(font)) continue;
                // Original glyphs and empty 0x0 atlas exports belong to the verified
                // payload. TMP's global build cleanup must not rewrite either.
                preservedFonts.Add(font);
                clearFontOnBuild.SetValue(font, false);
            }
            Debug.Log("[Skills] Preserving " + preservedFonts.Count + " original dynamic font atlases during build.");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            Restore();
        }

        public static void Restore()
        {
            if (!prepared) return;
            foreach (TMP_FontAsset font in preservedFonts)
                if (font != null) clearFontOnBuild.SetValue(font, true);
            preservedFonts.Clear();
            GraphicsSettings.defaultRenderPipeline = previousPipeline;
            if (previousShaderSettings != null) {
                var serialized = new SerializedObject(previousShaderSettings);
                serialized.FindProperty("m_StripUnusedVariants").boolValue = previousStripUnused;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(previousShaderSettings);
                AssetDatabase.SaveAssetIfDirty(previousShaderSettings);
                previousShaderSettings = null;
            }
            prepared = false;
        }
    }
}

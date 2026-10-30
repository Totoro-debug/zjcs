using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.Map15
{
    public static class OfficialMap15GroupDataAcceptance
    {
        private const string Key = "Map15.GroupDataAcceptance";
        private static bool started;
        private static int errors;

        public static void Run()
        {
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 240);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            started = false;
            errors = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.playModeStateChanged += Mode;
        }

        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= Mode;
            Application.logMessageReceived -= Observe;
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Observe(string text, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Official Map15 group-data Play Mode acceptance timed out.");
                EditorApplication.Exit(1);
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            Application.logMessageReceived += Observe;
            new GameObject("Official Map15 group-data acceptance").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static IEnumerator Verify()
        {
            try
            {
                VerifyResources();
                if (errors != 0) throw new InvalidDataException("Unity reported errors while loading official Map15 resources.");
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Official Map15 group-data Play Mode acceptance passed: 60 maps, 118 regions, 2,899 placements, 82 templates, entrance gate fields and all map resource closures.");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                SessionState.SetInt(Key + ".result", 1);
            }
            yield return null;
            EditorApplication.isPlaying = false;
        }

        private static void VerifyResources()
        {
            OfficialMap15GroupDataCatalog data = OfficialMap15GroupDataCatalog.LoadDefault();
            int placements = 0, regions = 0;
            int[] mapIds = new[] { 15 }.Concat(Enumerable.Range(42001, 53)).Concat(Enumerable.Range(42061, 6)).ToArray();
            foreach (int mapId in mapIds)
            {
                OfficialMap15GroupDataCatalog.MapFile map = data.LoadMap(mapId);
                if (map.map.ClassId != mapId || map.regionData.id != mapId)
                    throw new InvalidDataException("Official Map15 resource identity differs: " + mapId);
                placements += map.regionData.placement.entities.Length;
                regions += map.regions.Length;
            }
            if (placements != 2899 || regions != 118 || data.NextMap(15) != 42001)
                throw new InvalidDataException("Official Map15 group totals or entrance transition differ.");
            OfficialMap15GroupDataCatalog.MapFile entrance = data.LoadMap(15);
            if (entrance.regions.Length != 1 || entrance.regions[0].LimitServerRank != 5
                || entrance.regions[0].LimitServerRankUnlockDayOffset != 14)
                throw new InvalidDataException("Official Map15 entrance region release conditions differ.");
        }

        private sealed class Runner : MonoBehaviour { }
    }
}

using System;
using System.IO;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Presentation.Combat;
using Zjcs.Official.BattleMaterialPlayables;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleSummonGenerationValidation
    {
        [Serializable] private sealed class Result { public bool passed, appearance; public int scenes; public string error; }
        private const string ActiveKey = "Zjcs.SummonGeneration.Active";
        private const string ExitKey = "Zjcs.SummonGeneration.Exit";
        private const string PassedKey = "Zjcs.SummonGeneration.Passed";
        private static bool started;
        public static void Run()
        {
            AudioListener.volume = 0;
            BattleGameViewInput.Focus();
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Restore()
        { EditorApplication.update -= Update; EditorApplication.update += Update; }
        private static void Update()
        {
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                SessionState.SetBool(ExitKey, false);
                EditorApplication.Exit(SessionState.GetBool(PassedKey, false) ? 0 : 1);
            }
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying || started) return;
            AudioListener.volume = 0;
            Application.runInBackground = true;
            SessionState.SetInt(ActiveKey + ".capture", Time.captureFramerate);
            Time.captureFramerate = 60;
            started = true;
            new GameObject("Original summon lifecycle validation runner").AddComponent<Runner>();
        }
        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
            var result = new Result { appearance = Environment.GetEnvironmentVariable("ZJCS_SUMMON_APPEARANCE") == "1" };
            foreach (int entity in new[] { 3181, 3182, 3183 })
                foreach (bool restoring in new[] { false, true })
                {
                    var routine = CheckScene(entity, restoring);
                    while (true)
                    {
                        object current = null; bool more = false;
                        try { more = routine.MoveNext(); if (more) current = routine.Current; }
                        catch (Exception error) { result.error = error.ToString(); Debug.LogException(error); }
                        if (!more) break;
                        yield return current;
                    }
                    (routine as IDisposable)?.Dispose();
                    if (!string.IsNullOrEmpty(result.error)) { Complete(result); yield break; }
                    result.scenes++;
                }
            result.passed = true;
            Debug.Log("[Summon generation] Shared scene: original generation, settled idle, restored suppression, and later new summon passed for all models.");
            Complete(result);
            }
        }
        private static void Complete(Result result)
        {
            string directory = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(directory) || !Path.IsPathRooted(directory))
                throw new InvalidOperationException("Owned summon validation workspace is required.");
            File.WriteAllText(Path.Combine(directory, "summon-generation-results.json"), JsonUtility.ToJson(result, true));
            SessionState.SetBool(ActiveKey, false);
            Time.captureFramerate = SessionState.GetInt(ActiveKey + ".capture", 0);
            SessionState.SetBool(ExitKey, true);
            SessionState.SetBool(PassedKey, result.passed);
            EditorApplication.isPlaying = false;
        }

        private static IEnumerator CheckScene(int entityId, bool restoring)
        {
            var root = new GameObject("Original summon generation validation");
            using var scope = new OfflineBattleNativePlayableScope(root.transform, null);
            using var scene = new OfficialBattleSceneView(null, root.transform, _ => null,
                new OfficialBattleGridMapper(root.transform, new FightBattleGrid { Width = 8, Height = 8 }),
                (_, __) => "idle_01", (_, __) => { }, nativePlayableScope: scope, restoringNativeUnits: restoring);
            try
            {
                var state = new FightBattleCheckpoint();
                state.Units.Add(Unit(1, entityId));
                scene.Refresh(state);
                for (int i = 0; i < 6; i++) { Pump(); yield return null; }
                Require(Animation(1) == (restoring ? "idle_01" : "generate_01"),
                    "Initial original generation/restored idle differs: " + entityId + ", restoring=" + restoring + ", animation=" + Animation(1));
                for (int i = 0; i < 120; i++) { Pump(); yield return null; }
                Require(Animation(1) == "idle_01", "Original generation did not return to idle: " + entityId);
                state.Units.Add(Unit(2, entityId));
                scene.Refresh(state);
                for (int i = 0; i < 6; i++) { Pump(); yield return null; }
                Require(Animation(1) == "idle_01" && Animation(2) == "generate_01",
                    "Later new summon was suppressed by scene restoration or replayed the existing summon: " + entityId);
                for (int i = 0; i < 120; i++) { Pump(); yield return null; }
                Require(Animation(1) == "idle_01" && Animation(2) == "idle_01", "Original post-generation idle differs.");
                if (Environment.GetEnvironmentVariable("ZJCS_SUMMON_APPEARANCE") == "1")
                {
                    var materialCheck = CheckAppearance(scene, scope, state, entityId);
                    while (materialCheck.MoveNext()) yield return materialCheck.Current;
                }

                string Animation(int unitId)
                {
                    var unit = scene.TryGetUnitTransform(unitId);
                    Require(unit, "The original summon unit was not created: " + unitId);
                    var renderer = unit.GetComponentInChildren<BattleNativeMonsterRenderer>();
                    Require(renderer, "The scene did not select the full original summon renderer: " + entityId);
                    return renderer.Animation;
                }
                void Pump() { scope.Tick(1f / 60); scope.LateTick(); scene.Tick(1f / 60); }
            }
            finally { scene.Dispose(); scope.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
        }
        private static IEnumerator CheckAppearance(OfficialBattleSceneView scene, OfflineBattleNativePlayableScope scope,
            FightBattleCheckpoint state, int entityId)
        {
            var binding = BattleNativeMonsterAppearanceCatalog.Find(entityId);
            Require(binding != null && binding.Materials.Length == 1, "Original summon appearance is unbound.");
            var first = scene.TryGetUnitTransform(1);
            var second = scene.TryGetUnitTransform(2);
            var renderer = first.GetComponentInChildren<BattleNativeMonsterRenderer>();
            var otherRenderer = second.GetComponentInChildren<BattleNativeMonsterRenderer>();
            var property = first.GetComponent<MeshMaterialProperty>();
            var lite = renderer.GetComponent<AppearanceRendererLite>();
            Require(property && lite && lite.SkeletonRenderer == renderer && property.MeshRenderer == renderer.GetComponent<MeshRenderer>(),
                "Original root material property/appearance renderer binding differs.");
            var source = binding.Materials[0];
            var expectedTexture = binding.Textures.TextureByGuid(binding.Textures.TextureGuids.Single());
            Require(source.mainTexture != expectedTexture && renderer.GetComponent<MeshRenderer>().sharedMaterial.mainTexture == expectedTexture,
                "Original placeholder-to-Low texture loading failed.");
            var graph = Resources.Load<GameObject>("Combat/SummonRoleViews/Graphs/Prefabs/Playables/playable_die_1");
            Require(graph, "Original summon death graph is not packaged.");
            var original = graph.GetComponentsInChildren<MyPlayableMaterialProperty>(true).Single();
            string keyword = original.Keyword, parameter = original.FloatPropertyName;
            float baseline = source.GetFloat(parameter);
            bool baselineKeyword = source.IsKeywordEnabled(keyword);
            var playback = scope.Play(graph, first.gameObject, first.gameObject);
            try
            {
                for (int i = 0; i < 20; i++) { Pump(); yield return null; }
                var changed = renderer.GetComponent<MeshRenderer>().sharedMaterial;
                var unchanged = otherRenderer.GetComponent<MeshRenderer>().sharedMaterial;
                Require(changed && unchanged && changed != unchanged && changed != source,
                    "Original per-actor material isolation failed.");
                Require(changed.IsKeywordEnabled(keyword) == original.KeywordBoolValue
                    && Mathf.Abs(changed.GetFloat(parameter) - original.FloatCurve.Evaluate((20f / 60) / original.Duration)) < 0.05f,
                    "Original death material curve was not applied to the source actor.");
                Require(unchanged.IsKeywordEnabled(keyword) == baselineKeyword && Mathf.Approximately(unchanged.GetFloat(parameter), baseline)
                    && source.IsKeywordEnabled(keyword) == baselineKeyword && Mathf.Approximately(source.GetFloat(parameter), baseline),
                    "Death changed another actor or the packaged original material.");
                playback.Stop();
                for (int i = 0; i < 2; i++) { Pump(); yield return null; }
                Require(changed.IsKeywordEnabled(keyword) == baselineKeyword && Mathf.Approximately(changed.GetFloat(parameter), baseline),
                    "Stopping the original death graph did not restore material defaults.");
                state.Units.RemoveAt(0);
                scene.Refresh(state);
                for (int i = 0; i < 2; i++) { Pump(); yield return null; }
                Require(otherRenderer && otherRenderer.GetComponent<MeshRenderer>().sharedMaterial.mainTexture == expectedTexture,
                    "Disposing one actor released another actor's original appearance.");
                state.Units.Add(Unit(3, entityId));
                scene.Refresh(state);
                for (int i = 0; i < 6; i++) { Pump(); yield return null; }
                var recreated = scene.TryGetUnitTransform(3).GetComponentInChildren<BattleNativeMonsterRenderer>();
                Require(recreated && recreated.GetComponent<MeshRenderer>().sharedMaterial.mainTexture == expectedTexture,
                    "Original appearance could not be acquired after another actor was disposed.");
                Debug.Log("[Summon appearance] Original Low texture, death curve, independent material, stop restoration and concurrent disposal passed: " + entityId);
            }
            finally { if (playback) playback.Stop(); }
            void Pump() { scope.Tick(1f / 60); scope.LateTick(); scene.Tick(1f / 60); }
        }
        private static FightBattleUnitState Unit(int id, int entity) => new FightBattleUnitState
            { Id = id, MonsterId = entity, CharacterType = 3, Hp = 100, MaxHp = 100, X = id * 2, Y = 2 };
        private static void Require(bool value, string error)
        { if (!value) throw new InvalidOperationException(error); }
    }
}

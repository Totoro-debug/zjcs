using System;
using System.IO;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleSummonGenerationValidation
    {
        [Serializable] private sealed class Result { public bool passed; public int scenes; public string error; }
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
            var result = new Result();
            foreach (int entity in new[] { 3182, 3183 })
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
            Debug.Log("[Summon generation] Shared scene: original generation, settled idle, restored suppression, and later new summon passed for both models.");
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

                string Animation(int unitId) => scene.TryGetUnitTransform(unitId)
                    .GetComponentInChildren<BattleNativeMonsterRenderer>().Animation;
                void Pump() { scope.Tick(1f / 60); scope.LateTick(); scene.Tick(1f / 60); }
            }
            finally { scene.Dispose(); scope.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
        }
        private static FightBattleUnitState Unit(int id, int entity) => new FightBattleUnitState
            { Id = id, MonsterId = entity, CharacterType = 3, Hp = 100, MaxHp = 100, X = id * 2, Y = 2 };
        private static void Require(bool value, string error)
        { if (!value) throw new InvalidOperationException(error); }
    }
}

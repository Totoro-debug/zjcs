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
using Zjcs.Official.StoryPlayables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleSummonGenerationValidation
    {
        [Serializable] private sealed class Result { public bool passed, appearance, death; public int scenes; public string error; }
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
            var result = new Result { appearance = Environment.GetEnvironmentVariable("ZJCS_SUMMON_APPEARANCE") == "1",
                death = Environment.GetEnvironmentVariable("ZJCS_SUMMON_DEATH") == "1" };
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
                if (Environment.GetEnvironmentVariable("ZJCS_SUMMON_DEATH") == "1")
                {
                    var deathCheck = CheckDeath(scene, scope, state, entityId, restoring);
                    while (deathCheck.MoveNext()) yield return deathCheck.Current;
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
        private static IEnumerator CheckDeath(OfficialBattleSceneView scene, OfflineBattleNativePlayableScope scope,
            FightBattleCheckpoint state, int entityId, bool restoring)
        {
            var unit = state.Units.First();
            var other = state.Units.Last();
            var first = scene.TryGetUnitTransform(unit.Id);
            var renderer = first.GetComponentInChildren<BattleNativeMonsterRenderer>();
            var otherRenderer = scene.TryGetUnitTransform(other.Id).GetComponentInChildren<BattleNativeMonsterRenderer>();
            var source = BattleNativeMonsterAppearanceCatalog.Find(entityId).Materials.Single();
            var graph = Resources.Load<GameObject>("Combat/SummonRoleViews/Graphs/Prefabs/Playables/playable_die_1");
            var original = graph.GetComponentsInChildren<MyPlayableMaterialProperty>(true).Single();
            string parameter = original.FloatPropertyName, keyword = original.Keyword;
            float baseline = source.GetFloat(parameter);
            bool baselineKeyword = source.IsKeywordEnabled(keyword);
            using var capture = new DeathCapture(first.parent, renderer.GetComponent<MeshRenderer>(), otherRenderer.GetComponent<MeshRenderer>(),
                entityId + (restoring ? "-restore-" : "-new-"));
            capture.Save("alive");
            unit.Hp = 0;
            scene.Refresh(state);
            for (int i = 0; i < 20; i++) { Pump(); yield return null; }
            Require(scene.TryGetUnitTransform(unit.Id) == first && renderer.Animator.GetBool("die")
                && renderer.Animation == "die_01", "Actual summon death did not retain its source/Animator.");
            var material = renderer.GetComponent<MeshRenderer>().sharedMaterial;
            Require(material != source && material.IsKeywordEnabled(keyword) == original.KeywordBoolValue
                && Mathf.Abs(material.GetFloat(parameter) - original.FloatCurve.Evaluate((20f / 60) / original.Duration)) < .05f,
                "Actual summon death did not consume the original material curve.");
            Require(OtherUnchanged(), "Actual summon death changed a living instance or packaged material.");
            capture.Save("dying");
            unit.Hp = 100;
            scene.Refresh(state);
            for (int i = 0; i < 2; i++) { Pump(); yield return null; }
            Require(scene.TryGetUnitTransform(unit.Id) == first && !renderer.Animator.GetBool("die")
                && renderer.Animation == "idle_01" && Mathf.Approximately(material.GetFloat(parameter), baseline)
                && material.IsKeywordEnabled(keyword) == baselineKeyword, "Revival did not stop/restore the original summon death graph.");
            capture.Save("revived");
            unit.Hp = 0;
            scene.Refresh(state);
            for (int i = 0; i < 12; i++) { Pump(); yield return null; }
            ((IBattleSourceDeathView)scene).ForcePlayDiePlayable(first.gameObject);
            for (int i = 0; i < 6; i++) { Pump(); yield return null; }
            material = renderer.GetComponent<MeshRenderer>().sharedMaterial;
            Require(Mathf.Abs(material.GetFloat(parameter) - original.FloatCurve.Evaluate((6f / 60) / original.Duration)) < .05f,
                "ForcePlayDiePlayable did not restart the original summon material curve.");
            capture.Save("forced");
            for (int i = 0; i < 36; i++) { Pump(); yield return null; }
            Require(scene.TryGetUnitTransform(unit.Id) == first,
                "Summon visual retired before the selected original one-second delay.");
            for (int i = 0; i < 8; i++) { Pump(); yield return null; }
            Require(!first && !scene.TryGetUnitTransform(unit.Id) && otherRenderer && OtherUnchanged(),
                "Summon death did not retire at the selected delay, or disposed another instance.");
            capture.Save("retired");
            scene.Refresh(state);
            for (int i = 0; i < 2; i++) { Pump(); yield return null; }
            Require(!scene.TryGetUnitTransform(unit.Id), "Refreshing a retained dead checkpoint respawned a retired summon.");
            unit.Hp = 100;
            scene.Refresh(state);
            for (int i = 0; i < 6; i++) { Pump(); yield return null; }
            Require(scene.TryGetUnitTransform(unit.Id)?.GetComponentInChildren<BattleNativeMonsterRenderer>(),
                "A subsequent alive checkpoint could not reacquire the original summon model.");
            capture.Save("recreated");
            Debug.Log("[Summon death] Actual death/curve, revival restoration, forced replay without delaying retirement, "
                + "dead checkpoint suppression and subsequent alive reacquisition passed: " + entityId + ", restoring=" + restoring);
            bool OtherUnchanged()
            {
                var otherMaterial = otherRenderer.GetComponent<MeshRenderer>().sharedMaterial;
                return otherMaterial.IsKeywordEnabled(keyword) == baselineKeyword
                    && Mathf.Approximately(otherMaterial.GetFloat(parameter), baseline)
                    && source.IsKeywordEnabled(keyword) == baselineKeyword && Mathf.Approximately(source.GetFloat(parameter), baseline);
            }
            void Pump() { scope.Tick(1f / 60); scope.LateTick(); scene.Tick(1f / 60); }
        }
        private sealed class DeathCapture : IDisposable
        {
            private readonly RenderPipelineAsset previousDefault = GraphicsSettings.defaultRenderPipeline;
            private readonly RenderPipelineAsset previousQuality = QualitySettings.renderPipeline;
            private readonly UniversalRenderPipelineAsset pipeline;
            private readonly Renderer2DData rendererData;
            private readonly GameObject root;
            private readonly RenderTexture target;
            private readonly Camera camera;
            private readonly string prefix;
            internal DeathCapture(Transform parent, Renderer first, Renderer other, string prefix)
            {
                this.prefix = prefix;
                rendererData = ScriptableObject.CreateInstance<Renderer2DData>();
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
                root = new GameObject("Original summon death render validation"); root.transform.SetParent(parent, false);
                var light = root.AddComponent<Light2D>(); light.lightType = Light2D.LightType.Global; light.intensity = 1;
                camera = new GameObject("Original summon death camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                target = new RenderTexture(450, 600, 24); target.Create(); camera.targetTexture = target;
                camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.65f, .7f, .75f);
                var bounds = first.bounds; bounds.Encapsulate(other.bounds);
                camera.transform.position = bounds.center + new Vector3(0, 0, -10);
                camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.2f;
            }
            internal void Save(string stage)
            {
                camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = target;
                var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                try
                {
                    pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                        "death-" + prefix + stage + ".png"), pixels.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(pixels); }
            }
            public void Dispose()
            {
                GraphicsSettings.defaultRenderPipeline = previousDefault; QualitySettings.renderPipeline = previousQuality;
                camera.targetTexture = null; target.Release();
                UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(pipeline); UnityEngine.Object.Destroy(rendererData);
            }
        }
        private static void Require(bool value, string error)
        { if (!value) throw new InvalidOperationException(error); }
    }
}

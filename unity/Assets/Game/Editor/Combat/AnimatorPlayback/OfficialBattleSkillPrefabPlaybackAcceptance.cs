using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    [InitializeOnLoad]
    public static class OfficialBattleSkillPrefabPlaybackAcceptance
    {
        private const string Key = "Zjcs.NativeSkillPrefabAcceptance";
        static OfficialBattleSkillPrefabPlaybackAcceptance()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                    new GameObject("Original skill prefab validation").AddComponent<NativeSkillPrefabAcceptanceRunner>();
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key, false);
                    EditorApplication.delayCall += () => EditorApplication.Exit(SessionState.GetInt(Key + ".exit", 1));
                }
            };
        }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetInt(Key + ".exit", 1); SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }
        internal static void Finish(string failure)
        {
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                "native-skill-prefab-result.json"), JsonUtility.ToJson(new Result { passed = failure == null, error = failure }, true));
            SessionState.SetInt(Key + ".exit", failure == null ? 0 : 1);
            EditorApplication.isPlaying = false;
        }
        [Serializable] private sealed class Result { public bool passed; public string error; }
    }

    public sealed class NativeSkillPrefabAcceptanceRunner : MonoBehaviour
    {
        private OfficialBattleSkillEntityPlayback views;
        private OfflineBattleNativePlayableScope scope;
        private IDisposable pipeline;
        private Camera cameraView;
        private string failure;
        private readonly List<string> played = new List<string>();

        private IEnumerator Start()
        {
            Application.logMessageReceived += Observe;
            var check = Check();
            while (true)
            {
                object current;
                try { if (!check.MoveNext()) break; current = check.Current; }
                catch (Exception error) { failure = error.ToString(); break; }
                yield return current;
            }
            try { views?.Dispose(); }
            catch (Exception error) { failure = failure ?? error.ToString(); }
            views = null; scope = null;
            try { pipeline?.Dispose(); }
            catch (Exception error) { failure = failure ?? error.ToString(); }
            Application.logMessageReceived -= Observe;
            OfficialBattleSkillPrefabPlaybackAcceptance.Finish(failure);
        }

        private IEnumerator Check()
        {
            pipeline = Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets").Enter();
            Time.maximumDeltaTime = 1f / 60f;
            cameraView = new GameObject("Native skill camera").AddComponent<Camera>();
            cameraView.transform.position = new Vector3(5, 5, -20);
            cameraView.orthographic = true; cameraView.orthographicSize = 6;
            cameraView.clearFlags = CameraClearFlags.SolidColor;
            cameraView.backgroundColor = new Color(.06f, .09f, .15f);
            var root = new GameObject("Native skill coordinates").transform;
            var source = new GameObject("Source").transform; source.SetParent(root, false);
            source.localPosition = new Vector3(4, 5, 0); source.gameObject.AddComponent<MyTransform>();
            var target = new GameObject("Target").transform; target.SetParent(root, false);
            target.localPosition = new Vector3(6, 5, 0); target.gameObject.AddComponent<MyTransform>();
            var actor = new FightBattleUnitState { Id = 1, X = 4, Y = 5, Direction = 3,
                CharacterType = 1, Hp = 100, MaxHp = 100, HasFlipX = true };
            var victim = new FightBattleUnitState { Id = 2, X = 6, Y = 5, CharacterType = 1,
                Direction = 1, Hp = 100, MaxHp = 100 };
            scope = new OfflineBattleNativePlayableScope(root, null);
            scope.BindNativePrefabAdapter(instance =>
            {
                foreach (var playable in instance.GetComponentsInChildren<MyPlayable>(true))
                    playable.OnPlay += value => played.Add(value.gameObject.name.Replace("(Clone)", ""));
            });
            views = new OfficialBattleSkillEntityPlayback(root,
                new OfficialBattleGridMapper(root, new FightBattleGrid { Width = 16, Height = 16 }), scope,
                id => id == 1 ? actor : id == 2 ? victim : null);
            MyFightSkill StartCast(int classId, float duration)
            {
                var cast = new FightBattleSkillCastEvent { ActorId = 1, Slot = -1, SkillEntityId = classId,
                    Presentation = FightBattleSkillCastPresentation.Capture(actor,
                        new FightBattleSkillDefinition { EntityId = classId, Slot = -1, Duration = duration,
                            SkillTargetType = 1 }, new List<FightBattleUnitState> { victim }) };
                // Production completes the captured timing after resolving the
                // cast. This isolated view fixture supplies that resolved value.
                cast.Presentation.Duration = duration;
                views.Start(cast, source, target);
                return root.GetComponentsInChildren<MyFightSkill>().Single(value => value.Component != null
                    && ReferenceEquals(value.Component.Entity.GetComponent<FightSkillComponent>(), value.Component)
                    && value.myEntity.Entity.ClassId == classId && value.gameObject.activeInHierarchy);
            }
            var original = Resources.Load<GameObject>("Combat/SkillGraphs/Prefabs/EcEntity/Fight/FightSkill/13200/13200");
            var options = (List<PlayableOverrideInfo>)typeof(MyPlayableOverrider)
                .GetField("mPlayableOverrideInfos", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(original.GetComponent<MyPlayableOverrider>());
            Require(options.Count == 1 && options[0].Playables.Count == 11,
                "Original 13200 ordered override graph list changed.");
            var status = new FightBattleStatusState { InstanceId = 1, ClassId = 24183, StackCount = 5 };
            actor.Statuses.Add(status);
            var first = StartCast(13200, 1.5f);
            Require(first.myPlayablePrefab == options[0].Playables[5],
                "Original skill prefab did not select its stack-five graph before OnStart.");
            yield return new WaitForSeconds(1f);
            Capture("skill-prefab-stack-five.png");
            yield return new WaitForSeconds(.6f);
            Require(!first.gameObject.activeSelf, "Finite original skill did not return to its FIFO pool.");
            status.StackCount = 10;
            var second = StartCast(13200, 1.5f);
            Require(second == first && second.myPlayablePrefab == options[0].Playables[10],
                "Pooled original skill did not restore and replace the next cast graph.");
            actor.Statuses.Clear();
            Require(second.GetComponent<PlayableOverrideByStatusStack>().GetOverridePlayable(options[0])
                == options[0].Playables[10], "Settled status removal mutated the cast snapshot.");
            yield return new WaitForSeconds(1f);
            Capture("skill-prefab-stack-ten.png");
            yield return new WaitForSeconds(.6f);
            var third = StartCast(13200, 1.5f);
            Require(third == first && third.myPlayablePrefab == original.GetComponent<MyFightSkill>().myPlayablePrefab,
                "Pooled original skill retained an earlier cast override after status removal.");
            yield return new WaitForSeconds(1.6f);

            // Shared 24029 controls the original ten offset entries on these skills.
            actor.Statuses.Add(new FightBattleStatusState { InstanceId = 2, ClassId = 24166,
                StackCount = 3, SharedStackId = 24029, SharedStackLimit = 10 });
            var delayed = StartCast(12030, .25f);
            var delay = delayed.GetComponent<MyFightHitExtDelayInfoOverriderByStatusStack>();
            Require(delay && delay.Component != null && delay.GetOverriderIndex() == 2
                && delay.Info.Offsets.Count == 10, "Original delay override did not consume captured shared stacks/configuration.");
            actor.Statuses.Clear();
            Require(delay.GetOverriderIndex() == 2, "Delay override read settled source state instead of its cast snapshot.");
            yield return new WaitForSeconds(.3f);
            var reusedDelay = StartCast(12030, .2f).GetComponent<MyFightHitExtDelayInfoOverriderByStatusStack>();
            Require(reusedDelay == delay && delay.GetOverriderIndex() == -1,
                "Reused original delay override retained the preceding cast's shared stacks.");
            yield return new WaitForSeconds(.25f);

            int before = played.Count;
            var special = StartCast(54383, 1.5f);
            var driver = special.GetComponentInChildren<ECViewDriver>();
            Require(driver && driver.GetView<ECPlayableSupporterView>() != null,
                "Original serialized special skill view was not loaded.");
            yield return new WaitForSeconds(.75f);
            Require(played.Skip(before).Any(value => value.Contains("54380")),
                "Original ECPlayableSupporterView did not play its appear graph.");
            Capture("skill-prefab-managed-appear.png");
            yield return new WaitForSeconds(.85f);
            Require(!special.gameObject.activeSelf, "Special original skill did not remove and pool its managed view.");
            before = played.Count;
            Require(StartCast(54383, 1.5f) == special, "Original managed skill root was not reused from its FIFO pool.");
            yield return new WaitForSeconds(.75f);
            Require(played.Skip(before).Any(value => value.Contains("54380")),
                "Reused original managed view did not register and replay its appear graph.");
            yield return new WaitForSeconds(.85f);
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                "skill-prefab-played-graphs.json"), JsonUtility.ToJson(new Played { graphs = played.ToArray() }, true));
        }
        [Serializable] private sealed class Played { public string[] graphs; }
        private void Update() { views?.Tick(Time.deltaTime); }
        private void LateUpdate() { scope?.LateTick(); }
        private void Observe(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failure = failure ?? text + "\n" + stack; }
        private void Capture(string name)
        {
            var texture = new RenderTexture(540, 960, 24);
            var previous = RenderTexture.active; var target = cameraView.targetTexture;
            try
            {
                cameraView.targetTexture = texture; cameraView.Render(); RenderTexture.active = texture;
                var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), image.EncodeToPNG());
                    Require(image.GetPixels32().Select(color => ((int)color.r << 16) | ((int)color.g << 8) | color.b)
                        .Distinct().Take(5).Count() >= 5, "Original skill capture is blank: " + name);
                }
                finally { Destroy(image); }
            }
            finally { RenderTexture.active = previous; cameraView.targetTexture = target; Destroy(texture); }
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}

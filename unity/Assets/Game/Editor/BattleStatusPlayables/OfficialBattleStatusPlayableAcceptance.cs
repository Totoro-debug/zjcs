using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleStatusPlayableAcceptance
    {
        private const string Key = "Zjcs.Battle.StatusPlayableAcceptance";
        private const string Effect = "Combat/GroundEffects/Prefabs/EcEntity/Fight/FightSkill/Move/Effect/Eff_Role_Wave";
        private static bool started;
        private static int errors;

        public static void Run()
        {
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }

        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Status playable verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            Application.logMessageReceived += Observe;
            new GameObject("Status playable verification").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static IEnumerator Verify()
        {
            using (var scope = new Scope())
            {
                IEnumerator steps = scope.Steps();
                while (true)
                {
                    object next;
                    try { if (!steps.MoveNext()) break; next = steps.Current; }
                    catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                    yield return next;
                }
                if (errors == 0)
                {
                    SessionState.SetInt(Key + ".result", 0);
                    Debug.Log("Original status playable integration passed: real manager/pools/center, stack begin/stay/end order, deferred stops, retained stack history, source/target and group partitioning, equal priorities, strongest eligible resume, non-resuming interruption and native particle raster.");
                }
            }
            EditorApplication.isPlaying = false;
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Status playable: " + message); }

        private sealed partial class Scope : IDisposable, IStoryPlayableInstanceFactory
        {
            private readonly GameObject root = new GameObject("Status playable scope");
            private readonly MyPlayableCenter center = new MyPlayableCenter();
            private readonly MyPlayableManager manager;
            private readonly Func<Transform, IStoryPlayableInstanceFactory> previousFactory;
            private readonly List<string> events = new List<string>();
            private readonly List<GameObject> templates = new List<GameObject>();
            private readonly List<ProbePlayable> instances = new List<ProbePlayable>();
            private readonly Camera camera;
            private readonly GameObject sourceA, sourceB;

            public Scope()
            {
                root.SetActive(true);
                sourceA = new GameObject("Status owner A"); sourceA.transform.SetParent(root.transform, false);
                sourceA.transform.position = new Vector3(-1.1f, 0, 0);
                sourceB = new GameObject("Status owner B"); sourceB.transform.SetParent(root.transform, false);
                sourceB.transform.position = new Vector3(1.1f, 0, 0);
                camera = new GameObject("Status particle verification camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false); camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 2;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .06f, .06f);
                camera.gameObject.AddComponent<AudioListener>();
                previousFactory = StoryPlayableInstantiation.Resolve;
                StoryPlayableInstantiation.Resolve = _ => this;
                manager = new MyPlayableManager { ScopeRoot = root, MyPlayableCenter = center,
                    InstanceFactory = this, LowMemory = new GAction() };
                manager.Initialize();
            }

            public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
            {
                var instance = UnityEngine.Object.Instantiate(prefab, parent, worldPositionStays);
                var playable = instance.GetComponent<ProbePlayable>();
                playable.MyPlayableCenter = center;
                playable.Record = events.Add;
                var priority = instance.GetComponent<ApplyEffectByPriority>();
                if (priority != null) priority.MyPlayableCenter = center;
                instances.Add(playable);
                return instance;
            }

            private GameObject Template(string name, float duration, int priority = 0,
                string group = null, bool resume = true, EAttachTo attach = EAttachTo.Source)
            {
                var template = new GameObject(name); template.SetActive(false);
                template.transform.SetParent(root.transform, false);
                GameObject native = Resources.Load<GameObject>(Effect);
                Require(native != null, "Original native wave effect is missing.");
                var effect = UnityEngine.Object.Instantiate(native, template.transform, false);
                var playable = template.AddComponent<ProbePlayable>();
                playable.Id = name; playable.Duration = duration; playable.myApplyComponent = effect.transform;
                playable.myTriggerList = new List<MyPlayableTriggerTimeCfg>();
                playable.myFadeoutTrigerList = new List<MyPlayableTrigger>();
                playable.myEndTrigerList = new List<MyPlayableTrigger>();
                if (group != null)
                {
                    var component = template.AddComponent<ApplyEffectByPriority>();
                    component.myAttach = new AttachPosRotCfg { AttachTo = attach };
                    var serialized = new SerializedObject(component);
                    serialized.FindProperty("mPriorityID").stringValue = group;
                    serialized.FindProperty("mPriority").intValue = priority;
                    serialized.FindProperty("mAutoResume").boolValue = resume;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                templates.Add(template);
                return template;
            }

            private ProbePlayable Play(GameObject prefab, GameObject source, GameObject target = null)
            { return (ProbePlayable)manager.Play(prefab, source, target ?? source); }

            private void Flush()
            { center.Update(.02f); center.LateUpdate(); }

            private static bool Visible(ProbePlayable playable)
            { return playable != null && playable.myApplyComponent.gameObject.activeInHierarchy; }

            private void Capture(string name)
            {
                string directory = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                Require(!string.IsNullOrEmpty(directory), "Validation workspace is unbound.");
                var target = new RenderTexture(675, 400, 24);
                var previous = RenderTexture.active;
                var previousTarget = camera.targetTexture;
                var image = new Texture2D(675, 400, TextureFormat.RGBA32, false);
                try
                {
                    camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 675, 400), 0, 0); image.Apply();
                    int visible = image.GetPixels32().Count(pixel => pixel.r > 40 || pixel.g > 40 || pixel.b > 40);
                    Require(visible > 20, "Original particle raster is empty: " + name + ", pixels=" + visible);
                    File.WriteAllBytes(Path.Combine(directory, "status-playable-" + name + ".png"), image.EncodeToPNG());
                    Debug.Log("[Status playable raster] " + name + " visible=" + visible);
                }
                finally
                {
                    camera.targetTexture = previousTarget; RenderTexture.active = previous;
                    UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target);
                }
            }

            public IEnumerator Steps()
            {
                GameObject lowPrefab = Template("low", -1, 1, "status"),
                    middlePrefab = Template("middle", -1, 2, "status"),
                    highPrefab = Template("high", -1, 3, "status");
                var low = Play(lowPrefab, sourceA);
                var otherTarget = Play(highPrefab, sourceB);
                var otherGroup = Play(Template("other-group", -1, 99, "another"), sourceA);
                yield return null; Flush();
                Require(Visible(low) && Visible(otherTarget) && Visible(otherGroup), "Different source targets/groups interfered.");
                yield return new WaitForSeconds(.08f); Capture("independent");
                var middle = Play(middlePrefab, sourceA);
                var high = Play(highPrefab, sourceA);
                yield return null; Flush();
                Require(!Visible(low) && !Visible(middle) && Visible(high) && Visible(otherTarget), "Higher priority did not hide only its attachment group.");
                var equal = Play(highPrefab, sourceA);
                high.Stop(); yield return null; Flush();
                Require(!Visible(middle) && Visible(equal), "A surviving equal priority allowed a premature resume.");
                equal.Stop(); yield return null; Flush();
                Require(Visible(middle) && !Visible(low), "Strongest eligible lower priority was not resumed exclusively.");
                middle.Stop(); yield return null; Flush();
                Require(Visible(low), "Lowest effect failed to resume after its last superior stopped.");
                yield return new WaitForSeconds(.08f); Capture("resumed");

                // AttachTo chooses identity independently of the source object.
                var targetHigh = Play(Template("target-high", -1, 4, "status", true, EAttachTo.Target), sourceB, sourceA);
                yield return null; Flush();
                Require(!Visible(low) && Visible(otherTarget) && Visible(targetHigh), "Target attachment identity was compared as source identity.");
                var arrivingLow = Play(lowPrefab, sourceA);
                Require(!Visible(arrivingLow), "A new lower-priority effect escaped an already playing superior.");
                targetHigh.Stop(); yield return null; Flush();
                Require(Visible(low) && Visible(arrivingLow), "Equal lower-priority candidates did not resume together.");

                var terminal = Play(Template("terminal-low", -1, 1, "terminal", false), sourceA);
                var terminalHigh = Play(Template("terminal-high", -1, 2, "terminal"), sourceA);
                yield return null; Flush();
                Require(terminal.myStatus == MyPlayable.PlayableStatus.Stop && !terminal.gameObject.activeSelf,
                    "An interrupted non-resuming playable did not return to its real pool.");
                terminalHigh.Stop(); yield return null; Flush();
                Require(!terminal.gameObject.activeSelf, "A permanently interrupted effect was resurrected.");

                manager.RemoveAllPlayable(true); center.RemoveAll();
                yield return null;
                events.Clear();
                var begin = Template("begin", .01f);
                var stayA = Template("stay-A", -1);
                var stayB = Template("stay-B", -1);
                var endA = Template("end-A", .01f);
                var endB = Template("end-B", .01f);
                var stack = new MyPlayableStackPlay(manager);
                stack.Play(2, begin, stayA, endA, sourceA, sourceB);
                Require(events.SequenceEqual(new[] { "play:begin", "play:stay-A" }), "Stack increase did not start begin before stay.");
                yield return null; Flush();
                events.Clear();
                stack.Play(1, begin, stayB, endB, sourceA, sourceB);
                Require(events.Take(2).SequenceEqual(new[] { "play:end-A", "play:stay-B" }), "Stack decrease did not use the prior range's end before the new stay.");
                Require(!events.Contains("play:begin"), "A stack decrease played the increase transition.");
                yield return null; Flush();
                Require(events.Contains("stop:stay-A"), "Prior stay never completed its deferred stop through the center.");
                events.Clear(); stack.Stop(0, sourceA, sourceB);
                Require(events.Contains("play:end-B"), "Status removal did not play the current range's end.");
                yield return null; Flush();
                events.Clear();
                stack.Play(1, begin, stayA, endA, sourceA, sourceB);
                Require(events.SequenceEqual(new[] { "play:stay-A" }), "Stopping incorrectly discarded the original retained stack history.");
                stack.Stop(0, sourceA, sourceB); yield return null; Flush();
                Require(instances.Where(value => value != null && value.myStatus != MyPlayable.PlayableStatus.Stop)
                    .All(value => value.mySource == sourceA && value.myTarget == sourceB), "A pooled transition lost its source/target arguments.");
                manager.RemoveAllPlayable(true); center.RemoveAll(); yield return null;
                Require(instances.All(value => value == null || !value.gameObject.activeInHierarchy), "Final manager removal left live pooled visuals.");
                IEnumerator lifecycle = StatusViewSteps();
                while (lifecycle.MoveNext()) yield return lifecycle.Current;
            }

            public void Dispose()
            {
                manager.RemoveAllPlayable(true); center.RemoveAll(); manager.Dispose();
                StoryPlayableInstantiation.Resolve = previousFactory;
                UnityEngine.Object.Destroy(root);
            }
        }

        private sealed class Runner : MonoBehaviour { }

        public sealed partial class ProbePlayable : MyPlayable
        {
            public string Id;
            public float Duration;
            public Action<string> Record;
            public override float GetDuration() => Duration;
            protected override void PlayImpl()
            { transform.position = mySource.transform.position; Record?.Invoke("play:" + Id); }
            protected override void StopImpl()
            { Record?.Invoke("stop:" + Id); base.StopImpl(); }
        }
    }
}

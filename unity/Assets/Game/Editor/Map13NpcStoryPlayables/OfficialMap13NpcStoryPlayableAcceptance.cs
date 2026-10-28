using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.ProfessionUI;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Editor.Map13NpcStoryPlayables
{
    // This checks the original prefab scheduler and source parameter boundary.
    // Plain controller/transforms are test fixtures, not replacement NPC art.
    [InitializeOnLoad]
    public static class OfficialMap13NpcStoryPlayableAcceptance
    {
        private const string Key = "Map13.OriginalNpcStoryPlayables";
        private const string CatalogPath = "Assets/Game/Resources/Map13/Native/native-npc-story-playables-catalog.asset";
        private static bool started, failed;
        [Serializable] private sealed class Receipt { public Entry[] prefabs; }
        [Serializable] private sealed class Entry { public string guid, nativePath; }
        private static string Output => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ?? throw new InvalidDataException("Missing owned validation workspace.");
        static OfficialMap13NpcStoryPlayableAcceptance()
        {
            Application.logMessageReceived += (message, trace, type) => { if (SessionState.GetBool(Key, false)) Observe(message, trace, type); };
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
                SessionState.SetBool(Key, false); EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
            };
        }
        public static void Run()
        {
            try
            {
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                if (!Directory.Exists(Output)) throw new InvalidDataException("Validation workspace does not exist.");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT") ?? throw new InvalidDataException("Missing repository binding.");
                var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(repository, "assets/reference/official-map/map13-native-npc-story-playables-source-manifest.json")));
                Require(receipt.prefabs.Length == 8, "Original eight-prefab closure");
                var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(CatalogPath);
                if (!catalog) { catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
                catalog.Bindings = receipt.prefabs.Select(row => new NativeMap13AssetCatalog.Binding { OfficialPath = row.guid, Asset = AssetDatabase.LoadAssetAtPath<GameObject>(row.nativePath.Substring("unity/".Length)) }).ToArray();
                foreach (var binding in catalog.Bindings)
                {
                    Require(binding.Asset, "Original prefab import " + binding.OfficialPath);
                    foreach (var node in ((GameObject)binding.Asset).GetComponentsInChildren<Transform>(true))
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0, "Original component binding " + node.name);
                }
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
                SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
                SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 240);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0)) { Debug.LogError("Map13 NPC prefab playback timed out."); EditorApplication.Exit(1); return; }
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; new GameObject("Original NPC parameter boundary verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException("Map13 original NPC playable: " + message); }
        private static void Observe(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true; }
        private static IEnumerator Verify()
        {
            var steps = Steps();
            while (true)
            {
                object value;
                try { if (!steps.MoveNext()) break; value = steps.Current; }
                catch (Exception error) { failed = true; Debug.LogException(error); break; }
                yield return value;
            }
            if (!failed) { SessionState.SetInt(Key + ".result", 0); Debug.Log("Map13 original eight NPC story prefabs passed native import, original source lookup, animation bool application/restoration, UI-bone/root visibility, sound-event dispatch, delayed sequence scheduling and replay. Isolated parameter controllers/transforms only; actual NPC appearance, audible event media, complete story graph and Player remain pending."); }
            EditorApplication.isPlaying = false;
        }
        private static IEnumerator Steps()
        {
            Application.runInBackground = true;
            var catalog = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-npc-story-playables-catalog");
            var sound = new SoundProbe(); var oldSound = OfficialWwiseEvent.Host; OfficialWwiseEvent.Host = sound;
            foreach (var binding in catalog.Bindings)
            {
                var prefab = (GameObject)binding.Asset;
                var source = new GameObject("Isolated original source parameter boundary");
                var ui = new GameObject("UI"); ui.transform.SetParent(source.transform, false);
                var agent = source.AddComponent<AttachAgent>(); agent.myBoneNameList.Add("UI"); agent.myBoneList.Add(ui.transform); agent.Init();
                var animator = source.AddComponent<Animator>(); var controller = new AnimatorController(); controller.AddLayer("Base Layer");
                foreach (string name in new[] { "100/show_02", "run", "idle_01", "collect_06" }) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
                var state = controller.layers[0].stateMachine.AddState("Parameter boundary"); controller.layers[0].stateMachine.defaultState = state;
                animator.runtimeAnimatorController = controller; animator.Update(0);
                var animation = source.AddComponent<AnimationController>(); animation.myAnimator = animator; animation.NotCreateGraph = true; animation.Init();
                var instance = UnityEngine.Object.Instantiate(prefab); var sequence = instance.GetComponent<MyPlayableSequence>(); var center = new MyPlayableCenter();
                foreach (var playable in instance.GetComponentsInChildren<MyPlayable>(true)) playable.MyPlayableCenter = center;
                var leaf = instance.GetComponentInChildren<MyPlayableAnimator>(true);
                for (int replay = 0; replay < 2; replay++)
                {
                    source.SetActive(prefab.name != "playable_npc_active");
                    sequence.Init(source, null); sequence.TryPlay();
                    if (leaf) Require(leaf.myApplyComponent == animation && animator.GetBool(leaf.myBoolParam), "Original animation source and bool " + prefab.name);
                    if (prefab.name == "playable_npc_hideUI") Require(!ui.activeSelf && source.activeSelf, "Original UI-only bone hide");
                    if (prefab.name == "playable_npc_hide") Require(!source.activeSelf, "Original root hide");
                    if (prefab.name == "playable_npc_active") Require(source.activeSelf, "Original root activate");
                    for (int frame = 0; frame < 150; frame++) { yield return null; center.Update(1f / 60); center.LateUpdate(); }
                    sequence.Stop(true); yield return null; center.Update(0); center.LateUpdate();
                    if (leaf) Require(!animator.GetBool(leaf.myBoolParam), "Original animation bool restore " + prefab.name);
                    if (prefab.name == "playable_npc_hideUI") Require(ui.activeSelf, "Original UI bone restore");
                    sequence.ResetData();
                }
                center.RemoveAll(); UnityEngine.Object.Destroy(instance); UnityEngine.Object.Destroy(source); UnityEngine.Object.Destroy(controller); yield return null;
            }
            Require(sound.Events.Count == 2 && sound.Events[0] == sound.Events[1], "Original hide sound dispatch on both replays");
            OfficialWwiseEvent.Host = oldSound;
            File.WriteAllText(Path.Combine(Output, "npc-story-original-events.json"), JsonUtility.ToJson(new Events { ids = sound.Events.ToArray() }, true));
        }
        [Serializable] private sealed class Events { public uint[] ids; }
        private sealed class SoundProbe : IProfessionSoundHost
        {
            public readonly List<uint> Events = new List<uint>();
            public uint PostEvent(uint id, GameObject emitter) { Events.Add(id); return (uint)Events.Count; }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}

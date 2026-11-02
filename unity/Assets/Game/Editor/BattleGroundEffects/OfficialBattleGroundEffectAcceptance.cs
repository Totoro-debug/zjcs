using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.StoryGroundEffects;
using Zjcs.Official.StoryPlayables;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static class OfficialBattleGroundEffectAcceptance
    {
        private const string Key = "Zjcs.Battle.GroundEffectAcceptance";
        private const string Smoke = "4c9380a799575474285cd55a3759e8dc";
        private const string Wave = "f952ca09f5d495d4c89a343db347cf9d";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            // Existing save-root switch redirects startup reads/writes to this
            // validation workspace without starting the save-selection probe.
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 240);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe; SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Original battle ground effect verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original battle ground effect verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var steps = Steps();
            try
            {
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
                    Debug.Log("Original battle ground effect Play Mode passed: native prefab/particle/material/shader binding, terrain selection/IgnoreKey/invalid entries, original local-position sampling, source/target attachment, native FollowBone transform, terrain change/restart, stop/deactivation, handle destruction and independent hosts. Complete movement graphs and battle-host integration remain pending.");
                }
                EditorApplication.isPlaying = false;
            }
            finally { (steps as IDisposable)?.Dispose(); }
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original battle ground effect: " + message); }
        private static void Set(object target, string name, object value)
        { target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value); }
        private static void Stop(MyPlayable playable)
        {
            playable.Stop(true);
            // Original Stop queues termination; the center completes it in
            // the frame/post-frame phases and then invokes OnStop.
            var center = (MyPlayableCenter)playable.MyPlayableCenter;
            center.Update(0f); center.LateUpdate();
        }
        private sealed class Surface : IPlayableGroundSurface
        {
            public GroundType Type;
            public Vector3 Position;
            public GroundType GetGroundType(Vector3 position) { Position = position; return Type; }
        }
        private sealed class Host : IPlayableGroundEffectHost
        {
            private readonly OfficialBattleGroundEffectLibrary library;
            public readonly Surface Ground = new Surface();
            public readonly List<GameObject> Instances = new List<GameObject>();
            public readonly List<string> Guids = new List<string>();
            public int Disposed;
            public Host() { library = new OfficialBattleGroundEffectLibrary(() => Ground); }
            public IPlayableGroundSurface ResolveGround() => library.ResolveGround();
            public IPlayableGroundEffectAsset LoadAssetByGuid(string guid)
            { Guids.Add(guid); return new Lease(this, library.LoadAssetByGuid(guid)); }
            public GameObject Instantiate(GameObject prefab)
            { var value = library.Instantiate(prefab); Instances.Add(value); return value; }
            private sealed class Lease : IPlayableGroundEffectAsset
            {
                private Host owner;
                private readonly IPlayableGroundEffectAsset inner;
                public Lease(Host host, IPlayableGroundEffectAsset asset) { owner = host; inner = asset; }
                public GameObject Result => inner.Result;
                public void Dispose() { if (owner == null) return; inner.Dispose(); owner.Disposed++; owner = null; }
            }
        }
        private static ApplyByGroundType Configure(MyPlayable playable, Host host, Transform slot,
            bool automatic, EAttachTo attach = EAttachTo.Source)
        {
            playable.MyPlayableCenter = new MyPlayableCenter(); playable.myEndDestroy = false;
            playable.myTriggerList = new List<MyPlayableTriggerTimeCfg>();
            playable.myFadeoutTrigerList = new List<MyPlayableTrigger>(); playable.myEndTrigerList = new List<MyPlayableTrigger>();
            var apply = playable.gameObject.AddComponent<ApplyByGroundType>(); apply.Host = host;
            var entries = new MonoDictionary<GroundType, ApplyByGroundType.AssetGuidAndSlotProxy>();
            foreach (var pair in new[] { (GroundType.None, Smoke), (GroundType.Shallow, Wave), (GroundType.IgnoreKey, Smoke) })
            {
                var proxy = JsonUtility.FromJson<AssetGuidProxy<GameObject>>("{\"mAssetGuid\":\"" + pair.Item2 + "\"}");
                object boxed = new ApplyByGroundType.AssetGuidAndSlotProxy();
                Set(boxed, "<AssetGuidProxy>k__BackingField", proxy); Set(boxed, "<Slot>k__BackingField", slot);
                entries.Add(pair.Item1, (ApplyByGroundType.AssetGuidAndSlotProxy)boxed);
            }
            entries.Add(GroundType.Wall, default);
            Set(apply, "mGroundTypeAssetToApplyDict", entries); Set(apply, "mAttachType", attach);
            Set(apply, "mIsAutoUpdateApply", automatic); Set(apply, "mUpdateInterval", .05f);
            return apply;
        }
        private static IEnumerator Steps()
        {
            var root = new GameObject("Ground effect original integration fixture");
            var parent = new GameObject("Nonidentity map parent"); parent.transform.SetParent(root.transform, false);
            parent.transform.position = new Vector3(12, -7, 0); parent.transform.localScale = new Vector3(2, 3, 1);
            var source = new GameObject("Source"); source.transform.SetParent(parent.transform, false); source.transform.localPosition = new Vector3(1, 2, 0);
            var target = new GameObject("Target"); target.transform.SetParent(parent.transform, false); target.transform.localPosition = new Vector3(-3, 4, 0);
            var host = new Host(); var go = new GameObject("Original ground-selected playable"); go.SetActive(false); go.transform.SetParent(root.transform, false);
            var playable = go.AddComponent<IndefinitePlayable>(); Configure(playable, host, go.transform, true); go.SetActive(true);
            playable.Init(source, target); playable.TryPlay();
            Require(host.Guids.Count == 1 && host.Guids[0] == Smoke && host.Ground.Position == source.transform.localPosition, "original ground0 and attachment local position");
            Require(playable.myApplyComponent == host.Instances[0].transform && host.Instances[0].transform.parent == go.transform, "original slot and ApplyUtils binding");
            yield return new WaitForSeconds(.08f);
            Require(host.Guids.Count == 1, "unchanged terrain does not instantiate again");
            host.Ground.Type = GroundType.Shallow; yield return new WaitForSeconds(.08f);
            Require(host.Guids.Count == 2 && host.Guids[1] == Wave && !host.Instances[0].activeSelf && host.Instances[1].activeSelf, "native shallow-water substitution and previous effect deactivation");
            host.Ground.Type = GroundType.Wall; yield return new WaitForSeconds(.08f);
            Require(host.Guids.Count == 2 && !host.Instances[1].activeSelf, "invalid configured entry does not fall back");
            host.Ground.Type = GroundType.Hill; yield return new WaitForSeconds(.08f);
            Require(host.Guids.Count == 3 && host.Guids[2] == Smoke, "unconfigured ground uses original IgnoreKey fallback");
            Stop(playable); Require(!host.Instances[2].activeSelf && host.Disposed == 0, "stop disables effect and retains original load handles");
            // The original autoplay Update is not gated by playable status;
            // disable it while testing a stopped owner, as the prefab owners do.
            go.GetComponent<ApplyByGroundType>().enabled = false;
            UnityEngine.Object.Destroy(go); yield return null;
            Require(host.Disposed == 3, "all load handles disposed on owner destruction");

            var other = new Host(); other.Ground.Type = GroundType.Shallow;
            var followGo = new GameObject("Native FollowBone owner"); followGo.SetActive(false); followGo.transform.SetParent(root.transform, false);
            var follow = followGo.AddComponent<MyPlayableFollowBone>();
            // Exact 15300 startEffect attach values; no change to actor scale.
            follow.myAttach = new AttachPosRotCfg { AttachTo = EAttachTo.Source, myBoneName = "body", myOffset = new Vector3(0, -.419f, 0), myRotation = Quaternion.identity, myScale = 1.2f };
            follow.myDuration = 1f; follow.myFollowDuration = 0f;
            var body = new GameObject("body"); body.transform.SetParent(source.transform, false); body.transform.localPosition = new Vector3(0, .75f, 0);
            source.AddComponent<AttachAgent>().GenerateBoneMap();
            Configure(follow, other, followGo.transform, false); followGo.SetActive(true);
            follow.Init(source, target); follow.TryPlay();
            Require(other.Guids.Count == 1 && other.Guids[0] == Wave && host.Guids.Count == 3, "independent host ground/effect state");
            var effect = other.Instances[0];
            Require(Vector3.Distance(effect.transform.position, body.transform.TransformPoint(follow.myAttach.myOffset)) < .0001f, "native body bone and original offset");
            Require(Vector3.Distance(effect.transform.lossyScale, body.transform.lossyScale * 1.2f) < .0001f, "native follow effect scale");
            Require(source.transform.localScale == Vector3.one, "effect attachment preserves actor scale");
            Stop(follow); Require(!effect.activeSelf, "native FollowBone and apply stop release together");
            follow.Init(source, target); follow.TryPlay(); Require(other.Guids.Count == 2 && !effect.activeSelf, "stop clears original last terrain for restart");
            Stop(follow); UnityEngine.Object.Destroy(followGo); yield return null;
            Require(other.Disposed == 2, "restarted FollowBone releases both handles");

            var targetHost = new Host(); var targetGo = new GameObject("Target terrain owner"); targetGo.SetActive(false); targetGo.transform.SetParent(root.transform, false);
            var targetPlayable = targetGo.AddComponent<IndefinitePlayable>(); Configure(targetPlayable, targetHost, targetGo.transform, false, EAttachTo.Target); targetGo.SetActive(true);
            targetPlayable.Init(source, target); targetPlayable.TryPlay();
            Require(targetHost.Ground.Position == target.transform.localPosition, "target terrain sampled from target local position");
            Stop(targetPlayable); UnityEngine.Object.Destroy(targetGo); yield return null;
            Require(targetHost.Disposed == 1, "target owner resource cleanup");
            UnityEngine.Object.Destroy(root);

            foreach (var path in new[] { "Eff_Role_FlySmoke", "Eff_Role_JumpWave", "Eff_Role_Runsmoke", "Eff_Role_Wave" })
            {
                var prefab = Resources.Load<GameObject>("Combat/GroundEffects/Prefabs/EcEntity/Fight/FightSkill/Move/Effect/" + path);
                Require(prefab != null, "native prefab import " + path);
                var instance = UnityEngine.Object.Instantiate(prefab); instance.transform.position = Vector3.zero;
                int particles = 0;
                foreach (var system in instance.GetComponentsInChildren<ParticleSystem>(true))
                { system.Simulate(.12f, false, true, true); particles += system.particleCount; }
                Require(particles > 0, "native particle emission " + path);
                foreach (var renderer in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        if (material != null) Require(material.shader != null && material.shader.isSupported && material.shader.name == "Zjcs/Official/Combat/GroundEffects/NoiseMaskPro", "native Windows material shader " + path);
                yield return null;
                Capture(instance, path);
                UnityEngine.Object.Destroy(instance); yield return null;
            }
        }
        private static void Capture(GameObject effect, string name)
        {
            var go = new GameObject("Original ground effect camera"); var camera = go.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 3;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var rt = RenderTexture.GetTemporary(450, 800, 24); camera.targetTexture = rt;
            var previous = RenderTexture.active;
            try
            {
                camera.Render(); RenderTexture.active = rt;
                var image = new Texture2D(450, 800, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 450, 800), 0, 0); image.Apply();
                int visible = 0; foreach (var pixel in image.GetPixels32()) if (pixel.r > 8 || pixel.g > 8 || pixel.b > 8) visible++;
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"), image.EncodeToPNG());
                UnityEngine.Object.Destroy(image); Require(visible > 20, "native effect produces actual rendered pixels " + name);
                Debug.Log("Original ground effect pixels " + name + ": " + visible);
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.Destroy(go); }
        }
        public sealed class Runner : MonoBehaviour { }
        // The native base class has duration zero and stops on its initial
        // frame. This fixture supplies an indefinite owner for terrain-update
        // tests; the bone integration above uses unmodified FollowBone.
        public sealed class IndefinitePlayable : MyPlayable { public override float GetDuration() => -1f; }
    }
}

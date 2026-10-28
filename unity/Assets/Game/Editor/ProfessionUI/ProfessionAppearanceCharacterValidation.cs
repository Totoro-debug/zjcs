using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionAppearanceCharacterValidation
    {
        private const string Active = "Zjcs.ProfessionAppearanceCharacter.Active";
        private static bool ready;
        static ProfessionAppearanceCharacterValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SessionState.SetBool(Active, true); SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 120));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Character fixture did not enter Play Mode.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check();
                Debug.Log("Profession original AppearanceCharacter lifecycle passed: asset/result/dispose ordering including overwritten split handles, source sex/path selection, release before validity/filtering, ordered skins and append duplicates, part exclusion/dispatch, cloned dye values, one-channel dye cache, invalid/null branches, runtime-data constructor/reference behavior and destruction. Resource/appearance/part endpoints are fixtures; actual source character rendering and full profession UI are not validated.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Check()
        {
            var go = new GameObject("Original appearance character lifecycle fixture");
            var probe = go.AddComponent<AppearanceProbe>();
            var character = go.AddComponent<AppearanceCharacter>();
            var part = new GameObject("original part binding fixture").transform; part.SetParent(go.transform);
            typeof(AppearanceCharacter).GetField("mPartHandler", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(character, part);
            var graph = new Bindings(probe); character.Configure(graph);
            Require(ReferenceEquals(character.AppearanceComponent, probe), "original same-object component lookup");
            Require((int)Sex.Male == 0 && (int)Sex.Female == 1 && (int)Sex.None == 2, "original sex values");
            foreach (Sex sex in new[] { Sex.None, Sex.Male, Sex.Female, (Sex)99 })
            {
                string root = sex == Sex.None ? "" : "Assets/AppearanceAssets/Character/Model/" + (sex == Sex.Male ? "Male_1/" : "Female_1/");
                Require(ProfessionAppearanceCharacterPaths.GetPlayerPath(sex) == root, "source sex path branch");
                Require(ProfessionAppearanceCharacterPaths.GetPlayerSkeletonAssetPath(sex) == (root == "" ? "" : root + "character_" + (sex == Sex.Male ? "Male" : "Female") + "_1_SkeletonData.asset"), "source skeleton address");
                Require(ProfessionAppearanceCharacterPaths.GetPlayerSplitAnimationSoPath(sex) == (root == "" ? "" : root + "character_" + (sex == Sex.Male ? "Male" : "Female") + "_1_SkeletonData_SplitBinarySO.asset"), "source animation address");
            }
            character.LoadAsset(Sex.Male);
            Require(graph.Events.SequenceEqual(new[] { "load:0", "load:1", "result:0", "result:1", "init" }), "load and result consumption order");
            Require(ReferenceEquals(probe.Main, graph.Config) && ReferenceEquals(probe.Split, graph.Split), "original asset identities");
            graph.Events.Clear(); character.LoadAsset(Sex.Female);
            Require(graph.Events.SequenceEqual(new[] { "dispose:0", "load:2", "load:3", "result:2", "result:3", "init" }), "only prior main disposed before both new loads");
            Require(graph.Handles[0].Disposes == 1 && graph.Handles[1].Disposes == 0, "original overwritten split handle is not additionally disposed");
            var skinMap = new Dictionary<SkinType, string> { { SkinType.body, "body" }, { SkinType.eye, "emptySkin" }, { SkinType.backDecoration, "part" }, { SkinType.hair, "hair" } };
            var channel = new HsvChange { OffsetH = .1f, OffsetScaleS = -.2f, OffsetScaleV = .3f };
            var dyeMap = new Dictionary<SkinType, Dictionary<int, HsvChange>> { { SkinType.body, new Dictionary<int, HsvChange> { { 2, channel } } } };
            var append = new List<string> { "append", "body", "append" };
            probe.OnSetSkins = () => dyeMap[SkinType.body][2] = new HsvChange { OffsetH = 50 };
            graph.Events.Clear(); character.SetSkins(skinMap, dyeMap, append);
            Require(graph.Events.SequenceEqual(new[] { "release:False", "skins", "dye:body", "parts" }), "release, original skin list, dye and part execution order");
            Require(probe.Skins.SequenceEqual(new[] { "body", "hair", "append", "body", "append" }), "original iteration/filtering/append order and duplicates");
            Require(character.TryGetSkinName(SkinType.body, out string body) && body == "body" && !character.TryGetSkinName(SkinType.eye, out _) && !character.TryGetSkinName(SkinType.backDecoration, out _), "skin-name cache excludes empty/parts");
            Require(ReferenceEquals(graph.PartSkins, skinMap) && ReferenceEquals(graph.Part, part), "part handler receives original map identity");
            Require(probe.Dyes["body"][2].OffsetH == channel.OffsetH && probe.Dyes["body"][2].OffsetScaleS == channel.OffsetScaleS && probe.Dyes["body"][2].OffsetScaleV == channel.OffsetScaleV, "cached dye entries copied before callback mutation");
            character.SetDye("body", 1, channel); character.SetDye("hair", 3, channel);
            Require(probe.Dyes["hair"].Count == 1 && probe.Dyes["hair"].ContainsKey(3), "single-channel working dictionary clears previous channel");
            probe.OnSetSkins = null; graph.Events.Clear(); character.SetSkins(skinMap, null, null);
            Require(graph.Events.SequenceEqual(new[] { "release:False", "skins", "parts" }), "release clears prior dye cache even when next dye map is null");
            graph.Events.Clear(); character.Release(true);
            Require(graph.Events.SequenceEqual(new[] { "release:True", "release-parts:True" }) && !character.TryGetSkinName(SkinType.body, out _), "full release order and cache clear");
            probe.IsValid = false; graph.Events.Clear();
            int errors = 0; Application.LogCallback capture = (message, stack, type) => { if (type == LogType.Error && message == "AppearanceComponent invalid") errors++; };
            Application.logMessageReceived += capture;
            try { character.SetSkins(skinMap); character.SetDye("body", 2, channel); }
            finally { Application.logMessageReceived -= capture; }
            Require(errors == 2 && graph.Events.SequenceEqual(new[] { "release:False" }), "original invalid component error/early return branches");
            probe.IsValid = true;
            var data = new AppearanceRuntimeData(Sex.Male, skinMap, dyeMap, append);
            Require(ReferenceEquals(data.SkinMap, skinMap) && ReferenceEquals(data.DyeMap, dyeMap) && ReferenceEquals(data.AppendSkins, append), "original constructor retains references");
            Require(new AppearanceRuntimeData(Sex.None).SkinMap.Count == 0 && new AppearanceRuntimeData().SkinMap == null, "original constructor defaults");
            graph.Events.Clear(); character.SetRuntimeData(data);
            Require(graph.Events.SequenceEqual(new[] { "dispose:2", "load:4", "load:5", "result:4", "result:5", "init", "release:False", "skins", "dye:body", "parts" }), "runtime data loads then applies original skin flow");
            graph.NullMain = true; graph.Events.Clear(); errors = 0;
            capture = (message, stack, type) => { if (type == LogType.Error && message == "not exist character data") errors++; };
            Application.logMessageReceived += capture;
            try { character.LoadAsset(Sex.None); } finally { Application.logMessageReceived -= capture; }
            Require(errors == 1 && graph.Events.SequenceEqual(new[] { "dispose:4", "load:6", "load:7", "result:6", "result:7" }), "null main still consumes split result, emits original error and skips init");
            graph.Events.Clear(); UnityEngine.Object.DestroyImmediate(go);
            Require(graph.Events.SequenceEqual(new[] { "dispose:6", "dispose:7" }), "destroy disposes only current main and split handles");
            Require(graph.Handles[1].Disposes == 0 && graph.Handles[3].Disposes == 0 && graph.Handles[5].Disposes == 0, "no invented split-overwrite disposal");
            UnityEngine.Object.DestroyImmediate(graph.Config); UnityEngine.Object.DestroyImmediate(graph.Split);
        }
        public sealed class AppearanceProbe : MonoBehaviour, IAppearanceComponent
        {
            public bool IsValid = true; public bool Valid => IsValid;
            public ScriptableObject Main, Split; public Action OnSetSkins; public Bindings Graph;
            public string[] Skins; public readonly Dictionary<string, Dictionary<int, HsvChange>> Dyes = new Dictionary<string, Dictionary<int, HsvChange>>();
            public void Init(ScriptableObject main, ScriptableObject split) { Main = main; Split = split; Graph.Events.Add("init"); }
            public void SetSkins(List<string> skins) { Skins = skins.ToArray(); Graph.Events.Add("skins"); OnSetSkins?.Invoke(); }
            public void SetDye(string skin, Dictionary<int, HsvChange> values) { Dyes[skin] = new Dictionary<int, HsvChange>(values); Graph.Events.Add("dye:" + skin); }
            public bool TryGetMaterial(string skin, out Material material) { material = null; return false; }
            public void Release(bool all = false) { Graph.Events.Add("release:" + all); }
        }
        public sealed class Handle : IProfessionAppearanceCharacterAsset
        {
            public ScriptableObject Asset; public int Id, Disposes; public List<string> Events;
            public ScriptableObject Result { get { Events.Add("result:" + Id); return Asset; } }
            public void Dispose() { Disposes++; Events.Add("dispose:" + Id); }
        }
        public sealed class Bindings : IProfessionAppearanceCharacterBindings
        {
            public readonly List<string> Events = new List<string>(); public readonly List<Handle> Handles = new List<Handle>();
            public readonly AppearanceConfig Config = ScriptableObject.CreateInstance<AppearanceConfig>();
            public readonly AppearanceConfig Split = ScriptableObject.CreateInstance<AppearanceConfig>();
            public bool NullMain; public Dictionary<SkinType, string> PartSkins; public Component Part;
            AppearanceConfig IProfessionAppearanceCharacterBindings.Config => Config;
            public Bindings(AppearanceProbe probe)
            {
                probe.Graph = this;
                typeof(AppearanceConfig).GetField("mPartConfigs", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Config, new List<AppearancePartConfig> { new AppearancePartConfig { SkinType = SkinType.backDecoration } });
            }
            public IProfessionAppearanceCharacterAsset LoadAsset(string path)
            {
                int id = Handles.Count; Events.Add("load:" + id);
                bool split = id % 2 != 0;
                var handle = new Handle { Asset = split ? Split : NullMain ? null : Config, Id = id, Events = Events }; Handles.Add(handle); return handle;
            }
            public void SetPartSkins(Component handler, Dictionary<SkinType, string> skins) { Part = handler; PartSkins = skins; Events.Add("parts"); }
            public void ReleaseParts(Component handler, bool tryUnloadAllAssets) { Require(ReferenceEquals(handler, Part), "same part release target"); Events.Add("release-parts:" + tryUnloadAllAssets); }
        }
        private static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.World;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Offline.World.Map13Entities;

namespace Zjcs.Editor.Map13Entities
{
    public static class OfficialMap13EntityPresentationAcceptance
    {
        private const string Key = "Zjcs.Map13.EntityPresentationExecution";
        private static bool started;
        [Serializable] private sealed class Record { public string path, sha256; }
        [Serializable] private sealed class Receipt { public string manifest; public Record[] official_code, outputs, assemblies; public Record generator, source_receipt, tables, map, code_manifest; }
        [Serializable] private sealed class Data { public EntityMapDisplayInfo[] Displays; public EntityMapUIDisplayInfo[] UiDisplays; public EntityMapDisplayActionInfo[] Actions; }
        [Serializable] private sealed class References { public string manifest, mapSha256; public Case[] Cases; }
        [Serializable] private sealed class Case { public int PlacementId, EntityClassId, X, Y, Area, BaseGroundType, Display, Ui, Action; public string AreaTag, EntityPath; }

        public static string Repository
        {
            get
            {
                var directory = new DirectoryInfo(Application.dataPath);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "official-source.json"))) directory = directory.Parent;
                return directory?.FullName ?? throw new DirectoryNotFoundException("Map13 repository root is unavailable.");
            }
        }

        public static void ValidateSources()
        {
            string root = Repository;
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(root, "assets/reference/official-map/map13-entity-presentation-source-manifest.json")));
            Require(receipt.manifest == "871_156598", "Selected entity presentation version differs.");
            foreach (var record in receipt.official_code.Concat(receipt.outputs).Concat(receipt.assemblies)
                .Concat(new[] { receipt.generator, receipt.source_receipt, receipt.tables, receipt.map, receipt.code_manifest }))
                Require(Hash(File.ReadAllBytes(Path.Combine(root, record.path))) == record.sha256, "Entity presentation source bytes differ: " + record.path);
        }

        public static void Run()
        {
            ValidateSources();
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            SessionState.SetInt(Key + ".result", 1);
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod] private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
        private static void Hook()
        {
            started = false;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged; EditorApplication.playModeStateChanged += ModeChanged;
        }
        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map13 entity presentation execution timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            try
            {
                VerifyOriginalBranches(); VerifyCurrentData();
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map13 entity presentation Play Mode passed: 2053 complete current rows, 2690 original placement contexts, base-ground/area selection, ordered fallbacks without parent backtracking, original prefixes, duplicate rejection and EC static path-cache/disposal. Direct selectors only; native entity rendering, mouse input, gameplay and saves remain unverified.");
            }
            catch (Exception error) { Debug.LogException(error); }
            EditorApplication.isPlaying = false;
        }

        private static void VerifyCurrentData()
        {
            var dataAsset = Resources.Load<TextAsset>("Map13/entity-presentation");
            var referenceAsset = Resources.Load<TextAsset>("Map13/entity-presentation-reference");
            var reference = JsonUtility.FromJson<References>(referenceAsset.text);
            string mapText = File.ReadAllText(Path.Combine(Repository, "unity/Assets/Official/World/Map/13/map13.json"));
            Require(reference.manifest == "871_156598" && Hash(System.Text.Encoding.UTF8.GetBytes(mapText)) == reference.mapSha256, "Reference map differs.");
            var map = JsonUtility.FromJson<OfficialMapDefinition>(mapText);
            var data = JsonUtility.FromJson<Data>(dataAsset.text);
            using (var catalog = OfflineMap13EntityPresentation.LoadDefault())
            {
                foreach (var row in data.Displays)
                {
                    if (!string.IsNullOrEmpty(row.DirectionGuiderItemUIPath)) row.DirectionGuiderItemUIPath = "UI/DirectionGuider/" + row.DirectionGuiderItemUIPath;
                    Same(row, catalog.Displays.Get(row.EntityClassId, row.GroupId, row.MapClassId, row.AreaTag, row.GroundType, row.Tag));
                }
                foreach (var row in data.UiDisplays)
                {
                    if (!string.IsNullOrEmpty(row.MiniMapMark)) row.MiniMapMark = "UI/Map/MiniMap/MiniMapMark/" + row.MiniMapMark;
                    Same(row, catalog.UiDisplays.Get(row.EntityClassId, row.GroupId, row.MapClassId, row.AreaTag, row.Tag));
                }
                foreach (var row in data.Actions)
                    Same(row, catalog.Actions.Get(row.EntityClassId, row.GroupId, row.MapClassId, row.AreaTag, row.GroundType, row.State, row.Tag));
                Require(reference.Cases.Length == 2690, "Placement reference coverage differs.");
                int boats = 0;
                foreach (var item in reference.Cases)
                {
                    var selected = catalog.Resolve(map, item.EntityClassId, item.X, item.Y, 0, "");
                    Require(selected.EntityPath == item.EntityPath && selected.Area == item.Area && selected.AreaTag == item.AreaTag
                        && selected.BaseGroundType == item.BaseGroundType, "Original entity context/path differs: " + item.PlacementId);
                    Same(item.Display < 0 ? null : data.Displays[item.Display], selected.Display);
                    Same(item.Ui < 0 ? null : data.UiDisplays[item.Ui], selected.Ui);
                    Same(item.Action < 0 ? null : data.Actions[item.Action], selected.Action);
                    if (item.EntityClassId == 6680)
                    {
                        boats++;
                        Require(selected.Display == null && selected.EntityPath == "Assets/Prefabs/EcEntity/MapWorld/WorldGamePlay/Map_13_Play/6680.prefab", "Original boat must use its embedded view.");
                    }
                }
                Require(boats == 10, "Boat placement coverage differs.");
            }
            string sourceText = Resources.Load<TextAsset>("Map13/entity-presentation-source").text;
            bool rejected = false;
            try { using (var unused = new OfflineMap13EntityPresentation(dataAsset.text + " ", sourceText)) { } }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Changed packaged data was not rejected.");
        }

        private static void VerifyOriginalBranches()
        {
            var broad = new EntityMapDisplayInfo { EntityClassId = 1, GroupId = 0, MapClassId = 0, AreaTag = "IgnoreKey", GroundType = GroundType.IgnoreKey, Tag = "", DisplayPath = "broad", DirectionGuiderItemUIPath = "arrow" };
            var narrow = new EntityMapDisplayInfo { EntityClassId = 1, GroupId = 13, MapClassId = 99, AreaTag = "IgnoreKey", GroundType = GroundType.IgnoreKey, Tag = "", DisplayPath = "narrow" };
            var displays = new EntityMapDisplayInfoParser(); displays.Load(new[] { broad, narrow });
            Require(broad.DirectionGuiderItemUIPath == "UI/DirectionGuider/arrow" && ReferenceEquals(displays.Get(1, 7, 13, null, GroundType.Water, null), broad), "Original default/prefix selection differs.");
            Require(displays.Get(1, 13, 13, null, GroundType.Water, null) == null, "Selector incorrectly backtracks to group zero.");
            broad = new EntityMapDisplayInfo { EntityClassId = 2, GroupId = 13, MapClassId = 13, AreaTag = "IgnoreKey", GroundType = GroundType.IgnoreKey, Tag = "", DisplayPath = "ground default" };
            narrow = new EntityMapDisplayInfo { EntityClassId = 2, GroupId = 13, MapClassId = 13, AreaTag = "IgnoreKey", GroundType = GroundType.Water, Tag = "only", DisplayPath = "water only" };
            displays.Load(new[] { broad, narrow });
            Require(displays.Get(2, 13, 13, null, GroundType.Water, "missing") == null, "Selector incorrectly backtracks to default ground.");
            Require(ReferenceEquals(displays.Get(2, 13, 13, null, GroundType.Shallow, null), broad), "Ground fallback differs.");
            bool duplicate = false;
            try { displays.Load(new[] { broad, broad }); } catch (ArgumentException) { duplicate = true; }
            Require(duplicate, "Original duplicate display key must throw.");
            var ui = new EntityMapUIDisplayInfoParser();
            var uiDefault = new EntityMapUIDisplayInfo { EntityClassId = 3, AreaTag = "IgnoreKey", Tag = "", MiniMapMark = "mark" };
            ui.Load(new[] { uiDefault, new EntityMapUIDisplayInfo { EntityClassId = 3, GroupId = 13, MapClassId = 99, AreaTag = "IgnoreKey", Tag = "" } });
            Require(uiDefault.MiniMapMark == "UI/Map/MiniMap/MiniMapMark/mark" && ReferenceEquals(ui.Get(3, 4, 13, null, null), uiDefault), "UI prefix/default differs.");
            Require(ui.Get(3, 13, 13, null, null) == null, "UI selector incorrectly backtracks.");
            var actions = new EntityMapDisplayActionInfoParser();
            var defaultAction = new EntityMapDisplayActionInfo { EntityClassId = 4, AreaTag = "IgnoreKey", GroundType = GroundType.IgnoreKey, Tag = "", State = -1, InteractionActionId = 7 };
            var stateAction = new EntityMapDisplayActionInfo { EntityClassId = 4, AreaTag = "IgnoreKey", GroundType = GroundType.IgnoreKey, Tag = "", State = 1, InteractionActionId = 8 };
            actions.Load(new[] { defaultAction, stateAction });
            Require(ReferenceEquals(actions.Get(4, 13, 13, null, GroundType.Water, 1, null), stateAction)
                && ReferenceEquals(actions.Get(4, 13, 13, null, GroundType.Water, 2, null), defaultAction), "Original state/default -1 selection differs.");
            var directories = new System.Collections.Generic.Dictionary<string, int[]> { { "First", new[] { int.MaxValue } } };
            using (var index = new MyEntityIndexHandler(directories))
                Require(index.GetEntityPath(int.MaxValue) == "Assets/Prefabs/EcEntity/First/2147483647.prefab", "Original EC path differs.");
            Require(directories.Count == 0, "Original index disposal must clear its directory data.");
            using (var index = new MyEntityIndexHandler(new System.Collections.Generic.Dictionary<string, int[]> { { "Second", new[] { int.MaxValue } } }))
                Require(index.GetDir(int.MaxValue) == "Second" && index.GetEntityPath(int.MaxValue) == "Assets/Prefabs/EcEntity/First/2147483647.prefab", "Original static EC path cache lifetime differs.");
        }

        private static void Same(object expected, object actual) => Require((expected == null && actual == null)
            || expected != null && actual != null && JsonUtility.ToJson(expected) == JsonUtility.ToJson(actual), "Complete original selector row differs.");
        private static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    }
}

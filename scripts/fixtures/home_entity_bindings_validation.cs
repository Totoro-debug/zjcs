using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.HomeEntityBindings;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

public static class HomeEntityBindingsValidation
{
    [Serializable] sealed class Input
    {
        public string authoringJson;
        public Vector3 position, scale;
        public Quaternion rotation;
        public string[] nodeKeys;
    }
    sealed class Source : IHomeEntityManager, IHomeEntityDataAgent
    {
        public readonly List<HomeEntityDataProvider> Providers = new List<HomeEntityDataProvider>();
        public IHomeEntityDataAgent EntityDataAgent => this;
        public void GetDatas<T>(List<T> target) where T : HomeEntityData
        {
            foreach (var provider in Providers)
                foreach (var values in provider.DataDic.Values)
                    foreach (var value in values) if (value is T typed) target.Add(typed);
        }
    }
    static readonly List<GameObject> roots = new List<GameObject>();
    static readonly List<string> errors = new List<string>();
    static bool expectErrors;
    static int expectedErrors;
    static void Need(bool value, string message) { if (!value) throw new Exception(message); }
    static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < .000001f;
    static GameObject New(string name) { var value = new GameObject(name); roots.Add(value); return value; }
    static HomeEntityDataProvider Provider(GameObject root, params HomeEntityData[] data)
    {
        var provider = root.GetComponent<HomeEntityDataProvider>() ?? root.AddComponent<HomeEntityDataProvider>();
        var value = new SerializedObject(provider).FindProperty("mDataList");
        value.arraySize = data.Length;
        for (int i = 0; i < data.Length; i++) value.GetArrayElementAtIndex(i).objectReferenceValue = data[i];
        value.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return provider;
    }
    static HomeSpaceBindingData Space(string name, float y) => new HomeSpaceBindingData {
        SpaceId = name, SpaceRoute = new HomeSpaceRouteDefinition {
            Nodes = new List<HomeSpaceRouteNodeDefinition> {
                new HomeSpaceRouteNodeDefinition { NodeId = name + "_a", LocalPosition = new Vector3(0, y) },
                new HomeSpaceRouteNodeDefinition { NodeId = name + "_b", LocalPosition = new Vector3(10, y) }
            }, Edges = new List<HomeSpaceRouteEdgeDefinition> { new HomeSpaceRouteEdgeDefinition {
                EndpointA = new HomeSpaceRouteEndpointDefinition { RouteNodeId = name + "_a" },
                EndpointB = new HomeSpaceRouteEndpointDefinition { RouteNodeId = name + "_b" }, Cost = 30
            } }
        }
    };
    public static void Run()
    {
        int exit = 0;
        Application.logMessageReceived += OnLog;
        try
        {
            string project = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_BINDING_PROJECT"));
            Need(Path.GetFullPath(Application.dataPath) == Path.Combine(project, "Assets"), "Use the selected independent project");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var input = JsonUtility.FromJson<Input>(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_BINDING_WORK"), "storage-binding-input.json")));
            CheckStorage(input);
            CheckAutoConnections();
            Need(errors.Count == 0, "Unexpected runtime diagnostics: " + string.Join(";", errors));
            Debug.Log("HOME_ENTITY_BINDINGS_PASSED: exact storage authoring, original data caches/registry, transformed points, profile overrides, channels, dirty rebuild and ordered auto-connect costs.");
        }
        catch (Exception error) { Debug.LogException(error); exit = 1; }
        finally
        {
            foreach (var root in roots) if (root) Object.DestroyImmediate(root);
            Application.logMessageReceived -= OnLog;
        }
        EditorApplication.Exit(exit);
    }
    static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (expectErrors) expectedErrors++; else errors.Add(message);
    }
    static void CheckStorage(Input input)
    {
        var parent = New("NativeStorageParent");
        parent.transform.SetPositionAndRotation(new Vector3(3, 5, 7), Quaternion.Euler(0, 0, 17));
        parent.transform.localScale = new Vector3(2, 3, 1);
        var root = New("OriginalStorageAuthoring"); root.transform.SetParent(parent.transform, false);
        root.transform.localPosition = input.position; root.transform.localRotation = input.rotation; root.transform.localScale = input.scale;
        var authoring = root.AddComponent<HomeBindingAuthoring>();
        JsonUtility.FromJsonOverwrite(input.authoringJson, authoring);
        Need(authoring.Units.Count > 0, "Actual storage authoring is populated");
        var unit = authoring.Units.Single(); var point = unit.UnitDefinition.PointDefinitions.Single();
        Need(point.SupportsBehaviourTag("WatchWeapon") && !point.SupportsBehaviourTag("watchweapon")
            && point.TryGetActionGraphProxy("WatchWeapon", out var graph) && graph.IsValid && !string.IsNullOrEmpty(graph.AssetGuid), "Original ordinal behaviour lookup retains the actual graph GUID");
        Need(unit.TryBuildRuntimeBinding(root.transform) && Near(point.WorldPosition, root.transform.TransformPoint(point.LocalPosition)), "Original local-to-world binding uses the actual Unity transform");
        var provider = Provider(root, authoring);
        Need(provider.DataDic[typeof(HomeBindingAuthoring)].Single() == authoring && ReferenceEquals(provider.DataDic, provider.DataDic), "Original provider groups the same components and caches the dictionary");
        var registry = root.AddComponent<HomeEntityNodeRegister>(); registry.GameObjectDict = new MonoDictionary<string, GameObject>();
        foreach (string key in input.nodeKeys) registry.GameObjectDict.Add(key, New("OriginalNode_" + key));
        Need(registry.GetGameObject(null) == null && registry.GetGameObject("") == null && registry.GetGameObject("__absent__") == null
            && input.nodeKeys.All(key => registry.GetGameObject(key) == registry.GameObjectDict[key]), "Original registry preserves names, references and absent-key branches");
        var scene = New("BindingScene").AddComponent<HomeBindingAuthoring>();
        scene.Profile = authoring.Profile;
        scene.Spaces.Add(Space(unit.SpaceId, 0)); scene.Spaces.Add(Space("fixture_other_space", 5));
        scene.Channels.Add(new HomeChannelBindingData {
            ChannelId = "fixture_channel", SideA = new HomeChannelEndpointDefinition { SpaceId = unit.SpaceId, EntryRouteNodeId = unit.SpaceId + "_a", ExitRouteNodeId = unit.SpaceId + "_b" },
            SideB = new HomeChannelEndpointDefinition { SpaceId = "fixture_other_space", EntryRouteNodeId = "fixture_other_space_a", ExitRouteNodeId = "fixture_other_space_b" },
            AnchorBindings = new List<HomeChannelAnchorBinding> { new HomeChannelAnchorBinding { AnchorId = "fixture_anchor", LocalPosition = Vector3.one } }
        });
        var source = new Source(); source.Providers.Add(Provider(scene.gameObject, scene)); source.Providers.Add(provider);
        var cache = new HomeRuntimeBindingCacheService(source);
        var snapshot = cache.GetSnapshot(authoring.Profile);
        Need(snapshot.UnitBindings.Single() == unit && snapshot.FindSpace(unit.SpaceId).RuntimeUnitBindings.Single() == unit,
            "Original cache attaches the actual authored unit to its space");
        Need(snapshot.AnchorToUnitPointIndex[point.PointId].UnitId == unit.UnitInstanceId
            && Near(snapshot.AnchorToUnitPointIndex[point.PointId].WorldPosition, point.WorldPosition), "Semantic point index preserves actual world position");
        var channel = snapshot.Channels.Single();
        Need(channel.RuntimeAToB.FromRouteNodeIndex == 0 && channel.RuntimeAToB.ToRouteNodeIndex == 1
            && channel.RuntimeBToA.FromRouteNodeIndex == 0 && channel.RuntimeBToA.ToRouteNodeIndex == 1
            && Near(channel.RuntimeAToB.ExitRouteNodeWorldPosition, new Vector3(10, 5)), "Original channel cache binds both directions and exit poses");
        Need(snapshot.TryResolveChannelAnchorWorldPosition("fixture_anchor", out var anchor) && Near(anchor, Vector3.one), "Channel anchor resolution uses its authored root");
        Need(ReferenceEquals(snapshot, cache.GetSnapshot(authoring.Profile)), "Clean cache retains the original snapshot identity");
        var overrides = New("SpaceOverrides").AddComponent<HomeUnitSpaceOverrideData>();
        overrides.Profiles.Add(new HomeUnitSpaceOverrideProfileData { Profile = authoring.Profile,
            UnitOverrides = new List<HomeUnitSpaceOverrideEntry> { new HomeUnitSpaceOverrideEntry { UnitInstanceId = unit.UnitInstanceId, SpaceId = "fixture_other_space" } } });
        source.Providers.Add(Provider(overrides.gameObject, overrides));
        Need(ReferenceEquals(snapshot, cache.GetSnapshot(authoring.Profile)), "Source edits wait for the explicit dirty signal");
        cache.MarkDirty(); var changed = cache.GetSnapshot(authoring.Profile);
        Need(!ReferenceEquals(snapshot, changed) && changed.GetUnitEffectiveSpaceId(unit.UnitInstanceId) == "fixture_other_space"
            && changed.FindSpace("fixture_other_space").RuntimeUnitBindings.Single() == unit, "Dirty rebuild applies the original per-profile override");
        expectedErrors = 0; expectErrors = true;
        overrides.Profiles[0].UnitOverrides[0].SpaceId = "__missing_space__";
        cache.MarkDirty(); var excluded = cache.GetSnapshot(authoring.Profile);
        expectErrors = false;
        Need(expectedErrors == 2 && excluded.UnitBindings.Count == 0, "Invalid override logs the original two diagnostics and excludes the unit without a fabricated fallback");
        source.Providers.Remove(provider); source.Providers.RemoveAt(source.Providers.Count - 1); cache.MarkDirty();
        Need(cache.GetSnapshot(authoring.Profile).UnitBindings.Count == 0, "Removed native providers leave no retained binding on rebuild");
        var other = cache.GetSnapshot((EHomeActorBindingProfile)999);
        Need(other.UnitBindings.Count == 0 && ReferenceEquals(other, cache.GetSnapshot((EHomeActorBindingProfile)999)), "Unconfigured profile caches an empty snapshot independently");
    }
    static void CheckAutoConnections()
    {
        var nodes = new List<HomeNavigationAutoConnectNode> {
            new HomeNavigationAutoConnectNode(0, EHomeSpaceRouteEndpointKind.RouteNode, Vector3.zero, false, true),
            new HomeNavigationAutoConnectNode(1, EHomeSpaceRouteEndpointKind.RouteNode, new Vector3(10, 0), false, true),
            new HomeNavigationAutoConnectNode(2, EHomeSpaceRouteEndpointKind.UnitPoint, new Vector3(2, 4), true, false)
        };
        var edges = new List<HomeNavigationAutoConnectEdge> { new HomeNavigationAutoConnectEdge(0, 1, 30, true), new HomeNavigationAutoConnectEdge(1, 0, 30, true) };
        var plan = HomeNavigationAutoConnectPlanner.Build(nodes, edges); var route = plan.Results.Single();
        Need(route.TargetKind == EHomeNavigationAutoConnectTargetKind.RouteEdge && Math.Abs(route.RouteEdgeFromCost - 6) < .0001f
            && Math.Abs(route.RouteEdgeToCost - 24) < .0001f, "Original route projection apportions the authored edge cost rather than substituting distance");
        nodes.Add(new HomeNavigationAutoConnectNode(3, EHomeSpaceRouteEndpointKind.UnitPoint, new Vector3(2, 3), false, true));
        edges.Add(new HomeNavigationAutoConnectEdge(0, 3, 1, false));
        var direct = HomeNavigationAutoConnectPlanner.Build(nodes, edges).Results.Single();
        Need(direct.TargetKind == EHomeNavigationAutoConnectTargetKind.UnitPoint && direct.UnitPointNodeIndex == 3
            && Math.Abs(direct.UnitPointCost - 1) < .0001f, "Original reachable-unit preference and Euclidean unit cost are retained");
        var lonely = HomeNavigationAutoConnectPlanner.Build(nodes.Take(3).ToList(), new List<HomeNavigationAutoConnectEdge>());
        Need(lonely.Results.Count == 0 && lonely.UnconnectedNodeIndexes.SequenceEqual(new[] { 2 }), "A route node alone does not invent an automatic connection");
        var ignored = HomeNavigationAutoConnectPlanner.Build(new List<HomeNavigationAutoConnectNode> {
            new HomeNavigationAutoConnectNode(8, EHomeSpaceRouteEndpointKind.UnitPoint, Vector3.one, false, false),
            new HomeNavigationAutoConnectNode(9, EHomeSpaceRouteEndpointKind.UnitPoint, Vector3.zero, true, true)
        }, new List<HomeNavigationAutoConnectEdge>());
        Need(ignored.Results.Count == 0 && ignored.UnconnectedNodeIndexes.Count == 0, "Disabled auto-connect and already-connected points are not pending candidates");
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.HomeNativeAssets;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeModelEnvironment;
using Zjcs.Official.HomeUI;

[InitializeOnLoad]
public static class HomeNativePrefabResourcesValidation
{
    const string Active = "Zjcs.HomeNativePrefabResourcesValidation.Active";
    static IEnumerator checks; static int frame = -1, errors;
    static readonly List<GameObject> roots = new List<GameObject>();
    static HomeNativePrefabLoader loader, other;
    [Serializable] public sealed class Route { public string location; public string resource; public string prefab; }
    [Serializable] public sealed class Inputs { public Route[] routes; }
    static HomeNativePrefabResourcesValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false)) {
                AudioListener.volume = 0; Application.runInBackground = true;
                Application.logMessageReceived += OnLog; checks = Check();
            }
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Local native prefab resource validation timed out"));
            if (checks == null || !EditorApplication.isPlaying || frame == Time.frameCount) return;
            frame = Time.frameCount;
            try { if (!checks.MoveNext()) { checks = null; Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors)); } }
            catch (Exception failure) { checks = null; Finish(failure); }
        };
    }
    public static void Run()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_HOME_NATIVE_RESOURCE_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets"))
            throw new InvalidOperationException("Select the independent project explicitly");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true); SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 300);
        EditorApplication.EnterPlaymode();
    }
    static void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    static void Finish(Exception failure)
    {
        SessionState.SetBool(Active, false); loader?.Dispose(); other?.Dispose();
        foreach (var root in roots) if (root) UnityEngine.Object.DestroyImmediate(root);
        Application.logMessageReceived -= OnLog;
        if (failure == null) Debug.Log("HOME_NATIVE_PREFAB_RESOURCES_PASSED: original-address runtime Resources loading, all verified native prefab routes, exact prefab identities, shared scene leases, independent owners, null-result branches and teardown. Component/resource scope; full city/Player play remains separate.");
        else Debug.LogException(failure);
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    static void Require(bool value, string text) { if (!value) throw new Exception(text); Debug.Log("CHECK: " + text); }
    static GameObject InstantiateInactive(GameObject prefab)
    {
        var root = new GameObject("ResourceLoadFixture_" + prefab.name); root.SetActive(false); roots.Add(root);
        return UnityEngine.Object.Instantiate(prefab, root.transform, false);
    }
    static IEnumerator Check()
    {
        var input = JsonUtility.FromJson<Inputs>(File.ReadAllText(Environment.GetEnvironmentVariable("ZJCS_HOME_NATIVE_RESOURCE_INPUT")));
        Require(input.routes.Length == 1036 && input.routes.Select(row => row.location).Distinct().Count() == input.routes.Length,
            "Every previously verified house, storage and Home weapon prefab has one original-address local route");
        loader = new HomeNativePrefabLoader(); other = new HomeNativePrefabLoader();
        const string housePath = "Home/Dynamic/Theme/House/House";
        const string storagePath = "Home/Dynamic/Furniture/StorageBox/StorageBox";
        var paths = new HomeEntityPrefabPaths(HomeEntityCreationCatalog.Load());
        var storageSkins = new Dictionary<int, string> {
            { 4210, "Home/Dynamic/Furniture/StorageBox/StorageBox" },
            { 4220, "Home/Dynamic/Furniture/StorageBox/StorageBoxChristmas" },
            { 4226, "Home/Dynamic/Furniture/StorageBox/StorageBoxChineseStyle" },
            { 4238, "Home/Dynamic/Furniture/StorageBox/StorageBox1stAnniversary" },
            { 4248, "Home/Dynamic/Furniture/StorageBox/StorageBoxChineseOdyssey" },
            { 4261, "Home/Dynamic/Furniture/StorageBox/StorageBoxHalloween" },
        };
        Require(paths.GetThemePrefabPath(4700) == housePath && storageSkins.All(pair => paths.GetFurniturePrefabPath(pair.Key) == pair.Value),
            "Current original theme/furniture address builders feed the local loader unchanged for House and all 6 StorageBox skins");
        var first = loader.LoadPrefab(housePath); var duplicate = loader.LoadPrefab(housePath);
        var otherOwner = other.LoadPrefab(housePath);
        Require(first.Result && first.Result == duplicate.Result && otherOwner.Result == first.Result,
            "Real Resources requests share the native prefab while owners retain independent leases");
        var house = InstantiateInactive(first.Result);
        Require(house.GetComponentsInChildren<Transform>(true).Length == 98 && house.GetComponentsInChildren<HomeTimeDriveClock>(true).Length == 1,
            "Runtime resource route instantiates the complete original house hierarchy and clock component");
        first.Release(); first.Release();
        Require(!first.Result && duplicate.Result && otherOwner.Result, "Releasing one lease twice leaves other callers and owners valid");
        duplicate.Dispose(); loader.Dispose(); loader.Dispose();
        Require(!duplicate.Result && otherOwner.Result && house, "Owner cleanup preserves another owner and an already instantiated native model");
        bool rejected = false; try { loader.LoadPrefab(housePath); } catch (ObjectDisposedException) { rejected = true; }
        Require(rejected, "A disposed local resource owner cannot issue new leases");
        loader = new HomeNativePrefabLoader();
        IHomeEntityResourceLoader entityLoader = loader;
        IHomeEntityPrefabHandle storageLease = entityLoader.LoadPrefab(storagePath);
        IHomeFurniturePrefabHandle furnitureLease = loader.LoadPrefab(storagePath);
        Require(storageLease.Result && storageLease.Result == furnitureLease.Result, "Both original entity and furniture handle contracts resolve the same native storage asset");
        var storage = InstantiateInactive(storageLease.Result);
        Require(storage.GetComponentsInChildren<MonoBehaviour>(true).Length == 40 && storage.GetComponent<StoreBoxFurniture>() &&
            storage.GetComponentInChildren<HomeWeaponRack>(true), "Runtime storage route retains all original components and its weapon rack");
        storageLease.Release(); Require(!storageLease.Result && furnitureLease.Result && storage, "Original Release and Dispose contracts preserve separate active leases");
        furnitureLease.Dispose(); Require(!furnitureLease.Result, "Disposed furniture resource lease clears its own result");
        foreach (string missing in new[] { "Home/Dynamic/Furniture/NotMigrated/NotMigrated", "", null }) {
            using (var lease = loader.LoadPrefab(missing)) Require(!lease.Result, "Unavailable original address retains a null-result lease: " + (missing ?? "<null>"));
        }

        int count = 0;
        foreach (var route in input.routes) {
            using (var lease = loader.LoadPrefab(route.location)) {
                if (!lease.Result || HomeNativePrefabLoader.ResourcePath(route.location) != route.resource)
                    throw new Exception("Original prefab route failed: " + route.location);
                // AssetDatabase supplies an independent expected identity only;
                // the production loader and acquisition above use Resources.
                var expected = AssetDatabase.LoadAssetAtPath<GameObject>(route.prefab.Substring("unity/".Length));
                if (expected != lease.Result) throw new Exception("Runtime route changed native prefab identity: " + route.location);
            }
            count++;
            if (count % 64 == 0) yield return null;
        }
        Require(count == input.routes.Length, "All local original-address routes resolve their verified native prefab identities");
        var outstanding = loader.LoadPrefab(storagePath); loader.Dispose(); other.Dispose();
        Require(!outstanding.Result && !otherOwner.Result, "Scene owner teardown invalidates all outstanding local resource leases");
        foreach (var root in roots) if (root) UnityEngine.Object.Destroy(root);
        yield return null;
        Require(roots.All(root => !root), "Resource verification leaves no instantiated native model");
    }
}

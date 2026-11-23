using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.HomeRelations;

[InitializeOnLoad]
public static class HomeRelationValidation
{
    const string Active = "Zjcs.HomeRelationValidation.Active";
    static IEnumerator routine; static int frame = -1, errors, expectedErrors;
    static readonly List<GameObject> roots = new List<GameObject>();
    static readonly FieldInfo Layers = typeof(Light2D).GetField("m_ApplyToSortingLayers", BindingFlags.Instance | BindingFlags.NonPublic);
    static HomeRelationValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false)) {
                AudioListener.volume = 0; Application.runInBackground = true;
                Application.logMessageReceived += OnLog; routine = Check();
            }
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Home relation validation timed out"));
            if (routine == null || !EditorApplication.isPlaying || frame == Time.frameCount) return;
            frame = Time.frameCount;
            try { if (!routine.MoveNext()) { routine = null; Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors)); } }
            catch (Exception error) { routine = null; Finish(error); }
        };
    }
    public static void Run()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_HOME_RELATION_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets"))
            throw new InvalidOperationException("Select the independent project explicitly");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true); SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 120);
        EditorApplication.EnterPlaymode();
    }
    static void OnLog(string text, string stack, LogType type)
    {
        if (expectedErrors > 0 && type == LogType.Error && text.StartsWith("Official HomeRelationManager.", StringComparison.Ordinal)) { expectedErrors--; return; }
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }
    static void Finish(Exception failure)
    {
        SessionState.SetBool(Active, false); Application.logMessageReceived -= OnLog;
        foreach (var root in roots) if (root) UnityEngine.Object.DestroyImmediate(root);
        if (failure == null) Debug.Log("HOME_RELATIONS_PASSED: original overlap filters, registration/removal and ForceCheck ordering, live URP layer filtering/restoration and owner lifetime. Component scope only.");
        else Debug.LogException(failure);
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    static void Require(bool value, string text) { if (!value) throw new Exception(text); Debug.Log("CHECK: " + text); }
    static GameObject Root(string name) { var value = new GameObject(name); value.SetActive(false); roots.Add(value); return value; }
    static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    static HomeRelationProbe Probe(string name, IHomeRelationManager manager, HomeMutex.EMutexRole role, Vector3 position)
    {
        var root = Root(name); root.transform.position = position;
        var value = root.AddComponent<HomeRelationProbe>(); value.InjectRelationManager = manager;
        value.RelationGroup = EHomeRelationGroup.Bedroom; value.MutexRole = role; value.Rect = new Rect(-1, -1, 2, 2);
        return value;
    }
    static IEnumerator Check()
    {
        Require(Layers != null, "Installed native URP exposes the original light sorting-layer field");
        var manager = new HomeRelationManager();
        var master = Probe("OriginalMasterFixture", manager, HomeMutex.EMutexRole.Master, Vector3.zero);
        var slave = Probe("OriginalSlaveFixture", manager, HomeMutex.EMutexRole.Slave, new Vector3(1, 0, 10));
        Require(master.IsMutex(slave) && slave.IsMutex(master), "Original opposite-role world rectangles overlap independently of Z");
        slave.RelationGroup = EHomeRelationGroup.None; Require(!master.IsMutex(slave), "Different groups never overlap"); slave.RelationGroup = master.RelationGroup;
        slave.MutexRole = HomeMutex.EMutexRole.Master; Require(!master.IsMutex(slave), "Same roles never overlap"); slave.MutexRole = HomeMutex.EMutexRole.Slave;
        slave.transform.position = new Vector3(2, 0, 0); Require(!master.IsMutex(slave), "Touching rectangle edges keep the original non-overlap result");
        master.transform.localScale = Vector3.one * 100; master.transform.rotation = Quaternion.Euler(0, 0, 90);
        Require(!master.IsMutex(slave), "Original geometry translates rectangles without applying object scale or rotation");
        slave.transform.position = Vector3.right;
        master.gameObject.SetActive(true); Require(master.Changes == 0, "First registration has no counterpart callback");
        slave.gameObject.SetActive(true); Require(master.Changes == 1 && slave.Changes == 1, "Registration notifies the new object and its existing counterpart");
        slave.gameObject.SetActive(false); Require(master.Restores == 1 && slave.Restores == 1, "Unregistration restores both overlapping counterparts");
        master.gameObject.SetActive(false);

        int[] ids = SortingLayer.layers.Select(layer => layer.id).ToArray(); Require(ids.Length >= 2, "Original project sorting identities are available");
        var lightRoot = Root("OriginalLightLayerFixture");
        var light = lightRoot.AddComponent<Light2D>(); int[] original = { ids[0], ids[1], ids[0] }; Layers.SetValue(light, original);
        var relation = lightRoot.AddComponent<HomeMutexLightLayer>(); relation.InjectRelationManager = manager;
        relation.RelationGroup = EHomeRelationGroup.Bedroom; relation.MutexRole = HomeMutex.EMutexRole.Slave; relation.Rect = new Rect(-1, -1, 2, 2);
        Set(relation, "mLight", light); Set(relation, "mLayersToRemove", new SortingLayerMask { LayerIDs = new[] { ids[0] } });
        lightRoot.SetActive(true); yield return null;
        Require(!relation.Changed && ((int[])Layers.GetValue(light)).SequenceEqual(original), "Light Awake captures the original layer array without changing an unpaired light");
        master.gameObject.SetActive(true);
        Require(relation.Changed && ((int[])Layers.GetValue(light)).SequenceEqual(new[] { ids[1] }), "Live native light removes every occurrence of the configured sorting identity");
        var filtered = Layers.GetValue(light); manager.ForceCheck();
        Require(ReferenceEquals(filtered, Layers.GetValue(light)), "Changed guard preserves the existing filtered layer array on repeated checks");
        var second = Probe("SecondMasterFixture", manager, HomeMutex.EMutexRole.Master, Vector3.zero); second.gameObject.SetActive(true);
        master.gameObject.SetActive(false);
        Require(!relation.Changed && ReferenceEquals(original, Layers.GetValue(light)), "Original removal restores the captured array before any later global overlap check");
        manager.ForceCheck(); Require(relation.Changed, "Original ForceCheck reapplies a remaining master's overlap");
        second.transform.position = Vector3.right * 10; manager.ForceCheck();
        Require(!relation.Changed && ReferenceEquals(original, Layers.GetValue(light)), "Global recheck restores native layers after geometry moves apart");
        second.gameObject.SetActive(false); lightRoot.SetActive(false);

        var invalid = Probe("InvalidGroupFixture", manager, HomeMutex.EMutexRole.Master, Vector3.zero); invalid.RelationGroup = EHomeRelationGroup.None;
        expectedErrors = 2; invalid.gameObject.SetActive(true); invalid.gameObject.SetActive(false);
        Require(expectedErrors == 0, "None-group registration and removal retain both original diagnostics");
        manager.UnregisterHomeMutex(master); manager.ForceCheck();
        foreach (var root in roots) if (root) UnityEngine.Object.Destroy(root);
        yield return null;
        Require(roots.All(root => !root), "Fixture owner teardown leaves no live relation component");
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Zjcs.Presentation.Combat;

namespace UnityEngine
{
    public sealed class TextAsset
    {
        public readonly string text;
        public byte[] bytes { get { return Encoding.UTF8.GetBytes(text); } }
        public TextAsset(string value) { text = value; }
    }
    public sealed class GameObject { }
    public static class Resources
    {
        public static readonly Dictionary<string, object> Assets = new Dictionary<string, object>();
        public static T Load<T>(string path) where T : class
        { object value; return Assets.TryGetValue(path, out value) ? value as T : null; }
    }
    public static class JsonUtility
    {
        public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { IncludeFields = true });
    }
}

public static class BattleNativeGraphSourcesHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Native graph source: " + message); }
    private static void Reset()
    { typeof(OfficialBattleNativeGraphSources).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null); }
    private static void Put(string resource, string json, string manifest)
    {
        UnityEngine.Resources.Assets[resource] = new UnityEngine.TextAsset(json);
        string hash;
        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant();
        UnityEngine.Resources.Assets[resource + "-source-manifest"] = new UnityEngine.TextAsset(
            "{\"manifest\":\"" + manifest + "\",\"output_sha256\":\"" + hash + "\"}");
    }
    private static string Catalog(string version, string path)
    { return "{\"manifest\":\"" + version + "\",\"skills\":[{\"skill_entity_id\":10,\"playables\":[{\"field\":\"myPlayablePrefab\",\"resource\":\"" + path + "\"}]}]}"; }
    private static void Rejected(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (FormatException) { rejected = true; } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message);
    }
    public static string Run()
    {
        const string original = "Combat/skill-native-graphs", migrated = "Combat/migrated";
        var assets = UnityEngine.Resources.Assets;
        assets.Clear(); Reset();
        Put(original, Catalog("old-source", "old"), "old-source");
        Put(migrated, Catalog("current-source", "new"), "current-source");
        assets["Combat/skill-native-graph-sources"] = new UnityEngine.TextAsset("{\"resources\":[\"" + original + "\",\"" + migrated + "\"]}");
        assets["old"] = new UnityEngine.GameObject(); assets["new"] = new UnityEngine.GameObject();
        Check(OfficialBattleNativeGraphSources.LoadAll().Count == 2, "different verified sources were rejected");
        var catalog = OfficialBattleSkillGraphCatalog.TryLoadDefault();
        Check(catalog.Count == 1 && ReferenceEquals(catalog.Find(10, "myPlayablePrefab"), assets["new"]), "scoped graph did not replace its original binding");
        Put("Combat/skill-entity-views", "{\"manifest\":\"independent-source\",\"skills\":[{\"skillEntityId\":10,\"preferredPosToPlayable\":true,\"showSkillReplay\":false}]}", "independent-source");
        var view = OfficialBattleSkillEntityViewCatalog.Load().Find(10);
        Check(view.preferredPosToPlayable && !view.showSkillReplay, "verified native view fields still require a fixed source version");
        assets[migrated] = new UnityEngine.TextAsset(Catalog("current-source", "tampered")); Reset();
        Rejected(() => OfficialBattleNativeGraphSources.LoadAll(), "mutated payload was accepted");
        Put(migrated, Catalog("current-source", "new"), "other-source"); Reset();
        Rejected(() => OfficialBattleNativeGraphSources.LoadAll(), "mixed source metadata was accepted");
        Put(migrated, Catalog("current-source", "new"), "current-source");
        assets.Remove(migrated + "-source-manifest"); Reset();
        Rejected(() => OfficialBattleNativeGraphSources.LoadAll(), "missing source metadata was accepted");
        Put(migrated, Catalog("current-source", "new"), "current-source");
        assets["Combat/skill-native-graph-sources"] = new UnityEngine.TextAsset("{\"resources\":[\"" + original + "\",\"" + migrated + "\",\"" + migrated + "\"]}"); Reset();
        Rejected(() => OfficialBattleNativeGraphSources.LoadAll(), "duplicate source was accepted");
        Put("Combat/other", Catalog("next-source", "new"), "next-source");
        assets["Combat/skill-native-graph-sources"] = new UnityEngine.TextAsset("{\"resources\":[\"" + original + "\",\"" + migrated + "\",\"Combat/other\"]}"); Reset();
        Rejected(() => OfficialBattleSkillGraphCatalog.TryLoadDefault(), "two migrated graphs silently overrode each other");
        return "Native graph sources passed: independent sources, scoped replacement, payload integrity, source consistency, missing proofs and overlap rejection.";
    }
}

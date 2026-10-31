using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using PartnerPropertyReference.Common;

[InitializeOnLoad]
public static class PartnerPropertyCatalogValidation
{
    static PartnerPropertyCatalogValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("PartnerPropertyCatalog", false)) return;
            EditorApplication.delayCall += () => {
                try { Check(); EditorApplication.Exit(0); }
                catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
                finally { SessionState.SetBool("PartnerPropertyCatalog", false); }
            };
        };
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("PartnerPropertyCatalog", true);
        EditorApplication.isPlaying = true;
    }
    private static void Check()
    {
        Require(Application.isPlaying, "actual Play Mode");
        var catalog = OfficialPartnerAssetCatalog.LoadDefault();
        Require(catalog.Manifest == "884_156692" && catalog.PropertyClassIds.Count == 18, "selected version and classes");
        string path = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "reference-level-prop.g.bytes");
        int compared = 0;
        using (var original = new LevelPropBinaryReader(File.ReadAllBytes(path)))
        {
            foreach (int id in catalog.PropertyClassIds)
            {
                Require(original.TryGetLevelRange(id, out int first, out int last), "original class range");
                for (int level = first; level <= last; level++)
                {
                    Require(original.TryGetProps(id, level, out var expected), "original row");
                    var actual = catalog.GetLevelProps(id, level);
                    Require(actual != null && actual.Count == expected.Count && expected.All(value => actual.TryGetValue((int)value.Key, out long count) && count == value.Value), "original signed 64-bit property row");
                    if (level > first)
                    {
                        original.TryGetProps(id, level - 1, out var before);
                        var delta = expected.Where(value => !before.TryGetValue(value.Key, out long count) || count != value.Value)
                            .ToDictionary(value => (int)value.Key, value => value.Value - (before.TryGetValue(value.Key, out long count) ? count : 0));
                        var actualDelta = catalog.GetFriendshipLevelPropDiff(id, level - 1, level);
                        Require(actualDelta.Count == delta.Count && delta.All(value => actualDelta[value.Key] == value.Value), "original friendship parser delta");
                    }
                    compared++;
                }
                Require(catalog.GetLevelProps(id, first - 1) == null, "no invented lower row");
                var maximum = catalog.GetLevelProps(id, last);
                Require(ReferenceEquals(maximum, catalog.GetLevelProps(id, last + 1)), "original maximum clamp");
            }
        }
        Require(compared == 1800 && catalog.GetLevelProps(-1, 1) == null, "complete reference comparison");
        string root = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"), "unity/Assets/Official");
        Func<string, string> read = name => File.ReadAllText(Path.Combine(root, name));
        bool rejected = false;
        try { OfficialPartnerCatalog.Parse(read("Partners/partner-rules.json"), read("Partners/partner-properties.json").Replace("\"manifest\":\"884_156692\"", "\"manifest\":\"854_156383\"")); }
        catch (FormatException) { rejected = true; }
        Require(rejected, "mixed source versions rejected");
        var character = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"),
            read("Character/item-time-limits.json"), read("CharacterCreation/wardrobe.json"),
            read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"), catalog);
        foreach (var npc in catalog.Rows("npc"))
        {
            var legacy = character.Find("npc", Convert.ToInt32(npc["Id"]), "Id");
            Require(legacy != null && Convert.ToInt32(legacy["LevelPropId"]) == Convert.ToInt32(npc["LevelPropId"]), "verified shared NPC property identity");
        }
        var player = new OfflinePlayerProgress();
        player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8401, Level = 2 });
        var source = new OfflineCharacterConfiguredPropSource(character, CharacterPropHandleType.NpcFriendshipLevel);
        var result = source.CalculateProps(player);
        Require(result.Count == 2 && result[3] == 174 && result[57] == 300, "real property chunk receives current NPC bonus");
        var baseline = character.GetLevelProps(5015, 2);
        Require(baseline[3] == 174 && baseline[57] == 300, "shared character consumers use the selected NPC rows");
        Debug.Log("PARTNER_PROPERTY_CATALOG_PASSED: 1800 original binary rows, 1782 original deltas, bounds, 148 NPC identities, actual friendship property chunk. PlayMode=True; complete partner UI/save/Player not covered.");
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Partner property catalog: " + message);
    }
}

namespace PartnerPropertyReference.Common
{
    internal interface IPropDictionary : IDictionary<PropType, long> { }
    internal sealed class ReferenceDictionary : Dictionary<PropType, long>, IPropDictionary { }
    internal static class PropDictionaryUtils { public static IPropDictionary Create() => new ReferenceDictionary(); }
}
namespace PartnerPropertyReference.Framework.Logging
{
    internal static class Log
    {
        public static void Error(ref InterpolatedStringHandler.GLoggerErrorInterpolatedStringHandler message, object context, string member, string file, int line)
        { throw new InvalidDataException(message.ToString()); }
        public static void Error(Exception exception, ref InterpolatedStringHandler.GLoggerErrorInterpolatedStringHandler message, object context, string member, string file, int line)
        { throw new InvalidDataException(message.ToString(), exception); }
    }
}
namespace PartnerPropertyReference.Framework.Logging.InterpolatedStringHandler
{
    internal struct GLoggerErrorInterpolatedStringHandler
    {
        private System.Text.StringBuilder text;
        public GLoggerErrorInterpolatedStringHandler(int literal, int count, out bool enabled) { enabled = true; text = new System.Text.StringBuilder(); }
        public void AppendLiteral(string value) { text.Append(value); }
        public void AppendFormatted<T>(T value, int alignment, string format, string expression) { text.Append(value); }
        public override string ToString() => text.ToString();
    }
}

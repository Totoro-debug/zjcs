using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.PartnersUI;

[InitializeOnLoad]
public static class PartnerGiftConfigurationValidation
{
    static PartnerGiftConfigurationValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("PartnerGiftConfiguration", false)) return;
            EditorApplication.delayCall += () => {
                try { Check(); EditorApplication.Exit(0); }
                catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
                finally { SessionState.SetBool("PartnerGiftConfiguration", false); }
            };
        };
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("PartnerGiftConfiguration", true);
        EditorApplication.isPlaying = true;
    }
    private static int Int(Dictionary<string, object> row, string key) => Convert.ToInt32(row[key]);
    private static long Exp(Dictionary<string, object> row) => Convert.ToInt64(row["FriendshipExp"]);
    private static void Check()
    {
        Require(Application.isPlaying, "actual Play Mode");
        var catalog = OfficialPartnerAssetCatalog.LoadDefault();
        var configuration = OfficialPartnerGiftAssetCatalog.LoadDefault();
        var groups = catalog.Rows("npc_friendship_level").GroupBy(row => Int(row, "Id")).ToDictionary(group => group.Key, group => group.ToList());
        int thresholds = 0, caps = 0;
        foreach (var npc in catalog.Rows("npc"))
        {
            int id = Int(npc, "Id");
            var actual = configuration.GetNpcInfo(id);
            Require(actual.Id == id && actual.Properties.LevelPropId == Int(npc, "LevelPropId") && actual.NpcRoleType == Int(npc, "NpcRoleType"), "selected NPC identity");
            bool present = groups.TryGetValue(id, out var rows);
            Require(configuration.GetMaxLevel(id) == (present ? rows.Max(row => Int(row, "Level")) : 0), "global max");
            Require(present == (configuration.Get(id) != null), "absent group");
            if (!present) continue;
            Require(configuration.Get(id).Keys.SequenceEqual(rows.Select(row => Int(row, "Level"))), "original configured enumeration order");
            var ranks = rows.Select(row => Int(row, "SubRankLimit")).SelectMany(rank => new[] { rank - 1, rank, rank + 1 }).Append(0).Distinct();
            foreach (int rank in ranks)
            {
                int required = rows.Where(row => Int(row, "SubRankLimit") <= rank).Select(row => Int(row, "SubRankLimit")).DefaultIfEmpty(int.MinValue).Max();
                int expected = !(bool)npc["CanDevelop"] || required == int.MinValue ? 0 : rows.Where(row => Int(row, "SubRankLimit") == required).Max(row => Int(row, "Level"));
                Require(configuration.GetMaxSubRankLevel(id, rank) == expected, "rank-group cap and CanDevelop gate");
                caps++;
            }
            foreach (var row in rows)
            {
                long exp = Exp(row); int rank = Int(row, "SubRankLimit");
                Require(configuration.Get(id, Int(row, "Level")).FriendshipExp == exp, "original cumulative threshold");
                foreach (long sample in new[] { exp - 1, exp, exp + 1 })
                {
                    int expected = rows.Where(value => Exp(value) <= sample && Int(value, "SubRankLimit") <= rank).Select(value => Int(value, "Level")).DefaultIfEmpty().Max();
                    Require(configuration.GetFriendshipLevelByExp(id, sample, rank) == expected, "threshold before/exact/after");
                }
                thresholds++;
            }
        }
        var npcIds = new HashSet<int>(catalog.Rows("npc").Select(row => Int(row, "Id")));
        foreach (var group in groups.Where(pair => !npcIds.Contains(pair.Key)))
        {
            Require(configuration.GetNpcInfo(group.Key) == null && configuration.GetMaxLevel(group.Key) == 100
                && configuration.GetMaxSubRankLevel(group.Key, 99) == 0, "retained level group without NPC definition");
            foreach (var row in group.Value)
            {
                long exp = Exp(row); int rank = Int(row, "SubRankLimit");
                Require(configuration.Get(group.Key, Int(row, "Level")).FriendshipExp == exp, "unbound NPC group threshold");
                foreach (long sample in new[] { exp - 1, exp, exp + 1 })
                {
                    int expected = group.Value.Where(value => Exp(value) <= sample && Int(value, "SubRankLimit") <= rank).Select(value => Int(value, "Level")).DefaultIfEmpty().Max();
                    Require(configuration.GetFriendshipLevelByExp(group.Key, sample, rank) == expected, "unbound NPC group threshold boundary");
                }
                thresholds++;
            }
        }
        Require(thresholds == 10000, "all selected friendship rows including 8403/8419");
        Require(configuration.GetMaxLevel(-1) == 0 && configuration.Get(-1) == null && configuration.GetMaxLevelInfo(-1) == null && configuration.GetFriendshipLevelByExp(-1, long.MaxValue, 99) == 0, "unknown NPC");
        var npc8401 = configuration.GetNpcInfo(8401);
        Require(npc8401.GiftPreferenceDict.Count == 7 && npc8401.GiftPreferenceDict[1300] == 2 && npc8401.GiftPreferenceDict[1130] == 1.25, "selected preference map");
        var unlocks = catalog.Rows("npc_unlock_item").ToDictionary(row => Int(row, "ClassId"));
        var items = catalog.Rows("item").ToDictionary(row => Int(row, "ClassId"));
        int gifts = 0;
        foreach (var row in catalog.Rows("npc_gift"))
        {
            int id = Int(row, "ClassId"), friendship = Int(row, "Friendship");
            Require(configuration.GetGiftInfo(id).Friendship == friendship, "selected gift experience");
            bool allowed = Int(items[id], "ItemType") != 433 || (unlocks.TryGetValue(id, out var unlock) && (bool)unlock["CanSendGift"]);
            foreach (double factor in new[] { 1d, 1.25d, 2d, 0.5d })
                foreach (long count in new[] { 1L, 2L, 3L, 9L })
                {
                    int expected = allowed ? (int)Math.Round((double)(friendship * count) * factor, MidpointRounding.ToEven) : 0;
                    Require(configuration.GetAllExpByDict(new Dictionary<int, long> { [id] = count }, new Dictionary<int, double> { [id] = factor }) == expected, "original gift factor/rounding/unlock exclusion");
                }
            gifts++;
        }
        // Item 1130 gives 2400 before its 1.25 preference; mixed gifts round
        // per stack before summing, retaining the original double conversion.
        Require(configuration.GetAllExpByDict(new Dictionary<int, long> { [1130] = 1, [1300] = 1 }, npc8401.GiftPreferenceDict) == 3000 + 4800, "real preferred mixed gifts");
        Require(configuration.GetAllExpByDict(new Dictionary<int, long> { [1130] = 1 }, new Dictionary<int, double>()) == 2400, "configured default factor");
        Require(configuration.GetAllExpByDict(new Dictionary<int, long> { [1130] = 1, [-1] = 1 }, npc8401.GiftPreferenceDict) == 0, "unknown item resets entire result");
        foreach (int role in Enumerable.Range(0, 5))
            foreach (int level in Enumerable.Range(0, 102))
            {
                var expected = catalog.Rows("npc_friendship_tag").FirstOrDefault(row => row["NpcRoleTypes"] is List<object> roles && roles.Any(value => Convert.ToInt32(value) == role)
                    && row["LevelRanges"] is List<object> range && range.Count == 2 && level >= Convert.ToInt32(range[0]) && level <= Convert.ToInt32(range[1]));
                var actual = configuration.GetTagInfoByRoleTypeAndLevel(role, level);
                Require(expected == null ? actual == null : actual != null && actual.ClassId == Int(expected, "ClassId") && actual.TagBg == (string)expected["TagBg"], "original ordered inclusive tag range");
            }
        Require(configuration.CanCustomTagFriendShipLevel == 100 && configuration.CheckNpcFriendshipTagLegal("") == 1513 && configuration.CheckNpcFriendshipTagLegal(null) == 1513, "custom-title settings and empty error");
        Require(configuration.CheckNpcFriendshipTagLegal("伙伴伙伴伙伴") == 0 && configuration.CheckNpcFriendshipTagLegal("伙伴伙伴伙伴伙") == 1514, "CJK 12/14 boundary");
        Require(configuration.CheckNpcFriendshipTagLegal("abcdefghijkl") == 0 && configuration.CheckNpcFriendshipTagLegal("abcdefghijklm") == 1514, "ASCII 12/13 boundary");
        Require(configuration.GetCharacterCount("a\u0301中") == 3 && configuration.GetCharacterCount("\U00020000") == 2, "grapheme and extended CJK");
        string root = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        string characters = File.ReadAllText(Path.Combine(root, "unity/Assets/Official/Partners/partner-custom-characters.txt"));
        Require(configuration.IsAllCharacterValid(characters) && configuration.IsAllCharacterValid("伙伴ABC123") && configuration.IsAllCharacterValid(""), "entire original whitelist");
        var originalSet = new HashSet<char>(characters);
        char excluded = Enumerable.Range(1, 65534).Select(value => (char)value).First(value => !originalSet.Contains(value));
        Require(!configuration.IsAllCharacterValid("伙伴" + excluded), "original per-char whitelist rejection");
        Debug.Log("PARTNER_GIFT_CONFIGURATION_PASSED: " + thresholds + " thresholds x3, " + caps + " rank caps, " + gifts + " gift definitions x16, tag ranges, original CN whitelist/CJK. PlayMode=True; inventory/save/full windows/Player not covered.");
    }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Partner gift configuration: " + message); }
}

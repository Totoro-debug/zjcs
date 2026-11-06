using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Offline.PartnersNpc;
using Zjcs.Offline.Progression;
using Zjcs.Official.PartnersUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfflinePartnerDetailConfiguration
    {
        private readonly Dictionary<int, PartnerDetailPresentationInfo> npcs = new Dictionary<int, PartnerDetailPresentationInfo>();
        private readonly Dictionary<int, OriginalImages> images = new Dictionary<int, OriginalImages>();
        private readonly Dictionary<int, Dictionary<int, PartnerDetailAnimationInfo>> levels = new Dictionary<int, Dictionary<int, PartnerDetailAnimationInfo>>();
        public OfflinePartnerDetailConfiguration(OfficialPartnerCatalog catalog, OfflinePartnerGiftConfiguration gifts, string regionJson)
        {
            if (catalog == null || gifts == null) throw new ArgumentNullException();
            var data = JObject.Parse(regionJson);
            if ((string)data["manifest"] != catalog.Manifest) throw new FormatException("Partner region image version differs.");
            var regions = new Dictionary<int, RegionImages>();
            Dictionary<int, string> Read(JToken token) => token is JObject map
                ? map.Properties().ToDictionary(item => int.Parse(item.Name, CultureInfo.InvariantCulture), item => (string)item.Value) : null;
            foreach (var row in data["rows"])
                regions.Add((int)row["NpcId"], new RegionImages {
                    RegionFullBodyRImages = Read(row["RegionFullBodyRImages"]),
                    RegionFullBodyLiveImages = Read(row["RegionFullBodyLiveImages"]) });
            foreach (var row in catalog.Rows("npc"))
            {
                int id = Int(row, "Id");
                Vector3 Vector(string key)
                {
                    var value = (Dictionary<string, object>)row[key];
                    return new Vector3(Convert.ToSingle(value["X"], CultureInfo.InvariantCulture),
                        Convert.ToSingle(value["Y"], CultureInfo.InvariantCulture), Convert.ToSingle(value["Z"], CultureInfo.InvariantCulture));
                }
                npcs.Add(id, new PartnerDetailPresentationInfo { Id = id,
                    MapGroupId = Int(row, "MapGroupId"), TravelProfession = Int(row, "TravelProfession"),
                    PreferenceGiftType = Int(row, "PreferenceGiftType"), IsSpecial = Int(row, "NpcRoleType") == 1,
                    IsShadow = (bool)row["IsShadow"], NameKey = (string)row["NameKey"],
                    BigBgImage = (string)row["BigBgImage"],
                    FigureModelUI = OfflinePartnerNpcRuntime.ResolveFigureModelUI(id, (string)row["FigureModelUI"]),
                    LiveOffset = Vector("LiveOffset"), LiveScale = Vector("LiveScale"),
                    Properties = new PartnerDetailNpcInfo { Id = id, TreasureClassId = Int(row, "TreasureClassId"),
                        IsStarGodNpc = Int(row, "NpcRoleType") == 2, Friendship = gifts.GetNpcInfo(id) } });
                images.Add(id, new OriginalImages(id, (string)row["FullBodyImage"], (string)row["FullBodyLiveImage"],
                    npcId => regions.TryGetValue(npcId, out var value) ? value : null));
            }
            foreach (var row in catalog.Rows("npc_friendship_level"))
            {
                int id = Int(row, "Id");
                if (!levels.TryGetValue(id, out var group)) levels.Add(id, group = new Dictionary<int, PartnerDetailAnimationInfo>());
                group.Add(Int(row, "Level"), new PartnerDetailAnimationInfo { Animation = (string)row["Animation"],
                    TalkIdList = row["TalkIdList"] is List<object> values ? values.Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)).ToList() : null });
            }
        }
        public PartnerDetailPresentationInfo GetNpcInfo(int id) => npcs.TryGetValue(id, out var value) ? value : null;
        public PartnerDetailAnimationInfo GetFriendshipAnimation(int id, int level)
            => levels.TryGetValue(id, out var group) && group.TryGetValue(level, out var value) ? value : null;
        public PartnerFullBodyImageType GetFullBodyImagePath(int id, out string path, int? region = null)
            => images[id].GetFullBodyImagePath(out path, region);
        public bool TryGetFullBodyImagePath(int id, PartnerFullBodyImageType kind, out string path, int? region = null)
            => images[id].TryGetFullBodyImagePath(kind, out path, region);
        /*STARTING_ANIMATION*/
        private static int Int(Dictionary<string, object> row, string key) => Convert.ToInt32(row[key], CultureInfo.InvariantCulture);
        private sealed class RegionImages
        {
            public Dictionary<int, string> RegionFullBodyRImages, RegionFullBodyLiveImages;
        }
        private sealed class OriginalImages
        {
            private readonly int Id;
            private readonly string FullBodyImage, FullBodyLiveImage;
            private readonly Func<int, RegionImages> getRegion;
            internal OriginalImages(int id, string fullBodyImage, string fullBodyLiveImage, Func<int, RegionImages> getRegion)
            { Id = id; FullBodyImage = fullBodyImage; FullBodyLiveImage = fullBodyLiveImage; this.getRegion = getRegion; }
            /*REGION_METHODS*/
        }
    }
}

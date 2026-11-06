// Selected official methods; see partner-detail-configuration-source-manifest.json.
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
            public static string GetNpcStartingAnimName(int npcId)
    {
        return $"npc_{npcId}/idle_01";
    }
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
                public PartnerFullBodyImageType GetFullBodyImagePath(out string path, int? region = null)
    {
        int regionCodeEnum = region ?? 0;
        if (TryGetFullBodyImagePathInternal(PartnerFullBodyImageType.Live, regionCodeEnum, out path))
        {
            return PartnerFullBodyImageType.Live;
        }
        if (TryGetFullBodyImagePathInternal(PartnerFullBodyImageType.Static, regionCodeEnum, out path))
        {
            return PartnerFullBodyImageType.Static;
        }
        Debug.LogWarning("NpcInfo.GetFullBodyImagePath npcId = " + Id + " region = " + regionCodeEnum);
        path = null;
        return PartnerFullBodyImageType.None;
    }

    public bool TryGetFullBodyImagePath(PartnerFullBodyImageType imageType, out string path, int? region = null)
    {
        int regionCodeEnum = region ?? 0;
        if (TryGetFullBodyImagePathInternal(imageType, regionCodeEnum, out path))
        {
            return true;
        }
        if (imageType != PartnerFullBodyImageType.None)
        {
        Debug.LogWarning("NpcInfo.TryGetFullBodyImagePath npcId = " + Id + " region = " + regionCodeEnum);
        }
        return false;
    }

    private bool TryGetFullBodyImagePathInternal(PartnerFullBodyImageType imageType, int targetRegion, out string path)
    {
        path = null;
        RegionImages npcRegionImageInfo = getRegion(Id);
        switch (imageType)
        {
        case PartnerFullBodyImageType.Static:
            if (npcRegionImageInfo != null && TryGetRegionImagePath(npcRegionImageInfo.RegionFullBodyRImages, targetRegion, out path))
            {
                return true;
            }
            path = FullBodyImage;
            return !string.IsNullOrEmpty(path);
        case PartnerFullBodyImageType.Live:
            if (npcRegionImageInfo != null && TryGetRegionImagePath(npcRegionImageInfo.RegionFullBodyLiveImages, targetRegion, out path))
            {
                return true;
            }
            path = FullBodyLiveImage;
            return !string.IsNullOrEmpty(path);
        default:
        {
        Debug.LogError("NpcInfo.TryGetFullBodyImagePath npcId = " + Id + " 当前类型不支持! type = " + imageType);
            return false;
        }
        }
    }

    private bool TryGetRegionImagePath(Dictionary<int, string> regionImages, int targetRegion, out string path)
    {
        path = null;
        if (regionImages == null || !regionImages.TryGetValue(targetRegion, out path))
        {
            return false;
        }
        return !string.IsNullOrEmpty(path);
    }
        }
    }
}

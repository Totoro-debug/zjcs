// Selected original profile parser and friendship condition, with local configuration/player ports.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Official.Partners
{
    public interface IPartnerProfilePlayer
    {
        bool TryGetNpcFriendshipLevel(int npcId, out int level);
    }

    public sealed class PartnerProfileRules
    {
        private readonly Dictionary<int, List<ProfileInfo>> mNpcProfileInfoDict = new Dictionary<int, List<ProfileInfo>>();
        private readonly Dictionary<int, Dictionary<int, ProfileInfo>> profiles;
        public PartnerProfileRules(Func<string, IEnumerable<Dictionary<string, object>>> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            profiles = new Dictionary<int, Dictionary<int, ProfileInfo>>();
            foreach (var row in rows("npc_profile"))
            {
                int id = Convert.ToInt32(row["Id"]), chapter = Convert.ToInt32(row["ProfileId"]);
                var raw = (Dictionary<string, object>)row["UnlockCondition"];
                if ((string)raw["_type"] != "NpcFriendshipLevelCondition")
                    throw new NotSupportedException("Review the selected profile condition: " + raw["_type"]);
                var condition = new FriendshipCondition { NpcClassId = Convert.ToInt32(raw["NpcClassId"]),
                    FriendshipLevel = Convert.ToInt32(raw["FriendshipLevel"]), IsReverse = (bool)raw["IsReverse"] };
                var info = new ProfileInfo { Id = id, ProfileId = chapter, UnlockCondition = condition,
                    ShowLongContentImage = (bool)row["ShowLongContentImage"],
                    Reward = row["Reward"] == null ? null : ((Dictionary<string, object>)row["Reward"]).ToDictionary(
                        pair => int.Parse(pair.Key, System.Globalization.CultureInfo.InvariantCulture), pair => Convert.ToInt64(pair.Value)) };
                if (!profiles.TryGetValue(id, out var chapters)) profiles.Add(id, chapters = new Dictionary<int, ProfileInfo>());
                chapters.Add(chapter, info);
            }
            PostParse(profiles);
        }
        public ProfileInfo Get(int id, int chapter) => profiles.TryGetValue(id, out var chapters)
            && chapters.TryGetValue(chapter, out var info) ? info : null;
        /*PARSER_METHODS*/
        public sealed class ProfileInfo
        {
            public int Id, ProfileId;
            public bool ShowLongContentImage;
            public Dictionary<int, long> Reward;
            public FriendshipCondition UnlockCondition;
        }
        public sealed class FriendshipCondition
        {
            public int NpcClassId, FriendshipLevel;
            public bool IsReverse;
            public bool IsMet(IPartnerProfilePlayer player) => IsMet(new DataHub(player));
            /*CONDITION_METHODS*/
            /*DESCRIPTION*/
        }
        public readonly struct DataHub
        {
            private readonly IPartnerProfilePlayer player;
            public DataHub(IPartnerProfilePlayer player) { this.player = player; }
            public bool TryGetPlayer(out IPartnerProfilePlayer value) { value = player; return value != null; }
        }
    }
}

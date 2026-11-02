using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.Progression
{
    public interface IOfflinePartnerConversationWorld
    {
        int CalculateWorldMapProgress(int mapClassId, int region);
        bool IsRegionGoddessActive(int mapClassId, int region);
    }

    // Reads live session state after a reload/commit; no cached friendship,
    // story or ended-dialogue collections can outlive the saved player object.
    public sealed class OfflinePartnerConversationPlayer : IPartnerConversationPlayer,
        IPartnerConversationConditionPlayer
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialCharacterCatalog character;
        private readonly IOfflinePartnerConversationWorld world;
        private readonly Func<DateTime> clock;
        private readonly Action<PartnerConversationRecord> update;
        private readonly Func<OfflinePlayerProgress> playerSource;

        public OfflinePartnerConversationPlayer(OfflineMainlineProgressSession session,
            OfficialCharacterCatalog character, IPartnerConversationConfiguration configuration,
            IOfflinePartnerConversationWorld world, Func<DateTime> clock,
            Action<PartnerConversationRecord> update, Func<OfflinePlayerProgress> playerSource = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.character = character ?? throw new ArgumentNullException(nameof(character));
            DialogueConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.update = update ?? throw new ArgumentNullException(nameof(update));
            this.playerSource = playerSource ?? (() => session.Save.Player);
        }

        public OfflinePartnerConversationPlayer ForCharacterMutation(OfflinePlayerProgress player,
            Action<PartnerConversationRecord> update)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            return new OfflinePartnerConversationPlayer(session, character, DialogueConfiguration,
                world, clock, update, () => player);
        }

        public IPartnerConversationConfiguration DialogueConfiguration { get; }
        public DateTime Now => clock();
        public void FireNpcDialogueUpdate(PartnerConversationRecord record) => update(record);
        public int BaseLevel
        {
            get
            {
                var player = playerSource();
                int limit = OfflineCharacterExperience.BaseLevelLimit(player.SubRank, character.LevelLimits);
                return limit > 0 ? Math.Min(player.Level, limit) : player.Level;
            }
        }
        public long GetItemAmount(int classId) => playerSource().Inventory.FirstOrDefault(value => value.ItemId == classId)?.Count ?? 0;
        public bool IsMapAccessible(int mapClassId) => playerSource().UnlockedMapClassIds.Contains(mapClassId);
        public bool RegionIsActived(int mapClassId, int region) => session.Save.World.ActivatedGoddessRegions
            .Any(value => value.MapClassId == mapClassId && value.Region == region);
        public bool HasNpc(int npcClassId) => playerSource().CharacterProperties.NpcFriendships
            .Any(value => value.NpcClassId == npcClassId);
        public int GetNpcFriendshipLevel(int npcClassId) => playerSource().CharacterProperties.NpcFriendships
            .FirstOrDefault(value => value.NpcClassId == npcClassId)?.Level ?? 0;
        public IEnumerable<PartnerConversationRecord> GetEndDialogues() => playerSource().CharacterProperties
            .NpcConversations.Records.Where(value => value.DialogueStatus == PartnerConversationStatus.End)
            .Select(value => value.ToRecord());
        public int GetStoryCount(string storyKey) => session.StoryCount(storyKey);
        public int CalculateWorldMapProgress(int mapClassId, int region) => world.CalculateWorldMapProgress(mapClassId, region);
        public bool IsRegionGoddessActive(int mapClassId, int region) => world.IsRegionGoddessActive(mapClassId, region);
    }
}

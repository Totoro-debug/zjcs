using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public interface IOfflinePartnerPresentation
    {
        void BindNpcTipDot(TipDotNodeLinker linker, int npcClassId);
        void PostSound(AudioClip sound, GameObject owner);
        void ShowFriendshipLevelUp(NpcRosterItem npc, long oldFriendship, int oldLevel);
        void ShowExclusiveGift(int itemId, Component owner);
        void ShowNpcDetails(List<NpcRosterItem> npcs, int index);
        void ShowError(int code);
        void ShowProgressionResult(ProgressionResult result);
    }

    // Local replacement for MeManager/parser/proxy calls consumed by the original
    // roster controllers. All state changes still use the actual save service.
    public sealed class OfflinePartnerRosterUiServices : IPartnerRosterUiServices
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialCharacterCatalog catalog;
        private readonly OfflineCharacterProgressionService progression;
        private readonly IProfessionImageResourceLoader images;
        private readonly IOfflinePartnerPresentation presentation;
        private Dictionary<int, NpcRosterItem> known;

        public OfflinePartnerRosterUiServices(OfflineMainlineProgressSession session,
            OfficialCharacterCatalog catalog, OfflineCharacterProgressionService progression,
            IProfessionImageResourceLoader images, IOfflinePartnerPresentation presentation)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
            this.images = images ?? throw new ArgumentNullException(nameof(images));
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            known = Snapshot();
        }

        public bool MainPlayerAvailable => session.Save.Player != null;
        public bool IsNormalNpcDisabled => false; // Local play has no server maintenance switch.
        public bool CanPinNpc => progression.CanPinNpc;
        public int MetNpcCount => session.Save.Player.CharacterProperties.NpcFriendships.Count;
        public event Action<int, NpcMeetType> NpcAdded;
        public event Action<int, bool> NpcCollectedChanged;
        public event Action<int> NpcRemoved;
        public event Action<int> NpcFriendshipUpdated;
        public IReadOnlyList<NpcRosterItem> NpcRoster(bool showAll) => progression.NpcRoster(showAll);
        public NpcMeetStatus MeetStatusOf(int id) => progression.NpcMeetStatusOf(id);
        public NpcRosterItem GetNpc(int id) => SnapshotOne(id);

        public PartnerNpcDefinition GetDefinition(int id)
        {
            var row = catalog.Find("npc", id, "Id");
            if (row == null) throw new InvalidOperationException("Official partner definition is missing: " + id);
            return new PartnerNpcDefinition { Id = id, NpcRoleType = (NpcRoleType)Int(row, "NpcRoleType"),
                NameKey = Text(row, "NameKey"), FigureImage = Text(row, "FigureImage"), SmallBgImage = Text(row, "SmallBgImage"),
                ExclusiveGiftId = row.TryGetValue("ExclusiveGiftIds", out var gifts) && gifts is List<object> values
                    ? Convert.ToInt32(values.FirstOrDefault() ?? 0) : 0 };
        }

        public bool HasItemDefinition(int id) => catalog.FindItem(id) != null;
        public int FriendshipMaxLevel(int id) => catalog.Rows("npc_friendship_level").Where(row => Int(row, "Id") == id)
            .Select(row => Int(row, "Level")).DefaultIfEmpty().Max();
        public long FriendshipExp(int id, int level)
        {
            var row = catalog.Rows("npc_friendship_level").FirstOrDefault(value => Int(value, "Id") == id && Int(value, "Level") == level);
            if (row == null) throw new InvalidOperationException("Official partner friendship threshold is missing: " + id + "/" + level);
            return Number(row, "FriendshipExp");
        }

        public PartnerMeetProgress? MeetProgress(int id)
        {
            // NpcInfo.GetHasItemCanMeetCondition checks the condition itself or
            // the FIRST direct HasItem child of a composite, without flattening.
            var row = catalog.Find("npc", id, "Id");
            var condition = Map(row, "CanMeetCondition");
            if (condition == null) return null;
            string type = Text(condition, "_type");
            if (type == "CompositeAndCondition" || type == "CompositeOrCondition")
            {
                if (!(condition.TryGetValue("Ids", out var ids) && ids is List<object> values)) return null;
                condition = values.Select(value => Map(catalog.Find("conditions", Convert.ToInt32(value)), "Condition"))
                    .FirstOrDefault(value => value != null && Text(value, "_type") == "HasItemCondition");
            }
            if (condition == null || Text(condition, "_type") != "HasItemCondition") return null;
            int itemId = Int(condition, "ClassId");
            long count = session.Save.Player.Inventory.Where(item => item.ItemId == itemId).Sum(item => item.Count);
            return new PartnerMeetProgress(count, Number(condition, "Count"));
        }

        public IProfessionAssetOperation<Sprite> LoadSprite(string path)
        {
            var operation = images.LoadAsset<Sprite>(path);
            if (operation == null || !operation.IsValid || operation.Result == null)
            {
                operation?.Dispose();
                throw new InvalidOperationException("Official partner sprite is not explicitly bound: " + path);
            }
            return operation;
        }

        public async Task<bool> MeetNpc(int id)
        {
            // The original proxy completes on a later player-loop continuation.
            // Publish only after the local item/friendship transaction is saved.
            await Task.Yield();
            var result = progression.MeetNpc(id, "partner-ui-meet-" + Guid.NewGuid().ToString("N"));
            if (!result.IsApplied) { presentation.ShowProgressionResult(result); return false; }
            known = Snapshot();
            NpcAdded?.Invoke(id, NpcMeetType.Manual);
            NpcFriendshipUpdated?.Invoke(id);
            return true;
        }

        public ProgressionResult SetCollected(int id, bool collected)
        {
            var result = progression.SetNpcCollected(id, collected, "partner-ui-pin-" + Guid.NewGuid().ToString("N"));
            if (result.IsApplied) { known = Snapshot(); NpcCollectedChanged?.Invoke(id, collected); }
            else if (result.Status != ProgressionResultStatus.Unchanged) presentation.ShowProgressionResult(result);
            return result;
        }

        // Called when another local feature has saved player state. Keep the
        // original separate add/remove/friendship/collection notifications.
        public void Reload()
        {
            session.Reload();
            var current = Snapshot();
            var previous = known;
            known = current;
            foreach (var old in previous)
                if (!current.ContainsKey(old.Key)) NpcRemoved?.Invoke(old.Key);
            foreach (var entry in current)
            {
                if (!previous.TryGetValue(entry.Key, out var old))
                    NpcAdded?.Invoke(entry.Key, GetAutoMeet(entry.Key) ? NpcMeetType.Auto : NpcMeetType.Manual);
                else
                {
                    if (old.IsCollected != entry.Value.IsCollected) NpcCollectedChanged?.Invoke(entry.Key, entry.Value.IsCollected);
                    if (old.FriendshipLevel != entry.Value.FriendshipLevel || old.FriendshipExp != entry.Value.FriendshipExp)
                        NpcFriendshipUpdated?.Invoke(entry.Key);
                }
            }
        }

        private bool GetAutoMeet(int id) => catalog.Find("npc", id, "Id")?.TryGetValue("IsAutoMeet", out var value) == true && value is bool auto && auto;
        private Dictionary<int, NpcRosterItem> Snapshot() => session.Save.Player.CharacterProperties.NpcFriendships
            .ToDictionary(value => value.NpcClassId, value => SnapshotOne(value.NpcClassId));
        private NpcRosterItem SnapshotOne(int id)
        {
            var value = session.Save.Player.CharacterProperties.NpcFriendships.FirstOrDefault(npc => npc.NpcClassId == id);
            if (value == null) return null;
            var definition = GetDefinition(id);
            return new NpcRosterItem { NpcClassId = id, MeetStatus = NpcMeetStatus.HadMeet, NpcRoleType = (int)definition.NpcRoleType,
                FriendshipLevel = value.Level, FriendshipExp = value.FriendshipExp, ActivateTimeTicks = value.ActivateTimeTicks,
                IsCollected = value.IsCollected, CollectedTimeTicks = value.CollectedTimeTicks,
                ExclusiveGiftId = definition.ExclusiveGiftId, IsAutoMeet = GetAutoMeet(id) };
        }
        private static int Int(Dictionary<string, object> row, string key) => (int)Number(row, key);
        private static long Number(Dictionary<string, object> row, string key) => row != null && row.TryGetValue(key, out var value) ? Convert.ToInt64(value) : 0;
        private static string Text(Dictionary<string, object> row, string key) => row != null && row.TryGetValue(key, out var value) ? Convert.ToString(value) : string.Empty;
        private static Dictionary<string, object> Map(Dictionary<string, object> row, string key) => row != null && row.TryGetValue(key, out var value) ? value as Dictionary<string, object> : null;
        public void BindNpcTipDot(TipDotNodeLinker linker, int id) => presentation.BindNpcTipDot(linker, id);
        public void PostSound(AudioClip sound, GameObject owner) => presentation.PostSound(sound, owner);
        public void ShowFriendshipLevelUp(NpcRosterItem npc, long exp, int level) => presentation.ShowFriendshipLevelUp(npc, exp, level);
        public void ShowExclusiveGift(int id, Component owner) => presentation.ShowExclusiveGift(id, owner);
        public void ShowNpcDetails(List<NpcRosterItem> npcs, int index) => presentation.ShowNpcDetails(npcs, index);
        public void ShowError(int code) => presentation.ShowError(code);
    }
}

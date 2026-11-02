using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // Selected original page ports, backed by the same atomic character
    // transactions as the ordinary roster. No preview NPC is saved here.
    public sealed class OfflinePartnerStarGodUiServices : IPartnerStarGodUiServices, IDisposable
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialPartnerCatalog catalog;
        private readonly OfflinePartnerRosterUiServices roster;
        private readonly IOfflinePartnerPresentation presentation;
        private readonly Action<ItemIcon> bindIcon;
        private readonly Dictionary<int, PartnerStarGodNpcDefinition> definitions;
        private readonly Dictionary<string, Sprite> images;
        private readonly Dictionary<int, NpcRosterItem> owned = new Dictionary<int, NpcRosterItem>();
        private readonly Dictionary<int, int> notifiedLevels = new Dictionary<int, int>();
        private readonly HashSet<int> meeting = new HashSet<int>();
        private bool disposed;

        public OfflinePartnerStarGodUiServices(OfflineMainlineProgressSession session, OfficialPartnerCatalog catalog,
            OfflinePartnerRosterUiServices roster, OfficialPartnerStarGodAssetCatalog assets,
            IOfflinePartnerPresentation presentation, Action<ItemIcon> bindIcon)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            this.bindIcon = bindIcon ?? throw new ArgumentNullException(nameof(bindIcon));
            if (!assets || assets.Definitions == null || assets.Images == null) throw new ArgumentNullException(nameof(assets));
            definitions = assets.Definitions.ToDictionary(row => row.Id);
            images = assets.Images.ToDictionary(row => row.path, row => row.sprite, StringComparer.Ordinal);
            foreach (var row in definitions.Values)
            {
                if (!row.IsStarGodNpc || row.IsMapExploreBoss || !images.TryGetValue(row.StaticFullBodyImagePath, out var image) || !image)
                    throw new InvalidOperationException("Selected star-god definition/image is unbound: " + row.Id);
                var current = Synchronize(row.Id);
                if (current != null) notifiedLevels[row.Id] = current.FriendshipLevel;
            }
            roster.NpcAdded += Added;
            roster.NpcRemoved += Removed;
            roster.NpcCollectedChanged += Collected;
            roster.NpcFriendshipUpdated += FriendshipUpdated;
        }

        public bool MainPlayerAvailable => !disposed && roster.MainPlayerAvailable;
        public bool CanPinNpc => roster.CanPinNpc;
        public bool IsStarGodNpcDisabled => false;
        public IEnumerable<PartnerStarGodNpcDefinition> Definitions => definitions.Values;
        public IEnumerable<NpcRosterItem> OwnedNpcs => definitions.Keys.Select(GetNpc).Where(value => value != null);
        public event Action<int, bool> NpcCollectedChanged;
        public event Action<int, NpcMeetType> NpcAdded;
        public event Action<int, int, int> NpcFriendshipLevelUpdated;
        public PartnerStarGodNpcDefinition GetDefinition(int id) => definitions.TryGetValue(id, out var value) ? value : null;
        public NpcRosterItem GetNpc(int id) => Synchronize(id);
        public NpcMeetStatus MeetStatusOf(int id) => roster.MeetStatusOf(id);
        public NpcRosterItem CreatePreviewNpc(PartnerStarGodNpcDefinition definition, int level)
        {
            if (definition == null || !definitions.ContainsKey(definition.Id)) throw new ArgumentException("Unknown selected star-god.");
            return new NpcRosterItem { NpcClassId = definition.Id, NpcRoleType = (int)NpcRoleType.StarGod,
                FriendshipLevel = level, MeetStatus = MeetStatusOf(definition.Id) };
        }
        public void BindNpcTipDot(TipDotNodeLinker linker, int id) => roster.BindNpcTipDot(linker, id);
        public void PostSound(AudioClip clip, GameObject owner) => roster.PostSound(clip, owner);
        public void ShowFriendshipLevelUp(NpcRosterItem npc, long exp, int level) => roster.ShowFriendshipLevelUp(npc, exp, level);
        public void ShowStarGodDetails(List<NpcRosterItem> npcs, int index) => roster.ShowNpcDetails(npcs, index);
        public void ShowError(int code) => roster.ShowError(code);
        public List<(int, int)> GetMeetNpcItems(int id) => catalog.Conditions.GetMeetNpcItems(id);
        public long GetItemAmount(int id) => session.Save.Player.Inventory.Where(value => value.ItemId == id).Sum(value => value.Count);
        public void PrepareItemIcon(ItemIcon icon) => bindIcon(icon);
        public IProfessionAssetOperation<Sprite> LoadSprite(string path)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflinePartnerStarGodUiServices));
            if (!images.TryGetValue(path, out var image) || !image)
                throw new InvalidOperationException("Selected star-god image is unbound: " + path);
            return new ImageLease(image);
        }

        public async void RequestMeetNpc(int id)
        {
            if (disposed || !definitions.ContainsKey(id) || !meeting.Add(id)) return;
            try { await roster.MeetNpc(id); }
            catch (IOException error) { ReportWriteFailure(error); }
            catch (UnauthorizedAccessException error) { ReportWriteFailure(error); }
            finally { meeting.Remove(id); }
        }
        private void ReportWriteFailure(Exception error)
        {
            if (!disposed) presentation.ShowProgressionResult(new ProgressionResult {
                Status = ProgressionResultStatus.InvalidState, Message = error.Message });
        }

        // Original cards retain an owned NPC object across level notifications.
        // Update that object before forwarding events, including during Reload.
        private NpcRosterItem Synchronize(int id)
        {
            if (!definitions.ContainsKey(id)) return null;
            var current = roster.GetNpc(id);
            if (current == null) { owned.Remove(id); return null; }
            if (!owned.TryGetValue(id, out var value)) { owned.Add(id, current); return current; }
            value.MeetStatus = current.MeetStatus; value.NpcRoleType = current.NpcRoleType;
            value.FriendshipLevel = current.FriendshipLevel; value.FriendshipExp = current.FriendshipExp;
            value.IsCollected = current.IsCollected; value.CollectedTimeTicks = current.CollectedTimeTicks;
            value.ActivateTimeTicks = current.ActivateTimeTicks; value.ExclusiveGiftId = current.ExclusiveGiftId;
            value.IsAutoMeet = current.IsAutoMeet;
            return value;
        }
        private void Added(int id, NpcMeetType type)
        {
            var value = Synchronize(id);
            if (value == null) return;
            notifiedLevels[id] = value.FriendshipLevel;
            NpcAdded?.Invoke(id, type);
        }
        private void Removed(int id) { owned.Remove(id); notifiedLevels.Remove(id); }
        private void Collected(int id, bool value)
        { if (Synchronize(id) != null) NpcCollectedChanged?.Invoke(id, value); }
        private void FriendshipUpdated(int id)
        {
            int previousLevel = notifiedLevels.TryGetValue(id, out int old) ? old : 0;
            var current = Synchronize(id);
            if (current != null) notifiedLevels[id] = current.FriendshipLevel;
            if (current != null && previousLevel != current.FriendshipLevel)
                NpcFriendshipLevelUpdated?.Invoke(id, previousLevel, current.FriendshipLevel);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            roster.NpcAdded -= Added; roster.NpcRemoved -= Removed;
            roster.NpcCollectedChanged -= Collected; roster.NpcFriendshipUpdated -= FriendshipUpdated;
            NpcAdded = null; NpcCollectedChanged = null; NpcFriendshipLevelUpdated = null;
            owned.Clear(); notifiedLevels.Clear();
        }
        private sealed class ImageLease : IProfessionAssetOperation<Sprite>
        {
            private Sprite value;
            private bool disposed;
            internal ImageLease(Sprite value) { this.value = value; }
            public bool IsValid => !disposed;
            public int Status { get { Check(); return 2; } }
            public Sprite Result { get { Check(); return value; } }
            public event Action<IProfessionAssetOperation<Sprite>> Completed
            { add { Check(); value(this); } remove { Check(); } }
            public void Dispose() { disposed = true; value = null; }
            private void Check() { if (disposed) throw new ObjectDisposedException(nameof(ImageLease)); }
        }
    }
}

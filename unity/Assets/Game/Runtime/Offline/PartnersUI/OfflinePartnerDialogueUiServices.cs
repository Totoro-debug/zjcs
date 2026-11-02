using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;

namespace Zjcs.Offline.PartnersUI
{
    public interface IOfflinePartnerDialoguePresentation
    {
        GameObject Instantiate(GameObject prefab, Transform parent, bool resetTransformOnly);
        void InitializeNpcIcon(Component icon, PartnerDialogueNpcData npc);
        void ShowError(int code);
        void ShowDialogue(PartnerDialogueData dialogue);
    }

    // The original address book keeps references to these views in its sorted
    // lists. Retain each view across reloads and deliver saved deltas one at a
    // time, as the original manager's add/remove/update consumers expect.
    public sealed class OfflinePartnerDialogueUiServices : IPartnerDialogueUiServices
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly IPartnerConversationPlayer player;
        private readonly IOfflinePartnerDialoguePresentation presentation;
        private readonly Dictionary<long, PartnerDialogueNpcData> npcs = new Dictionary<long, PartnerDialogueNpcData>();
        private readonly Dictionary<long, PartnerDialogueData> dialogues = new Dictionary<long, PartnerDialogueData>();
        private readonly Dictionary<long, OfflineNpcConversation> known = new Dictionary<long, OfflineNpcConversation>();

        public OfflinePartnerDialogueUiServices(OfflineMainlineProgressSession session,
            IPartnerConversationPlayer player, IOfflinePartnerDialoguePresentation presentation)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            Synchronize(false);
        }
        public bool MainPlayerAvailable => session.Save.Player != null;
        public string PlayerName => session.Save.Player.CharacterName;
        public int DialogueStartNodeId => 1; // Original NpcDialogueInfoParser.sDialogueStartNodeId.
        public IReadOnlyDictionary<long, PartnerDialogueNpcData> Npcs => npcs;
        public IReadOnlyDictionary<long, PartnerDialogueData> Dialogues => dialogues;
        public event Action<PartnerDialogueData> DialogueAdded;
        public event Action<PartnerDialogueData> DialogueRemoved;
        public event Action<PartnerDialogueData> DialogueUpdated;

        public void Reload() { session.Reload(); Synchronize(true); }
        public void SynchronizeCommittedState() => Synchronize(true);
        private void Synchronize(bool publish)
        {
            var records = session.Save.Player.CharacterProperties.NpcConversations.Records;
            var next = records.ToDictionary(value => value.DialogueId,
                value => OfflineNpcConversation.FromRecord(value.NpcClassId, value.ToRecord()));
            var nextNpcs = new Dictionary<long, PartnerDialogueNpcData>();
            var classIdentities = new Dictionary<int, long>();
            long? playerId = null;
            foreach (var record in next.Values)
            {
                if (!session.Save.Player.CharacterProperties.NpcFriendships.Any(value => value.NpcClassId == record.NpcClassId))
                    throw new InvalidDataException("Saved conversation belongs to an unmet NPC: " + record.NpcClassId);
                if (playerId.HasValue && playerId.Value != record.PlayerId)
                    throw new InvalidDataException("Saved conversations contain different player identities.");
                playerId = record.PlayerId;
                if (classIdentities.TryGetValue(record.NpcClassId, out long identity) && identity != record.NpcId)
                    throw new InvalidDataException("Saved NPC class has multiple conversation identities.");
                classIdentities[record.NpcClassId] = record.NpcId;
                if (nextNpcs.TryGetValue(record.NpcId, out var npc) && npc.NpcClassId != record.NpcClassId)
                    throw new InvalidDataException("Saved NPC identity belongs to multiple NPC classes.");
                nextNpcs[record.NpcId] = new PartnerDialogueNpcData {
                    NpcId = record.NpcId, NpcClassId = record.NpcClassId, PlayerId = record.PlayerId };
                foreach (int node in record.DialogueNodes) RequireNode(record.DialogueClassId, node);
                if (known.TryGetValue(record.DialogueId, out var previous)
                    && (previous.PlayerId != record.PlayerId || previous.NpcId != record.NpcId
                        || previous.NpcClassId != record.NpcClassId || previous.DialogueClassId != record.DialogueClassId))
                    throw new InvalidDataException("A saved conversation changed its immutable identity.");
            }
            // Retain removed NPC views until their final dialogue removal has
            // reached the original list's complete reinitialization callback.
            foreach (var entry in nextNpcs)
            {
                if (npcs.TryGetValue(entry.Key, out var value))
                { value.NpcClassId = entry.Value.NpcClassId; value.PlayerId = entry.Value.PlayerId; }
                else npcs.Add(entry.Key, entry.Value);
            }
            foreach (long id in known.Keys.Where(id => !next.ContainsKey(id)).ToArray())
            {
                var view = dialogues[id]; dialogues.Remove(id); known.Remove(id);
                if (publish) DialogueRemoved?.Invoke(view);
            }
            foreach (var entry in next)
            {
                var value = entry.Value;
                bool added = !known.TryGetValue(entry.Key, out var previous);
                bool changed = added || previous.CurrentNode != value.CurrentNode
                    || previous.DialogueStatus != value.DialogueStatus || previous.DateTimeTicks != value.DateTimeTicks
                    || previous.ClockKind != value.ClockKind || !previous.DialogueNodes.SequenceEqual(value.DialogueNodes);
                if (!changed) continue;
                if (!dialogues.TryGetValue(entry.Key, out var view))
                    dialogues.Add(entry.Key, view = new PartnerDialogueData());
                view.NpcId = value.NpcId; view.DialogueId = value.DialogueId;
                view.PlayerId = value.PlayerId;
                view.DialogueNodes.Clear(); view.DialogueNodes.AddRange(value.DialogueNodes);
                view.DialogueClassId = value.DialogueClassId; view.CurrentNode = value.CurrentNode;
                view.DialogueStatus = (PartnerDialogueStatus)value.DialogueStatus;
                view.DateTime = new DateTime(value.DateTimeTicks, (DateTimeKind)value.ClockKind);
                known[entry.Key] = value;
                if (publish)
                {
                    if (added) DialogueAdded?.Invoke(view);
                    else DialogueUpdated?.Invoke(view);
                }
            }
            foreach (long id in npcs.Keys.Where(id => !nextNpcs.ContainsKey(id)).ToArray()) npcs.Remove(id);
        }
        private PartnerConversationNode RequireNode(int dialogue, int node) => player.DialogueConfiguration.Get(dialogue, node)
            ?? throw new InvalidDataException("Original conversation node is absent: " + dialogue + ":" + node);
        public IReadOnlyList<int> GetNextNodes(int dialogue, int node) => RequireNode(dialogue, node).NextNodes;
        public bool IsNodeUnlocked(int dialogue, int node) => RequireNode(dialogue, node).UnlockCondition.IsMet(player);
        public PartnerConversationRecord GetRecord(long dialogueId)
        {
            var value = session.Save.Player.CharacterProperties.NpcConversations.Records.FirstOrDefault(row => row.DialogueId == dialogueId);
            return value?.ToRecord() ?? throw new KeyNotFoundException("Saved conversation is absent: " + dialogueId);
        }
        public GameObject Instantiate(GameObject prefab, Transform parent, bool resetTransformOnly) =>
            presentation.Instantiate(prefab, parent, resetTransformOnly);
        public void InitializeNpcIcon(Component icon, PartnerDialogueNpcData npc) => presentation.InitializeNpcIcon(icon, npc);
        public void ShowError(int code) => presentation.ShowError(code);
        public void ShowDialogue(PartnerDialogueData dialogue)
        {
            if (dialogue == null || !dialogues.TryGetValue(dialogue.DialogueId, out var owned) || !ReferenceEquals(owned, dialogue))
                throw new InvalidOperationException("The original dialogue row no longer belongs to this saved profile.");
            presentation.ShowDialogue(owned);
        }
    }
}

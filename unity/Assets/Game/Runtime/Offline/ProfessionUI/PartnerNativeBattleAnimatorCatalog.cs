using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed class PartnerNativeBattleAnimatorCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public int NpcId;
            public RuntimeAnimatorController Controller;
        }

        [SerializeField] private Entry[] entries;
        public IReadOnlyList<Entry> Entries => entries;

        public RuntimeAnimatorController Find(int npcId)
        {
            foreach (var entry in entries)
                if (entry.NpcId == npcId) return entry.Controller;
            return null;
        }
    }
}

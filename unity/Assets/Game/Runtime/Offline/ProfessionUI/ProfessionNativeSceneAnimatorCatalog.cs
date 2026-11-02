using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed class ProfessionNativeSceneAnimatorCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string Address;
            public RuntimeAnimatorController Controller;
        }
        [SerializeField] private Entry[] entries;
        public IReadOnlyList<Entry> Entries => entries;
        public bool Contains(string address)
        {
            foreach (var entry in entries) if (entry.Address == address) return true;
            return false;
        }
        public RuntimeAnimatorController Load(string address)
        {
            foreach (var entry in entries) if (entry.Address == address) return entry.Controller;
            return null;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Zjcs.Offline.Equipment
{
    [Serializable]
    public sealed class OfflineEquipmentProfile
    {
        public long NextInstanceId = 1;
        public List<OfflineEquipmentInstance> Instances = new List<OfflineEquipmentInstance>();
        public List<OfflineEquippedSlot> Equipped = new List<OfflineEquippedSlot>();
        public List<OfflineEquipmentSlotProgress> SlotProgress = new List<OfflineEquipmentSlotProgress>();
        public OfflineGemForgeProgress GemForge = new OfflineGemForgeProgress();
    }

    [Serializable]
    public sealed class OfflineGemForgeProgress
    {
        public List<OfflineGemForgeRankProgress> Ranks = new List<OfflineGemForgeRankProgress>();
        public int GuaranteedStoryIndex;
        public long RandomState;
        public List<string> ClaimedReceipts = new List<string>();
    }

    [Serializable]
    public sealed class OfflineGemForgeRankProgress
    {
        public int Rank;
        public int MissCount;
    }

    [Serializable]
    public sealed class OfflineEquipmentInstance
    {
        public long InstanceId;
        public int ItemId;
        public int ItemLevel;
        public int BlessLevel;
        public int SecondaryProperty;
        public int SuitId;
        public bool Locked;
        public bool SystemLocked;
        public List<OfflineEquipmentTerm> Terms = new List<OfflineEquipmentTerm>();
        public List<OfflineEquipmentGem> Gems = new List<OfflineEquipmentGem>();
    }

    [Serializable]
    public sealed class OfflineEquipmentTerm
    {
        public int Type;
        public int PropertyId;
        public int PropertyType;
        public float Factor;
        public int Group;
        public int SkillQuality;
        public List<int> Indices = new List<int>();
    }

    [Serializable]
    public sealed class OfflineEquipmentGem
    {
        public int Slot;
        public int ItemId;
    }

    [Serializable]
    public sealed class OfflineEquippedSlot
    {
        public int Position;
        public long InstanceId;
    }

    [Serializable]
    public sealed class OfflineEquipmentSlotProgress
    {
        public int Position;
        public int BaseLevel;
        public List<OfflineEquipmentBlessProgress> BlessLevels = new List<OfflineEquipmentBlessProgress>();
    }

    [Serializable]
    public sealed class OfflineEquipmentBlessProgress
    {
        public int SeasonId;
        public int Level;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.BattleDamageUI
{
    // Local projection of the original fight root, resource leases and shared
    // notifications. The manager retains presentation decisions; this seam does
    // not settle damage, status effects, cost or replay rules.
    public abstract class CharacterDamageUIContext
    {
        public abstract Transform DamageFloatsRoot { get; }
        public abstract FightDamageSettings Settings { get; }
        public abstract bool UseGameObjectPool { get; }
        public abstract IEnumerable<CharacterDamageUIRole> Characters { get; }
        public abstract MyEntity GetEntity(EcEntityIdentifier id);
        public abstract GameObject LoadPrefab(string assetGuid);
        public abstract void ReleasePrefabs();
        public abstract int? StageType { get; }
        public abstract CharacterDamageUIWarmupSettings WarmupSettings { get; }
        public abstract void GetSkillPrefabAsync(EcEntityIdentifier id, Action<MyFightSkill> ready);
        public abstract ElementType GetSkillElementType(MyFightDamage damage);
        // Replaces original framework injection for locally instantiated UI.
        public virtual void PrepareUI(CharacterDamageUI ui) { }
        public event Action<CharacterDamageUIRole> OnCharacterAdd;
        public event Action<CharacterDamageUIRole> OnCharacterRemove;
        public event Action<CharacterDamageUISkillReplay> OnSkillReplay;
        protected void CharacterAdded(CharacterDamageUIRole role) => OnCharacterAdd?.Invoke(role);
        protected void CharacterRemoved(CharacterDamageUIRole role) => OnCharacterRemove?.Invoke(role);
        protected void SkillReplayed(CharacterDamageUISkillReplay replay) => OnSkillReplay?.Invoke(replay);
    }

    // Only fields consumed by CharacterDamageUIManager/ElementConfrontationUI;
    // the JSON payload retains the original complete warmup records.
    [Serializable]
    public sealed class CharacterDamageUIWarmupSettings
    {
        public float ElementConfrontThreshold;
        public List<CharacterDamageUIStageWarmup> FightWarmupSettings;
    }
    [Serializable]
    public sealed class CharacterDamageUIStageWarmup
    {
        public int StageType;
        public List<CharacterDamageUIPresetCount> DamageEffectUIs;
    }
    [Serializable]
    public sealed class CharacterDamageUIPresetCount
    {
        public EffectUIType EffectUIType;
        public int PresetCount;
    }

    // One subscription identity for a role's original client and shared events.
    // Callers publish only notifications actually emitted by the rule executor.
    public sealed class CharacterDamageUIRole
    {
        public MyFightRoleFight Fight { get; }
        public EcEntityIdentifier Id => Fight.myEntity.Id;
        public event Action<CharacterDamageUIRole, EcEntityIdentifier, long, long> OnSkillCostHp;
        public event Action<CharacterDamageUIRole> OnDisperseStatus;
        public event Action<EcEntityIdentifier, long, bool> OnStatusDamage;
        public event Action<EcEntityIdentifier, EStatusCustomAction> OnStatusTriggerCustomAction;
        public CharacterDamageUIRole(MyFightRoleFight fight)
        { Fight = fight != null ? fight : throw new ArgumentNullException(nameof(fight)); }
        public void SkillCostHp(EcEntityIdentifier skill, long preHp, long hp) => OnSkillCostHp?.Invoke(this, skill, preHp, hp);
        public void DisperseStatus() => OnDisperseStatus?.Invoke(this);
        public void StatusDamage(long value, bool cure) => OnStatusDamage?.Invoke(Id, value, cure);
        public void StatusTriggerCustomAction(EStatusCustomAction action) => OnStatusTriggerCustomAction?.Invoke(Id, action);
    }

    public readonly struct CharacterDamageUISkillReplay
    {
        public readonly EcEntityIdentifier CharacterId, SkillId;
        public readonly bool IsExtra;
        public CharacterDamageUISkillReplay(EcEntityIdentifier characterId, EcEntityIdentifier skillId, bool isExtra)
        { CharacterId = characterId; SkillId = skillId; IsExtra = isExtra; }
    }
}

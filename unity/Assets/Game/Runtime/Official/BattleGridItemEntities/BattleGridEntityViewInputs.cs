using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.BattleStatusPlayables;

namespace Zjcs.Official.BattleGridItemEntities
{
    // Data/service boundaries consumed by the original client components. These
    // contain no server rules, inheritance formulas or synthetic entity events.
    public interface IBattleGridComponentViewData { }

    public sealed class BattleRoleCharacterIdentity
    {
        public long PlayerId;
        public int Profession;
    }

    public abstract class BattleRoleCharacterViewData
    {
        public abstract BattleRoleCharacterIdentity Character { get; }
        public abstract EcEntityIdentifier MasterId { get; }
        public abstract bool Die { get; }
        public abstract bool IsNew { get; }
        public abstract event Action<bool, long> OnDie;
    }

    public interface IBattleRolePlayerAppearanceData
    {
        BattleRoleCharacterIdentity CharacterComponent { get; }
        int Sex { get; }
        object AppearanceData { get; }
    }

    public interface IBattleRoleNpcViewData { int NpcClassId { get; } }

    // Original 3051/3054 store null HUD/appearance references. Explicit abstract
    // bindings keep those branches without constructing replacement game UI.
    public abstract class BattleRolePlayerHud : MonoBehaviour
    {
        public abstract UnityEngine.Object NameTxt { get; }
        public abstract void InitByNpcClassId(int classId);
        public abstract void InitByPlayerId(long playerId);
    }

    public abstract class BattleRolePlayerAppearance : MonoBehaviour
    {
        public Action OnAnimatorChanged;
        public abstract void RefreshAppearance(long playerId, int sex, int profession, object data);
        public abstract void SetRuntimeDataOverride(object data);
    }

    public interface IBattleGridPlayableEnvironment
    {
        bool IsFastforward { get; }
        bool NotPlayGeneratePlayable { get; }
    }

    public interface IBattleEntityEndEvents
    {
        event Action<ECViewEntityData, object> Ended;
    }

    public interface IBattleStatusDisplayController
    {
        void Start();
        void End();
    }

    public interface IBattleStatusDisplayFactory
    {
        IBattleStatusDisplayController Create(MyFightStatusAgent agent, UnityEngine.Object item,
            Transform slot, IBattleGridRoleViewRoot root, int columns, int rows);
    }

    public abstract class BattleGridItemViewData
    {
        public abstract EcEntityIdentifier OwnerEntId { get; }
    }

    public interface IBattleGridRoleViewRoot
    {
        long? MainPlayerId { get; }
        bool IsPvpStage { get; }
        bool HasFightRoot { get; }
        MyFightRoleCharacter Character { get; set; }
        IEnumerable<MyFightRoleCharacter> Characters { get; }
        MyEntity GetEntity(EcEntityIdentifier identifier);
        void AddCharacter(MyFightRoleCharacter character);
        void RemoveCharacter(MyFightRoleCharacter character);
        int? FactionOf(MyEntity entity);
    }
}

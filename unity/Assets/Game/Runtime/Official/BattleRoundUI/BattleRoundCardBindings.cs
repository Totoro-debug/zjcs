using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.BattleRoundUI
{
    // Local presentation projections at the original entity/config/player-info seam.
    // These are not replacements for the shared combat entity implementation.
    public enum BattleRoundGuardKind { Other, Arena, LegionFight, MultiGroupRivalry }
    public sealed class BattleRoundPlayer
    {
        public long Id;
        public Sex Sex;
        public int Profession;
        public object AppearanceData;
    }
    public interface IBattleRoundMimic
    {
        bool TryGetTargetPlayerId(out long id);
        IDisposable Subscribe(Action<long> onTargetInitial);
    }
    public sealed class BattleRoundRole
    {
        public int? NpcClassId;
        public long? PlayerId;
        public int? PetEntityClassId;
        public bool HasGuard;
        public BattleRoundGuardKind GuardKind;
        public BattleRoundPlayer GuardAppearance;
        public IBattleRoundMimic Mimic;
        public int MonsterId;
    }
    public sealed class BattleRoundMonsterIcon
    {
        public int Type;
        public int NpcClassId;
        public string Path;
    }
    public interface IBattleRoundPlayerRequest : IDisposable
    {
        void Request(long id);
    }
    public interface IBattleRoundCardBindings
    {
        BattleRoundPlayer MainPlayer { get; }
        Color MimicsMonsterColor { get; }
        GameEffectData GetFaction(BattleRoundRole role);
        bool TryGetNpcIcon(int id, out string path);
        bool TryGetMonsterIcon(int id, out BattleRoundMonsterIcon icon);
        Sprite GetSprite(string path);
        IBattleRoundPlayerRequest CreatePlayerRequest(Action<BattleRoundPlayer> onResult);
    }
}

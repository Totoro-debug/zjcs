using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    public interface IOfficialBattleCharacterSexSource
    {
        string Sex { get; }
    }

    /// <summary>Mode adapter inputs. Checkpoint contains official grid, roster, skills and result
    /// rules. The common module owns HUD, scene, movement, cooldowns, turns and statistics.
    /// Rewards and durable mode transactions remain with the caller.</summary>
    public sealed class OfficialBattleRequest
    {
        // Required. A fresh checkpoint starts once; use RestoreCheckpoint for persisted state.
        // Each request owns one active session until DisposeSession; mode rewards are committed
        // only after CanCompleteBattle (including the final hit presentation).
        public FightBattleCheckpoint Checkpoint;
        public Transform WorldRoot;
        public Transform HudParent;
        public OfficialBattleHostPresentation HostPresentation;
        // Optional mode adapter. The controller invokes it at the same completion boundaries
        // exposed by its public events, so mode runners do not need to subscribe manually.
        public IOfficialBattleModeAdapter ModeAdapter;
        // The common module resolves Battle.Speeds/PlayModeSpeeds from CombatAssets during Begin.
        // The mode supplies only its official condition evaluation; absent evaluation keeps
        // conditional speeds locked. No second initialization call is required.
        public int SpeedModeType;
        public Func<int, bool> SpeedConditionMet;
        // Optional presentation adapters. Omitted resolvers use the shared official catalogs;
        // maps with a captured player portrait or a version-specific NPC catalog override them.
        public Func<int, Sprite> SkillIcon;
        // Partial override: null results use the shared official monster/fallback catalog.
        public Func<FightBattleUnitState, Sprite> TimelinePortrait;
        public Func<FightBattleUnitState, string> ModelKey;
        public Func<FightBattleUnitState, FightBattleStepEvent, string> AnimationForEvent;
        public OfficialCombatAssetCatalog CombatAssets;
        public OfficialBattleModelCatalog ModelCatalog;
        public OfficialBattleHudCatalog HudCatalog;
        public bool RestoreCheckpoint;
        public Func<FightBattleUnitState, Sprite> TimelineFactionIcon;
        public int EnvironmentGameId;
        public bool? EnvironmentIsDay;
        public Func<FightBattleUnitState, string> AnimationGroupResolver;
        public OfflineMainlineProgressSession StatueProgress;
        public OfficialBattleSessionOptions Options;
        public Func<FightBattleUnitState, IOfficialBattleNativeModel> NativeNpcFactory;
        public OfficialBattlePlayerAppearanceSource PlayerAppearance;
        // Optional Wwise host for sound leaves inside native FightSkill graphs.
        // Visual graphs remain usable when omitted; their sound leaves use the
        // scope's silent local fallback until a mode supplies the official bank host.
        public IProfessionSoundHost SkillPlayableSoundHost;
        // Local entry pacing; not an official battle rule.
        public float InitialStepDelay = 0.8f;
    }
}

using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Combat
{
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
        // Optional presentation adapters. Omitted resolvers use the shared official catalogs;
        // maps with a captured player portrait or a version-specific NPC catalog override them.
        public Func<int, Sprite> SkillIcon;
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
        // Local entry pacing; not an official battle rule.
        public float InitialStepDelay = 0.8f;
    }
}

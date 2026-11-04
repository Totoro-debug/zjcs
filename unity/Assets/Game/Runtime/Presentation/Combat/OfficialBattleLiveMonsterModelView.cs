using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // The common scene owns movement/time; the full source skeleton owns geometry
    // and follower bones. No sampled-pose or historical transform binding is used.
    public sealed class OfficialBattleLiveMonsterModelView : IOfficialBattleNativeModel, IOfficialBattleNativeModelSpeed
    {
        private readonly BattleNativeMonsterRenderer renderer;
        private readonly OfficialBattleUnitSorting sorting;
        private readonly OfficialBattleShadowView shadow;
        private readonly OfficialBattleShadowBoneFollower follower;
        private float playbackSpeed = 1f;
        private bool loop = true;
        private bool disposed;

        public Transform Transform => renderer.EntityRoot;
        public Transform ModelTransform => renderer.transform;
        public bool IsAnimationFinished => !disposed && renderer.IsAnimationFinished;

        public static OfficialBattleLiveMonsterModelView TryCreate(FightBattleUnitState state, Transform parent)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            // Role-player arena guards may carry a monster stat row.
            if (state.CharacterType == 1) return null;
            int entityId = state.MonsterTemplateId > 0 ? state.MonsterTemplateId : state.MonsterId;
            var assets = BattleNativeMonsterAssets.Load();
            return assets.Find(entityId) == null ? null
                : new OfficialBattleLiveMonsterModelView(assets, entityId, parent, "BattleUnit_" + state.Id);
        }

        private OfficialBattleLiveMonsterModelView(BattleNativeMonsterAssets assets, int entityId,
            Transform parent, string name)
        {
            try
            {
                renderer = BattleNativeMonsterRenderer.Create(assets, entityId, parent, name);
                var source = (Dictionary<string, object>)assets.Find(entityId)["meshRenderer"];
                sorting = new OfficialBattleUnitSorting(renderer.transform,
                    checked(Convert.ToInt32(source["m_SortingLayer"]) * 1000 + Convert.ToInt32(source["m_SortingOrder"])));
                shadow = OfficialBattleShadowView.Create(entityId, renderer.EntityRoot, renderer)
                    ?? throw new InvalidOperationException("Original monster shadow is unbound: " + entityId);
                follower = shadow.Root.GetComponent<OfficialBattleShadowBoneFollower>();
                if (!follower) throw new InvalidOperationException("Original monster bone follower is unbound: " + entityId);
            }
            catch { Dispose(); throw; }
        }

        public bool HasAnimation(string name) => !disposed && renderer.AnimationNames.Contains(name);
        public bool Play(string name, bool loopAnimation, bool restart = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveMonsterModelView));
            if (!HasAnimation(name)) return false;
            bool changed = restart || renderer.Animation != name || loop != loopAnimation || renderer.IsAnimationFinished;
            if (!renderer.Play(name, loopAnimation, restart)) return false;
            loop = loopAnimation;
            if (changed) playbackSpeed = 1f;
            follower.ForceUpdate();
            return true;
        }
        public void SetPlaybackSpeed(float speed)
        { playbackSpeed = float.IsFinite(speed) && speed > 0 ? speed : 1f; }
        public void Tick(float deltaTime)
        {
            if (disposed) return;
            renderer.Advance(deltaTime * playbackSpeed);
            follower.ForceUpdate();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            shadow?.Dispose();
            sorting?.Dispose();
            if (renderer) renderer.Release();
        }
    }
}

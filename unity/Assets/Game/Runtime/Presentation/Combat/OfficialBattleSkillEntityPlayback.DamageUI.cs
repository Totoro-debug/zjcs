using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSkillEntityPlayback
    {
        private OfficialBattleDamageUIPlayback damageUI;
        private Func<int, Transform> damageUITarget;
        internal bool HasDamageUI => damageUI != null;
        public void BindDamageUI(Transform parent, int stageType)
        {
            if (damageUI != null) throw new InvalidOperationException("Original battle damage UI is already bound.");
            scope.Dispatch(() => damageUI = new OfficialBattleDamageUIPlayback(this, parent, stageType));
            foreach (var entity in entities.Values) damageUI.Register(entity);
        }
        internal void BindDamageUITargets(Func<int, Transform> resolve) => damageUITarget = resolve;
        internal void PrepareDamageUITarget(int id)
        {
            if (damageUI == null) return;
            var target = damageUITarget?.Invoke(id);
            if (target) EnsureUnit(id, target);
        }
        internal MyFightSkill GetSkillPrefab(int classId) => skillViews.GetPrefab(classId)?.GetComponent<MyFightSkill>();
        internal bool PlayStatusCustomAction(FightBattleStatusCustomActionEvent action)
        {
            if (damageUI == null || action == null) return false;
            PrepareDamageUITarget(action.UnitId);
            bool played = false;
            scope.Dispatch(() => played = damageUI.StatusCustomAction(action));
            return played;
        }
        internal bool CanPlayStatusCustomAction(FightBattleStatusCustomActionEvent action)
        {
            if (damageUI == null || action == null) return false;
            PrepareDamageUITarget(action.UnitId);
            return damageUI.CanPlayStatusCustomAction(action);
        }
        internal void SetDamageUIVisible(bool visible) => damageUI?.SetVisible(visible);
        internal void UpdateDamageUIVisibility() => damageUI?.UpdateVisibility();
    }
}

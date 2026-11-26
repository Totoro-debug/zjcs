using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleStatusGlobalRoundUpdateState
    {
        public bool RulesSupported;
        public bool TryAtStart;
        public bool RemoveWhenNotEnoughTimes;
        public int MaxInvokeCount;
        public int InvokeCount;
        public bool StartChecked;
        public int LastGlobalRound = -1;
        public List<FightBattleStatusCooldownInvoke> Invokes = new List<FightBattleStatusCooldownInvoke>();

        public FightBattleStatusGlobalRoundUpdateState Copy()
        {
            var copy = (FightBattleStatusGlobalRoundUpdateState)MemberwiseClone();
            copy.Invokes = Invokes.ConvertAll(value => new FightBattleStatusCooldownInvoke {
                Target = value.Target, Change = value.Change });
            return copy;
        }
    }

    [Serializable]
    public sealed class FightBattleStatusCooldownInvoke
    {
        public int Target;
        public int Change;
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        private static FightBattleStatusGlobalRoundUpdateState ParseGlobalRoundUpdate(Dictionary<string, object> source)
        {
            if (source == null) return null;
            var result = new FightBattleStatusGlobalRoundUpdateState {
                TryAtStart = Bool(source, "TryAtStart"),
                RemoveWhenNotEnoughTimes = Bool(source, "RemoveWhenNotEnoughTimes"),
                MaxInvokeCount = Int(source, "MaxInvokeCount", -1),
                RulesSupported = FieldArray(source, "Conditions").Count == 0
                    && FieldArray(source, "StatusCfgs").Count == 0 && FieldArray(source, "Skills").Count == 0
            };
            foreach (object value in FieldArray(source, "InvokeList"))
            {
                var invoke = Object(value, "global round status invocation");
                if (StringField(invoke, "_type") != "FightStatusChangeSkillCDInvokeCfg" || Int(invoke, "Type") != 1)
                { result.RulesSupported = false; continue; }
                int target = Int(invoke, "Target");
                result.RulesSupported &= target >= 1 && target <= 4;
                result.Invokes.Add(new FightBattleStatusCooldownInvoke { Target = target, Change = Int(invoke, "ChangeCD") });
            }
            return result;
        }
    }

    public sealed partial class FightBattleExecutor
    {
        private bool IsPendingGlobalRoundStart(FightBattleUnitState owner, FightBattleStatusState status)
        {
            var info = status?.Components?.GlobalRoundUpdate;
            return owner != null && !owner.Ended && info != null && info.RulesSupported
                && !info.StartChecked && info.TryAtStart;
        }

        private int StartGlobalRoundStatus(FightBattleUnitState owner, FightBattleStatusState status)
        {
            var info = status?.Components?.GlobalRoundUpdate;
            if (info == null || info.StartChecked) return 0;
            info.StartChecked = true;
            info.LastGlobalRound = turns.GlobalRound();
            return info.TryAtStart ? InvokeGlobalRoundStatus(owner, status) : 0;
        }

        private int InvokeGlobalRoundStatus(FightBattleUnitState owner, FightBattleStatusState status)
        {
            var info = status?.Components?.GlobalRoundUpdate;
            if (info == null || !info.RulesSupported || owner == null || owner.Ended
                || status.ForbiddenType != 0 || !owner.Statuses.Contains(status)) return 0;
            if (info.MaxInvokeCount >= 0 && info.InvokeCount >= info.MaxInvokeCount) return 0;
            info.InvokeCount++;
            foreach (var invoke in info.Invokes)
            {
                int targetId = FightStatusLifecycle.ResolveTargetUnitId(status, invoke.Target, units);
                if (!unitById.TryGetValue(targetId, out var target) || target.Ended || target.SkillState == null) continue;
                var skillState = new FightSkillState(target.SkillState);
                bool changed = false;
                foreach (var slot in target.SkillState.Slots)
                    if (slot != null && skillState.LeftCooldown(slot.Slot) > 0)
                        changed |= skillState.ChangeCooldown(slot.Slot, invoke.Change);
                if (changed) OnCooldownActionTriggered(target);
            }
            RecordStatusView(owner, status, FightBattleStatusViewEventKind.Invoke);
            if (info.RemoveWhenNotEnoughTimes && info.MaxInvokeCount >= 0 && info.InvokeCount >= info.MaxInvokeCount)
                FightStatusLifecycle.End(owner, status);
            return 1;
        }

        private void UpdateGlobalRoundStatuses(int round)
        {
            foreach (var owner in new List<FightBattleUnitState>(units))
                foreach (var status in new List<FightBattleStatusState>(owner.Statuses))
                {
                    var info = status.Components?.GlobalRoundUpdate;
                    if (info == null || info.LastGlobalRound == round) continue;
                    info.LastGlobalRound = round;
                    info.StartChecked = true;
                    InvokeGlobalRoundStatus(owner, status);
                }
        }
    }
}

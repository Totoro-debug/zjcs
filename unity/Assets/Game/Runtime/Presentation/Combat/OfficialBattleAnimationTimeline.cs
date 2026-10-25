using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleAnimationCue
    {
        public int UnitId;
        public string Animation;
        public bool Loop;
        public float Delay;
        public int Priority;
        public int Order;
        public OfficialBattleSkillPlayableCue SkillPlayable;
    }

    [Serializable]
    public sealed class OfficialBattleSkillPlayableCatalogData
    {
        public string manifest;
        public List<OfficialBattleSkillPlayableRecord> skills = new List<OfficialBattleSkillPlayableRecord>();
    }

    [Serializable]
    public sealed class OfficialBattleSkillPlayableRecord
    {
        public int skill_entity_id;
        public List<OfficialBattleSkillPlayableCue> playables = new List<OfficialBattleSkillPlayableCue>();
    }

    [Serializable]
    public sealed class OfficialBattleSkillPlayableCue
    {
        public int kind;
        public float time;
        public float speed = 1f;
        public string animation;
        public string clip_resource;
        public string origin_clip_name;
        public string trigger_param;
        public List<string> pre_state_name_allow_list = new List<string>();
        public string my_bool_param;
        public bool my_bool_value;
        public string my_float_param;
        public float my_float_value;
        public string int_param;
        public int int_value;
        public float my_duration;
        public string layer_name;
        public float layer_weight;
        public bool end_resume_param;
    }

    public static class OfficialBattleAnimationTimeline
    {
        public static List<OfficialBattleAnimationCue> Build(FightBattleStepEvent stepEvent)
        {
            return Build(stepEvent, null);
        }

        public static List<OfficialBattleAnimationCue> Build(FightBattleStepEvent stepEvent,
            OfficialBattleSkillPlayableCatalogData skillCatalog)
        {
            var cues = new List<OfficialBattleAnimationCue>();
            if (stepEvent == null) return cues;

            int order = 0;
            if (skillCatalog != null && skillCatalog.skills != null && stepEvent.SkillCasts != null)
                foreach (FightBattleSkillCastEvent cast in stepEvent.SkillCasts)
                {
                    if (cast == null || cast.ActorId <= 0 || cast.SkillEntityId <= 0) continue;
                    OfficialBattleSkillPlayableRecord record = FindSkill(skillCatalog, cast.SkillEntityId);
                    if (record == null || record.playables == null) continue;
                    foreach (OfficialBattleSkillPlayableCue playable in record.playables)
                    {
                        if (playable == null || (playable.kind != 0 && playable.kind != 1)) continue;
                        cues.Add(new OfficialBattleAnimationCue
                        {
                            UnitId = cast.ActorId,
                            Animation = playable.kind == 1 ? playable.animation : string.Empty,
                            Loop = false,
                            Delay = NormalizeDelay(playable.time),
                            Priority = 1,
                            Order = order++,
                            SkillPlayable = playable
                        });
                    }
                }

            if (stepEvent.Movements != null)
                foreach (FightBattleMovementEvent movement in stepEvent.Movements)
                {
                    if (movement == null || movement.UnitId <= 0 || movement.Path == null
                        || movement.Path.Count <= 1) continue;
                    cues.Add(new OfficialBattleAnimationCue
                    {
                        UnitId = movement.UnitId,
                        Animation = "run_01",
                        Loop = true,
                        Delay = NormalizeDelay(movement.Delay),
                        Priority = 0,
                        Order = order++
                    });
                }

            if (stepEvent.DamageEvents != null)
                foreach (FightBattleDamageEvent damage in stepEvent.DamageEvents)
                {
                    if (damage == null || damage.IsSkillCost || damage.TargetId <= 0) continue;
                    cues.Add(new OfficialBattleAnimationCue
                    {
                        UnitId = damage.TargetId,
                        Animation = damage.Block ? "block_01" : "hit_01",
                        Loop = false,
                        Delay = NormalizeDelay(damage.Delay),
                        Priority = 2,
                        Order = order++
                    });
                }

            cues.Sort(Compare);
            return cues;
        }

        private static OfficialBattleSkillPlayableRecord FindSkill(
            OfficialBattleSkillPlayableCatalogData catalog, int skillEntityId)
        {
            foreach (OfficialBattleSkillPlayableRecord skill in catalog.skills)
                if (skill != null && skill.skill_entity_id == skillEntityId) return skill;
            return null;
        }

        private static int Compare(OfficialBattleAnimationCue left, OfficialBattleAnimationCue right)
        {
            int result = left.Delay.CompareTo(right.Delay);
            if (result != 0) return result;
            result = left.Priority.CompareTo(right.Priority);
            return result != 0 ? result : left.Order.CompareTo(right.Order);
        }

        private static float NormalizeDelay(float delay)
        {
            return float.IsNaN(delay) || float.IsInfinity(delay) || delay < 0f ? 0f : delay;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleSkillPlayableCatalog
    {
        private const string Manifest = "854_156383";
        private const string ResourcePath = "Combat/skill-playables";
        private const string ClipResourcePrefix = "Combat/SkillAnimations/";

        private readonly Dictionary<int, OfficialBattleSkillPlayableRecord> skills =
            new Dictionary<int, OfficialBattleSkillPlayableRecord>();
        private readonly Dictionary<string, AnimationClip> clips =
            new Dictionary<string, AnimationClip>(StringComparer.Ordinal);

        private OfficialBattleSkillPlayableCatalog(OfficialBattleSkillPlayableCatalogData data)
        {
            Data = data;
            foreach (OfficialBattleSkillPlayableRecord skill in data.skills)
            {
                if (skill == null || skill.skill_entity_id <= 0
                    || skills.ContainsKey(skill.skill_entity_id))
                    throw new InvalidOperationException("Official battle skill Playable catalog has an invalid or duplicate skill id.");
                if (skill.playables == null) skill.playables = new List<OfficialBattleSkillPlayableCue>();
                foreach (OfficialBattleSkillPlayableCue playable in skill.playables)
                {
                    if (playable == null || playable.kind < 0 || playable.kind > 1
                        || float.IsNaN(playable.time) || float.IsInfinity(playable.time)
                        || float.IsNaN(playable.speed) || float.IsInfinity(playable.speed))
                        throw new InvalidOperationException("Official battle skill Playable catalog contains an invalid cue.");
                    if (playable.pre_state_name_allow_list == null)
                        playable.pre_state_name_allow_list = new List<string>();
                }
                skills.Add(skill.skill_entity_id, skill);
            }
        }

        public OfficialBattleSkillPlayableCatalogData Data { get; }

        public static OfficialBattleSkillPlayableCatalog LoadDefault()
        {
            TextAsset text = Resources.Load<TextAsset>(ResourcePath);
            if (text == null)
                throw new InvalidOperationException("Official battle skill Playable catalog is missing: " + ResourcePath);
            OfficialBattleSkillPlayableCatalogData data =
                JsonUtility.FromJson<OfficialBattleSkillPlayableCatalogData>(text.text);
            if (data == null || data.manifest != Manifest || data.skills == null)
                throw new InvalidOperationException("Official battle skill Playable catalog is malformed or uses the wrong manifest.");
            return new OfficialBattleSkillPlayableCatalog(data);
        }

        public AnimationClip LoadClip(OfficialBattleSkillPlayableCue playable)
        {
            if (playable == null || playable.kind != 1 || string.IsNullOrEmpty(playable.clip_resource)) return null;
            if (clips.TryGetValue(playable.clip_resource, out AnimationClip cached)) return cached;
            AnimationClip clip = Resources.Load<AnimationClip>(ClipResourcePrefix + playable.clip_resource);
            if (clip != null) clips.Add(playable.clip_resource, clip);
            return clip;
        }
    }
}

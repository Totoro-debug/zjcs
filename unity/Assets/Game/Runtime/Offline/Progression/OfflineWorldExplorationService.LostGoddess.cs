using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Zjcs.Offline.Skills;
using static Zjcs.Offline.Progression.OfficialWorldExplorationCatalog;

namespace Zjcs.Offline.Progression
{
    [Serializable] public sealed class OfflineExploreLostGoddessAward
    {
        public int Type, SkillId, SourceRank, TargetRank, SourceLevel, TargetLevel;
        public OfflineExtraProperty[] Properties = Array.Empty<OfflineExtraProperty>();
    }

    public sealed partial class OfflineWorldExplorationService
    {
        // Local replacement for OperPlayerGetLostGoddessAward. The APK only
        // supplies category/group weights and the response consumed by the UI.
        // Candidate selection, free skill rank and retained level XP are the
        // explicitly confirmed single-player policy.
        public ProgressionResult AwardLostGoddess(int serial, int mapClassId, long now,
            OfficialCharacterCatalog character, OfflineSkillRules skills, out OfflineExploreLostGoddessAward award)
        {
            award = null;
            session.Reload();
            var entity = session.Save.World.Exploration.Entities.SingleOrDefault(v => v.Serial == serial);
            if (entity == null || entity.MapClassId != mapClassId || entity.Completed || !entity.Active(now))
                return Failure(string.Empty, ProgressionResultStatus.Unchanged);
            var components = Catalog.Components(entity.EntityClassId);
            var stage = Object(components["WorldStageComponent"]);
            var state = Object(Values(stage, "StateInfos")[entity.State]);
            var sub = Object(Values(state, "SubStateList").Single());
            if (Int(Object(sub["Behaviour"]), "CompType") != 2 || Values(sub, "Conditions").Count != 0
                || Values(sub, "Results").Count == 0 || Values(sub, "Results").Select(Object).Any(v => Int(v, "CompType") != 251))
                return Failure("This entity does not use the native lost goddess reward handler.", ProgressionResultStatus.Unsupported);
            var settings = character.Rows("game_settings").Single();
            var weights = Object(settings["LostGoddnessAwardWeights"]);
            var conditions = Object(settings["LostGoddnessRequireConditionIds"]);
            if (weights.Any(v => v.Key != "2" && v.Key != "3" && v.Key != "4" && Convert.ToInt32(v.Value) != 0)
                || conditions.Any(v => Convert.ToInt32(v.Value) != 0))
                throw new InvalidDataException("Unconverted native lost goddess category or requirement.");
            string receipt = "world-explore-lost-goddess:" + serial;
            OfflineExploreLostGoddessAward result = null;
            var transaction = session.ApplyInventoryTransaction(receipt, save =>
            {
                var next = save.World.Exploration.Entities.Single(v => v.Serial == serial);
                var player = save.Player;
                var availableSkills = player.Skills.Skills.Where(v => v.Rank > 0
                    && skills.Skills.Any(def => def.ClassId == v.ClassId && v.Rank < def.MaximumRank)).ToArray();
                int levelCount = Int(settings, "LostGoddnessLevelUpCount");
                int levelLimit = Math.Min(OfflineCharacterExperience.BaseLevelLimit(player.SubRank, character.LevelLimits),
                    character.CumulativeLevelExperience.Count - 1);
                var types = new List<int>();
                if (availableSkills.Length > 0) types.Add(2);
                if (levelCount > 0 && player.Level + levelCount <= levelLimit) types.Add(3);
                types.Add(4);
                int type = types[Choose(types.Select(v => Convert.ToInt32(weights[v.ToString(CultureInfo.InvariantCulture)])).ToArray(),
                    ref player.InventoryRandomState)];
                result = new OfflineExploreLostGoddessAward { Type = type };
                if (type == 2)
                {
                    var skill = availableSkills[Choose(Enumerable.Repeat(1, availableSkills.Length).ToArray(), ref player.InventoryRandomState)];
                    result.SkillId = skill.ClassId; result.SourceRank = skill.Rank;
                    result.TargetRank = ++skill.Rank;
                }
                else if (type == 3)
                {
                    result.SourceLevel = player.Level; result.TargetLevel = checked(player.Level + levelCount);
                    long retained = Math.Max(0, player.Experience - character.CumulativeLevelExperience[player.Level]);
                    player.Experience = checked(character.CumulativeLevelExperience[result.TargetLevel] + retained);
                    player.Level = result.TargetLevel;
                }
                else
                {
                    var groups = Values(settings, "LostGoddnessPropAwardWeights").Select(Object).ToArray();
                    var group = groups[Choose(groups.Select(v => Int(v, "Wegiht")).ToArray(), ref player.InventoryRandomState)];
                    result.Properties = Values(group, "Props").Select(Object).Select(v => new OfflineExtraProperty
                    {
                        SourceId = receipt, PropId = Int(v, "PropId"), Level = Int(v, "Level"), PropType = Int(v, "PropType"), Count = 1
                    }).ToArray();
                    foreach (var property in result.Properties)
                    {
                        if (!character.GetLevelProps(property.PropId, property.Level).ContainsKey(property.PropType))
                            throw new InvalidDataException("Native lost goddess property table is not bound.");
                        player.CharacterProperties.ExtraProperties.Add(property);
                    }
                }
                next.LostGoddessAward = result;
                next.State = Catalog.Parameters(next.EntityClassId).CompleteState;
                next.Completed = true;
            });
            if (transaction.IsApplied) award = result;
            return transaction;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static StatusReplayProbe ConfigureStatusReplayProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_STATUS_REPLAY_ACCEPTANCE") != "1") return null;
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var skill = player.Skills.First(value => value.Slot == 0);
            skill.HasReplay = false;
            skill.CanTriggerChild = true;
            skill.ReplayMaxCountEachRound = -1;
            skill.ReplayReplacementEntities.Clear();
            for (int i = 0; i < 2; i++)
                player.Statuses.Add(new FightBattleStatusState { InstanceId = 9901101 + i, ClassId = 9901101 + i,
                    OwnerId = 1, CreatorId = 1, StartRound = 0, DurationRound = -1, CurDurationRound = -1,
                    SkillReplay = new FightBattleStatusReplayState { Rate = 1f, MaxCountEachRound = 1 } });
            var probe = new StatusReplayProbe();
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_STATUS_PARAMETERS_ACCEPTANCE") == "1")
            {
                var values = new List<FightStatusReplacementValue> {
                    new FightStatusReplacementValue { PropertyId = 5001, RawValue = 10000, IsFloat = true } };
                var status = OfficialCombatAssetCatalog.LoadDefault().Load().CreateStatus(60170, 1, 1,
                    9901103, replacementProperties: values);
                Require(status.RoundStartRulesSupported && status.RoundStartRate == 1f,
                    "The original equipment status did not resolve its typed round-start parameter.");
                player.Statuses.Add(status);
                probe.ParameterStatusInstanceId = status.InstanceId;
            }
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_STATUS_PROPERTIES_ACCEPTANCE") == "1")
            {
                var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
                var template = catalog.CreateStatus(60541, 1, 1, sourceProperties: player.Properties,
                    replacementProperties: new List<FightStatusReplacementValue> {
                        new FightStatusReplacementValue { PropertyId = 1, RawValue = 17 } },
                    blessPropertyScales: new List<FightStatusPropertyScale> {
                        new FightStatusPropertyScale { EntityId = 60541, PropertyId = 1, Factor = 1.5 } });
                template.DurationRound = -1;
                template.MaxStackedCount = -1;
                var applied = FightStatusLifecycle.Apply(player, template, 0, 9901104);
                Require(applied.ResolvedProperties == null,
                    "A runtime-applied property status had an empty resolved cache before initialization.");
                probe.PropertyStatusInstanceId = applied.InstanceId;
            }
            return probe;
        }

        private sealed class StatusReplayProbe
        {
            public int ParameterStatusInstanceId;
            public int PropertyStatusInstanceId;
            private bool propertyStackUpdated;

            public void Observe(FightBattleCheckpoint checkpoint)
            {
                foreach (var status in checkpoint.Units.Single(value => value.Id == 1).Statuses
                    .Where(value => value.InstanceId == 9901101 || value.InstanceId == 9901102))
                    Require(status.SkillReplay.CountEachRound <= 1, "A passive replay exceeded its independent round cap.");
                if (PropertyStatusInstanceId > 0)
                {
                    var status = checkpoint.Units.Single(value => value.Id == 1).Statuses
                        .Single(value => value.InstanceId == PropertyStatusInstanceId);
                    long expected = propertyStackUpdated ? 50 : 25;
                    Require(status.StatusProperties != null && status.StatusProperties.Single(value => value.Id == 1).Value == 25
                        && status.ResolvedProperties != null && status.ResolvedProperties.Single(value => value.Id == 1).Value == expected,
                        "Original raw replacement, blessing truncation or stacked property projection was lost.");
                    if (!propertyStackUpdated)
                    {
                        FightStatusLifecycle.AddStack(status, 1);
                        var assets = OfficialCombatAssetCatalog.LoadDefault();
                        FightStatusPropertyRuntime.RebuildAll(checkpoint.Units, assets.Load(), assets.LoadDamageFormula());
                        Require(status.ResolvedProperties.Single(value => value.Id == 1).Value == 50,
                            "A stack change retained the old property cache.");
                        propertyStackUpdated = true;
                    }
                }
            }

            public void Verify(string name, FightBattleCheckpoint checkpoint)
            {
                Observe(checkpoint);
                var replays = checkpoint.Receipts.SelectMany(value => value.Event.SkillCasts)
                    .Where(value => value.ActorId == 1 && value.IsReplay
                        && (value.ReplayStatusInstanceId == 9901101 || value.ReplayStatusInstanceId == 9901102)).ToArray();
                Require(replays.Length >= 2 && replays[0].ReplayStatusInstanceId == 9901101
                    && replays[1].ReplayStatusInstanceId == 9901102,
                    name + " did not execute both independent passive replays in FIFO order.");
                var player = checkpoint.Units.Single(value => value.Id == 1);
                Require(player.SkillState.Slots.First(value => value.Slot == 0).ReleasedTimes
                    >= replays.Length, name + " did not include passive replays in its release count.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    name + " did not preserve passive replay progress and provenance in its checkpoint.");
                Debug.Log("[Status replay hosts] " + name + ": independent FIFO replays=" + replays.Length
                    + ", round caps, release counts and checkpoint passed.");
                if (ParameterStatusInstanceId > 0)
                {
                    int count = checkpoint.Receipts.SelectMany(value => value.Event.SkillCasts)
                        .Count(value => value.ActorId == 1 && value.SkillEntityId == 60171);
                    Require(count > 0, name + " did not execute the original parameterized round-start child skill.");
                    Debug.Log("[Status parameter hosts] " + name + ": original 60170 -> 60171 casts=" + count
                        + ", typed probability and checkpoint passed.");
                }
                if (PropertyStatusInstanceId > 0)
                    Debug.Log("[Status property hosts] " + name
                        + ": runtime application, raw attack 17 -> blessing 25 -> two stacks 50 and checkpoint passed.");
            }
        }
    }
}

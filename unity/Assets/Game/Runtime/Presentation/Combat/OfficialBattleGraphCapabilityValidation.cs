using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleSkillPlayables;

namespace Zjcs.Presentation.Combat
{
    // Isolated original-graph inputs for the public weekly host, never user unlocks.
    public sealed class OfficialBattleGraphCapabilityValidation
    {
        private readonly FightOfficialCombatCatalog catalog;
        private readonly FightBattleSkillDefinition finisher;
        private readonly int skillId;
        private readonly string clipName;
        private bool settled, animated, captured, finisherScheduled;
        private int nativeCasts, nativeChildren;
        private float castTime;
        private readonly OfficialBattleAfterimageValidation afterimages;
        private OfficialBattleSkillEntityPlayback entityViews;
        private bool movementObserved, movementAdvanced, movementRemoved, loopObserved, escapeObserved;
        private bool afterimageObserved, loopCaptured, movementCaptured;
        private Vector3 movementFrom, movementTo;
        private int approachClass;
        private Vector3 approachFrom, approachTo;
        private bool approachObserved, approachAdvanced, approachArrived, approachRemoved, approachCaptured;
        private int forcedTargetId;
        private Quaternion forcedTargetRotation;
        private int forcedFrames;
        private int previousCaptureFramerate;
        private bool ValidateForcedMovement => skillId == 13550;
        private bool ValidateMovement => skillId == 11510
            && Environment.GetEnvironmentVariable("ZJCS_NATIVE_MOVEMENT_ACCEPTANCE") == "1";
        private bool ValidateApproach => ValidateMovement
            && Environment.GetEnvironmentVariable("ZJCS_NATIVE_MOVEMENT_APPROACH") == "1";
        public string CaptureName { get; private set; }

        private OfficialBattleGraphCapabilityValidation(FightOfficialCombatCatalog catalog,
            FightBattleSkillDefinition finisher, int skillId, string clipName)
        {
            this.catalog = catalog; this.finisher = finisher; this.skillId = skillId; this.clipName = clipName;
            if (Environment.GetEnvironmentVariable("ZJCS_AFTERIMAGE_ACCEPTANCE") == "1")
                afterimages = new OfficialBattleAfterimageValidation(skillId == 13970 ? 4 : 6);
        }

        public static OfficialBattleGraphCapabilityValidation Configure(FightBattleCheckpoint checkpoint)
        {
            string mode = Environment.GetEnvironmentVariable("ZJCS_GRAPH_CAPABILITY_ACCEPTANCE");
            Require(int.TryParse(mode, out int id) && (id == 13640 || id == 13870 || id == 13970
                || id == 11510 || id == 12260 || id == 13550), "Unknown original graph.");
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var enemy = checkpoint.Units.First(value => value.Side != player.Side);
            var probe = new OfficialBattleGraphCapabilityValidation(catalog,
                player.Skills.First(value => value.Slot == 0), id,
                id == 13640 ? "200/skill_07" : id == 13870 ? "100/skill_72"
                    : id == 11510 ? "100/skill_55" : id == 12260 ? "100/skill_56" : "100/skill_02");
            int monster = id == 11510 || id == 13550 ? 3331 : 3665;
            enemy.MonsterId = monster; enemy.MonsterGroupNo = 0;
            enemy.CharacterType = catalog.MonsterCharacterType(monster);
            enemy.MonsterType = catalog.MonsterType(monster); enemy.AiType = catalog.MonsterAiType(monster);
            enemy.MaxHpPlies = catalog.MonsterHpPliesFor(monster, 0, enemy.Level);
            OfficialBattleUnitAssembler.ApplyMonsterIdentity(catalog, enemy);
            player.X = enemy.X; player.Y = enemy.Y - 2;
            if (probe.ValidateForcedMovement)
            {
                // A cold resource-load frame can span the entire original 0.25s track.
                // Sample this isolated visual check at a fixed cadence; gameplay is unchanged.
                probe.previousCaptureFramerate = Time.captureFramerate;
                Time.captureFramerate = 60;
                enemy.Direction = 1; enemy.HasFlipX = true; enemy.FlipX = true;
                probe.forcedTargetId = enemy.Id;
            }
            if (probe.ValidateApproach) player.Y = enemy.Y - 6;
            string layout = Environment.GetEnvironmentVariable("ZJCS_AFTERIMAGE_LAYOUT");
            if (!string.IsNullOrEmpty(layout))
            {
                Require(id == 13970 && (layout == "down" || layout == "left" || layout == "right"), "Unknown afterimage layout.");
                player.X = enemy.X + (layout == "left" ? 2 : layout == "right" ? -2 : 0);
                player.Y = enemy.Y + (layout == "down" ? 2 : 0);
            }
            if (id == 13640)
            {
                var ally = new FightBattleUnitState {
                    Id = checkpoint.Units.Max(value => value.Id) + 1,
                    MasterId = player.Id, Side = player.Side,
                    MonsterId = 9320, MonsterTemplateId = catalog.MonsterTemplateId(9320),
                    CharacterType = catalog.MonsterCharacterType(9320), MonsterType = catalog.MonsterType(9320),
                    AiType = catalog.MonsterAiType(9320), BodyOffsets = catalog.MonsterBodyOffsets(9320),
                    X = player.X - 1, Y = player.Y,
                    Level = player.Level, SubRank = player.SubRank,
                    Properties = player.Properties.Select(value => new FightPropertyValue { Id = value.Id, Value = value.Value }).ToList(),
                    BaseProperties = (player.BaseProperties ?? player.Properties).Select(value => new FightPropertyValue { Id = value.Id, Value = value.Value }).ToList(),
                    Turn = new FightTurnUnit()
                };
                ally.Turn.Id = ally.Id; ally.Turn.MasterId = player.Id;
                checkpoint.Units.Add(ally);
            }
            foreach (var unit in checkpoint.Units)
            {
                unit.Statuses.Clear(); unit.Skills.Clear(); unit.SkillState.Slots.Clear();
                long maxHp = unit.Side == player.Side ? 5000000 : 1000000000;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = maxHp;
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 100;
                    if (property.Id == catalog.PropertyType("Speed")) property.Value = unit == player ? 1000 : 1;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = maxHp;
                unit.Turn.Speed = unit == player ? 1000 : 1;
            }
            if (id == 13640)
            {
                var ally = checkpoint.Units.Single(value => value.Side == player.Side && value.Id != player.Id);
                ally.Hp = ally.Turn.Hp = 2500000;
                player.Statuses.Add(catalog.CreateStatus(25136, player.Id, player.Id, 9913640,
                    13970, 10209, 7, 30, player.SubRank));
            }
            if (id == 13870) enemy.Hp = enemy.Turn.Hp = 200000000;
            if (id == 13970) player.Statuses.Add(catalog.CreateStatus(24721, player.Id, player.Id, 9913970));
            int item = id == 13640 ? 10204 : id == 13870 ? 10201
                : id == 11510 ? 10067 : id == 12260 ? 10110 : id == 13550 ? 10679 : 10209;
            var skill = catalog.CreateSkill(id, 0, item, 5, 20, player.SubRank,
                catalog.CalculateSkillProps(id, 5, 20, player.SubRank)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            skill.LimitedTimes = 1;
            player.Skills.Add(skill); player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            checkpoint.MaxRound = 8;
            return probe;
        }

        public void BindPresentation(OfficialBattleRuntimeController controller)
        {
            var graph = (OfficialBattleSkillGraphPlayback)typeof(OfficialBattleRuntimeController)
                .GetField("skillGraphPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            var entities = (OfficialBattleSkillEntityPlayback)typeof(OfficialBattleSkillGraphPlayback)
                .GetField("entityViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(graph);
            entityViews = entities;
            if (ValidateForcedMovement)
                forcedTargetRotation = controller.TryGetUnitTransform(forcedTargetId).localRotation;
            entities.CastStarted += cast => {
                if (cast.ActorId != 1) return;
                if (cast.SkillEntityId == 13643) nativeChildren++;
                if (cast.SkillEntityId != skillId) return;
                nativeCasts++; castTime = Time.realtimeSinceStartup;
                var source = controller.TryGetUnitTransform(1);
                var renderer = source ? source.GetComponentInChildren<ProfessionNativeSceneRenderer>(true) : null;
                Require(renderer, "Original player renderer is absent.");
                renderer.OnClipApplied += (clip, name, layer, weight, time, previous, backward) => {
                    if (name == clipName && time > .04f && weight > 0) animated = true;
                    if (ValidateMovement && weight > 0f)
                    {
                        if (name == "100/skill_55_2" && time > .01f) loopObserved = true;
                        if (name == "100/skill_55_3" && time > .04f) escapeObserved = true;
                    }
                };
            };
        }

        public void ObserveStep(OfficialBattleRuntimeController controller)
        {
            if (ValidateApproach && approachClass == 0)
            {
                var approach = controller.Checkpoint.Receipts.Last().Event.Movements
                    .FirstOrDefault(value => value.UnitId == 1 && value.MoveBehaviourId != 11513);
                if (approach != null)
                {
                    Require(entityViews.OwnsMovement(approach), "Ordinary approach did not bind its original action.");
                    approachClass = approach.MoveBehaviourId;
                    var from = approach.Path[0]; var to = approach.Path[approach.Path.Count - 1];
                    approachFrom = entityViews.ToWorldPos(entityViews.GridToPos(
                        new Zjcs.Official.World.NativeTerrain.Common.Pos2d(from.X, from.Y)));
                    approachTo = entityViews.ToWorldPos(entityViews.GridToPos(
                        new Zjcs.Official.World.NativeTerrain.Common.Pos2d(to.X, to.Y)));
                }
            }
            if (settled) return;
            var checkpoint = controller.Checkpoint; var step = checkpoint.Receipts.Last().Event;
            if (!step.SkillCasts.Any(value => value.ActorId == 1 && value.SkillEntityId == skillId)) return;
            var player = checkpoint.Units.Single(value => value.Id == 1);
            if (skillId == 13640)
            {
                var ally = checkpoint.Units.Single(value => value.Side == player.Side && value.Id != player.Id);
                var children = step.SkillCasts.Where(value => value.SkillEntityId == 13643).ToArray();
                Require(children.Length == 2 && children[0].TargetIds.SequenceEqual(new[] { ally.Id })
                    && children[1].TargetIds.SequenceEqual(new[] { player.Id }), "HP ordering/full-HP second target differs.");
                Require(step.DamageEvents.Count(value => value.SkillEntityId == 13643) == 2
                    && ally.Hp > 2500000 && player.Hp == player.MaxHp
                    && player.Statuses.Any(value => value.ClassId == 24435 && value.ShieldRemainValue > 0),
                    "Explicit child recovery or full-HP conversion differs.");
            }
            else
            {
                var hits = step.DamageEvents.Where(value => value.SkillEntityId == skillId).ToArray();
                if (ValidateForcedMovement)
                    Require(hits.Length == 1 && hits[0].HasDamageValue && !hits[0].Dodge && !hits[0].Immunity,
                        "Original forced hit did not resolve.");
                else Require(hits.Length > 0 && hits.All(value => value.Damage < 0),
                        "Conditional graph has no actual damage settlement.");
                if (skillId == 13970) Require(hits.Length > 1 && hits.All(value => value.HasHitGridPosition),
                    "Original area lost large-target cell occurrences.");
            }
            Require(player.SkillState.Slots[0].ReleasedTimes == 1, "Graph spent extra ordinary releases.");
            if (ValidateMovement)
            {
                var movement = step.Movements.Single(value => value.UnitId == 1 && value.MoveBehaviourId == 11513);
                Require(entityViews.OwnsMovement(movement) && movement.HasDirection && movement.HasFacing,
                    "Original 11513 action or its direction/facing snapshot is unbound.");
                var start = movement.Path[0]; var end = movement.Path[movement.Path.Count - 1];
                movementFrom = entityViews.ToWorldPos(entityViews.GridToPos(
                    new Zjcs.Official.World.NativeTerrain.Common.Pos2d(start.X, start.Y)));
                movementTo = entityViews.ToWorldPos(entityViews.GridToPos(
                    new Zjcs.Official.World.NativeTerrain.Common.Pos2d(end.X, end.Y)));
            }
            if (ValidateForcedMovement)
            {
                var movement = step.Movements.Single(value => value.UnitId == forcedTargetId
                    && value.MoveBehaviourId == 13553);
                Require(entityViews.OwnsMovement(movement) && movement.HasDirection && movement.Direction == 1
                    && movement.HasFacing && movement.EndFlipX,
                    "Forced action lost the target direction or independent mirror.");
                var start = movement.Path[0]; var end = movement.Path[movement.Path.Count - 1];
                Require(start.X == end.X && end.Y > start.Y,
                    "Forced fixture did not settle a distinct vertical knockback path.");
                movementFrom = entityViews.ToWorldPos(entityViews.GridToPos(
                    new Zjcs.Official.World.NativeTerrain.Common.Pos2d(start.X, start.Y)));
                movementTo = entityViews.ToWorldPos(entityViews.GridToPos(
                    new Zjcs.Official.World.NativeTerrain.Common.Pos2d(end.X, end.Y)));
            }
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            Require(restored.RandomSeed == checkpoint.RandomSeed
                && restored.Receipts.Last().Event.SkillCasts.Count == step.SkillCasts.Count,
                "Graph checkpoint differs.");
            Debug.Log("[Graph capability] " + skillId + ": actual original graph, one release and checkpoint passed.");
            settled = true; player.Skills.Clear(); player.SkillState.Slots.Clear();
        }

        public void ObserveFrame(OfficialBattleRuntimeController controller)
        {
            CaptureName = null;
            bool afterimageVisible = afterimages?.Observe() ?? false;
            afterimageObserved |= afterimageVisible;
            if (ValidateForcedMovement && settled)
            {
                var target = controller.TryGetUnitTransform(forcedTargetId);
                var action = target ? target.root.GetComponentsInChildren<MyFightActionSkillMove>()
                    .FirstOrDefault(value => value.myEntity.Entity != null && value.myEntity.Entity.ClassId == 13553
                        && value.myApplyEntity && value.myApplyEntity.transform == target) : null;
                if (action)
                {
                    var parameters = (System.Collections.IDictionary)typeof(MyFightActionSkillMove)
                        .GetField("mParam", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(action);
                    Require(parameters != null && Convert.ToInt32(parameters["ParamSource2TargetDirection"]) == 2,
                        "Original forced graph did not receive the left-facing target snapshot.");
                    movementObserved = true;
                    float travelled = Vector3.Distance(target.position, movementFrom);
                    movementAdvanced |= travelled > .01f && travelled < Vector3.Distance(movementTo, movementFrom) - .01f;
                    if (movementAdvanced && !movementCaptured)
                    { movementCaptured = true; CaptureName = "native-forced-movement-13553"; return; }
                }
                else if (movementObserved && !movementRemoved)
                {
                    movementRemoved = true;
                    Require(target && Vector3.Distance(target.position, movementTo) < .01f,
                        "Forced action End did not restore the target's settled position.");
                    Require(Quaternion.Angle(target.localRotation, forcedTargetRotation) < .01f,
                        "Forced action direction rotated the role grid instead of its graph parameter.");
                }
                if (nativeCasts > 0 && ++forcedFrames == 120 && !captured)
                    Require(false, "Forced frame observation incomplete: observed=" + movementObserved
                        + ", advanced=" + movementAdvanced + ", removed=" + movementRemoved
                        + ", captured=" + movementCaptured + ", nativeCasts=" + nativeCasts
                        + ", target=" + (target ? target.position.ToString("F3") : "missing")
                        + ", from=" + movementFrom.ToString("F3") + ", to=" + movementTo.ToString("F3"));
            }
            if (ValidateApproach && approachClass != 0 && !approachRemoved)
            {
                var approachingSource = controller.TryGetUnitTransform(1);
                var approach = approachingSource.root.GetComponentsInChildren<MyFightActionSkillMove>()
                    .FirstOrDefault(value => value.myEntity.Entity != null
                        && value.myEntity.Entity.ClassId == approachClass
                        && value.myApplyEntity && value.myApplyEntity.transform == approachingSource);
                approachArrived |= Vector3.Distance(approachingSource.position, approachTo) < .01f;
                if (approach)
                {
                    approachObserved = true;
                    float travelled = Vector3.Distance(approachingSource.position, approachFrom);
                    approachAdvanced |= travelled > .01f && travelled < Vector3.Distance(approachTo, approachFrom) - .01f;
                    if (approachAdvanced && !approachCaptured)
                    { approachCaptured = true; CaptureName = "native-ordinary-approach"; return; }
                }
                else if (approachObserved) approachRemoved = true;
            }
            if (ValidateMovement && settled)
            {
                var movingSource = controller.TryGetUnitTransform(1);
                var active = movingSource ? movingSource.root.GetComponentsInChildren<MyFightActionSkillMove>()
                    .Where(value => value.myEntity.Entity != null && value.myEntity.Entity.ClassId == 11513
                        && value.myApplyEntity && value.myApplyEntity.transform == movingSource).ToArray()
                    : Array.Empty<MyFightActionSkillMove>();
                if (active.Length > 0)
                {
                    movementObserved = true;
                    float travelled = Vector3.Distance(movingSource.position, movementFrom);
                    float length = Vector3.Distance(movementTo, movementFrom);
                    movementAdvanced |= travelled > .01f && travelled < length - .01f;
                    if (movementAdvanced && !movementCaptured)
                    { movementCaptured = true; CaptureName = "native-movement-11513"; return; }
                }
                else if (movementObserved && !movementRemoved)
                {
                    movementRemoved = true;
                    Require(Vector3.Distance(movingSource.position, movementTo) < .01f,
                        "Original movement End did not restore the settled grid position.");
                }
                if (loopObserved && !loopCaptured)
                { loopCaptured = true; CaptureName = "native-movement-loop-11510"; return; }
            }
            if (captured && !finisherScheduled)
            {
                var player = controller.Checkpoint.Units.Single(value => value.Id == 1);
                player.Skills.Add(finisher); player.SkillState.Slots.Add(catalog.CreateSkillSlot(finisher, 0));
                foreach (var property in player.Properties.Concat(player.BaseProperties ?? player.Properties))
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 1000000000;
                finisherScheduled = true;
            }
            if (captured || !settled || (!ValidateForcedMovement && !animated) || nativeCasts != 1
                || skillId == 13640 && nativeChildren != 2
                || ValidateForcedMovement && (!movementObserved || !movementAdvanced || !movementRemoved || !movementCaptured)
                || ValidateApproach && (!approachObserved || !approachAdvanced || !approachArrived || !approachRemoved)
                || (ValidateMovement ? !movementObserved || !movementAdvanced || !movementRemoved
                    || !loopObserved || !escapeObserved || afterimages != null && !afterimageObserved
                    : afterimages != null ? !afterimageVisible : Time.realtimeSinceStartup - castTime < .85f)) return;
            var source = controller.TryGetUnitTransform(1);
            if (!source || !source.GetComponentsInChildren<MeshRenderer>().Any(value => value.enabled
                && value.isVisible && !value.forceRenderingOff
                && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0)) return;
            captured = true; CaptureName = "graph-capability-" + skillId;
        }

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            if (ValidateForcedMovement) Time.captureFramerate = previousCaptureFramerate;
            afterimages?.Verify();
            if (ValidateApproach) Require(approachObserved && approachAdvanced && approachArrived
                && approachRemoved && approachCaptured, "Original ordinary approach differs: observed=" + approachObserved
                    + ", advanced=" + approachAdvanced + ", arrived=" + approachArrived
                    + ", removed=" + approachRemoved + ", captured=" + approachCaptured);
            if (ValidateMovement) Require(movementObserved && movementAdvanced && movementRemoved
                && loopObserved && escapeObserved && loopCaptured && movementCaptured,
                "Original native movement differs: observed=" + movementObserved
                    + ", advanced=" + movementAdvanced + ", removed=" + movementRemoved
                    + ", loop=" + loopObserved + ", escape=" + escapeObserved
                    + ", loopCaptured=" + loopCaptured + ", movementCaptured=" + movementCaptured);
            if (ValidateForcedMovement) Require(movementObserved && movementAdvanced && movementRemoved && movementCaptured,
                "Forced action did not advance, end and remove: observed=" + movementObserved
                    + ", advanced=" + movementAdvanced + ", removed=" + movementRemoved + ", captured=" + movementCaptured);
            Require(settled && (ValidateForcedMovement || animated) && captured && nativeCasts == 1 && checkpoint.Finished
                && (skillId != 13640 || nativeChildren == 2), "Native graph, action, input or settlement is incomplete.");
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Graph-capability diagnostic: " + message); }
    }
}

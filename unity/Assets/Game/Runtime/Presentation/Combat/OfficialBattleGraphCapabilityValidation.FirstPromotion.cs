using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private int firstPromotionItemId;
        private int firstPromotionDispelAllyId;
        private int[] firstPromotionHitScopeTargetIds = Array.Empty<int>();
        private int firstPromotionFacingIndex;
        private float firstPromotionCastClock;
        private int firstPromotionFrameCount;
        private float firstPromotionCaptureAfter = .85f;
        private int[] firstPromotionSummonMonsterIds = Array.Empty<int>();
        private int firstPromotionExpectedSummonCount;
        private int[] firstPromotionSpawnedUnitIds = Array.Empty<int>();
        private readonly System.Collections.Generic.HashSet<int> firstPromotionGeneratedUnitIds
            = new System.Collections.Generic.HashSet<int>();
        private static readonly float[] FirstPromotionFrameTimes = { .1f, .25f, .45f, .7f };
        private bool ValidateSummonDeath => firstPromotionItemId != 0
            && Environment.GetEnvironmentVariable("ZJCS_SUMMON_DEATH_ACCEPTANCE") == "1";
        private bool summonDeathArmed, summonDeathComplete;
        private int summonDeathEnemyId;
        private readonly HashSet<int> summonLethalHits = new HashSet<int>();
        private readonly HashSet<int> summonDeathFrames = new HashSet<int>();
        private readonly HashSet<int> summonRetiredFrames = new HashSet<int>();

        private bool ObserveFirstPromotionDeathFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateSummonDeath || !captured || summonDeathComplete) return false;
            var checkpoint = controller.Checkpoint;
            if (!summonDeathArmed)
            {
                Require(firstPromotionSpawnedUnitIds.Length > 0, "The death diagnostic requires original summoned units.");
                var player = checkpoint.Units.Single(value => value.Id == 1);
                var enemy = checkpoint.Units.First(value => value.Side != player.Side && value.Hp > 0);
                Require(enemy.MonsterId == 3331 && catalog.MonsterActiveSkillItems(3331, 0).Contains(14678),
                    "The death diagnostic's original enemy attack binding changed.");
                // Adjust only the isolated roster, then let the original AI and
                // 3331 attack 14678 -> 59460 -> 59461 settle actual lethal hits.
                const long playerHp = 1000000000000;
                foreach (var prop in player.Properties.Concat(player.BaseProperties ?? player.Properties))
                    if (prop.Id == catalog.PropertyType("MaxHp")) prop.Value = playerHp;
                player.Hp = player.Turn.Hp = player.MaxHp = player.LastMaxHp = playerHp;
                player.Targetable = false;
                int enemySpeed = firstPromotionItemId == 10058 ? 20000 : 1000;
                foreach (var prop in enemy.Properties.Concat(enemy.BaseProperties ?? enemy.Properties))
                {
                    if (prop.Id == catalog.PropertyType("Attack")) prop.Value = 1000000000;
                    if (prop.Id == catalog.PropertyType("Speed")) prop.Value = enemySpeed;
                }
                enemy.Turn.Speed = enemySpeed;
                var attack = catalog.CreateSkill(59460, 0, 14678, 2, 20, player.SubRank,
                    catalog.CalculateSkillProps(59460, 2, 20, player.SubRank)
                        .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                attack.LimitedTimes = firstPromotionSpawnedUnitIds.Length * 3;
                enemy.Skills.Clear(); enemy.SkillState.Slots.Clear();
                enemy.Skills.Add(attack); enemy.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
                checkpoint.MaxRound = 24;
                summonDeathEnemyId = enemy.Id; summonDeathArmed = true;
            }
            var graph = Resources.Load<GameObject>("Combat/SummonRoleViews/Graphs/Prefabs/Playables/playable_die_1");
            var property = graph.GetComponentsInChildren<Zjcs.Official.BattleMaterialPlayables.MyPlayableMaterialProperty>(true).Single();
            foreach (int unitId in firstPromotionSpawnedUnitIds)
            {
                if (!summonLethalHits.Contains(unitId)) continue;
                var root = controller.TryGetUnitTransform(unitId);
                var renderer = root ? root.GetComponentInChildren<Zjcs.Offline.ProfessionUI.BattleNativeMonsterRenderer>() : null;
                var material = renderer ? renderer.GetComponent<MeshRenderer>().sharedMaterial : null;
                if (renderer && renderer.Animator.GetBool("die") && material
                    && material.IsKeywordEnabled(property.Keyword) == property.KeywordBoolValue
                    && material.GetFloat(property.FloatPropertyName) > .1f
                    && material.GetFloat(property.FloatPropertyName) < .8f && summonDeathFrames.Add(unitId))
                {
                    CaptureName = "first-promotion-" + firstPromotionItemId + "-summon-death-" + unitId;
                    return true;
                }
                if (!root && summonDeathFrames.Contains(unitId) && summonRetiredFrames.Add(unitId))
                {
                    CaptureName = "first-promotion-" + firstPromotionItemId + "-summon-retired-" + unitId;
                    return true;
                }
            }
            summonDeathComplete = firstPromotionSpawnedUnitIds.All(id => summonLethalHits.Contains(id)
                && summonDeathFrames.Contains(id) && summonRetiredFrames.Contains(id));
            if (summonDeathComplete)
            {
                checkpoint.Units.Single(value => value.Id == 1).Targetable = true;
                Debug.Log("[Summon death battle] Original enemy attack, lethal settlement, death curve and visual retirement passed: "
                    + firstPromotionItemId + " / " + string.Join(",", firstPromotionSpawnedUnitIds));
            }
            return !summonDeathComplete;
        }

        private void ObserveFirstPromotionDeathStep(FightBattleStepEvent step)
        {
            if (!ValidateSummonDeath || !summonDeathArmed) return;
            foreach (var cast in step.SkillCasts.Where(value => value.ActorId == summonDeathEnemyId))
                Debug.Log("[Summon death battle] Enemy cast " + cast.SkillEntityId);
            foreach (var hit in step.DamageEvents)
            {
                if (hit.SourceId == summonDeathEnemyId)
                    Debug.Log("[Summon death battle] Hit " + hit.TargetId + ": " + hit.BeforeHp + " -> " + hit.AfterHp);
                if (hit.SourceId == summonDeathEnemyId && hit.SkillEntityId == 59460 && !hit.IsCure
                    && hit.BeforeHp > 0 && hit.AfterHp == 0 && firstPromotionSpawnedUnitIds.Contains(hit.TargetId))
                    summonLethalHits.Add(hit.TargetId);
            }
        }

        private void VerifyFirstPromotionDeath(FightBattleCheckpoint checkpoint)
        {
            if (!ValidateSummonDeath) return;
            Require(summonDeathComplete, "Original summon death was not settled, rendered and retired.");
            var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            Require(firstPromotionSpawnedUnitIds.All(id => saved.Units.Any(unit => unit.Id == id && unit.Hp == 0)),
                "Visual retirement changed the retained original summon checkpoint.");
        }

        private void ObserveFirstPromotionGeneration(OfficialBattleRuntimeController controller)
        {
            foreach (int unitId in firstPromotionSpawnedUnitIds)
            {
                var root = controller.TryGetUnitTransform(unitId);
                var renderer = root ? root.GetComponentInChildren<Zjcs.Offline.ProfessionUI.BattleNativeMonsterRenderer>() : null;
                if (renderer && renderer.Animation == "generate_01" && firstPromotionGeneratedUnitIds.Add(unitId))
                    Debug.Log("[Graph capability] Original summon generation observed: unit=" + unitId);
            }
        }

        private bool CaptureFirstPromotionFrame()
        {
            if (firstPromotionItemId == 0 || nativeCasts == 0 || finisherScheduled
                || firstPromotionFrameCount >= FirstPromotionFrameTimes.Length
                || Time.time - firstPromotionCastClock < FirstPromotionFrameTimes[firstPromotionFrameCount]) return false;
            CaptureName = "first-promotion-" + firstPromotionItemId + "-phase-" + (++firstPromotionFrameCount);
            return true;
        }

        public static (int Profession, int InitialWeaponType, int InitialSkillItemId) FirstPromotionSource()
        {
            Require(int.TryParse(Environment.GetEnvironmentVariable("ZJCS_FIRST_PROMOTION_SKILL"), out int itemId),
                "A first-promotion skill item is required.");
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var items = assets.LoadSkillItems();
            var rows = JsonUtility.FromJson<FirstPromotionRules>(assets.Rules.text).profession_base;
            var profession = ReadFirstPromotionProfession(rows, items, itemId);
            var root = profession;
            while (root.PrePro != 0) root = rows.Single(row => row.Profession == root.PrePro);
            Require(root.Profession == 1 || root.Profession == 10, "Unknown initial weapon lineage.");
            int weapon = root.Profession == 1 ? FightOfficialSkillItemCatalog.SwordInitialWeaponType
                : FightOfficialSkillItemCatalog.WandInitialWeaponType;
            return (profession.Profession, weapon, items.InitialActiveSkill(weapon));
        }

        private static FirstPromotionProfession ReadFirstPromotionProfession(FirstPromotionProfession[] rows,
            FightOfficialSkillItemCatalog items, int itemId)
        {
            var profession = rows.SingleOrDefault(row => new[] { 1, 2, 6, 10, 11, 15 }.Contains(row.Profession)
                && row.Skills.Concat(row.FightUsedSkills).Contains(itemId));
            Require(profession != null && items.ItemTypeFor(itemId) == 100,
                "The requested item is outside the six-profession active scope.");
            return profession;
        }

        private static OfficialBattleGraphCapabilityValidation ConfigureFirstPromotion(FightBattleCheckpoint checkpoint)
        {
            Require(int.TryParse(Environment.GetEnvironmentVariable("ZJCS_FIRST_PROMOTION_SKILL"), out int itemId),
                "A first-promotion skill item is required.");
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var items = assets.LoadSkillItems(); var catalog = assets.Load();
            var rules = JsonUtility.FromJson<FirstPromotionRules>(assets.Rules.text);
            var profession = ReadFirstPromotionProfession(rules.profession_base, items, itemId);
            var binding = items.Required(itemId);
            var skill = catalog.CreateSkill(binding.EntityId, 0, itemId, 2, 20, 1,
                catalog.CalculateSkillProps(binding.EntityId, 2, 20, 1)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            int direction = Environment.GetEnvironmentVariable("ZJCS_DIRECTIONAL_SKILL_FACING") switch {
                "up" => 0, "down" => 1, "left" => 2, "right" => 3,
                _ => throw new InvalidOperationException("Choose up/down/left/right for the skill diagnostic.") };
            int entityId = binding.DirectionEntityIds.TryGetValue(direction, out int mapped) ? mapped : binding.EntityId;
            var cues = JsonUtility.FromJson<OfficialBattleSkillPlayableCatalogData>(Resources.Load<TextAsset>("Combat/skill-playables").text);
            var cue = cues.skills.Single(row => row.skill_entity_id == entityId).playables
                .FirstOrDefault(value => value.kind == 1 && !string.IsNullOrEmpty(value.animation));
            Require(cue != null, "This diagnostic requires an authored body clip; use a dedicated probe for a pure logic graph.");
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            Require(player.Profession == profession.Profession, "The diagnostic profile has the wrong original profession.");
            var enemy = checkpoint.Units.First(unit => unit.Side != player.Side);
            var probe = new OfficialBattleGraphCapabilityValidation(catalog, player.Skills.First(value => value.Slot == 0),
                entityId, cue.animation) { firstPromotionItemId = itemId,
                    firstPromotionFacingIndex = new[] { 2, 0, 1, 3 }[direction] };
            enemy.MonsterId = 3331; enemy.MonsterGroupNo = 0;
            enemy.CharacterType = catalog.MonsterCharacterType(3331);
            enemy.MonsterType = catalog.MonsterType(3331); enemy.AiType = catalog.MonsterAiType(3331);
            enemy.MaxHpPlies = catalog.MonsterHpPliesFor(3331, 0, enemy.Level);
            OfficialBattleUnitAssembler.ApplyMonsterIdentity(catalog, enemy);
            enemy.BodyOffsets = new List<FightGridPoint> { new FightGridPoint(0, 0) };
            int dx = direction == 2 ? -1 : direction == 3 ? 1 : 0;
            int dy = direction == 0 ? 1 : direction == 1 ? -1 : 0;
            int distance = skill.Range.Where(point => point.X * dy == 0 && point.Y * dx == 0
                && point.X * dx + point.Y * dy > 0).Select(point => Math.Abs(point.X) + Math.Abs(point.Y))
                .DefaultIfEmpty(1).Min();
            // Keep every cardinal probe on its requested axis while the ordinary
            // AI chooses its landing. Adjacent placement can attract an equally
            // valid landing on another side before the actual cast.
            if (itemId == 10029)
                distance = skill.Range.Where(point => point.X * dy == 0 && point.Y * dx == 0
                    && point.X * dx + point.Y * dy > 0)
                    .Select(point => Math.Abs(point.X) + Math.Abs(point.Y)).DefaultIfEmpty(distance).Max();
            var summonHits = skill.HitGraphEntities.Where(hit => hit.IsSummon && hit.SummonReferenceType == 1
                && hit.SummonGridItemClassId == 0).ToArray();
            probe.firstPromotionSummonMonsterIds = summonHits.SelectMany(hit => hit.SummonPools)
                .Select(pool => pool.MonsterId).Distinct().ToArray();
            probe.firstPromotionExpectedSummonCount = summonHits.Where(hit => !hit.SummonIsRandom
                && hit.SummonPools.Count == 1).Sum(hit => hit.SummonPositions.Count);
            if (probe.firstPromotionSummonMonsterIds.Length > 0)
            {
                // Leave the source's authored summon footprint free in every facing.
                // Fixture placement must not duplicate or modify the runtime scope transform.
                distance = Math.Max(distance, summonHits.SelectMany(hit => hit.SummonPositions)
                    .Select(offset => Math.Abs(offset.X) + Math.Abs(offset.Y)).DefaultIfEmpty(0).Max() + 1);
                // The fire beasts have an authored two-round life. Observe their
                // death before expiry without changing their original status.
                probe.firstPromotionCaptureAfter = probe.ValidateSummonDeath && itemId == 10058 ? 1f : 3f;
            }
            player.X = enemy.X - dx * distance; player.Y = enemy.Y - dy * distance;
            player.Direction = new[] { 2, 0, 1, 3 }[direction];
            foreach (var unit in checkpoint.Units)
            {
                unit.Statuses.Clear(); unit.Skills.Clear(); unit.SkillState.Slots.Clear();
                long hp = unit.Side == player.Side ? 5000000 : 1000000000;
                int speed = unit == player ? 1000 : probe.ValidateSummonDeath ? (itemId == 10058 ? 20000 : 1000) : 1;
                foreach (var prop in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (prop.Id == catalog.PropertyType("MaxHp")) prop.Value = hp;
                    if (prop.Id == catalog.PropertyType("Attack")) prop.Value = 100;
                    if (prop.Id == catalog.PropertyType("Speed")) prop.Value = speed;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = hp;
                unit.Turn.Speed = speed;
            }
            // Give the original healing AI a valid wounded target in this fixture.
            if (skill.SkillType == 3) player.Hp = player.Turn.Hp = player.MaxHp / 2;
            if (itemId == 10066)
            {
                var ally = JsonUtility.FromJson<FightBattleUnitState>(JsonUtility.ToJson(enemy));
                ally.Id = checkpoint.Units.Max(unit => unit.Id) + 1;
                ally.Turn.Id = ally.Id; ally.Side = player.Side;
                ally.X = player.X + dy; ally.Y = player.Y - dx;
                checkpoint.Units.Add(ally); probe.firstPromotionDispelAllyId = ally.Id;
                foreach (int statusId in new[] { 24040, 24041 })
                    foreach (var unit in new[] { player, ally })
                        unit.Statuses.Add(catalog.CreateStatus(statusId, unit.Id, enemy.Id, 990000 + statusId * 10 + unit.Id));
            }
            string absorptionProbe = Environment.GetEnvironmentVariable("ZJCS_STATUS_HIT_CUSTOM_CURE_ACCEPTANCE");
            if (Environment.GetEnvironmentVariable("ZJCS_STATUS_MOVE_SCOPE_ACCEPTANCE") == "1")
            {
                Require((itemId == 10029 || itemId == 10055) && direction == 0 && checkpoint.Grid.Height >= 9,
                    "The movement-burning diagnostic uses original 10029/10810 or 10055/11070 facing up.");
                enemy.X = player.X = checkpoint.Grid.Width / 2;
                enemy.Y = Math.Min(checkpoint.Grid.Height - 2, 8); player.Y = enemy.Y - 5;
                var passive = catalog.CreateStatus(25027, player.Id, player.Id, 900006,
                    0, 10561, 2, 20, player.SubRank);
                // Isolate routing and native terrain; original per-cell 30% and
                // stream order are separately checked against the saved RNG.
                foreach (var cell in passive.Components.MoveScope.Cells) cell.Rate = 1f;
                player.Statuses.Add(passive);
            }
            if (!string.IsNullOrEmpty(absorptionProbe))
            {
                Require(itemId == 10029 && entityId == 10810
                    || probe.ValidateHitCustomCureTargets && itemId == 10072 && entityId == 11690,
                    "The absorption diagnostic requires an original supported source graph.");
                Require(new[] { "normal", "blocked", "shield" }.Contains(absorptionProbe), "Unknown absorption diagnostic combination.");
                player.Hp = player.Turn.Hp = player.MaxHp / 2;
                player.Statuses.Add(catalog.CreateStatus(25023, player.Id, player.Id, 900004,
                    0, 10544, 2, 20, player.SubRank));
                if (absorptionProbe != "normal")
                    player.Statuses.Add(catalog.CreateStatus(absorptionProbe == "blocked" ? 24223 : 25136,
                        player.Id, player.Id, 900005, 0, 0, 2, 20, player.SubRank));
                probe.ConfigureHitCustomCureTargets(checkpoint, enemy, dx, dy);
            }
            if (Environment.GetEnvironmentVariable("ZJCS_STATUS_DARK_HIT_ACCEPTANCE") == "1")
            {
                Require(itemId == 10029 && entityId == 10810, "The dark-hit diagnostic uses original 10029/10810.");
                var passive = catalog.CreateStatus(25031, player.Id, player.Id, 900003,
                    0, 10565, 2, 20, player.SubRank);
                // Isolate event routing; the original affected 40% probability is
                // independently checked against the original formula and random stream.
                passive.Components.DarkHit.Statuses[0].BasePercent = 1f;
                passive.Components.DarkHit.Statuses[0].AffectedByProp = false;
                player.Statuses.Add(passive);
            }
            if (Environment.GetEnvironmentVariable("ZJCS_STATUS_HIT_SCOPE_ACCEPTANCE") == "1")
            {
                Require(itemId == 10072, "The fire-scope diagnostic uses original 10072/11690.");
                var passive = catalog.CreateStatus(25045, player.Id, player.Id, 900002,
                    0, 10576, 2, 20, player.SubRank);
                // Deterministic isolated routing/graphics probe; original 20% is
                // separately checked against the saved random stream in rule tests.
                passive.Components.HitScope.Rate = 1f;
                player.Statuses.Add(passive);
                if (probe.ValidateHitScopeLifecycle)
                {
                    // Two separate original targets occupy two cells of 11690's
                    // authored area; the ordinary AI still chooses and casts.
                    var other = JsonUtility.FromJson<FightBattleUnitState>(JsonUtility.ToJson(enemy));
                    other.Id = checkpoint.Units.Max(unit => unit.Id) + 1; other.Turn.Id = other.Id;
                    other.X += dx; other.Y += dy;
                    checkpoint.Units.Add(other);
                    probe.firstPromotionHitScopeTargetIds = new[] { enemy.Id, other.Id };
                }
            }
            if (Environment.GetEnvironmentVariable("ZJCS_STATUS_LIFE_REDUCTION_ACCEPTANCE") == "1")
            {
                Require(itemId == 10019, "The lifespan diagnostic uses original 10019/3181.");
                player.Statuses.Add(catalog.CreateStatus(25112, player.Id, player.Id, 900001,
                    0, 10603, 2, 20, player.SubRank));
            }
            probe.ConfigureActionInvisibleDiagnostic(checkpoint);
            probe.ConfigureAirComboDiagnostic(itemId, checkpoint);
            skill.LimitedTimes = 1;
            player.Skills.Add(skill); player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            checkpoint.MaxRound = 12;
            probe.ConfigureSummonLifeDiagnostic(checkpoint);
            probe.ConfigureDarkStatusDiagnostic(checkpoint);
            // Preserve short authored particle/action phases across cold-loading frames.
            probe.previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = 60;
            return probe;
        }

        private void ObserveFirstPromotionStep(FightBattleCheckpoint checkpoint, FightBattleStepEvent step)
        {
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            var cast = step.SkillCasts.Single(value => value.ActorId == player.Id && value.Slot == 0);
            Require(cast.SkillItemId == firstPromotionItemId && cast.SkillEntityId == skillId
                && cast.Presentation != null && cast.Presentation.FacingIndex == firstPromotionFacingIndex
                && player.SkillState.Slots[0].ReleasedTimes == 1,
                "Original equipped source, directional entity or release count differs: item="
                + cast.SkillItemId + "/" + firstPromotionItemId + "; entity=" + cast.SkillEntityId + "/" + skillId
                + "; facing=" + cast.Presentation?.FacingIndex + "/" + firstPromotionFacingIndex
                + "; releases=" + player.SkillState.Slots[0].ReleasedTimes);
            if (Environment.GetEnvironmentVariable("ZJCS_STATUS_MOVE_SCOPE_ACCEPTANCE") == "1")
            {
                var component = player.Statuses.Single(status => status.ClassId == 25027).Components.MoveScope;
                var movement = step.Movements.Single(value => value.UnitId == player.Id);
                bool walking = firstPromotionItemId == 10055;
                int expected = walking ? movement.Path.Count : 2;
                Require(component.StartChecked && component.TriggeredCount == expected
                    && (walking ? movement.Path.Count >= 3 && movement.MoveBehaviourId == 11073
                        : movement.Path.Count == 2 && movement.MoveSkillEntityId == 15300)
                    && step.SkillCasts.Any(value => value.SkillEntityId == 11630),
                    "Original first activation, single jump landing or independent round-start skill differs.");
                Require(checkpoint.Receipts.Count >= 2 && checkpoint.Receipts[0].Event.GridViewEvents.Count(value =>
                    value.Kind == FightBattleGridViewEventKind.Create) == 5,
                    "Initial movement burning was not emitted as five original terrain creations.");
                float arrival = movement.Delay;
                for (int index = 1; index < movement.Path.Count; index++)
                {
                    var delta = movement.Path[index] - movement.Path[index - 1];
                    arrival += movement.MoveDuration >= 0 ? movement.MoveDuration
                        : (float)Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) / movement.MoveSpeed;
                    Require(step.GridViewEvents.Count(value => value.Kind == FightBattleGridViewEventKind.Create
                        && Math.Abs(value.Delay - arrival) < .0001f) == 5,
                        "Walked cell/jump landing terrain did not retain its original arrival delay.");
                }
                if (walking) terrainScopeWalkingPath = movement;
                var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                var savedComponent = saved.Units.Single(unit => unit.Id == player.Id).Statuses
                    .Single(status => status.ClassId == 25027).Components.MoveScope;
                Require(savedComponent.StartChecked && savedComponent.TriggeredCount == expected,
                    "Movement-burning activation or count was lost in the Unity checkpoint.");
                Debug.Log("[Move scope] Original 25027 first activation and " + (walking ? "11073 walked cells" : "15300 landing")
                    + " -> 3320/11632; independent 11630 and checkpoint passed; isolated cells=1.");
            }
            if (firstPromotionSummonMonsterIds.Length > 0)
            {
                firstPromotionSpawnedUnitIds = step.SpawnedUnitIds.ToArray();
                Require(firstPromotionSpawnedUnitIds.Length >= firstPromotionExpectedSummonCount,
                    "Original summon count differs: expected=" + firstPromotionExpectedSummonCount
                    + "; actual=" + firstPromotionSpawnedUnitIds.Length);
                foreach (int monsterId in firstPromotionSummonMonsterIds)
                    Require(checkpoint.Units.Any(unit => firstPromotionSpawnedUnitIds.Contains(unit.Id)
                        && unit.MonsterId == monsterId && unit.MasterId == player.Id),
                        "Original summon was not created in the fixture: monster=" + monsterId);
            }
            if (firstPromotionItemId == 10066)
                foreach (var unit in new[] { player,
                    checkpoint.Units.SingleOrDefault(value => value.Id == firstPromotionDispelAllyId) })
                    Require(unit != null
                        && unit.Statuses.Count(status => status.ClassId == 24040 || status.ClassId == 24041) == 1
                        && unit.Statuses.Any(status => status.ClassId == 24237)
                        && step.DispersedUnitIds.Contains(unit.Id)
                        && step.DamageEvents.Count(hit => hit.TargetId == unit.Id) == 1,
                        "Original area dispel and independent buff differ: unit=" + unit?.Id
                        + "; statuses=" + (unit == null ? "null" : string.Join(",", unit.Statuses.Select(status => status.ClassId)))
                        + "; notified=" + string.Join(",", step.DispersedUnitIds)
                        + "; hits=" + string.Join(",", step.DamageEvents.Select(hit => hit.TargetId)));
            if (Environment.GetEnvironmentVariable("ZJCS_STATUS_HIT_SCOPE_ACCEPTANCE") == "1")
            {
                var component = player.Statuses.Single(status => status.ClassId == 25045).Components.HitScope;
                int hits = step.DamageEvents.Count(hit => hit.SourceId == player.Id && hit.SkillEntityId == 11690
                    && !hit.Dodge && !hit.Immunity && !hit.Blinding);
                int triggered = step.SkillCasts.Count(value => value.SkillEntityId == 11580);
                Require(hits >= 3 && triggered == hits && component.TriggeredCount == hits
                    && checkpoint.GridItems.Any(item => item.GridItemClassId == 3320),
                    "Original multi-hit fire scope did not create burning ground for each successful occurrence.");
                if (ValidateHitScopeLifecycle)
                {
                    Require(firstPromotionHitScopeTargetIds.Length == 2 && hits == 6,
                        "Original three-stage fire must hit both independent targets: " + hits);
                    foreach (int targetId in firstPromotionHitScopeTargetIds)
                    {
                        var target = checkpoint.Units.Single(value => value.Id == targetId);
                        Require(step.DamageEvents.Count(hit => hit.SourceId == player.Id && hit.SkillEntityId == 11690
                            && hit.TargetId == targetId && !hit.Dodge && !hit.Immunity && !hit.Blinding) == 3
                            && checkpoint.GridItems.Any(item => !item.Ended && item.GridItemClassId == 3320
                                && item.X == target.X && item.Y == target.Y && item.Runtime.Statuses.Any(status =>
                                    status.ClassId == 11582 && status.ApplyingSkillItemId == 10576
                                    && status.SourceSkillRank == 2 && status.SourceSkillLevel == 20)),
                            "Original per-target terrain or cultivation source differs: " + targetId);
                    }
                    var creates = step.GridViewEvents.Where(value => value.Kind == FightBattleGridViewEventKind.Create).ToArray();
                    Require(creates.Length == 6 && creates.Select(value => value.Delay).SequenceEqual(
                        step.SkillCasts.Where(value => value.SkillEntityId == 11580).Select(value => value.Delay)),
                        "Original fire terrain did not retain each target occurrence's hit delay.");
                }
                var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                Require(saved.Units.Single(unit => unit.Id == player.Id).Statuses.Single(status => status.ClassId == 25045)
                    .Components.HitScope.TriggeredCount == hits,
                    "Fire-scope trigger count was lost in the Unity checkpoint.");
                Debug.Log("[Fire-hit scope] Original 11690 -> 25045 -> 11580 -> 3320/11582, per-hit routing and checkpoint passed; isolated rate=1.");
            }
            string absorptionProbe = Environment.GetEnvironmentVariable("ZJCS_STATUS_HIT_CUSTOM_CURE_ACCEPTANCE");
            if (!string.IsNullOrEmpty(absorptionProbe))
            {
                int hits = step.DamageEvents.Count(hit => hit.SourceId == player.Id && hit.SkillEntityId == skillId
                    && !hit.Dodge && !hit.Immunity && !hit.Blinding);
                var cures = step.DamageEvents.Where(hit => hit.SourceId == player.Id && hit.SkillEntityId == 10520 && hit.IsCure).ToArray();
                var component = player.Statuses.Single(status => status.ClassId == 25023).Components.HitCustomCure;
                Require(hits > 0 && component.Filter.InvokeCount == hits && cures.Length == hits
                    && cures.All(hit => hit.TargetId == player.Id), "Original per-hit absorption did not dispatch self-cure.");
                Require(absorptionProbe == "normal" ? cures.Sum(hit => hit.AfterHp - hit.BeforeHp) > 0
                    : cures.All(hit => hit.AfterHp == hit.BeforeHp), "Absorption healing or interception differs.");
                if (absorptionProbe == "shield")
                    Require(player.Statuses.Any(status => status.ClassId == 24435 && status.ShieldRemainValue > 0),
                        "Original absorption healing was not converted to its configured shield.");
                var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                Require(saved.Units.Single(unit => unit.Id == player.Id).Statuses.Single(status => status.ClassId == 25023)
                    .Components.HitCustomCure.Filter.InvokeCount == hits, "Absorption counter was lost in the Unity checkpoint.");
                ObserveHitCustomCureTargets(checkpoint, step);
                Debug.Log("[Hit custom cure] Original " + skillId + " -> 25023 -> 10520, " + absorptionProbe + ", checkpoint and self-cure passed.");
            }
            if (Environment.GetEnvironmentVariable("ZJCS_STATUS_DARK_HIT_ACCEPTANCE") == "1")
            {
                int hits = step.DamageEvents.Count(hit => hit.SourceId == player.Id && hit.SkillEntityId == 10810
                    && !hit.Dodge && !hit.Immunity && !hit.Blinding);
                var component = player.Statuses.Single(status => status.ClassId == 25031).Components.DarkHit;
                Require(hits > 0 && component.TriggerCount == hits
                    && checkpoint.Units.Any(unit => unit.Side != player.Side && unit.Statuses.Any(status => status.ClassId == 24222
                        && status.ApplyingSkillItemId == 10565 && status.CreatorId == player.Id)),
                    "Original dark hit did not apply the original passive status with its source.");
                var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                Require(saved.Units.Single(unit => unit.Id == player.Id).Statuses.Single(status => status.ClassId == 25031)
                    .Components.DarkHit.TriggerCount == hits, "Dark-hit counter was lost in the Unity checkpoint.");
                Debug.Log("[Dark hit] Original 10810 -> 25031 -> 24222, passive source and checkpoint passed; isolated chance=1.");
            }
            if (Environment.GetEnvironmentVariable("ZJCS_STATUS_LIFE_REDUCTION_ACCEPTANCE") == "1")
            {
                var summon = checkpoint.Units.Single(unit => firstPromotionSpawnedUnitIds.Contains(unit.Id));
                var life = summon.Statuses.Single(status => status.ClassId == 10712);
                var reduction = summon.Statuses.Single(status => status.ClassId == 24072).Components.LifeReduction;
                Require(life.DurationRound == 5 && life.CurDurationRound == 3
                    && reduction.ReduceRoundCount == 2 && reduction.ReducedStatusKeys.Count == 1,
                    "Original 25112/24072 did not shorten the new rock spirit lifespan once.");
                var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                var restored = saved.Units.Single(unit => unit.Id == summon.Id);
                Require(restored.Statuses.Single(status => status.ClassId == 10712).CurDurationRound == 3
                    && restored.Statuses.Single(status => status.ClassId == 24072)
                        .Components.LifeReduction.ReducedStatusKeys.SequenceEqual(reduction.ReducedStatusKeys),
                    "Lifespan reduction or its receipt was lost in the Unity checkpoint.");
                Debug.Log("[Lifespan reduction] Original 25112 -> 24072 shortened 10712 from five to three; checkpoint retained receipt.");
            }
            ObserveSummonLifeStep(checkpoint);
            // This shared probe checks actual routing/presentation. Exact damage, status,
            // summon and passive expectations remain in their original-data rule suites.
            Debug.Log("[First promotion graph] item=" + firstPromotionItemId + " entity=" + skillId
                + " clip=" + clipName + " targets=" + string.Join(",", cast.TargetIds)
                + " hits=" + step.DamageEvents.Count);
        }

        [Serializable] private sealed class FirstPromotionRules { public FirstPromotionProfession[] profession_base; }
        [Serializable] private sealed class FirstPromotionProfession
        { public int Profession; public int PrePro; public int[] Skills; public int[] FightUsedSkills; }
    }
}

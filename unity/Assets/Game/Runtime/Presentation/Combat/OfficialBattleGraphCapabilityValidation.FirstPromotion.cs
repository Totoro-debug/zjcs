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
        private int firstPromotionFacingIndex;
        private float firstPromotionCastClock;
        private int firstPromotionFrameCount;
        private static readonly float[] FirstPromotionFrameTimes = { .1f, .25f, .45f, .7f };

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
            player.X = enemy.X - dx * distance; player.Y = enemy.Y - dy * distance;
            player.Direction = new[] { 2, 0, 1, 3 }[direction];
            foreach (var unit in checkpoint.Units)
            {
                unit.Statuses.Clear(); unit.Skills.Clear(); unit.SkillState.Slots.Clear();
                long hp = unit.Side == player.Side ? 5000000 : 1000000000;
                foreach (var prop in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (prop.Id == catalog.PropertyType("MaxHp")) prop.Value = hp;
                    if (prop.Id == catalog.PropertyType("Attack")) prop.Value = 100;
                    if (prop.Id == catalog.PropertyType("Speed")) prop.Value = unit == player ? 1000 : 1;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = hp;
                unit.Turn.Speed = unit == player ? 1000 : 1;
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
            skill.LimitedTimes = 1;
            player.Skills.Add(skill); player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            checkpoint.MaxRound = 12;
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
                "Original equipped source, directional entity or release count differs.");
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public sealed partial class NativeHitAcceptanceRunner
    {
        private IEnumerator CheckEncouragementImpact(Transform parent, FightBattleGrid grid)
        {
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var catalog = assets.Load();
            var formula = assets.LoadDamageFormula();
            FightBattleUnitState Unit(int id, int side, int x)
            {
                var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 2,
                    Level = 50, SubRank = 5, X = x, Y = 7, Direction = 3,
                    Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
                unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
                foreach (var prop in new Dictionary<int, long> { { 1, 1000 }, { 2, 10 }, { 3, unit.MaxHp }, { 4, unit.Turn.Speed } })
                    unit.Properties.Add(new FightPropertyValue { Id = prop.Key, Value = prop.Value });
                foreach (var name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
                    "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
                    unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
                unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
                return unit;
            }

            for (int direction = 0; direction < 4; direction++)
            {
                var units = Enumerable.Range(0, 6).Select(index => Unit(index + 1, 1, 5 + index)).ToList();
                units[0].Direction = direction;
                var anotherNear = Unit(7, 1, 5); anotherNear.Y = 8; units.Add(anotherNear);
                units.Add(Unit(8, 2, 14));
                foreach (var unit in units.Take(7))
                {
                    unit.Statuses.Add(catalog.CreateStatus(24040, unit.Id, 1, 100000 + unit.Id * 10));
                    unit.Statuses.Add(catalog.CreateStatus(24041, unit.Id, 1, 100001 + unit.Id * 10));
                }
                var skill = catalog.CreateSkill(11550, 0, 10066, 2, 20, 1,
                    catalog.CalculateSkillProps(11550, 2, 20, 1).Select(value =>
                        new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                skill.LimitedTimes = 1; units[0].Skills.Add(skill);
                units[0].SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
                var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                checkpoint.Units.AddRange(units);
                var wave = new FightBattleWaveState(); wave.UnitIds.Add(8); checkpoint.Waves.Add(wave);
                FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, assets.LoadSkillItems());
                var display = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint)).Units.ToArray();
                var root = new GameObject("Isolated encouragement " + direction).transform; root.SetParent(parent, false);
                var bodies = new Dictionary<int, Transform>();
                foreach (var unit in units)
                {
                    var body = new GameObject("Encouragement unit " + unit.Id).transform; body.SetParent(root, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    bodies.Add(unit.Id, body);
                }
                var appearance = NativeJumpAppearanceFixture.Create(bodies[1].gameObject, 2);
                var scope = new OfflineBattleNativePlayableScope(root, null);
                ParticleSystem ring = null; int pauseFrames = 0; bool resumed = false;
                scope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name != "playable11550" || leaf.name != "effect" || !leaf.myApplyComponent) return;
                    ring = leaf.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true).Single(value => value.name == "kuosan_huan");
                    ring.Pause(false);
                };
                var settlements = new Dictionary<int, int>();
                var statuses = new List<(int Target, int Frame, float Age)>();
                var disperses = new Dictionary<int, int>();
                bool captured = false;
                cameraView.transform.position = new Vector3(7, 7, -20);
                using (var views = new OfficialBattleSkillEntityPlayback(root, new OfficialBattleGridMapper(root, grid), scope,
                    id => display.FirstOrDefault(value => value.Id == id)))
                {
                    views.BindDamageUI(root, 0);
                    foreach (var pair in bodies) views.EnsureUnit(pair.Key, pair.Value);
                    Transform Body(int id) => bodies.TryGetValue(id, out var body) ? body : null;
                    using (var driver = new OfficialBattleLiveGraphDriver(checkpoint, copy => FightBattleExecutor.Restore(copy,
                        formula, new FightDamageSettlement(), catalog, assets.LoadSkillItems()), views,
                        id => id == 11550, Body, null, (damage, projected) => {
                            settlements.Add(damage.TargetId, Time.frameCount);
                            display[Array.FindIndex(display, value => value.Id == damage.TargetId)] = projected;
                        }, observeStatusViews: true))
                    {
                        driver.StatusViewChanged += notice => {
                            if (notice.Kind != FightBattleStatusViewEventKind.Start || notice.Status.ClassId != 24237) return;
                            var particles = new ParticleSystem.Particle[ring.main.maxParticles];
                            int count = ring.GetParticles(particles);
                            Require(count > 0 && settlements.TryGetValue(notice.OwnerId, out int frame) && frame == Time.frameCount,
                                "Encouragement status must settle on the original visible ring in its impact frame.");
                            statuses.Add((notice.OwnerId, Time.frameCount, particles[0].startLifetime - particles[0].remainingLifetime));
                        };
                        driver.DispersePresented += damage => {
                            Require(settlements.TryGetValue(damage.TargetId, out int frame) && frame == Time.frameCount,
                                "Encouragement's dispel notice must share its original ring impact frame.");
                            disperses.Add(damage.TargetId, Time.frameCount);
                        };
                        display = checkpoint.Units.Select(value => driver.DisplayUnit(value.Id)).ToArray();
                        float deadline = Time.realtimeSinceStartup + 15f;
                        while (!driver.IsCompleted && Time.realtimeSinceStartup < deadline)
                        {
                            driver.Pump(Time.deltaTime); views.Tick(Time.deltaTime); scope.LateTick();
                            if (ring && !resumed)
                            {
                                Require(statuses.Count == 0 && settlements.Count == 0 && disperses.Count == 0,
                                    "A paused encouragement ring cannot settle status or dispel.");
                                if (++pauseFrames == 8) { ring.Play(false); resumed = true; }
                            }
                            if (direction == 0 && !captured && statuses.Count > 0)
                            { Capture("encouragement-native-impact.png", false); captured = true; }
                            yield return null;
                        }
                        Require(driver.IsCompleted && resumed && statuses.Count == 7 && settlements.Count == 7 && disperses.Count == 7,
                            "The original encouragement ring must reach all seven selected recipients.");
                        for (int id = 1; id <= 6; id++)
                        {
                            var current = statuses.Single(value => value.Target == id);
                            Require(current.Age > 0f && (id == 1 || current.Frame > statuses.Single(value => value.Target == id - 1).Frame),
                                "Encouragement recipients must follow the original expanding ring.");
                        }
                        Require(statuses.Single(value => value.Target == 7).Frame == statuses.Single(value => value.Target == 2).Frame,
                            "Recipients reached by the same ring must settle in the same frame.");
                        Require(driver.TryTakeCompleted(out var completed, out _) && units.Take(7).All(unit => {
                            var saved = completed.State.Units.Single(value => value.Id == unit.Id);
                            return saved.Statuses.Any(status => status.ClassId == 24237)
                                && saved.Statuses.Count(status => status.ClassId == 24040 || status.ClassId == 24041) == 1;
                        }), "The final checkpoint must contain all seven status applications and one dispel each.");
                    }
                }
                scope.Dispose(); appearance.Dispose(); Destroy(root.gameObject);
                cameraView.transform.position = new Vector3(5, 5, -20);
            }

            // The expanding-ring observer is also used by the original fire ring.
            // Verify its existing two damage waves against real particles after reuse.
            {
                var units = new[] { Unit(1, 1, 5), Unit(2, 2, 6), Unit(3, 2, 7) };
                units[0].Profession = 10;
                var skill = catalog.CreateSkill(10830, 0, 10031, 2, 20, 1,
                    catalog.CalculateSkillProps(10830, 2, 20, 1).Select(value =>
                        new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                skill.LimitedTimes = 1; units[0].Skills.Add(skill);
                units[0].SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
                var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                checkpoint.Units.AddRange(units);
                var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3 }); checkpoint.Waves.Add(wave);
                FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, assets.LoadSkillItems());
                var display = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint)).Units.ToArray();
                var root = new GameObject("Isolated fire ring regression").transform; root.SetParent(parent, false);
                var bodies = new Dictionary<int, Transform>();
                foreach (var unit in units)
                {
                    var body = new GameObject("Fire ring unit " + unit.Id).transform; body.SetParent(root, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    bodies.Add(unit.Id, body);
                }
                var appearance = NativeJumpAppearanceFixture.Create(bodies[1].gameObject, 10);
                var scope = new OfflineBattleNativePlayableScope(root, null);
                ParticleSystem ring = null;
                scope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name == "playable10830" && leaf.name == "effect" && leaf.myApplyComponent)
                        ring = leaf.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true).Single(value => value.name == "difeng (3)");
                };
                var damage = new List<(int Target, int Frame)>();
                using (var views = new OfficialBattleSkillEntityPlayback(root, new OfficialBattleGridMapper(root, grid), scope,
                    id => display.FirstOrDefault(value => value.Id == id)))
                {
                    views.BindDamageUI(root, 0);
                    foreach (var pair in bodies) views.EnsureUnit(pair.Key, pair.Value);
                    Transform Body(int id) => bodies.TryGetValue(id, out var body) ? body : null;
                    using (var driver = new OfficialBattleLiveGraphDriver(checkpoint, copy => FightBattleExecutor.Restore(copy,
                        formula, new FightDamageSettlement(), catalog, assets.LoadSkillItems()), views,
                        id => id == 10830, Body, null, (hit, projected) => {
                            Require(ring && ring.particleCount > 0, "Fire ring damage must wait for its visible particle.");
                            damage.Add((hit.TargetId, Time.frameCount));
                            display[Array.FindIndex(display, value => value.Id == hit.TargetId)] = projected;
                        }))
                    {
                        display = checkpoint.Units.Select(value => driver.DisplayUnit(value.Id)).ToArray();
                        float deadline = Time.realtimeSinceStartup + 10f;
                        while (!driver.IsCompleted && Time.realtimeSinceStartup < deadline)
                        { driver.Pump(Time.deltaTime); views.Tick(Time.deltaTime); scope.LateTick(); yield return null; }
                        Require(driver.IsCompleted && damage.Count == 2 && damage[0].Target == 2 && damage[1].Target == 3
                            && damage[1].Frame > damage[0].Frame, "The original fire ring must retain both expanding damage waves.");
                    }
                }
                scope.Dispose(); appearance.Dispose(); Destroy(root.gameObject);
            }
        }
    }
}

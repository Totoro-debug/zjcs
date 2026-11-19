using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public sealed partial class NativeHitAcceptanceRunner
    {
        // Exercises the official actor, native particles, live local rules and ground
        // status together. The delayed status case changes a test instance only.
        private IEnumerator CheckFlameSpray(Transform root, FightBattleGrid grid)
        {
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var catalog = assets.Load();
            var formula = assets.LoadDamageFormula();
            FightBattleUnitState UnitForFlame(int id, int side)
            {
                var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 1,
                    Level = 50, SubRank = 5, X = 7, Y = 7, Direction = 3, Hp = 1000000, MaxHp = 1000000,
                    LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
                unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
                foreach (var prop in new Dictionary<int, long> { { 1, 1000 }, { 2, 10 }, { 3, unit.MaxHp }, { 4, unit.Turn.Speed } })
                    unit.Properties.Add(new FightPropertyValue { Id = prop.Key, Value = prop.Value });
                foreach (var name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
                    "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
                    unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
                unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
                return unit;
            }

            int previousRate = Time.captureFramerate;
            float previousCameraSize = cameraView.orthographicSize;
            cameraView.orthographicSize = 8f;
            try
            {
                // The official ground leaf starts immediately. The final scenario
                // additionally exercises a deferred leaf at normal frame rate.
                foreach (var scenario in new[] { (Rate: 5, DelayGround: false),
                    (Rate: 60, DelayGround: false), (Rate: 60, DelayGround: true) })
                foreach (uint seed in new uint[] { 1, 17, 913, 43123 })
                foreach (var direction in new[] { new[] { 1, 0, 3 }, new[] { -1, 0, 1 }, new[] { 0, 1, 2 }, new[] { 0, -1, 0 } })
                {
                    int rate = scenario.Rate; bool delayGround = scenario.DelayGround;
                    Time.captureFramerate = rate;
                    var units = new[] { UnitForFlame(1, 1), UnitForFlame(2, 2), UnitForFlame(3, 2), UnitForFlame(4, 2) };
                    units[0].Profession = 10; units[0].Direction = direction[2];
                    for (int index = 1; index < units.Length; index++)
                    { units[index].X += direction[0] * index; units[index].Y += direction[1] * index; }
                    var skill = catalog.CreateSkill(11690, 0, 10072, 2, 20, 1,
                        catalog.CalculateSkillProps(11690, 2, 20, 1).Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                    skill.LimitedTimes = 1; units[0].Skills.Add(skill);
                    units[0].SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
                    var passive = catalog.CreateStatus(25045, 1, 1, 900002, 0, 10576, 2, 20, 1);
                    passive.Components.HitScope.Rate = 1f; units[0].Statuses.Add(passive);
                    var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                        Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 0 } };
                    checkpoint.Units.AddRange(units); var wave = new FightBattleWaveState();
                    wave.UnitIds.AddRange(new[] { 2, 3, 4 }); checkpoint.Waves.Add(wave);
                    FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, assets.LoadSkillItems());
                    var display = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint)).Units.ToArray();
                    var scene = new GameObject("Flame native acceptance").transform; scene.SetParent(root, false);
                    var bodies = new Dictionary<int, Transform>();
                    foreach (var unit in units)
                    {
                        var body = new GameObject("Flame unit " + unit.Id).transform; body.SetParent(scene, false);
                        body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                        var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false;
                        renderer.sharedMaterials = Array.Empty<Material>();
                        body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                        bodies.Add(unit.Id, body);
                    }
                    var targetAppearance = NativeJumpAppearanceFixture.Create(bodies[2].gameObject);
                    var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig");
                    var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
                    var appearance = new ProfessionAppearanceResolver(tables).BuildDefaultAppearanceData(config, Sex.Male, (Profession)10);
                    var profile = new OfflinePlayerProgress { InitialWeaponType = 201 };
                    profile.Wardrobe.Sex = (int)Sex.Male; profile.Wardrobe.Profession = 10;
                    foreach (var pair in appearance.SkinMap)
                        profile.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
                    foreach (var pair in appearance.DyeMap)
                        profile.Wardrobe.Dyes.Add(new OfflineAppearanceDye { Type = (int)pair.Key, ColorId = pair.Value.ColorId,
                            Hue = pair.Value.Hsv.H, Saturation = pair.Value.Hsv.S, Value = pair.Value.Hsv.V });
                    var resources = new OfflineProfessionBattlePlayerResources();
                    var player = resources.Create(scene, 17, () => profile, _ => profile.InitialWeaponType);
                    Destroy(bodies[1].gameObject); bodies[1] = player.Actor.Root.transform;
                    bodies[1].localPosition = new Vector3(units[0].X, units[0].Y, 0);
                    bodies[1].gameObject.AddComponent<MyTransform>();
                    Require(player.Actor.Attachments.Agent && player.Actor.Attachments.Agent.myBoneNameList.Contains("shouR_1"),
                        "The original player hand is missing.");
                    var native = new OfflineBattleNativePlayableScope(scene, null);
                    var burnStarts = new Dictionary<GameObject, int>();
                    var arrivals = new List<int>();
                    ParticleSystem fire = null; bool resumed = false; int pausedFrames = 0;
                    native.NativePlayableStarted += (prefab, leaf) => {
                        if (prefab.name == "playable11581" && leaf.name == "effect" && leaf.mySource)
                            burnStarts[leaf.mySource] = Time.frameCount;
                        if (!prefab.name.StartsWith("playable11690_") || leaf.name != "effect (2)" || !leaf.myApplyComponent) return;
                        fire = leaf.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true).Single(value => value.name == "firean");
                        fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        fire.useAutoRandomSeed = false; fire.randomSeed = seed; fire.Play(true); Time.timeScale = 0f;
                    };
                    cameraView.transform.position = new Vector3(7, 7, -20);
                    using (var flameViews = new OfficialBattleSkillEntityPlayback(scene, new OfficialBattleGridMapper(scene, grid),
                        native, id => display.FirstOrDefault(value => value.Id == id)))
                    using (var graph = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), native, flameViews))
                    {
                        // Delay the original ground leaf only in this acceptance instance.
                        if (delayGround)
                        {
                            var field = typeof(OfflineBattleNativePlayableScope).GetField("sourcePrefabAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
                            var adapter = (Action<GameObject, GameObject>)field.GetValue(native);
                            adapter += (prefab, instance) => {
                                if (prefab.name != "playable11581") return;
                                foreach (var entry in instance.GetComponent<MyPlayableSequence>().myPlayables)
                                    if (entry.myPlayable && entry.myPlayable.name == "effect") entry.myTime = .05f;
                            };
                            field.SetValue(native, adapter);
                        }
                        flameViews.BindDamageUI(scene, 0);
                        foreach (var pair in bodies) flameViews.EnsureUnit(pair.Key, pair.Value);
                        Transform Body(int id) => bodies.TryGetValue(id, out var body) ? body : null;
                        void Damage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                        {
                            if (damage.SkillEntityId == 11590)
                            {
                                var ground = graph.TryGetGridTransform(damage.SourceId);
                                Require(ground && damage.HitEntityId == 11591 && burnStarts.TryGetValue(ground.gameObject, out int started)
                                    && started == Time.frameCount, "Burning damage missed the actual ground-effect frame.");
                            }
                            else
                            {
                                Require(resumed && fire && damage.SkillEntityId == 11690 && damage.HitEntityId == 11691
                                    && damage.DamageEntityId == 11692 && damage.TargetId >= 2 && damage.TargetId <= 4
                                    && FlameCoversTarget(fire, cameraView, Body(damage.TargetId).position),
                                    "Flame damage missed the rendered target.");
                                arrivals.Add(damage.TargetId);
                            }
                            display[Array.FindIndex(display, value => value.Id == damage.TargetId)] = projected;
                        }
                        graph.BindGridEntities(scene, new OfficialBattleGridMapper(scene, checkpoint.Grid), checkpoint, Body, _ => false);
                        using var driver = graph.CreateLiveStep(checkpoint,
                            copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, assets.LoadSkillItems()),
                            Body, null, Damage, observeGridViews: true);
                        display = checkpoint.Units.Select(value => driver.DisplayUnit(value.Id)).ToArray();
                        float start = Time.time;
                        while ((!driver.IsCompleted || !resumed) && Time.time - start < 6f)
                        {
                            resources.Update(); driver.Pump(Time.deltaTime); graph.Tick(Time.deltaTime); graph.LateTick();
                            if (fire && !resumed && ++pausedFrames >= 8)
                            {
                                Require(arrivals.Count == 0 && Time.timeScale == 0f, "A paused flame caused damage.");
                                Time.timeScale = 1f; resumed = true;
                            }
                            yield return null;
                        }
                        bool completed = driver.TryTakeCompleted(out var executor, out var result);
                        Require(completed && resumed && arrivals.Count == 9
                            && arrivals.GroupBy(value => value).All(group => group.Count() == 3),
                            "Flame waves did not complete for " + rate + "/" + delayGround + "/" + seed + "/" + direction[2]
                            + ": completed=" + completed + ", targets=" + string.Join(",", arrivals));
                        typeof(OfficialBattleSkillGraphPlayback).GetMethod("AdoptExecutor", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(graph, new object[] { executor });
                        Require(result.Event.SkillCasts.Count(value => value.SkillEntityId == 11580) == 9
                            && result.Event.GridViewEvents.Count(value => value.Kind == FightBattleGridViewEventKind.Create) == 9,
                            "Flame hits did not create every original burning-ground child.");
                        var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(result.Checkpoint));
                        Require(restored.GridItems.Count(value => !value.Ended && !value.Runtime.Ended) == 3
                            && restored.GridItems.Where(value => !value.Ended && !value.Runtime.Ended).All(value => graph.TryGetGridTransform(value.Id)),
                            "The final ground statuses did not survive checkpoint adoption and serialization.");
                        if (rate == 60 && !delayGround && seed == 1 && direction[2] == 3)
                        {
                            var restoredRoot = new GameObject("Restored flame ground").transform; restoredRoot.SetParent(root, false);
                            var restoredBodies = new Dictionary<int, Transform>();
                            foreach (var unit in restored.Units)
                            {
                                var body = new GameObject("Restored unit " + unit.Id).transform; body.SetParent(restoredRoot, false);
                                body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                                var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false;
                                renderer.sharedMaterials = Array.Empty<Material>();
                                body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                                restoredBodies.Add(unit.Id, body);
                            }
                            Transform RestoredBody(int id) => restoredBodies.TryGetValue(id, out var body) ? body : null;
                            scene.gameObject.SetActive(false);
                            using (var restoredScope = new OfflineBattleNativePlayableScope(restoredRoot, null))
                            using (var restoredViews = new OfficialBattleSkillEntityPlayback(restoredRoot,
                                new OfficialBattleGridMapper(restoredRoot, restored.Grid), restoredScope,
                                id => restored.Units.FirstOrDefault(value => value.Id == id)))
                            using (var restoredGrid = new OfficialBattleGridEntityPlayback(restoredRoot,
                                new OfficialBattleGridMapper(restoredRoot, restored.Grid), restoredScope, restoredViews,
                                restored, RestoredBody, _ => false))
                            {
                                restoredGrid.Restore();
                                Require(restoredGrid.Count == 3 && restored.GridItems.Where(value => !value.Ended && !value.Runtime.Ended)
                                    .All(value => restoredGrid.TryGetTransform(value.Id)), "Native ground views were not restored.");
                                yield return null; restoredScope.LateTick(); Capture("flame-spray-ground.png", false);
                            }
                            Destroy(restoredRoot.gameObject); scene.gameObject.SetActive(true);
                        }
                    }
                    native.Dispose(); player.Dispose(); resources.Dispose(); targetAppearance.Dispose(); Destroy(scene.gameObject);
                }
            }
            finally { Time.captureFramerate = previousRate; Time.timeScale = 1f; cameraView.orthographicSize = previousCameraSize; }
        }

        private static bool FlameCoversTarget(ParticleSystem fire, Camera camera, Vector3 target)
        {
            var renderer = fire.GetComponent<ParticleSystemRenderer>();
            if (!renderer || !camera || fire.particleCount == 0) return false;
            var mesh = new Mesh();
            try
            {
                renderer.BakeMesh(mesh, camera, true);
                var vertices = mesh.vertices; var triangles = mesh.triangles; var point = camera.WorldToScreenPoint(target);
                float Cross(Vector3 a, Vector3 b, Vector3 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                for (int index = 0; index + 2 < triangles.Length; index += 3)
                {
                    var a = camera.WorldToScreenPoint(vertices[triangles[index]]);
                    var b = camera.WorldToScreenPoint(vertices[triangles[index + 1]]);
                    var c = camera.WorldToScreenPoint(vertices[triangles[index + 2]]);
                    if (point.z <= 0 || a.z <= 0 || b.z <= 0 || c.z <= 0 || Mathf.Abs(Cross(a, b, c)) < .00001f) continue;
                    float ab = Cross(a, b, point), bc = Cross(b, c, point), ca = Cross(c, a, point);
                    if ((ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0)) return true;
                }
                return false;
            }
            finally { Destroy(mesh); }
        }
    }
}

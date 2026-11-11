using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.BattleMaterialPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleDamageReceiptAcceptance
    {
        public static void Begin(GameObject host) => host.AddComponent<NativeDamageReceiptAcceptanceRunner>();
    }

    public sealed class NativeDamageReceiptAcceptanceRunner : MonoBehaviour
    {
        private sealed class Observed
        {
            public MyEntity Entity, Skill;
            public EcEntityIdentifier SkillId, Identity;
            public MyFightDamage Damage;
        }
        private OfficialBattleSkillGraphPlayback playback;
        private OfflineBattleNativePlayableScope scope;
        private IDisposable pipeline;
        private string failure;
        private void LateUpdate() { scope?.LateTick(); }
        private IEnumerator Start()
        {
            Application.logMessageReceived += Observe;
            var run = Check();
            while (true)
            {
                object current;
                try { if (!run.MoveNext()) break; current = run.Current; }
                catch (Exception error) { failure = error.ToString(); break; }
                yield return current;
            }
            try { playback?.Dispose(); pipeline?.Dispose(); }
            catch (Exception error) { failure = failure ?? error.ToString(); }
            playback = null; scope = null;
            Application.logMessageReceived -= Observe;
            OfficialBattleDamagePlaybackAcceptance.Finish(failure);
        }

        private IEnumerator Check()
        {
            AudioListener.volume = 0f; Application.runInBackground = true;
            pipeline = Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets").Enter();
            var camera = new GameObject("Receipt damage camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 4;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .09f, .15f);
            foreach (string name in new[] { "position", "replay", "legacy", "body", "none", "cure" })
            {
                var checkpoint = Build(name, out var step);
                var expected = step.DamageEvents.Where(d => !d.IsSkillCost).OrderBy(d => d.Delay).ThenBy(d => d.DamageId).ToArray();
                Require(expected.Length > 0 && expected.All(d => d.HasDamagePosition && d.HasDamageTriggerStatus), "Actual settled occurrence metadata.");
                if (name == "replay") Require(step.SkillCasts.Count == 3, "Actual same-class replay fixture.");
                if (name == "body") Require(expected.Any(d => d.HasHitGridPosition)
                    && expected.All(d => !d.HasHitGridPosition || d.DamageX == d.HitGridX && d.DamageY == d.HitGridY),
                    "Original random hit body cells survive the settled damage receipt.");
                if (name == "legacy") foreach (var damage in expected) damage.HasDamagePosition = false;
                var root = new GameObject("Receipt flow " + name).transform;
                var mapper = new OfficialBattleGridMapper(root, checkpoint.Grid);
                var transforms = new Dictionary<int, Transform>();
                foreach (var unit in checkpoint.Units)
                {
                    var actor = new GameObject("Receipt actor " + unit.Id).transform; actor.SetParent(root, false);
                    actor.position = mapper.GridToWorld(new FightGridPoint(unit.X, unit.Y));
                    actor.gameObject.AddComponent<MyTransform>();
                    var renderer = actor.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false;
                    renderer.sharedMaterials = Array.Empty<Material>();
                    actor.gameObject.AddComponent<MeshMaterialProperty>().MeshRenderer = renderer;
                    transforms.Add(unit.Id, actor);
                }
                var point = mapper.GridToWorld(new FightGridPoint(expected[0].DamageX, expected[0].DamageY));
                camera.transform.position = point + new Vector3(0, 0, -20);
                // Later state must not replace the original point-hit position.
                if (name == "position") { checkpoint.Units[1].X += 4; checkpoint.Units[1].Y += 3; }
                scope = new OfflineBattleNativePlayableScope(root, null);
                var views = new OfficialBattleSkillEntityPlayback(root, mapper, scope, id => checkpoint.Units.FirstOrDefault(u => u.Id == id));
                playback = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), scope, views);
                var observed = new List<Observed>(); int hitNotifications = 0;
                foreach (var unit in checkpoint.Units)
                {
                    var entity = views.EnsureUnit(unit.Id, transforms[unit.Id]);
                    entity.GetComponent<MyFightRoleFight>().OnDamage += (role, damage) => observed.Add(new Observed {
                        Entity = damage.myEntity, Damage = damage, SkillId = damage.Component.SkillId,
                        Identity = damage.myEntity.Id,
                        Skill = views.GetEntity(damage.Component.SkillId) });
                    entity.GetComponent<MyFightRoleFight>().OnHit += (role, damage) => hitNotifications++;
                }
                playback.Play(step, checkpoint, id => transforms.TryGetValue(id, out var value) ? value : null);
                var boundaries = new SortedSet<float> { 0f };
                foreach (var cast in step.SkillCasts) { boundaries.Add(cast.Delay); boundaries.Add(cast.Delay + cast.Presentation.Duration); }
                foreach (var receipt in expected)
                { boundaries.Add(receipt.Delay); boundaries.Add(receipt.Delay + Lifetime(receipt)); }
                if (name != "body") boundaries.Add(expected[0].Delay + .125f);
                float clock = 0f; bool captured = false;
                foreach (float boundary in boundaries)
                {
                    float interval = boundary - clock;
                    if (interval > .0002f)
                    {
                        playback.Tick(interval - .0001f);
                        Require(observed.Count == (name == "legacy" ? 0 : expected.Count(d => d.Delay < boundary)),
                            "A damage view was created before its exact settled time.");
                        // float Tick additions can stop a few ULPs before the
                        // mathematical boundary. Check just after it without
                        // changing production scheduling or the original delay.
                        playback.Tick(.000102f);
                    }
                    else playback.Tick(Mathf.Max(0f, boundary + .000002f - clock));
                    clock = Mathf.Max(clock, boundary + .000002f);
                    var due = name == "legacy" ? new FightBattleDamageEvent[0] : expected.Where(d => d.Delay <= boundary + .00001f).ToArray();
                    Require(observed.Count == due.Length && hitNotifications == due.Length,
                        $"{name} at {boundary:R}: target={observed.Count}, source={hitNotifications}, expected={due.Length}; one original notification per actual damage.");
                    int active = root.GetComponentsInChildren<MyFightDamage>().Count();
                    Require(active == due.Count(d => d.Delay + Lifetime(d) > boundary + .00001f), "Original configured damage lifetime and pool removal.");
                    for (int index = 0; index < due.Length; index++)
                    {
                        var value = observed[index]; var receipt = due[index];
                        Require(value.Skill && value.Skill.ClassId == receipt.SkillEntityId && value.SkillId.EntityId > 0,
                            "Damage creation resolved the exact live skill entity.");
                        if (!value.Entity || !value.Entity.gameObject.activeSelf || !value.Entity.Id.Equals(value.Identity))
                        {
                            Require(views.GetEntity(value.Identity) == null, "A stale damage identity must not alias its pooled replacement.");
                            continue;
                        }
                        var result = value.Damage.Component.DamageResult;
                        var expectedType = name == "none" ? DamageResultType.None
                            : name == "cure" ? DamageResultType.Cure : DamageResultType.Damage;
                        Require(result.ResultType == expectedType && result.IsCure == (name == "cure")
                            && result.IsDamage == (name != "none" && name != "cure"),
                            "Actual damage/full-health cure and status-only result projection.");
                        Require(result.Pos.X == receipt.DamageX && result.Pos.Y == receipt.DamageY
                            && result.DamageValue == receipt.DamageValue && result.ShieldValue == receipt.ShieldDamage
                            && result.AbsoluteDamage == receipt.AbsoluteDamage,
                            "Frozen body/point position and full numeric result reach the original view.");
                        var cast = step.SkillCasts.Single(c => c.DamageEventIds.Contains(receipt.DamageId));
                        var direction = new[] { Direction.Down, Direction.Left, Direction.Up, Direction.Right }[cast.Presentation.FacingIndex];
                        Require(Quaternion.Angle(value.Entity.transform.localRotation, direction.DirToRotation()) < .001f
                            && (value.Entity.transform.localScale.x < 0f) == cast.Presentation.FlipX,
                            "Original damage transform uses its owning cast's frozen direction and flip.");
                    }
                    if (!captured && name != "legacy" && name != "body" && boundary >= expected[0].Delay + .125f)
                    { Capture(camera, "damage-receipt-" + name + ".png"); captured = true; }
                    yield return null;
                }
                if (name == "replay") Require(observed.Select(value => value.SkillId).Distinct().Count() == 3,
                    "Three actual same-class casts retain three independent damage skill identities.");
                Require(root.GetComponentsInChildren<MyFightDamage>().Length == 0, "No damage entity survived its final removal boundary.");
                playback.Dispose(); playback = null; scope = null;
                Destroy(root.gameObject); yield return null;
                Require(!root, "Complete original graph/pool release.");
            }
            Debug.Log("Formal damage receipts passed: exact cast-relative creation, original duration removal, actual same-class replay ownership, frozen point/body positions, full results, original notifications, legacy unknown results and complete release.");
        }

        private static FightBattleCheckpoint Build(string name, out FightBattleStepEvent step)
        {
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var checkpoint = new FightBattleCheckpoint { StageId = 1, RandomSeed = 11,
                Grid = new FightBattleGrid { Width = 16, Height = 16 } };
            for (int id = 1; id <= 3; id++)
            {
                var unit = new FightBattleUnitState { Id = id, Side = id == 1 ? 1 : 2,
                    CharacterType = id == 1 ? 1 : 2, X = id == 1 ? 4 : id == 2 ? 7 : 10, Y = 5,
                    Hp = 1000000, MaxHp = 1000000, Level = 150, SubRank = 1, Direction = 3, HasFlipX = true, MoveMask = 5,
                    Turn = new FightTurnUnit { Id = id, Hp = 1000000, Speed = 100 - id } };
                foreach (var pair in new[] { new[] { 1, 10001 }, new[] { 2, 10003 }, new[] { 3, 1000000 }, new[] { 4, 100 - id } })
                    unit.Properties.Add(new FightPropertyValue { Id = pair[0], Value = pair[1] });
                checkpoint.Units.Add(unit);
            }
            FightBattleSkillDefinition skill;
            if (name == "body")
            {
                skill = assets.Load().CreateSkill(10910, 0, 0, 5, 20, 1, null);
                checkpoint.Units[1].BodyOffsets.AddRange(new[] { new FightGridPoint(1, 0), new FightGridPoint(0, 1), new FightGridPoint(1, 1) });
            }
            else
            {
                skill = new FightBattleSkillDefinition { EntityId = 10100, Slot = 0, TargetType = 1, Duration = .5f, ActionDuration = .5f };
                skill.Range.Add(new FightGridPoint(3, 0));
                var hit = new FightBattleHitDefinition { EntityId = 10101, DamageId = 13012, Delay = .25f,
                    DamageType = 8, HasCustomDamage = true, CustomDamage = 5, TargetMode = 0 };
                if (name == "none") hit.ActionType = 0;
                if (name == "cure")
                {
                    hit.ActionType = 5; skill.HasCustomCure = true; skill.CustomCure = 10;
                    foreach (var unit in checkpoint.Units)
                        foreach (string prop in new[] { "BaseCureAdd", "BaseBeCureAdd" })
                            unit.Properties.Add(new FightPropertyValue { Id = assets.Load().PropertyType(prop), Value = 10000 });
                }
                hit.AppliedStatuses.Add(new FightBattleStatusApplication { StatusId = 24031, TargetType = 0,
                    Chance = 1, AffectedByProp = false, LessThanHpPer = -1f }); skill.Hits.Add(hit);
                if (name == "replay")
                {
                    skill.HasReplay = true; skill.ReplayStatusId = 24031; skill.ReplayStackCount = 10;
                    skill.ReplayRelation = 1; skill.ReplayConsumeStack = true; skill.ReplayConsumeCount = 10;
                    var status = assets.Load().CreateStatus(24031, 1, 1); status.StackCount = 20;
                    checkpoint.Units[0].Statuses.Add(status);
                }
            }
            checkpoint.Units[0].Skills.Add(skill);
            checkpoint.Units[0].SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3 }); checkpoint.Waves.Add(wave);
            step = assets.BeginBattle(checkpoint).Step(0, 1).Event;
            if (name == "none")
            {
                var receipt = step.DamageEvents.Single(d => !d.IsSkillCost);
                Require(!receipt.HasAbsoluteDamage && !receipt.HasDamageValue && !receipt.IsCure
                    && receipt.IsDamageTriggerStatus && receipt.BeforeHp == receipt.AfterHp,
                    "Real status-only occurrence has no damage/cure settlement.");
                Require(checkpoint.Statistics.Unit(1).BuffApplications == 1
                    && checkpoint.Statistics.Skill(1, skill.EntityId, 0).DamageHits == 0
                    && checkpoint.Statistics.Skill(1, skill.EntityId, 0).CureHits == 0,
                    "Accepted status application is counted once, without a damage/cure hit.");
            }
            foreach (var cast in step.SkillCasts) cast.Delay += .125f;
            if (name == "position")
            { step.SkillCasts[0].Presentation.FacingIndex = 0; step.SkillCasts[0].Presentation.FlipX = true; }
            foreach (var receipt in step.DamageEvents) receipt.Delay += .125f;
            return checkpoint;
        }

        private static float Lifetime(FightBattleDamageEvent receipt)
        {
            Require(OfficialCombatAssetCatalog.LoadDefault().Load().TryGetComponent(receipt.DamageEntityId, "FightDamageComponent", out var config), "Original duration config.");
            return config.TryGetValue("Duration", out var value) ? Convert.ToSingle(value) : .5f;
        }
        private void Observe(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failure = failure ?? message + "\n" + stack; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Capture(Camera camera, string name)
        {
            var target = new RenderTexture(540, 960, 24); var previous = RenderTexture.active; var old = camera.targetTexture;
            Texture2D pixels = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(540, 960, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, 540, 960), 0, 0); pixels.Apply();
                Require(pixels.GetPixels32().Select(p => ((int)p.r << 16) | ((int)p.g << 8) | p.b).Distinct().Take(5).Count() >= 5,
                    "Recorded status effect has no visible output.");
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = old; RenderTexture.active = previous; if (pixels) Destroy(pixels); Destroy(target); }
        }
    }
}

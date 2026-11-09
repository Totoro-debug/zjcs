using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialEmbeddedMecanimProbe
    {
        private OfficialBattleSkillGraphPlayback hitPlayback;

        private sealed class HitGrid : FightHitGridTransformData
        {
            public Pos2d Position;
            public OfficialBattleSkillEntityPlayback Views;
            public FightBattleHitScopeTransform Center;
            public override Pos2d Pos => Position;
            public override Direction Direction => Zjcs.Official.World.NativeTerrain.Common.Direction.Right;
            public override bool FlipX => false;
            public override ECGridTransformInfo Info => null;
            public override Vector3 GridToPosition(Pos2d value) => Views.GridToPos(value);
            public override Pos2d PositionToGrid(Vector3 value) => new Pos2d((int)value.x, (int)value.y);
            public override void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver value) { }
            public override void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver value) { }
            public override Pos2d LocalToWorldPos(Pos2d local)
            {
                var position = FightGridTransformMath.CalcLocalToWorldPos(
                    new FightGridPoint(local.X, local.Y), new FightGridPoint(Pos.X, Pos.Y),
                    Center.RealCenterX, Center.RealCenterY, (int)Direction, FlipX);
                return new Pos2d(position.X, position.Y);
            }
        }

        private IEnumerator CheckHitEntity()
        {
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            assets.LoadDamageFormula();
            var combat = assets.Load();
            var skill = combat.CreateSkill(57780, -1, 14657, 1, 1, 0, null);
            Require(skill.HitGraphEntities.All(value => value.EntityId != 57784),
                "Review a newly configured parent before changing this unused-hit diagnostic.");
            // Fixture setup reads the canonical parser without adding this orphan
            // to a skill or introducing a production trigger for unused content.
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var components = typeof(FightOfficialCombatCatalog).GetMethod("RequiredEntity", hidden)
                .Invoke(combat, new object[] { 57784 });
            var create = typeof(FightOfficialCombatCatalog).GetMethod("CreateHitEntity", hidden);
            var parameters = create.GetParameters();
            var definition = (FightBattleHitEntity)create.Invoke(combat, new[] {
                (object)57784, components, Activator.CreateInstance(parameters[2].ParameterType),
                Activator.CreateInstance(parameters[3].ParameterType) });
            var root = new GameObject("Original unused hit lifecycle diagnostic").transform;
            root.SetParent(transform, false);
            var checkpoint = new FightBattleCheckpoint();
            var actor = new FightBattleUnitState { Id = 1, CharacterType = 1,
                X = 2, Y = 5, Hp = 100, MaxHp = 100, Direction = 3, HasFlipX = true };
            var victim = new FightBattleUnitState { Id = 2, CharacterType = 1,
                X = 6, Y = 5, Hp = 100, MaxHp = 100, Direction = 1, HasFlipX = true };
            checkpoint.Units.Add(actor); checkpoint.Units.Add(victim);
            var source = new GameObject("Original hit caster").transform;
            var targetUnit = new GameObject("Original hit victim").transform;
            source.SetParent(root, false); targetUnit.SetParent(root, false);
            source.localPosition = new Vector3(actor.X, actor.Y, 0);
            targetUnit.localPosition = new Vector3(victim.X, victim.Y, 0);
            source.gameObject.AddComponent<MyTransform>(); targetUnit.gameObject.AddComponent<MyTransform>();
            var mapper = new OfficialBattleGridMapper(root, new FightBattleGrid { Width = 16, Height = 16 });
            scope = new OfflineBattleNativePlayableScope(root, null);
            var entities = new OfficialBattleSkillEntityPlayback(root, mapper, scope,
                id => checkpoint.Units.FirstOrDefault(value => value.Id == id));
            hitPlayback = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), scope, entities);
            var cast = new FightBattleSkillCastEvent { ActorId = 1, Slot = -1, SkillEntityId = 57780,
                TargetIds = new List<int> { 2 }, Presentation = FightBattleSkillCastPresentation.Capture(actor, skill,
                    new[] { victim }) };
            cast.Presentation.Duration = skill.Duration;
            var step = new FightBattleStepEvent { ActorId = 1 }; step.SkillCasts.Add(cast);
            hitPlayback.Play(step, checkpoint, id => id == 1 ? source : targetUnit);
            var actualSkill = root.GetComponentsInChildren<MyFightSkill>(true)
                .Single(value => value.myEntity.ClassId == 57780).myEntity;
            view.transform.position = new Vector3(5, 5, -20); view.orthographicSize = 6;
            MyEntity first = null;
            for (int replay = 0; replay < 2; replay++)
            {
                var identity = new EcEntityIdentifier(57784, replay + 1);
                var grid = new HitGrid { Position = new Pos2d(6 + replay, 5), Views = entities,
                    Center = definition.ScopeTransform };
                var data = new OfficialBattleHitEntityData(identity, actualSkill.Id, definition, grid, new Pos2d(2, 5));
                var hit = entities.CreateHit(data);
                Require(hit.ClassId == 57784 && entities.GetEntity(identity) == hit,
                    "The original unused hit did not enter the production entity registry.");
                if (replay == 0) first = hit;
                else Require(ReferenceEquals(first, hit), "Original FIFO hit pooling did not reuse the entity.");
                for (int frame = 0; frame < 36; frame++)
                {
                    hitPlayback.Tick(1f / 60); hitPlayback.LateTick();
                    yield return null;
                }
                var embedded = root.GetComponentsInChildren<BattleNativeMonsterRenderer>(true);
                Require(embedded.Length > 0 && embedded.All(IsDormantAsAuthored),
                    "The actual hit graph activated its authored dormant bone track.");
                Require(embedded.All(native => native.GetComponents<MonoBehaviour>().All(value => !value
                    || value.GetType().FullName != "Spine.Unity.SkeletonMecanim")),
                    "The actual hit graph retained an obsolete bone renderer.");
                view.Render();
                var previous = RenderTexture.active; RenderTexture.active = target;
                var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(output, "embedded-hit-" + replay + ".png"), pixels.EncodeToPNG());
                Destroy(pixels);
                entities.RemoveHit(identity);
                Require(entities.GetEntity(identity) == null && !hit.gameObject.activeSelf,
                    "The original unused hit did not leave its registry and return to the pool.");
                yield return null;
            }
            hitPlayback.Dispose(); hitPlayback = null;
            scope.Dispose(); scope = null;
            Destroy(root.gameObject);
            yield return null;
            Require(!root, "The unused-hit diagnostic retained its original graph and pool hierarchy.");
            Debug.Log("[Embedded hit] Production 57780 skill identity, controlled orphan 57784 view, original dormant 3205 track, FIFO hit replay and complete graph release passed; no unused branch was added to battle rules.");
        }

        private static bool IsDormantAsAuthored(BattleNativeMonsterRenderer native)
        {
            if (native.State != null || native.gameObject.activeInHierarchy) return false;
            var original = EmbeddedMonsterMecanimCatalog.Load();
            var authored = original.SourcePrefab.transform.Find(original.SkeletonPath);
            for (var current = native.transform; authored && authored != original.SourcePrefab.transform;
                 current = current.parent, authored = authored.parent)
                if (!current || current.gameObject.activeSelf != authored.gameObject.activeSelf) return false;
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using Spine.Unity;
using TMPro;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleSceneView : IDisposable
    {
        private readonly OfficialBattleModelCatalog catalog;
        private readonly Transform root;
        private readonly Func<FightBattleUnitState, string> modelKey;
        private readonly Func<FightGridPoint, Vector3> gridToWorld;
        private readonly Func<FightBattleUnitState, FightBattleStepEvent, string> animationForEvent;
        private readonly Action<Transform, FightBattleUnitState> orientUnit;
        private readonly Dictionary<int, UnitView> units = new Dictionary<int, UnitView>();
        private readonly Dictionary<int, Transform> attachedTransforms = new Dictionary<int, Transform>();
        private readonly Dictionary<int, UnitMotion> motions = new Dictionary<int, UnitMotion>();
        private readonly List<int> motionScratch = new List<int>();
        private const float SecondsPerCell = 0.5f;

        private sealed class UnitMotion
        {
            public List<Vector3> Points;
            public float Elapsed;
            public float Duration;
        }

        private sealed class UnitView
        {
            public FightBattleUnitState State;
            public SkeletonAnimation Skeleton;
            public OfficialBattleModelBinding Binding;
            public string CurrentAnimation;
        }

        public OfficialBattleSceneView(OfficialBattleModelCatalog catalog, Transform root,
            Func<FightBattleUnitState, string> modelKey, OfficialBattleGridMapper gridMapper,
            Func<FightBattleUnitState, FightBattleStepEvent, string> animationForEvent,
            Action<Transform, FightBattleUnitState> orientUnit)
        {
            this.catalog = catalog != null ? catalog : OfficialBattleModelCatalog.LoadDefault();
            this.root = root != null ? root : throw new ArgumentNullException(nameof(root));
            this.modelKey = modelKey ?? this.catalog.ResolveModelKey;
            if (gridMapper == null) throw new ArgumentNullException(nameof(gridMapper));
            gridToWorld = gridMapper.GridToWorld;
            this.animationForEvent = animationForEvent ?? throw new ArgumentNullException(nameof(animationForEvent));
            this.orientUnit = orientUnit ?? throw new ArgumentNullException(nameof(orientUnit));
        }

        /// <summary>
        /// Spawns one official-coloured damage label per resolved hit at the target grid position.
        /// Blinding/dodge/immunity labels have no localised text in the payload yet and are skipped
        /// instead of showing a fake number.
        /// </summary>
        public void ShowDamageFloats(FightBattleStepEvent stepEvent, FightBattleCheckpoint checkpoint,
            TMP_FontAsset font, OfficialBattleDamageColors colors)
        {
            if (stepEvent == null || stepEvent.DamageEvents == null || checkpoint == null
                || font == null || colors == null) return;
            foreach (FightBattleDamageEvent damage in stepEvent.DamageEvents)
            {
                if (damage == null || damage.Damage == 0) continue;
                if (damage.Dodge || damage.Immunity || damage.Blinding) continue;
                FightBattleUnitState unit = null;
                foreach (FightBattleUnitState candidate in checkpoint.Units)
                    if (candidate != null && candidate.Id == damage.TargetId) { unit = candidate; break; }
                if (unit == null) continue;
                Vector3 position = gridToWorld(new FightGridPoint(unit.X, unit.Y))
                    + new Vector3(colors.Spacing.x, 1.2f + colors.Spacing.y, colors.Spacing.z);
                OfficialBattleDamageFloat.Spawn(root, font,
                    Math.Abs(damage.Damage).ToString(CultureInfo.InvariantCulture),
                    colors.Resolve(damage, damage.ElementType), position);
            }
        }

        public void Refresh(FightBattleCheckpoint checkpoint, FightBattleStepEvent stepEvent = null)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            var present = new HashSet<int>();
            foreach (FightBattleUnitState state in checkpoint.Units)
            {
                if (state == null || !IsWaveSpawned(checkpoint, state.Id)) continue;
                // Dead units leave the battlefield; the official death AnimationClip is not migrated
                // yet, so the model is removed without a death animation.
                if (state.Hp <= 0) continue;
                present.Add(state.Id);
                if (attachedTransforms.TryGetValue(state.Id, out Transform attached) && attached != null)
                {
                    PlaceUnit(state.Id, attached, state, stepEvent);
                    orientUnit(attached, state);
                    continue;
                }
                string key = modelKey(state);
                if (string.IsNullOrEmpty(key)) continue;
                UnitView view = GetOrCreate(state);
                view.State = state;
                PlaceUnit(state.Id, view.Skeleton.transform, state, stepEvent);
                orientUnit(view.Skeleton.transform, state);
                if (stepEvent != null && (stepEvent.ActorId == state.Id
                    || (stepEvent.TargetIds != null && stepEvent.TargetIds.Contains(state.Id))))
                    Play(view, animationForEvent(state, stepEvent), false);
                else if (string.IsNullOrEmpty(view.CurrentAnimation))
                    Play(view, "idle_01", true);
            }

            var removed = new List<int>();
            foreach (KeyValuePair<int, UnitView> entry in units)
                if (!present.Contains(entry.Key)) removed.Add(entry.Key);
            foreach (int id in removed)
            {
                UnityEngine.Object.Destroy(units[id].Skeleton.gameObject);
                units.Remove(id);
                motions.Remove(id);
            }
        }

        /// <summary>
        /// Attaches an existing transform (for example the map avatar used for the main player) to a
        /// battle unit so the same grid movement playback drives it instead of teleporting it.
        /// </summary>
        public void AttachUnitTransform(int unitId, Transform transform)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            attachedTransforms[unitId] = transform;
        }

        public void DetachUnitTransform(int unitId)
        {
            attachedTransforms.Remove(unitId);
            motions.Remove(unitId);
        }

        private void PlaceUnit(int unitId, Transform body, FightBattleUnitState state,
            FightBattleStepEvent stepEvent)
        {
            Vector3 target = gridToWorld(new FightGridPoint(state.X, state.Y));
            FightBattleMovementEvent movement = FindMovement(stepEvent, unitId);
            if (movement != null && movement.Path != null && movement.Path.Count > 1)
                BeginMotion(unitId, body, movement);
            else if (!motions.ContainsKey(unitId))
                body.position = target;
        }

        public void Play(int unitId, string animation, bool loop = false)
        {
            if (!units.TryGetValue(unitId, out UnitView view))
                throw new KeyNotFoundException("Battle unit view is not mounted: " + unitId);
            Play(view, animation, loop);
        }

        /// <summary>
        /// Advances in-progress grid movement. Each cell takes the official move time; the caller
        /// scales the delta with the battle speed so movement follows the same clock as combat.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (motions.Count == 0 || deltaTime <= 0f) return;
            motionScratch.Clear();
            motionScratch.AddRange(motions.Keys);
            foreach (int unitId in motionScratch)
            {
                UnitMotion motion = motions[unitId];
                Transform body = Body(unitId);
                if (body == null)
                {
                    motions.Remove(unitId);
                    continue;
                }
                motion.Elapsed += deltaTime;
                float percent = motion.Duration <= 0f ? 1f : Mathf.Clamp01(motion.Elapsed / motion.Duration);
                int segments = motion.Points.Count - 1;
                float position = percent * segments;
                int index = Mathf.Min(Mathf.FloorToInt(position), segments - 1);
                float segment = position - index;
                body.position = Vector3.Lerp(motion.Points[index], motion.Points[index + 1], segment);
                if (percent >= 1f)
                {
                    body.position = motion.Points[motion.Points.Count - 1];
                    motions.Remove(unitId);
                }
            }
        }

        private void BeginMotion(int unitId, Transform body, FightBattleMovementEvent movement)
        {
            var motion = new UnitMotion { Points = new List<Vector3>(movement.Path.Count) };
            foreach (FightGridPoint point in movement.Path) motion.Points.Add(gridToWorld(point));
            if (motion.Points.Count <= 1) return;
            body.position = motion.Points[0];
            motion.Duration = (motion.Points.Count - 1) * SecondsPerCell;
            motions[unitId] = motion;
        }

        private Transform Body(int unitId)
        {
            if (attachedTransforms.TryGetValue(unitId, out Transform attached) && attached != null)
                return attached;
            return units.TryGetValue(unitId, out UnitView view) && view.Skeleton != null
                ? view.Skeleton.transform : null;
        }

        private static FightBattleMovementEvent FindMovement(FightBattleStepEvent stepEvent, int unitId)
        {
            if (stepEvent == null || stepEvent.Movements == null) return null;
            foreach (FightBattleMovementEvent movement in stepEvent.Movements)
                if (movement != null && movement.UnitId == unitId) return movement;
            return null;
        }

        public void Dispose()
        {
            foreach (UnitView view in units.Values)
                if (view.Skeleton != null) UnityEngine.Object.Destroy(view.Skeleton.gameObject);
            units.Clear();
            motions.Clear();
            attachedTransforms.Clear();
        }

        private UnitView GetOrCreate(FightBattleUnitState state)
        {
            if (units.TryGetValue(state.Id, out UnitView existing)) return existing;
            string key = modelKey(state);
            OfficialBattleModelBinding binding = catalog.Find(key);
            if (binding == null)
                throw new KeyNotFoundException("Official battle model binding is missing for unit " + state.Id + ": " + key);

            var gameObject = new GameObject("BattleUnit_" + state.Id);
            gameObject.transform.SetParent(root, false);
            SkeletonAnimation skeleton = gameObject.AddComponent<SkeletonAnimation>();
            skeleton.skeletonDataAsset = binding.skeleton;
            skeleton.initialSkinName = binding.skin;
            skeleton.Initialize(true);
            if (skeleton.Skeleton == null)
            {
                UnityEngine.Object.Destroy(gameObject);
                throw new InvalidOperationException("Official battle skeleton failed to initialize: " + key);
            }
            if (binding.scale <= 0)
            {
                UnityEngine.Object.Destroy(gameObject);
                throw new InvalidOperationException("Official battle model scale is invalid: " + key);
            }
            gameObject.transform.localScale = Vector3.one * binding.scale;
            var view = new UnitView { State = state, Skeleton = skeleton, Binding = binding };
            units.Add(state.Id, view);
            return view;
        }

        private static bool IsWaveSpawned(FightBattleCheckpoint checkpoint, int unitId)
        {
            if (checkpoint.Waves == null || checkpoint.Waves.Count == 0) return true;
            foreach (FightBattleWaveState wave in checkpoint.Waves)
            {
                if (wave == null || wave.UnitIds == null || !wave.UnitIds.Contains(unitId)) continue;
                return wave.Spawned;
            }
            return true;
        }

        private static void Play(UnitView view, string animation, bool loop)
        {
            if (string.IsNullOrEmpty(animation)) return;
            string spineName = null;
            foreach (OfficialBattleAnimationAlias alias in view.Binding.animations)
                if (alias.name == animation) { spineName = alias.spineName; break; }
            if (spineName == null)
                throw new InvalidOperationException("Official animation alias is missing: " + animation + " for unit " + view.State.Id);
            if (view.CurrentAnimation == spineName && view.Skeleton.AnimationState.GetCurrent(0) != null)
                return;
            view.Skeleton.AnimationState.SetAnimation(0, spineName, loop);
            view.CurrentAnimation = spineName;
        }
    }
}

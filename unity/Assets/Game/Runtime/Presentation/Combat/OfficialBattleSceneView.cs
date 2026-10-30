using System;
using System.Collections.Generic;
using System.Globalization;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleSceneView : IDisposable
    {
        private readonly OfficialBattleModelCatalog catalog;
        private readonly OfficialBattleNativeAnimationLibrary nativeModelLibrary;
        private readonly Transform root;
        private readonly Func<FightBattleUnitState, string> modelKey;
        private readonly Func<FightGridPoint, Vector3> gridToWorld;
        private readonly Func<FightBattleUnitState, FightBattleStepEvent, string> animationForEvent;
        private readonly bool usesOfficialAnimationFallback;
        private readonly Func<FightBattleUnitState, string> animationGroupResolver;
        private readonly Func<FightBattleUnitState, IOfficialBattleNativeModel> nativeNpcFactory;
        private readonly Action<Transform, FightBattleUnitState> orientUnit;
        private readonly OfficialBattleHudCatalog hudCatalog;
        private readonly OfficialBattleSkillPlayableCatalog skillPlayableCatalog;
        private readonly OfficialBattleDamageVisibilityCatalog damageVisibility;
        private readonly Transform damageRoot;
        private readonly Dictionary<int, UnitView> units = new Dictionary<int, UnitView>();
        private readonly Dictionary<int, Transform> attachedTransforms = new Dictionary<int, Transform>();
        private readonly Dictionary<int, OfficialBattleAttachedModelView> attachedModels =
            new Dictionary<int, OfficialBattleAttachedModelView>();
        private readonly Dictionary<int, OfficialBattleUnitSorting> attachedSorting =
            new Dictionary<int, OfficialBattleUnitSorting>();
        private readonly Dictionary<int, AttachedAnimatorView> attachedAnimators =
            new Dictionary<int, AttachedAnimatorView>();
        private readonly Dictionary<int, OfficialBattleUnitWorldHudView> attachedUnitHuds =
            new Dictionary<int, OfficialBattleUnitWorldHudView>();
        private readonly Dictionary<int, UnitMotion> motions = new Dictionary<int, UnitMotion>();
        private readonly List<int> motionScratch = new List<int>();
        private readonly List<PendingDamageFloat> pendingDamageFloats = new List<PendingDamageFloat>();
        private readonly List<PendingUnitAnimation> pendingUnitAnimations = new List<PendingUnitAnimation>();
        private readonly List<OfficialBattleDamageFloat> activeDamageFloats = new List<OfficialBattleDamageFloat>();
        private readonly List<OfficialBattleDamageFloat> damageFloatStackScratch = new List<OfficialBattleDamageFloat>();
        private const float SecondsPerCell = 0.5f;
        private Vector3 damageUiSpacing = new Vector3(0f, -0.1f, 0f);
        private FightBattleCheckpoint latestCheckpoint;
        private long pendingAnimationSerial;

        private sealed class PendingDamageFloat
        {
            public FightBattleDamageEvent Damage;
            public FightBattleUnitState Target;
            public string PrefabKey;
            public Color Color;
            public float Elapsed;
        }

        private sealed class PendingUnitAnimation
        {
            public OfficialBattleAnimationCue Cue;
            public float Elapsed;
            public long Serial;
        }

        private sealed class UnitMotion
        {
            public List<Vector3> Points;
            public List<float> SegmentStarts;
            public List<float> SegmentDurations;
            public float Elapsed;
        }

        private sealed class UnitView
        {
            public FightBattleUnitState State;
            public SkeletonAnimation Skeleton;
            public IOfficialBattleNativeModel NativeModel;
            public OfficialBattleModelBinding Binding;
            public OfficialBattleUnitWorldHudView Hud;
            public string CurrentAnimation;
            public bool AnimationLoops;
            public bool Dying;
        }

        private sealed class AttachedAnimatorView
        {
            public Animator Animator;
            public string Group;
            public string CurrentAnimation;
            public bool AnimationLoops;
            public bool Dying;
            public bool DeathFinished;
            public int CurrentStateHash;
            public RuntimeAnimatorController OriginalController;
            public AnimatorOverrideController SkillOverrideController;
            public readonly Dictionary<string, OfficialBattleAnimatorParameterStack<bool>> BoolParameters =
                new Dictionary<string, OfficialBattleAnimatorParameterStack<bool>>(StringComparer.Ordinal);
            public readonly Dictionary<string, OfficialBattleAnimatorParameterStack<float>> FloatParameters =
                new Dictionary<string, OfficialBattleAnimatorParameterStack<float>>(StringComparer.Ordinal);
            public readonly Dictionary<string, OfficialBattleAnimatorParameterStack<int>> IntParameters =
                new Dictionary<string, OfficialBattleAnimatorParameterStack<int>>(StringComparer.Ordinal);
            public readonly List<ActiveAttachedPlayable> ActivePlayables = new List<ActiveAttachedPlayable>();
        }

        private sealed class ActiveAttachedPlayable
        {
            public long Serial;
            public float Remaining;
            public readonly List<string> BoolParameters = new List<string>();
            public readonly List<string> FloatParameters = new List<string>();
            public readonly List<string> IntParameters = new List<string>();
            public int LayerIndex = -1;
            public float PreviousLayerWeight;
        }

        public OfficialBattleSceneView(OfficialBattleModelCatalog catalog, Transform root,
            Func<FightBattleUnitState, string> modelKey, OfficialBattleGridMapper gridMapper,
            Func<FightBattleUnitState, FightBattleStepEvent, string> animationForEvent,
            Action<Transform, FightBattleUnitState> orientUnit,
            OfficialBattleHudCatalog hudCatalog = null, Transform hudParent = null,
            OfficialBattleNativeAnimationLibrary nativeModelLibrary = null,
            Func<FightBattleUnitState, string> animationGroupResolver = null,
            Func<FightBattleUnitState, IOfficialBattleNativeModel> nativeNpcFactory = null)
        {
            this.catalog = catalog != null ? catalog : OfficialBattleModelCatalog.LoadDefault();
            this.nativeModelLibrary = nativeModelLibrary;
            this.root = root != null ? root : throw new ArgumentNullException(nameof(root));
            this.modelKey = modelKey ?? this.catalog.ResolveModelKey;
            if (gridMapper == null) throw new ArgumentNullException(nameof(gridMapper));
            gridToWorld = gridMapper.GridToWorld;
            usesOfficialAnimationFallback = animationForEvent == null;
            this.animationForEvent = animationForEvent ?? OfficialBattleAnimationFallback.Resolve;
            skillPlayableCatalog = usesOfficialAnimationFallback
                ? OfficialBattleSkillPlayableCatalog.LoadDefault() : null;
            this.animationGroupResolver = animationGroupResolver;
            this.nativeNpcFactory = nativeNpcFactory;
            this.orientUnit = orientUnit ?? ((_, __) => { });
            this.hudCatalog = hudCatalog;
            damageVisibility = OfficialBattleDamageVisibilityCatalog.Load();
            damageRoot = hudParent != null ? hudParent : root;
        }

        public static string FormatBattleNumber(long value)
        {
            string[] symbols = { "", "万", "亿", "兆", "京" };
            double scaled = Math.Abs((double)value);
            int symbolIndex = 0;
            while (scaled >= 10000.0 && symbolIndex < symbols.Length - 1)
            {
                scaled /= 10000.0;
                symbolIndex++;
            }
            if (symbolIndex == 0) return value.ToString(CultureInfo.InvariantCulture);
            double precision = scaled < 10.0 ? 100.0 : scaled < 100.0 ? 10.0 : 1.0;
            scaled = Math.Floor(scaled * precision) / precision;
            return scaled.ToString("0.##", CultureInfo.InvariantCulture) + symbols[symbolIndex];
        }

        public void ShowDamageFloats(FightBattleStepEvent stepEvent, FightBattleCheckpoint checkpoint,
            OfficialBattleDamageColors colors)
        {
            ShowDamageFloats(stepEvent, checkpoint, hudCatalog == null ? null : hudCatalog.Font, colors);
        }

        /// <summary>
        /// Uses official damage prefabs and their localized status text when the HUD catalog is bound.
        /// </summary>
        public void ShowDamageFloats(FightBattleStepEvent stepEvent, FightBattleCheckpoint checkpoint,
            TMP_FontAsset font, OfficialBattleDamageColors colors)
        {
            if (stepEvent == null || checkpoint == null || colors == null) return;
            damageUiSpacing = colors.Spacing;
            if (stepEvent.DamageEvents != null)
                foreach (FightBattleDamageEvent damage in stepEvent.DamageEvents)
                {
                    if (damage == null) continue;
                    FightBattleUnitState target = FindUnit(checkpoint, damage.TargetId);
                    if (target == null) continue;
                    if (!damageVisibility.CanShow(damage, target)) continue;
                    string prefabKey = DamagePrefabKey(damage);
                    Color tint = colors.Resolve(damage, damage.ElementType);
                    if (hudCatalog != null && hudCatalog.Font != null)
                    {
                        var pending = new PendingDamageFloat
                        {
                            Damage = damage,
                            Target = target,
                            PrefabKey = prefabKey,
                            Color = tint
                        };
                        if (damage.Delay > 0f) pendingDamageFloats.Add(pending);
                        else SpawnOfficialDamageFloat(pending);
                        continue;
                    }
                    if (font == null || damage.ShieldAddFinalDamageValue == 0
                        || damage.Dodge || damage.Immunity || damage.Blinding) continue;
                    Vector3 position = gridToWorld(new FightGridPoint(target.X, target.Y))
                        + new Vector3(colors.Spacing.x, 1.2f + colors.Spacing.y, colors.Spacing.z);
                    OfficialBattleDamageFloat.Spawn(damageRoot, font,
                        FormatBattleNumber(AbsoluteDamage(damage.ShieldAddFinalDamageValue)), tint, position);
                }

            if (hudCatalog == null) return;
            if (stepEvent.StatusCustomActions != null)
                foreach (FightBattleStatusCustomActionEvent action in stepEvent.StatusCustomActions)
                    SpawnCustomActionFloat(action, checkpoint);
            if (stepEvent.DispersedUnitIds!=null)
                foreach (int targetId in stepEvent.DispersedUnitIds)
                {
                    var target=FindUnit(checkpoint,targetId);
                    if (target!=null) SpawnOfficialDamageFloat(new PendingDamageFloat
                        {Target=target,PrefabKey="damage_FightDisperseDamageFloatEffect"});
                }
            if (stepEvent.SkillCasts == null) return;
            foreach (FightBattleSkillCastEvent cast in stepEvent.SkillCasts)
            {
                if (cast == null || !cast.IsReplay || cast.ReplayType==1
                    || !OfficialBattleWorldTips.CanShowReplay(cast.SkillEntityId)) continue;
                FightBattleUnitState actor = FindUnit(checkpoint, cast.ActorId);
                if (actor == null) continue;
                SpawnOfficialDamageFloat(new PendingDamageFloat
                {
                    Target = actor,
                    PrefabKey = "damage_FightResetSkillCDFloatEffect",
                    Color = Color.white
                });
            }
        }

        private void SpawnCustomActionFloat(FightBattleStatusCustomActionEvent action,
            FightBattleCheckpoint checkpoint)
        {
            if (action == null) return;
            FightBattleUnitState target = FindUnit(checkpoint, action.UnitId);
            if (target == null) return;
            var binding=OfficialBattleWorldTips.Find("damage_BattleCustomDamageFloatEffect");
            if (binding==null) throw new InvalidOperationException("Original custom action world binding is missing.");
            if (string.IsNullOrEmpty(binding.prefab.TextForAction(action.Action))) return;
            SpawnWorldTip(new PendingDamageFloat {Target=target},binding,action.Action);
        }

        private static long AbsoluteDamage(long value) => value == long.MinValue ? long.MaxValue : Math.Abs(value);

        private static FightBattleUnitState FindUnit(FightBattleCheckpoint checkpoint, int unitId)
        {
            foreach (FightBattleUnitState unit in checkpoint.Units)
                if (unit != null && unit.Id == unitId) return unit;
            return null;
        }

        private static string DamagePrefabKey(FightBattleDamageEvent damage)
        {
            // GetEffectUITypeByDamage tests the raw signed result before type flags.
            if ((damage.HasDamageValue ? damage.DamageValue : damage.Damage) > 0)
                return "damage_BattleCureFloatEffect";
            if (damage.Immunity) return "damage_BattleImmunityDamageFloatEffect";
            if (damage.Dodge) return "damage_FightDodgeDamageFloatEffect";
            if (damage.Blinding) return "damage_FightBlindingDamageFloatEffect";
            if (damage.ElementType == 200)
                return damage.Critical ? "damage_AbyssCritDamageFloatEffect"
                    : damage.Block ? "damage_AbyssBlockDamageFloatEffect" : "damage_AbyssDamageFloatEffect";
            if (damage.Critical) return "damage_BattleCritDamageFloatEffect";
            if (damage.Block) return "damage_BattleBlockDamageFloatEffect";
            return "damage_BattleDamageFloatEffect";
        }

        private OfficialBattleDamageFloat SpawnOfficialDamageFloat(PendingDamageFloat pending)
        {
            var tip=OfficialBattleWorldTips.Find(pending.PrefabKey);
            if (tip!=null) return SpawnWorldTip(pending,tip);
            BattleHudPrefab prefab = hudCatalog.FindPrefab(pending.PrefabKey);
            if (prefab == null || prefab.root == null || prefab.damageUi == null)
                throw new InvalidOperationException("Official battle damage float is missing: " + pending.PrefabKey);
            var configured = hudCatalog.FindWorldDamage(prefab.damageUi.type);
            if (configured != null) return SpawnWorldDamageFloat(pending, configured);
            var renderer = new OfficialBattleHudRenderer(hudCatalog);
            GameObject instance = renderer.Create(pending.PrefabKey, damageRoot);
            OfficialBattleHudAnimationPlayer animations = renderer.InitializeAnimations(
                pending.PrefabKey, instance, useManualTick: true);
            BattleHudDamageFloatBinding binding = prefab.damageUi;
            bool hasOfficialAnimation = PlayDamageFloatAnimation(prefab, animations);
            Transform number = FindPrefabPath(instance.transform, binding.numberPath);
            if (number != null)
                foreach (TMP_Text label in number.GetComponentsInChildren<TMP_Text>(true))
                    label.text = FormatBattleNumber(AbsoluteDamage(pending.Damage == null ? 0 : pending.Damage.ShieldAddFinalDamageValue));

            Transform trueDamage = FindPrefabPath(instance.transform, binding.trueDamagePath);
            Transform breakResilience = FindPrefabPath(instance.transform, binding.breakResiliencePath);
            bool isBreak = pending.Damage != null && pending.Damage.BreakResilience;
            bool isTrue = pending.Damage != null && pending.Damage.TrueDamage && !isBreak;
            if (trueDamage != null) trueDamage.gameObject.SetActive(isTrue);
            if (breakResilience != null) breakResilience.gameObject.SetActive(isBreak);
            foreach (TMP_Text label in instance.GetComponentsInChildren<TMP_Text>(true))
                label.color = pending.Color.linear;

            Vector3 world = gridToWorld(new FightGridPoint(pending.Target.X, pending.Target.Y))
                + new Vector3(binding.sceneOffset != null && binding.sceneOffset.Length > 0 ? binding.sceneOffset[0] : 0f,
                    1.2f + (binding.sceneOffset != null && binding.sceneOffset.Length > 1 ? binding.sceneOffset[1] : 0f),
                    binding.sceneOffset != null && binding.sceneOffset.Length > 2 ? binding.sceneOffset[2] : 0f);
            RectTransform rect = instance.transform as RectTransform;
            Canvas canvas = damageRoot.GetComponentInParent<Canvas>();
            bool screenSpace = canvas != null && canvas.renderMode != RenderMode.WorldSpace;
            RectTransform parentRect = screenSpace ? damageRoot as RectTransform : null;
            Camera projectionCamera = Camera.main != null ? Camera.main : (canvas == null ? null : canvas.worldCamera);
            Camera eventCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            if (screenSpace && rect != null && parentRect != null && projectionCamera != null)
            {
                Vector3 screen = projectionCamera.WorldToScreenPoint(world);
                if (screen.z < 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect,
                        screen, eventCamera, out Vector2 local))
                {
                    UnityEngine.Object.Destroy(instance);
                    return null;
                }
                rect.anchoredPosition = local;
            }
            else
            {
                instance.transform.position = world;
                if (Camera.main != null) instance.transform.rotation = Camera.main.transform.rotation;
            }

            var group = instance.GetComponent<CanvasGroup>() ?? instance.AddComponent<CanvasGroup>();
            TMP_Text primary = null;
            if (number != null) primary = number.GetComponentInChildren<TMP_Text>(true);
            if (primary == null) primary = instance.GetComponentInChildren<TMP_Text>(true);
            OfficialBattleDamageFloat floatView = instance.AddComponent<OfficialBattleDamageFloat>();
            floatView.InitializeOfficial(binding.type, primary, group, animations,
                binding.showTime, screenSpace, hasOfficialAnimation, pending.Target.Id,
                binding.height, world, parentRect, projectionCamera, eventCamera);
            activeDamageFloats.Add(floatView);
            RefreshDamageFloatStack(pending.Target.Id);
            return floatView;
        }

        private OfficialBattleDamageFloat SpawnWorldDamageFloat(PendingDamageFloat pending,
            BattleCustomActionWorldRuntimeData prefab)
        {
            var renderer = new OfficialBattleHudRenderer(hudCatalog);
            GameObject instance = renderer.CreateWorldFloat(prefab, root, out var nodes);
            Vector3 sceneOffset=new Vector3(prefab.sceneOffset[0],prefab.sceneOffset[1],prefab.sceneOffset[2]);
            Transform entity=Body(pending.Target.Id);
            Vector3 world=(entity!=null?entity.position:gridToWorld(new FightGridPoint(pending.Target.X,pending.Target.Y)))+sceneOffset;
            instance.transform.position = world;
            if (Camera.main != null)
            {
                instance.transform.rotation = Camera.main.transform.rotation;
                if (Camera.main.orthographic)
                    instance.transform.localScale = Vector3.one * (Camera.main.orthographicSize / prefab.cameraScale);
            }
            var animations = renderer.InitializeWorldAnimations(prefab.nodes, instance, nodes, useManualTick: true);
            var element = OfficialBattleElementFeedback.Bind(prefab.effectType,nodes,animations,out var elementBinding);
            TextMesh number = nodes[prefab.numberPath].GetComponent<TextMesh>();
            if (number == null) throw new InvalidOperationException("Original TextMesh number binding is missing.");
            number.text = FormatBattleNumber(AbsoluteDamage(pending.Damage.ShieldAddFinalDamageValue));
            Color tint = prefab.effectType >= 5 && prefab.effectType <= 7 ? Color.white : pending.Color.linear;
            tint.a = number.color.a;
            number.color = tint;
            bool isBreak = pending.Damage.BreakResilience && !string.IsNullOrEmpty(prefab.breakPath);
            bool isElement = false;
            if (!isBreak && element != null)
            {
                element.Reset();
                if (pending.Damage.IsElementAffect)
                {
                    element.SetContent(pending.Damage.ElementConfrontation,pending.Color);
                    isElement = true;
                    if (Mathf.Approximately(pending.Damage.ElementConfrontation,1f))
                        OfficialBattleElementFeedback.HideImmuneNumbers(elementBinding,nodes);
                }
            }
            bool isTrue = pending.Damage.TrueDamage && !isBreak && !isElement;
            if (!string.IsNullOrEmpty(prefab.breakPath)) nodes[prefab.breakPath].SetActive(isBreak);
            if (!string.IsNullOrEmpty(prefab.truePath)) nodes[prefab.truePath].SetActive(isTrue);
            if (isTrue && !string.IsNullOrEmpty(prefab.truePath))
                foreach (var label in nodes[prefab.truePath].GetComponentsInChildren<TMP_Text>(true))
                    label.color = pending.Color.linear;
            var sorting = instance.GetComponent<SortingGroup>();
            if (sorting == null) sorting = instance.AddComponent<SortingGroup>();
            sorting.sortingOrder = prefab.sortingOrderStart + instance.transform.GetSiblingIndex();
            var view = instance.AddComponent<OfficialBattleDamageFloat>();
            view.InitializeOfficial(prefab.effectType, null, null, animations, prefab.showTime, false,
                true, pending.Target.Id, prefab.height, world, null, Camera.main, null,
                prefab.cameraScale, prefab.sortingOrderStart,entity,sceneOffset);
            activeDamageFloats.Add(view);
            RefreshDamageFloatStack(pending.Target.Id);
            return view;
        }

        private OfficialBattleDamageFloat SpawnWorldTip(PendingDamageFloat pending,BattleWorldTipBinding binding,int customAction=0)
        {
            var prefab=binding.prefab;
            var renderer=new OfficialBattleHudRenderer(hudCatalog);
            GameObject instance=renderer.CreateWorldFloat(prefab,root,out var nodes);
            foreach (var source in binding.texts)
            {
                var text=OfficialBattleElementFeedback.BindText(source,nodes);
                if (pending.Damage!=null) text.color=pending.Color.linear;
            }
            foreach (var source in binding.sprites)
            {
                var sprite=nodes[source.path].GetComponent<SpriteRenderer>();
                sprite.sprite=Resources.Load<Sprite>(OfficialBattleElementFeedbackCatalog.ResourceRoot+source.sprite);
                sprite.sharedMaterial=Resources.Load<Material>(OfficialBattleElementFeedbackCatalog.ResourceRoot+source.material);
                if (sprite.sprite==null || sprite.sharedMaterial==null) throw new InvalidOperationException("Original world tip Sprite assets are missing.");
                sprite.drawMode=(SpriteDrawMode)source.drawMode;
                sprite.tileMode=(SpriteTileMode)source.tileMode;
                sprite.size=new Vector2(source.size[0],source.size[1]);
                sprite.sortingOrder=source.order;
            }
            if (prefab.effectType==11)
            {
                nodes[prefab.textPath].GetComponent<TMP_Text>().text=prefab.TextForAction(customAction);
                // DamageCustomActionUI.ShouldShowIcon: only EStatusCustomAction.CDModify.
                nodes[prefab.iconPath].SetActive(customAction==3);
            }
            Vector3 sceneOffset=new Vector3(prefab.sceneOffset[0],prefab.sceneOffset[1],prefab.sceneOffset[2]);
            Transform entity=Body(pending.Target.Id);
            Vector3 world=(entity!=null?entity.position:gridToWorld(new FightGridPoint(pending.Target.X,pending.Target.Y)))+sceneOffset;
            instance.transform.position=world;
            if (Camera.main!=null)
            {
                instance.transform.rotation=Camera.main.transform.rotation;
                if (Camera.main.orthographic) instance.transform.localScale=Vector3.one*(Camera.main.orthographicSize/prefab.cameraScale);
            }
            var sorting=instance.AddComponent<SortingGroup>();
            sorting.sortingOrder=prefab.sortingOrderStart+instance.transform.GetSiblingIndex();
            var animations=renderer.InitializeWorldAnimations(prefab.nodes,instance,nodes,useManualTick:true);
            var view=instance.AddComponent<OfficialBattleDamageFloat>();
            view.InitializeOfficial(prefab.effectType,nodes[prefab.textPath].GetComponent<TMP_Text>(),null,
                animations,prefab.showTime,false,true,pending.Target.Id,prefab.height,world,null,Camera.main,null,
                prefab.cameraScale,prefab.sortingOrderStart,entity,sceneOffset);
            activeDamageFloats.Add(view);
            RefreshDamageFloatStack(pending.Target.Id);
            return view;
        }

        private void RefreshDamageFloatStack(int targetId)
        {
            damageFloatStackScratch.Clear();
            foreach (OfficialBattleDamageFloat floatView in activeDamageFloats)
                if (floatView != null && floatView.TargetId == targetId)
                    damageFloatStackScratch.Add(floatView);

            Vector3 offset = Vector3.zero;
            for (int index = damageFloatStackScratch.Count - 1; index >= 0; index--)
            {
                OfficialBattleDamageFloat floatView = damageFloatStackScratch[index];
                floatView.SetStackOffset(offset);
                offset += new Vector3(damageUiSpacing.x,
                    floatView.StackHeight + damageUiSpacing.y, damageUiSpacing.z);
            }
        }

        private static bool PlayDamageFloatAnimation(BattleHudPrefab prefab,
            OfficialBattleHudAnimationPlayer animations)
        {
            if (prefab.nodes == null || animations == null) return false;
            bool hasAutomaticAnimation = false;
            foreach (BattleHudNode node in prefab.nodes)
            {
                if (node != null && node.animation_play_automatically
                    && !string.IsNullOrEmpty(node.default_animation)) hasAutomaticAnimation = true;
                if (node?.animations == null) continue;
                foreach (BattleHudAnimation animation in node.animations)
                {
                    if (animation == null || string.IsNullOrEmpty(animation.name)
                        || !animation.name.StartsWith("GameCanvas_FlyText_", StringComparison.Ordinal)) continue;
                    return animations.Play(animation.owner, animation.name);
                }
            }
            return hasAutomaticAnimation;
        }

        private static Transform FindPrefabPath(Transform prefabRoot, string path)
        {
            if (prefabRoot == null || string.IsNullOrEmpty(path)) return null;
            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int index = parts.Length > 0 && parts[0] == prefabRoot.name ? 1 : 0;
            Transform current = prefabRoot;
            for (; index < parts.Length; index++)
            {
                current = current.Find(parts[index]);
                if (current == null) return null;
            }
            return current;
        }

        public void Refresh(FightBattleCheckpoint checkpoint, FightBattleStepEvent stepEvent = null)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            latestCheckpoint = checkpoint;
            var present = new HashSet<int>();
            foreach (FightBattleUnitState state in checkpoint.Units)
            {
                if (state == null || state.Ended && state.Hp > 0 || !IsWaveSpawned(checkpoint, state.Id)) continue;
                if (state.Hp <= 0)
                {
                    Transform deadAttached = attachedTransforms.TryGetValue(state.Id, out Transform deadBody)
                        ? deadBody : null;
                    if (deadAttached != null)
                    {
                        if (TryBeginAttachedDeath(state, deadAttached)) present.Add(state.Id);
                    }
                    else if (TryBeginDeath(state)) present.Add(state.Id);
                    continue;
                }
                present.Add(state.Id);
                if (attachedTransforms.TryGetValue(state.Id, out Transform attached) && attached != null)
                {
                    PlaceUnit(state.Id, attached, state, stepEvent);
                    orientUnit(attachedModels.TryGetValue(state.Id, out var model) ? model.Model : attached, state);
                    RefreshAttachedAnimation(state, attached, stepEvent);
                    RefreshAttachedHud(state, attached);
                    continue;
                }
                UnitView view = GetOrCreate(state);
                if (view == null) continue;
                if (view.Dying)
                {
                    view.Dying = false;
                    PlayAnimation(view, "idle_01", true);
                }
                view.State = state;
                view.Hud?.Refresh(state, checkpoint.Units);
                Transform body = Body(state.Id);
                if (body == null) continue;
                PlaceUnit(state.Id, body, state, stepEvent);
                orientUnit(body, state);
                if (stepEvent != null && (stepEvent.ActorId == state.Id
                    || (stepEvent.TargetIds != null && stepEvent.TargetIds.Contains(state.Id))))
                {
                    if (!usesOfficialAnimationFallback)
                        PlayAnimation(view, animationForEvent(state, stepEvent), false);
                }
                else if (string.IsNullOrEmpty(view.CurrentAnimation))
                    PlayAnimation(view, "idle_01", true);
            }

            if (usesOfficialAnimationFallback && stepEvent != null)
                ScheduleOfficialAnimationCues(stepEvent);

            var removed = new List<int>();
            foreach (KeyValuePair<int, UnitView> entry in units)
                if (!present.Contains(entry.Key)) removed.Add(entry.Key);
            foreach (int id in removed)
            {
                RemoveUnit(id);
                units.Remove(id);
                motions.Remove(id);
            }
            var removedAttachedHuds = new List<int>();
            foreach (int id in attachedUnitHuds.Keys)
                if (!present.Contains(id)) removedAttachedHuds.Add(id);
            foreach (int id in removedAttachedHuds)
            {
                attachedUnitHuds[id]?.Dispose();
                attachedUnitHuds.Remove(id);
            }
            var removedAttachedAnimators = new List<int>();
            foreach (int id in attachedAnimators.Keys)
                if (!present.Contains(id)) removedAttachedAnimators.Add(id);
            foreach (int id in removedAttachedAnimators)
            {
                ReleaseAttachedAnimator(attachedAnimators[id]);
                attachedAnimators.Remove(id);
            }
        }

        /// <summary>
        /// Attaches an existing transform (for example the map avatar used for the main player) to a
        /// battle unit so the same grid movement playback drives it instead of teleporting it.
        /// </summary>
        public void AttachUnitTransform(int unitId, Transform transform)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            if (attachedModels.TryGetValue(unitId, out var currentModel) && currentModel.Source == transform)
                return;
            if (units.ContainsKey(unitId))
            {
                RemoveUnit(unitId);
                units.Remove(unitId);
            }
            DetachUnitTransform(unitId);
            FightBattleUnitState state = FindUnit(latestCheckpoint, unitId);
            if (state == null) throw new InvalidOperationException("Cannot attach a model to an unknown battle unit: " + unitId);
            int entityId = OfficialBattleSceneBindings.EntityId(state);
            var attachedModel = new OfficialBattleAttachedModelView(transform, root, entityId);
            attachedModels.Add(unitId, attachedModel);
            Transform body = attachedModel.Root;
            attachedTransforms[unitId] = body;
            attachedSorting[unitId] = OfficialBattleSceneBindings.BindModel(attachedModel.Model, entityId);
            PlaceUnit(unitId, body, state, null);
            orientUnit(attachedModel.Model, state);
            if (state.Hp <= 0)
            {
                TryBeginAttachedDeath(state, body);
                return;
            }
            RefreshAttachedAnimation(state, body, null);
            RefreshAttachedHud(state, body);
        }

        public void DetachUnitTransform(int unitId)
        {
            if (attachedSorting.TryGetValue(unitId, out OfficialBattleUnitSorting sorting)) sorting?.Dispose();
            attachedSorting.Remove(unitId);
            attachedTransforms.Remove(unitId);
            motions.Remove(unitId);
            if (attachedAnimators.TryGetValue(unitId, out AttachedAnimatorView oldAnimator))
                ReleaseAttachedAnimator(oldAnimator);
            attachedAnimators.Remove(unitId);
            if (attachedUnitHuds.TryGetValue(unitId, out OfficialBattleUnitWorldHudView hud))
            {
                hud?.Dispose();
                attachedUnitHuds.Remove(unitId);
            }
            if (attachedModels.TryGetValue(unitId, out var model)) model.Dispose();
            attachedModels.Remove(unitId);
        }

        private void PlaceUnit(int unitId, Transform body, FightBattleUnitState state,
            FightBattleStepEvent stepEvent)
        {
            Vector3 target = gridToWorld(new FightGridPoint(state.X, state.Y));
            if (HasMovement(stepEvent, unitId))
                BeginMotion(unitId, body, stepEvent);
            else if (!motions.ContainsKey(unitId))
                body.position = target;
        }

        public void Play(int unitId, string animation, bool loop = false)
        {
            if (!units.TryGetValue(unitId, out UnitView view))
                throw new KeyNotFoundException("Battle unit view is not mounted: " + unitId);
            if (!PlayAnimation(view, animation, loop))
                throw new KeyNotFoundException("Battle unit animation is not mounted: " + unitId + "/" + animation);
        }

        /// <summary>
        /// Advances in-progress grid movement. Each cell takes the official move time; the caller
        /// scales the delta with the battle speed so movement follows the same clock as combat.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (motions.Count > 0)
            {
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
                    bool complete = true;
                    for (int index = 0; index < motion.SegmentDurations.Count; index++)
                    {
                        float segmentStart = motion.SegmentStarts[index];
                        if (motion.Elapsed < segmentStart)
                        {
                            body.position = motion.Points[index];
                            complete = false;
                            break;
                        }
                        float segmentDuration = motion.SegmentDurations[index];
                        float progress = segmentDuration <= 0f ? 1f
                            : Mathf.Clamp01((motion.Elapsed - segmentStart) / segmentDuration);
                        body.position = Vector3.Lerp(motion.Points[index], motion.Points[index + 1], progress);
                        if (progress < 1f)
                        {
                            complete = false;
                            break;
                        }
                    }
                    if (complete)
                    {
                        body.position = motion.Points[motion.Points.Count - 1];
                        motions.Remove(unitId);
                    }
                }
            }

            var completedDeaths = new List<int>();
            foreach (KeyValuePair<int, UnitView> entry in units)
            {
                UnitView view = entry.Value;
                if (view.NativeModel != null)
                {
                    view.NativeModel.Tick(deltaTime);
                    if (view.CurrentAnimation == "run_01" && !IsMotionRunning(entry.Key))
                        PlayAnimation(view, "idle_01", true);
                    else if (view.NativeModel.IsAnimationFinished)
                    {
                        if (view.Dying) completedDeaths.Add(entry.Key);
                        else if (!view.AnimationLoops)
                            PlayAnimation(view, IsMotionRunning(entry.Key) ? "run_01" : "idle_01", true);
                    }
                }
                else if (view.Skeleton != null && view.Skeleton.AnimationState != null)
                {
                    Spine.TrackEntry track = view.Skeleton.AnimationState.GetCurrent(0);
                    if (view.CurrentAnimation == "run_01" && !IsMotionRunning(entry.Key))
                        PlayAnimation(view, "idle_01", true);
                    else if (track != null && track.IsComplete)
                    {
                        if (view.Dying) completedDeaths.Add(entry.Key);
                        else if (!view.AnimationLoops)
                            PlayAnimation(view, IsMotionRunning(entry.Key) ? "run_01" : "idle_01", true);
                    }
                }
            }
            foreach (int id in completedDeaths)
            {
                RemoveUnit(id);
                units.Remove(id);
                motions.Remove(id);
            }

            var completedAttachedDeaths = new List<int>();
            foreach (KeyValuePair<int, AttachedAnimatorView> entry in attachedAnimators)
            {
                AttachedAnimatorView view = entry.Value;
                if (view.Animator == null) continue;
                if (view.Dying)
                {
                    if (!view.DeathFinished && AttachedAnimationFinished(view))
                    {
                        view.DeathFinished = true;
                        completedAttachedDeaths.Add(entry.Key);
                    }
                    continue;
                }
                if (!view.AnimationLoops && AttachedAnimationFinished(view))
                    PlayAttachedAnimation(view,
                        IsMotionRunning(entry.Key) ? "run_01" : "idle_01", true);
                else if (view.CurrentAnimation == "run_01" && !IsMotionRunning(entry.Key))
                    PlayAttachedAnimation(view, "idle_01", true);
            }
            AdvanceAttachedPlayableLifetimes(deltaTime);
            AdvancePendingUnitAnimations(deltaTime);

            foreach (int id in completedAttachedDeaths)
            {
                if (!attachedUnitHuds.TryGetValue(id, out OfficialBattleUnitWorldHudView hud)) continue;
                hud?.Dispose();
                attachedUnitHuds.Remove(id);
            }

            for (int index = activeDamageFloats.Count - 1; index >= 0; index--)
            {
                OfficialBattleDamageFloat floatView = activeDamageFloats[index];
                if (floatView == null)
                {
                    activeDamageFloats.RemoveAt(index);
                    continue;
                }
                floatView.Tick(deltaTime);
            }
            for (int index = pendingDamageFloats.Count - 1; index >= 0; index--)
            {
                PendingDamageFloat pending = pendingDamageFloats[index];
                float previous = pending.Elapsed;
                pending.Elapsed += deltaTime;
                if (pending.Elapsed < pending.Damage.Delay) continue;
                pendingDamageFloats.RemoveAt(index);
                OfficialBattleDamageFloat floatView = SpawnOfficialDamageFloat(pending);
                if (floatView != null)
                    floatView.Tick(Mathf.Max(0f, pending.Elapsed - Math.Max(previous, pending.Damage.Delay)));
            }
            foreach (UnitView view in units.Values) view.Hud?.Tick(deltaTime);
            foreach (OfficialBattleUnitWorldHudView hud in attachedUnitHuds.Values) hud?.Tick(deltaTime);
        }

        private void BeginMotion(int unitId, Transform body, FightBattleStepEvent stepEvent)
        {
            var motion = new UnitMotion
            {
                Points = new List<Vector3>(),
                SegmentStarts = new List<float>(),
                SegmentDurations = new List<float>()
            };
            float cursor = 0f;
            if (stepEvent.Movements != null)
                foreach (FightBattleMovementEvent movement in stepEvent.Movements)
                {
                    if (movement == null || movement.UnitId != unitId
                        || movement.Path == null || movement.Path.Count <= 1) continue;
                    cursor = Mathf.Max(cursor, Mathf.Max(0f, movement.Delay));
                    Vector3 first = gridToWorld(movement.Path[0]);
                    if (motion.Points.Count == 0) motion.Points.Add(first);
                    else if (motion.Points[motion.Points.Count - 1] != first)
                    {
                        motion.Points.Add(first);
                        motion.SegmentStarts.Add(cursor);
                        motion.SegmentDurations.Add(0f);
                    }
                    for (int index = 1; index < movement.Path.Count; index++)
                    {
                        Vector3 next = gridToWorld(movement.Path[index]);
                        Vector3 previous = motion.Points[motion.Points.Count - 1];
                        float duration = movement.MoveDuration >= 0f ? movement.MoveDuration
                            : movement.MoveSpeed > 0f
                                ? Vector3.Distance(previous, next) / movement.MoveSpeed
                                : SecondsPerCell;
                        motion.SegmentStarts.Add(cursor);
                        motion.SegmentDurations.Add(duration);
                        motion.Points.Add(next);
                        cursor += duration;
                    }
                }
            if (motion.SegmentDurations.Count == 0) return;
            body.position = motion.Points[0];
            motions[unitId] = motion;
        }

        private bool IsMotionRunning(int unitId)
        {
            if (!motions.TryGetValue(unitId, out UnitMotion motion)) return false;
            for (int index = 0; index < motion.SegmentDurations.Count; index++)
            {
                float start = motion.SegmentStarts[index];
                float end = start + motion.SegmentDurations[index];
                if (motion.Elapsed >= start && motion.Elapsed < end) return true;
            }
            return false;
        }

        internal void PreviewPreparationPosition(int unitId, Vector3 world)
        {
            Transform body = Body(unitId);
            if (body != null) body.position = world;
        }

        private Transform Body(int unitId)
        {
            if (attachedTransforms.TryGetValue(unitId, out Transform attached) && attached != null)
                return attached;
            if (!units.TryGetValue(unitId, out UnitView view)) return null;
            if (view.NativeModel != null) return view.NativeModel.Transform;
            return view.Skeleton == null ? null : view.Skeleton.transform;
        }

        private bool TryBeginDeath(FightBattleUnitState state)
        {
            if (!units.TryGetValue(state.Id, out UnitView view)) return false;
            view.State = state;
            if (view.Dying) return true;
            if (!PlayAnimation(view, "die_01", false)) return false;
            RemovePendingAnimations(state.Id);
            view.Dying = true;
            return true;
        }

        private bool TryBeginAttachedDeath(FightBattleUnitState state, Transform body)
        {
            AttachedAnimatorView view = GetOrCreateAttachedAnimator(state, body);
            if (view == null) return false;
            if (view.Dying) return true;
            StopAttachedPlayables(view);
            if (!PlayAttachedAnimation(view, "die_01", false)) return false;
            RemovePendingAnimations(state.Id);
            view.Dying = true;
            view.DeathFinished = false;
            return true;
        }

        private void RefreshAttachedAnimation(FightBattleUnitState state, Transform body,
            FightBattleStepEvent stepEvent)
        {
            AttachedAnimatorView view = GetOrCreateAttachedAnimator(state, body);
            if (view == null) return;
            if (view.Dying)
            {
                view.Dying = false;
                view.DeathFinished = false;
            }
            if (usesOfficialAnimationFallback)
            {
                if (string.IsNullOrEmpty(view.CurrentAnimation))
                    PlayAttachedAnimation(view, "idle_01", true);
                return;
            }
            string animation = AttachedAnimationForEvent(state, stepEvent);
            if (animation == null && motions.ContainsKey(state.Id)) animation = "run_01";
            if (animation == null) return;
            bool loop = animation == "idle_01" || animation == "run_01";
            PlayAttachedAnimation(view, animation, loop, restart: !loop);
        }

        private AttachedAnimatorView GetOrCreateAttachedAnimator(FightBattleUnitState state, Transform body)
        {
            if (attachedAnimators.TryGetValue(state.Id, out AttachedAnimatorView existing)
                && existing.Animator != null) return existing;
            if (animationGroupResolver == null) return null;
            Animator animator = body.GetComponent<Animator>();
            // Unity can return a managed component wrapper that compares equal to null.
            // Use Unity's null check before looking inside the recovered model hierarchy.
            if (animator == null) animator = body.GetComponentInChildren<Animator>(true);
            if (animator == null) return null;
            string group = animationGroupResolver(state);
            if (string.IsNullOrEmpty(group)) return null;
            var view = new AttachedAnimatorView
            {
                Animator = animator,
                Group = group,
                OriginalController = animator.runtimeAnimatorController
            };
            attachedAnimators[state.Id] = view;
            return view;
        }

        private void ScheduleOfficialAnimationCues(FightBattleStepEvent stepEvent)
        {
            foreach (OfficialBattleAnimationCue cue in OfficialBattleAnimationTimeline.Build(
                         stepEvent, skillPlayableCatalog == null ? null : skillPlayableCatalog.Data))
            {
                FightBattleUnitState state = FindUnit(latestCheckpoint, cue.UnitId);
                if (state == null || state.Hp <= 0) continue;
                bool hasAttached = attachedTransforms.TryGetValue(cue.UnitId, out Transform attached)
                    && attached != null;
                if (!hasAttached && !units.ContainsKey(cue.UnitId)) continue;
                pendingUnitAnimations.Add(new PendingUnitAnimation
                {
                    Cue = cue,
                    Serial = ++pendingAnimationSerial
                });
            }
        }

        private void AdvancePendingUnitAnimations(float deltaTime)
        {
            foreach (PendingUnitAnimation pending in pendingUnitAnimations)
                pending.Elapsed += deltaTime;

            pendingUnitAnimations.Sort((left, right) =>
            {
                int result = (left.Cue.Delay - left.Elapsed)
                    .CompareTo(right.Cue.Delay - right.Elapsed);
                if (result != 0) return result;
                result = left.Cue.Priority.CompareTo(right.Cue.Priority);
                return result != 0 ? result : left.Serial.CompareTo(right.Serial);
            });

            int completed = 0;
            while (completed < pendingUnitAnimations.Count
                && pendingUnitAnimations[completed].Elapsed >= pendingUnitAnimations[completed].Cue.Delay)
            {
                PendingUnitAnimation pending = pendingUnitAnimations[completed];
                PlayScheduledAnimation(pending.Cue, pending.Serial,
                    Mathf.Max(0f, pending.Elapsed - pending.Cue.Delay));
                completed++;
            }
            if (completed > 0) pendingUnitAnimations.RemoveRange(0, completed);
        }

        private void PlayScheduledAnimation(OfficialBattleAnimationCue cue, long serial,
            float elapsedSinceScheduled)
        {
            FightBattleUnitState state = FindUnit(latestCheckpoint, cue.UnitId);
            if (state == null || state.Hp <= 0) return;
            if (cue.SkillPlayable != null)
            {
                PlayScheduledSkillPlayable(cue, state, serial, elapsedSinceScheduled);
                return;
            }
            if (attachedTransforms.TryGetValue(cue.UnitId, out Transform attached) && attached != null)
            {
                AttachedAnimatorView attachedView = GetOrCreateAttachedAnimator(state, attached);
                if (attachedView == null || attachedView.Dying) return;
                PlayAttachedAnimation(attachedView, cue.Animation, cue.Loop, restart: !cue.Loop);
                return;
            }
            if (!units.TryGetValue(cue.UnitId, out UnitView view) || view.Dying) return;
            PlayAnimation(view, cue.Animation, cue.Loop);
        }

        private void PlayScheduledSkillPlayable(OfficialBattleAnimationCue eventCue,
            FightBattleUnitState state, long serial, float elapsedSinceScheduled)
        {
            OfficialBattleSkillPlayableCue playable = eventCue.SkillPlayable;
            if (attachedTransforms.TryGetValue(state.Id, out Transform attached) && attached != null)
            {
                AttachedAnimatorView view = GetOrCreateAttachedAnimator(state, attached);
                PlayAttachedSkillPlayable(view, playable, serial, elapsedSinceScheduled);
                return;
            }

            if (!units.TryGetValue(state.Id, out UnitView unit) || unit.Dying) return;
            string exactAnimation = playable.kind == 1 ? playable.animation : null;
            if (string.IsNullOrEmpty(exactAnimation)) return;
            if (unit.NativeModel != null && unit.NativeModel.HasAnimation(exactAnimation))
            {
                unit.NativeModel.Play(exactAnimation, false, restart: true);
                unit.CurrentAnimation = exactAnimation;
                unit.AnimationLoops = false;
                return;
            }
            if (unit.Binding != null && HasExactSpineAlias(unit.Binding, exactAnimation))
                PlayAnimation(unit, exactAnimation, false);
        }

        private void PlayAttachedSkillPlayable(AttachedAnimatorView view,
            OfficialBattleSkillPlayableCue playable, long serial, float elapsedSinceScheduled)
        {
            if (view == null || view.Animator == null || playable == null) return;

            if (playable.kind == 1)
            {
                AnimationClip clip = skillPlayableCatalog == null ? null : skillPlayableCatalog.LoadClip(playable);
                if (clip != null) ApplyOfficialClipOverride(view, playable.origin_clip_name, clip);
            }

            Animator animator = view.Animator;
            if (playable.pre_state_name_allow_list != null
                && playable.pre_state_name_allow_list.Count > 0)
            {
                AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
                bool allowed = false;
                foreach (string stateName in playable.pre_state_name_allow_list)
                    if (!string.IsNullOrEmpty(stateName) && current.IsName(stateName))
                    {
                        allowed = true;
                        break;
                    }
                if (!allowed) return;
            }

            var active = new ActiveAttachedPlayable { Serial = serial };
            if (HasAnimatorParameter(animator, playable.my_bool_param,
                    AnimatorControllerParameterType.Bool))
            {
                if (playable.end_resume_param)
                {
                    if (!view.BoolParameters.TryGetValue(playable.my_bool_param,
                            out OfficialBattleAnimatorParameterStack<bool> boolStack))
                    {
                        boolStack = new OfficialBattleAnimatorParameterStack<bool>(
                            animator.GetBool(playable.my_bool_param));
                        view.BoolParameters.Add(playable.my_bool_param, boolStack);
                    }
                    boolStack.Push(serial, playable.my_bool_value);
                    active.BoolParameters.Add(playable.my_bool_param);
                }
                animator.SetBool(playable.my_bool_param, playable.my_bool_value);
            }
            if (HasAnimatorParameter(animator, playable.my_float_param,
                    AnimatorControllerParameterType.Float))
            {
                if (playable.end_resume_param)
                {
                    if (!view.FloatParameters.TryGetValue(playable.my_float_param,
                            out OfficialBattleAnimatorParameterStack<float> floatStack))
                    {
                        floatStack = new OfficialBattleAnimatorParameterStack<float>(
                            animator.GetFloat(playable.my_float_param));
                        view.FloatParameters.Add(playable.my_float_param, floatStack);
                    }
                    floatStack.Push(serial, playable.my_float_value);
                    active.FloatParameters.Add(playable.my_float_param);
                }
                animator.SetFloat(playable.my_float_param, playable.my_float_value);
            }
            if (HasAnimatorParameter(animator, playable.int_param,
                    AnimatorControllerParameterType.Int))
            {
                if (playable.end_resume_param)
                {
                    if (!view.IntParameters.TryGetValue(playable.int_param,
                            out OfficialBattleAnimatorParameterStack<int> intStack))
                    {
                        intStack = new OfficialBattleAnimatorParameterStack<int>(
                            animator.GetInteger(playable.int_param));
                        view.IntParameters.Add(playable.int_param, intStack);
                    }
                    intStack.Push(serial, playable.int_value);
                    active.IntParameters.Add(playable.int_param);
                }
                animator.SetInteger(playable.int_param, playable.int_value);
            }
            if (HasAnimatorParameter(animator, playable.trigger_param,
                    AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(playable.trigger_param);
            if (!string.IsNullOrEmpty(playable.layer_name))
            {
                int layer = animator.GetLayerIndex(playable.layer_name);
                if (layer >= 0)
                {
                    if (playable.end_resume_param)
                    {
                        active.LayerIndex = layer;
                        active.PreviousLayerWeight = animator.GetLayerWeight(layer);
                    }
                    animator.SetLayerWeight(layer, playable.layer_weight);
                }
            }

            if (playable.end_resume_param
                && (active.BoolParameters.Count > 0 || active.FloatParameters.Count > 0
                    || active.IntParameters.Count > 0 || active.LayerIndex >= 0))
            {
                float duration = playable.my_duration;
                if (duration >= 0f)
                {
                    float speed = playable.speed > 0f && !float.IsNaN(playable.speed)
                        && !float.IsInfinity(playable.speed) ? playable.speed : 1f;
                    duration /= speed;
                    active.Remaining = duration - elapsedSinceScheduled;
                }
                else
                {
                    active.Remaining = -1f;
                }
                view.ActivePlayables.Add(active);
                if (active.Remaining <= 0f) RestoreAttachedPlayable(view, active);
            }
        }

        private void AdvanceAttachedPlayableLifetimes(float deltaTime)
        {
            foreach (AttachedAnimatorView view in attachedAnimators.Values)
            {
                if (view == null || view.Animator == null) continue;
                for (int index = view.ActivePlayables.Count - 1; index >= 0; index--)
                {
                    ActiveAttachedPlayable active = view.ActivePlayables[index];
                    if (active.Remaining < 0f) continue;
                    active.Remaining -= deltaTime;
                    if (active.Remaining <= 0f) RestoreAttachedPlayable(view, active);
                }
            }
        }

        private static void RestoreAttachedPlayable(AttachedAnimatorView view,
            ActiveAttachedPlayable active)
        {
            if (view == null || active == null) return;
            Animator animator = view.Animator;
            if (animator != null)
            {
                foreach (string name in active.BoolParameters)
                    if (view.BoolParameters.TryGetValue(name,
                            out OfficialBattleAnimatorParameterStack<bool> boolStack))
                    {
                        boolStack.Remove(active.Serial);
                        animator.SetBool(name, boolStack.Current);
                        if (boolStack.Count == 0) view.BoolParameters.Remove(name);
                    }
                foreach (string name in active.FloatParameters)
                    if (view.FloatParameters.TryGetValue(name,
                            out OfficialBattleAnimatorParameterStack<float> floatStack))
                    {
                        floatStack.Remove(active.Serial);
                        animator.SetFloat(name, floatStack.Current);
                        if (floatStack.Count == 0) view.FloatParameters.Remove(name);
                    }
                foreach (string name in active.IntParameters)
                    if (view.IntParameters.TryGetValue(name,
                            out OfficialBattleAnimatorParameterStack<int> intStack))
                    {
                        intStack.Remove(active.Serial);
                        animator.SetInteger(name, intStack.Current);
                        if (intStack.Count == 0) view.IntParameters.Remove(name);
                    }
                if (active.LayerIndex >= 0)
                    animator.SetLayerWeight(active.LayerIndex, active.PreviousLayerWeight);
            }
            view.ActivePlayables.Remove(active);
        }

        private static void StopAttachedPlayables(AttachedAnimatorView view)
        {
            if (view == null) return;
            while (view.ActivePlayables.Count > 0)
                RestoreAttachedPlayable(view, view.ActivePlayables[view.ActivePlayables.Count - 1]);
        }

        private static bool ApplyOfficialClipOverride(AttachedAnimatorView view,
            string originClipName, AnimationClip clip)
        {
            if (view == null || view.Animator == null || clip == null
                || string.IsNullOrEmpty(originClipName)) return false;
            if (view.SkillOverrideController == null)
            {
                AnimatorOverrideController current = view.Animator.runtimeAnimatorController
                    as AnimatorOverrideController;
                if (current == null) return false;
                view.OriginalController = view.Animator.runtimeAnimatorController;
                view.SkillOverrideController = new AnimatorOverrideController(current);
                view.Animator.runtimeAnimatorController = view.SkillOverrideController;
            }

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            view.SkillOverrideController.GetOverrides(overrides);
            bool found = false;
            for (int i = 0; i < overrides.Count; i++)
            {
                AnimationClip original = overrides[i].Key;
                if (original == null || original.name != originClipName) continue;
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, clip);
                found = true;
            }
            if (found) view.SkillOverrideController.ApplyOverrides(overrides);
            return found;
        }

        private static bool HasAnimatorParameter(Animator animator, string name,
            AnimatorControllerParameterType type)
        {
            if (animator == null || string.IsNullOrEmpty(name)) return false;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.name == name && parameter.type == type) return true;
            return false;
        }

        private static bool HasExactSpineAlias(OfficialBattleModelBinding binding, string name)
        {
            if (binding == null || binding.animations == null || string.IsNullOrEmpty(name)) return false;
            foreach (OfficialBattleAnimationAlias alias in binding.animations)
                if (alias != null && alias.name == name) return true;
            return false;
        }

        private static void ReleaseAttachedAnimator(AttachedAnimatorView view)
        {
            if (view == null) return;
            StopAttachedPlayables(view);
            if (view.Animator != null && view.SkillOverrideController != null
                && view.Animator.runtimeAnimatorController == view.SkillOverrideController)
                view.Animator.runtimeAnimatorController = view.OriginalController;
            if (view.SkillOverrideController != null)
                UnityEngine.Object.Destroy(view.SkillOverrideController);
            view.SkillOverrideController = null;
        }

        private void RemovePendingAnimations(int unitId)
        {
            pendingUnitAnimations.RemoveAll(pending => pending.Cue.UnitId == unitId);
        }

        private static string AttachedAnimationForEvent(FightBattleUnitState state,
            FightBattleStepEvent stepEvent)
        {
            if (stepEvent?.DamageEvents != null)
                foreach (FightBattleDamageEvent damage in stepEvent.DamageEvents)
                    if (damage != null && !damage.IsSkillCost && damage.TargetId == state.Id)
                        return damage.Block ? "block_01" : "hit_01";
            if (HasMovement(stepEvent, state.Id)) return "run_01";
            if (stepEvent == null) return "idle_01";
            if (stepEvent.SkillCasts != null)
                foreach (FightBattleSkillCastEvent cast in stepEvent.SkillCasts)
                    if (cast != null && cast.ActorId == state.Id) return null;
            if (stepEvent.ActorId != state.Id) return null;
            return "idle_01";
        }

        private static bool PlayAttachedAnimation(AttachedAnimatorView view, string animation, bool loop,
            bool restart = false)
        {
            if (view == null || view.Animator == null || string.IsNullOrEmpty(animation)) return false;
            if (view.CurrentAnimation == animation && view.AnimationLoops == loop
                && (loop || (!restart && !AttachedAnimationFinished(view)))) return true;
            if (!OfficialBattleCharacterAnimationCatalog.TryResolve(view.Animator.runtimeAnimatorController,
                    view.Group + "/" + animation, out string stateName)) return false;
            int stateHash = Animator.StringToHash(stateName);
            if (!view.Animator.HasState(0, stateHash)) return false;
            view.Animator.Play(stateHash, 0, 0f);
            view.Animator.Update(0f);
            view.CurrentAnimation = animation;
            view.CurrentStateHash = stateHash;
            view.AnimationLoops = loop;
            return true;
        }

        private static bool AttachedAnimationFinished(AttachedAnimatorView view)
        {
            if (view == null || view.Animator == null || string.IsNullOrEmpty(view.CurrentAnimation)
                || view.Animator.IsInTransition(0)) return false;
            AnimatorStateInfo state = view.Animator.GetCurrentAnimatorStateInfo(0);
            return state.fullPathHash == view.CurrentStateHash && state.normalizedTime >= 1f;
        }

        private void RemoveUnit(int unitId)
        {
            RemovePendingAnimations(unitId);
            if (!units.TryGetValue(unitId, out UnitView view)) return;
            view.Hud?.Dispose();
            if (view.NativeModel != null) view.NativeModel.Dispose();
            else if (view.Skeleton != null) UnityEngine.Object.Destroy(view.Skeleton.gameObject);
        }

        private static bool HasMovement(FightBattleStepEvent stepEvent, int unitId)
        {
            if (stepEvent == null || stepEvent.Movements == null) return false;
            foreach (FightBattleMovementEvent movement in stepEvent.Movements)
                if (movement != null && movement.UnitId == unitId
                    && movement.Path != null && movement.Path.Count > 1) return true;
            return false;
        }

        public void Dispose()
        {
            foreach (UnitView view in units.Values)
            {
                view.Hud?.Dispose();
                if (view.NativeModel != null) view.NativeModel.Dispose();
                else if (view.Skeleton != null) DestroySceneObject(view.Skeleton.gameObject);
            }
            units.Clear();
            motions.Clear();
            pendingUnitAnimations.Clear();
            foreach (OfficialBattleUnitSorting sorting in attachedSorting.Values) sorting?.Dispose();
            attachedSorting.Clear();
            attachedTransforms.Clear();
            foreach (AttachedAnimatorView view in attachedAnimators.Values)
                ReleaseAttachedAnimator(view);
            attachedAnimators.Clear();
            foreach (OfficialBattleUnitWorldHudView hud in attachedUnitHuds.Values) hud?.Dispose();
            attachedUnitHuds.Clear();
            foreach (var model in attachedModels.Values) model.Dispose();
            attachedModels.Clear();
            pendingDamageFloats.Clear();
            foreach (OfficialBattleDamageFloat floatView in activeDamageFloats)
                if (floatView != null) DestroySceneObject(floatView.gameObject);
            activeDamageFloats.Clear();
        }

        private static void DestroySceneObject(GameObject instance)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(instance);
            else UnityEngine.Object.DestroyImmediate(instance);
        }

        private UnitView GetOrCreate(FightBattleUnitState state)
        {
            if (units.TryGetValue(state.Id, out UnitView existing)) return existing;
            var summonData = Resources.Load<OfficialSummonSkeletonData>("Combat/Summons/" + state.MonsterId + "-data");
            if (summonData != null)
            {
                var model = new OfficialSummonNativeModelView(summonData, root, "BattleUnit_" + state.Id);
                var summonView = new UnitView { State = state, NativeModel = model };
                PlayAnimation(summonView, "idle_01", true);
                summonView.Hud = OfficialBattleUnitWorldHudView.Create(hudCatalog, state, model.Transform);
                units.Add(state.Id, summonView);
                return summonView;
            }
            if (nativeNpcFactory != null)
            {
                IOfficialBattleNativeModel npcModel = nativeNpcFactory(state);
                if (npcModel != null)
                {
                    var npcView = new UnitView { State = state, NativeModel = npcModel };
                    PlayAnimation(npcView, "idle_01", true);
                    npcView.Hud = OfficialBattleUnitWorldHudView.Create(hudCatalog, state, npcModel.Transform);
                    units.Add(state.Id, npcView);
                    return npcView;
                }
            }
            if (nativeModelLibrary != null)
            {
                string nativeKey = nativeModelLibrary.ResolveModelKey(state);
                OfficialBattleNativeModelRecord nativeBinding = nativeModelLibrary.FindModel(nativeKey);
                if (nativeBinding != null && nativeModelLibrary.HasAnimation(nativeKey, "idle_01"))
                {
                    var nativeModel = new OfficialBattleNativeModelView(nativeModelLibrary,
                        nativeBinding, root, "BattleUnit_" + state.Id);
                    OfficialBattleSceneBindings.BindModel(nativeModel.ModelTransform, nativeBinding.monsterId);
                    var nativeView = new UnitView { State = state, NativeModel = nativeModel };
                    PlayAnimation(nativeView, "idle_01", true);
                    nativeView.Hud = OfficialBattleUnitWorldHudView.Create(hudCatalog, state,
                        nativeModel.Transform);
                    units.Add(state.Id, nativeView);
                    return nativeView;
                }
            }

            string key = modelKey(state);
            if (string.IsNullOrEmpty(key)) return null;
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
            OfficialBattleSceneBindings.BindModel(gameObject.transform, OfficialBattleSceneBindings.EntityId(state));
            var view = new UnitView { State = state, Skeleton = skeleton, Binding = binding };
            view.Hud = OfficialBattleUnitWorldHudView.Create(hudCatalog, state, skeleton.transform);
            units.Add(state.Id, view);
            return view;
        }

        private void RefreshAttachedHud(FightBattleUnitState state, Transform body)
        {
            if (!attachedUnitHuds.TryGetValue(state.Id,
                    out OfficialBattleUnitWorldHudView hud))
            {
                hud = OfficialBattleUnitWorldHudView.Create(hudCatalog, state, body);
                attachedUnitHuds.Add(state.Id, hud);
            }
            hud?.Refresh(state, latestCheckpoint?.Units);
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

        private static bool PlayAnimation(UnitView view, string animation, bool loop)
        {
            if (view == null || string.IsNullOrEmpty(animation)) return false;
            if (view.NativeModel != null)
            {
                if (!view.NativeModel.Play(animation, loop)) return false;
                view.CurrentAnimation = animation;
                view.AnimationLoops = loop;
                return true;
            }
            if (view.Skeleton == null || view.Binding == null) return false;
            string spineName = null;
            foreach (OfficialBattleAnimationAlias alias in view.Binding.animations)
                if (alias.name == animation) { spineName = alias.spineName; break; }
            if (spineName == null) return false;
            Spine.TrackEntry current = view.Skeleton.AnimationState.GetCurrent(0);
            if (view.CurrentAnimation == animation && current != null && !current.IsComplete
                && view.AnimationLoops == loop) return true;
            view.Skeleton.AnimationState.SetAnimation(0, spineName, loop);
            view.CurrentAnimation = animation;
            view.AnimationLoops = loop;
            return true;
        }
    }
}

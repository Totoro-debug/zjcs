using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleUnitUI;
using Zjcs.Official.World.Map13EntityRendering;

namespace Zjcs.Presentation.Combat
{
    // The original BattleHpSpriteSlider consumes the common battle snapshot
    // through its health/effect port. No gameplay state is mutated by this view.
    public sealed class OfficialBattlePartnerHpView : IBattleHpSliderBindings, IDisposable
    {
        private readonly BattleHpSpriteSlider slider;
        private readonly OfficialBattlePartnerHpEffects effects;
        private readonly Transform ui;
        private readonly Transform lockParent;
        private readonly GameEffectData data;
        private long hp, maxHp, shield;
        private bool listening, disposed;
        public long Hp => hp;
        public long LastMaxHp => maxHp;
        // An original NPC has no CharacterComponent and never subscribes to
        // player spectator changes in BattleHpSpriteSlider.
        public bool WatchesPlayer => false;
        public event Action<long> OnSpactatedPlayerId { add { } remove { } }
        public event Action<long, long, long, long> OnHpOrShieldChange;
        public BattleHpSpriteSlider Slider => slider;

        public OfficialBattlePartnerHpView(FightBattleUnitState state, PartnerNativeBattleActor actor,
            OfficialBattlePartnerHpEffects effects)
        {
            this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
            data = effects.ForFaction(state.Side);
            int entityId = Convert.ToInt32(actor.OriginalRow["entityId"]);
            var prefab = Resources.Load<GameObject>("Combat/PartnerHpBars/Prefabs/EcEntity/Fight/FightNpc/" + entityId);
            if (!prefab) throw new InvalidOperationException("Original partner HP prefab is absent: " + entityId);
            Transform original = actor.Root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "HpBarGame");
            ui = original.parent;
            var row = effects.Binding(entityId);
            lockParent = actor.Root.GetComponentsInChildren<Transform>(true)
                .Single(t => RelativePath(t, actor.Root) == row.lockParentPath);
            var go = UnityEngine.Object.Instantiate(prefab, ui, false);
            go.name = original.name;
            go.transform.SetSiblingIndex(original.GetSiblingIndex());
            actor.Attachments.RebindOriginalSubtree(go.transform);
            slider = go.GetComponent<BattleHpSpriteSlider>()
                ?? throw new InvalidOperationException("Original partner HP component is unbound.");
            Read(state);
            slider.Init(this);
            slider.OnStart();
            Tick();
        }

        private static string RelativePath(Transform t, Transform root)
        {
            string value = "";
            for (; t && t != root; t = t.parent) value = t.name + (value.Length == 0 ? "" : "/" + value);
            return value;
        }

        public void Refresh(FightBattleUnitState state)
        {
            long previousHp = hp, previousMax = maxHp, previousShield = shield;
            Read(state);
            if (listening && (hp != previousHp || maxHp != previousMax || shield != previousShield)) Publish();
        }

        private void Read(FightBattleUnitState state)
        {
            hp = state.Hp;
            maxHp = state.LastMaxHp;
            shield = FightBattleShieldRuntime.PersistentShieldTotal(state);
        }

        // BattleHpAndShieldHandle.RefreshShieldAndHp, preserving raw long
        // arithmetic and the original argument order (including HP under shield).
        private void Publish()
        {
            if (shield > 0)
            {
                long total = shield + hp;
                if (total <= maxHp) OnHpOrShieldChange?.Invoke(maxHp, hp, maxHp, total);
                else OnHpOrShieldChange?.Invoke(total, hp, total, total);
            }
            else OnHpOrShieldChange?.Invoke(maxHp, hp, 0L, 0L);
        }

        public void OnEnable() { listening = true; Publish(); }
        public void OnDisable() { listening = false; }
        public GameEffectData GetData() => data;
        public GameObject Borrow(GameObject prefab, Transform slot, bool autoActive = true)
            => effects.Borrow(prefab, slot, autoActive);
        public void Return(GameObject effect, GameObject prefab) => effects.Return(effect, prefab);

        public void Tick()
        {
            if (disposed || !ui || !lockParent) return;
            // Original TransformLocker.UpdateScale/UpdateRot. The native actor
            // writes Unity transforms directly, so evaluate at the same view tick.
            Vector3 scale = ui.localScale;
            Vector3 parentScale = lockParent.localScale;
            if (Math.Sign(scale.x * parentScale.x) < 0) scale.x = -scale.x;
            if (Math.Sign(scale.y * parentScale.y) < 0) scale.y = -scale.y;
            if (Math.Sign(scale.z * parentScale.z) < 0) scale.z = -scale.z;
            ui.localScale = scale;
            ui.rotation = Quaternion.Euler(0, 0, 0);
            SlicedFilledRenderService.Current.RefreshAll();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (slider) slider.OnEnd();
            OnHpOrShieldChange = null;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleRoundUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // Projects checkpoint changes onto the original client's round driver events.
    // Queue placement, pooling, motion and card lifecycle belong to GameRoundUI.
    public sealed class OfficialBattleSceneTimelineView : MonoBehaviour, IBattleRoundUIBindings
    {
        private readonly Dictionary<int, OfficialBattleRoundCardContext> contexts = new Dictionary<int, OfficialBattleRoundCardContext>();
        private readonly HashSet<int> pendingDeaths = new HashSet<int>();
        private readonly HashSet<int> present = new HashSet<int>();
        private readonly HashSet<int> visible = new HashSet<int>();
        private BattleRoundPortraitAssets portraitAssets;
        private FightDynamicTextureETC2 playerPortraitAtlas;
        private Transform inactiveCards;
        private GameRoundUI manager;
        private bool initialized;
        public GamePlayManagerConfig Configuration => portraitAssets.Configuration;
        public GameRoundScene Scene { get; private set; }
        public Camera SceneCamera { get; private set; }
        public BattleRoundDriver Driver { get; } = new BattleRoundDriver();
        public bool IsPlaying { get; internal set; }
        public event Action<BattleRoundDamage> Damage;
        public event Action<int, int, int, int> Die;
        public event Action<int> EntityAdded;
        public event Action SameGameUpdated;
        public bool HasEntity(int id) => present.Contains(id);
        public bool ShowOnRoundProgress(int id) => visible.Contains(id);
        public BattleRoundRole GetRole(int id) => contexts.TryGetValue(id, out var context) ? context.Role : null;

        public static OfficialBattleSceneTimelineView Mount(GameObject hudRoot, Camera sceneCamera = null)
        {
            Transform retained = hudRoot.transform.Find("Content/Top/FixedContent/RoundUI");
            var scenePrefab = Resources.Load<GameObject>("Combat/RoundUI/GameRoundScene");
            var uiPrefab = Resources.Load<GameObject>("Combat/RoundUI/RoundUI");
            if (retained == null || scenePrefab == null || uiPrefab == null)
                throw new InvalidOperationException("Restore the complete original native round UI and scene before entering battle.");
            // Replace the retained geometry shell with the identical native subtree.
            // Native serialized anchors, sizes, sibling order and asset links are used.
            GameObject ui = Instantiate(uiPrefab, retained.parent, false);
            ui.name = "RoundUI";
            ui.transform.SetSiblingIndex(retained.GetSiblingIndex());
            retained.name = "Retained round geometry";
            retained.gameObject.SetActive(false);
            Destroy(retained.gameObject);
            GameObject root = Instantiate(scenePrefab);
            var view = root.AddComponent<OfficialBattleSceneTimelineView>();
            view.Scene = root.GetComponent<GameRoundScene>();
            view.SceneCamera = sceneCamera;
            view.manager = ui.GetComponent<GameRoundUI>();
            view.portraitAssets = Resources.Load<BattleRoundPortraitAssets>("Combat/RoundUI/PortraitAssets");
            if (view.manager == null || view.portraitAssets == null)
                throw new InvalidOperationException("Original round manager/configuration bindings are missing.");
            view.playerPortraitAtlas = new FightDynamicTextureETC2();
            view.playerPortraitAtlas.Initialize(view.Configuration, view.portraitAssets.LoadTexture);
            var inactive = new GameObject("Native card injection");
            inactive.SetActive(false);
            inactive.transform.SetParent(hudRoot.transform, false);
            view.inactiveCards = inactive.transform;
            view.Scene.SetAttachUI(view.manager.IconBack, sceneCamera);
            hudRoot.AddComponent<OfficialBattleTimelineLifetime>().Initialize(root);
            return view;
        }

        public GameObject InstantiateCard(GameObject prefab, Transform parent)
        {
            // Dependencies reach the original selector before its Awake runs.
            GameObject root = Instantiate(prefab, inactiveCards, false);
            foreach (var selector in root.GetComponentsInChildren<GameRoundPlayerPortrait>(true))
                selector.BindConfiguration(Configuration);
            foreach (var head in root.GetComponentsInChildren<HeadCharacterPortraitETC2Scene>(true)) head.BindAtlas(playerPortraitAtlas);
            foreach (var head in root.GetComponentsInChildren<HeadCharacterPortraitETC2>(true)) head.BindAtlas(playerPortraitAtlas);
            foreach (var handler in root.GetComponentsInChildren<GameRoundPortraitETC2SceneHandler>(true)) handler.BindAtlas(playerPortraitAtlas);
            foreach (var handler in root.GetComponentsInChildren<GameRoundPortraitETC2Handler>(true)) handler.BindAtlas(playerPortraitAtlas);
            root.transform.SetParent(parent, false);
            root.SetActive(true);
            return root;
        }

        public void PrepareCard(IGameRoundCharacterItem card, int id)
        {
            var context = contexts[id];
            card.Transform.name = "GameRoundCharacterItem_" + id;
            if (card is GameRoundCharacterItemScene sceneCard) sceneCard.Bind(context);
            else if (card is GameRoundCharacterItem uiCard) uiCard.Bind(context);
            foreach (var head in card.Transform.GetComponentsInChildren<HeadCharacterPortrait>(true)) head.Bind(context);
        }

        public void Refresh(FightBattleCheckpoint checkpoint, Func<FightBattleUnitState, Sprite> portrait,
            Func<FightBattleUnitState, Sprite> faction, bool animate, FightBattleStepEvent step)
        {
            var all = checkpoint.Units.Where(unit => unit != null).ToDictionary(unit => unit.Id);
            var roundIds = new HashSet<int>(checkpoint.Timeline?.Entries?.Where(entry => entry != null)
                .Select(entry => entry.Id) ?? Enumerable.Empty<int>());
            present.Clear(); visible.Clear();
            var addedEntities = new List<int>();
            foreach (var unit in all.Values.Where(unit => IsSpawned(checkpoint, unit.Id)))
            {
                present.Add(unit.Id);
                if (ShowOnRoundProgress(unit)) visible.Add(unit.Id);
                if (!visible.Contains(unit.Id) || !roundIds.Contains(unit.Id) || unit.Hp <= 0 || unit.Ended
                    || contexts.ContainsKey(unit.Id)) continue;
                var summon = Resources.Load<OfficialSummonSkeletonData>("Combat/Summons/" + unit.MonsterId + "-data");
                contexts.Add(unit.Id, new OfficialBattleRoundCardContext(unit,
                    unit.NpcClassId > 0 ? OfficialBattleNpcPortraitCatalog.Load().Find(unit.NpcClassId)
                    : summon != null ? summon.Portrait : portrait(unit), faction?.Invoke(unit), portraitAssets));
                addedEntities.Add(unit.Id);
            }
            var entries = checkpoint.Timeline?.Entries?.Where(entry => entry != null && all.TryGetValue(entry.Id, out var unit)
                && unit.Hp > 0 && !unit.Ended && present.Contains(entry.Id)).OrderBy(entry => entry.Next).ToArray()
                ?? new FightTurnEntry[0];
            int previous = Driver.CurActingEntityId;
            int current = checkpoint.Timeline?.Acting ?? 0;
            if (!entries.Any(entry => entry.Id == current)) current = entries.Length == 0 ? 0 : entries[0].Id;
            Driver.CurActingEntityId = current;
            var inserted = new List<BattleRoundData>();
            var updated = new List<BattleRoundData>();
            foreach (var entry in entries)
            {
                var data = Driver.FindData(entry.Id);
                double interval = all[entry.Id].Turn?.IntervalTicks ?? 0;
                // The local executor has already scheduled the actor's next turn.
                // Keep its displayed time at this action until the actor changes.
                double nextTime = entry.Id == checkpoint.Timeline.Acting
                    ? checkpoint.Timeline.Current : entry.Next;
                if (data == null)
                {
                    data = new BattleRoundData { EntityId = entry.Id };
                    Driver.DataList.Add(data); inserted.Add(data);
                }
                else if (data.NextTime != nextTime || data.Interval != interval) updated.Add(data);
                data.PreTime = entry.Pre;
                data.NextTime = nextTime; data.Interval = interval;
            }
            var keep = new HashSet<int>(entries.Select(entry => entry.Id));
            if (!initialized || !animate)
            {
                StopAllCoroutines(); pendingDeaths.Clear();
                Driver.DataList.RemoveAll(data => !keep.Contains(data.EntityId));
                Driver.DataList.Sort((a, b) => a.NextTime.CompareTo(b.NextTime));
                if (!initialized) { initialized = true; manager.Initialize(this); }
                else SameGameUpdated?.Invoke();
                return;
            }
            // Presentation delays are already resolved by the battle host. Deliver
            // original damage/death notifications at those visual impact times.
            if (step?.DamageEvents != null)
                foreach (var damage in step.DamageEvents.Where(damage => !damage.IsSkillCost))
                    StartCoroutine(DeliverDamage(new BattleRoundDamage { TargetId = damage.TargetId,
                        Damage = damage.Damage, Shield = damage.ShieldDamage,
                        HasHitEntity = damage.HitEntityId > 0, DamageType = damage.DamageType }, damage.Delay));
            foreach (int id in Driver.DataList.Select(data => data.EntityId).Where(id => !keep.Contains(id)).ToArray())
            {
                if (pendingDeaths.Contains(id)) continue;
                if (all.TryGetValue(id, out var dead) && dead.Hp <= 0)
                {
                    pendingDeaths.Add(id);
                    float delay = step?.DamageEvents?.Where(damage => damage.TargetId == id && damage.AfterHp == 0)
                        .Select(damage => damage.Delay).DefaultIfEmpty(0).Max() ?? 0;
                    StartCoroutine(DeliverDeath(id, delay));
                }
                else RemoveData(id);
            }
            foreach (int id in addedEntities) EntityAdded?.Invoke(id);
            foreach (var data in inserted) Driver.NotifyInsert(data);
            if (previous != current) Driver.NotifyActing(previous, current);
            foreach (var data in updated) Driver.NotifyUpdate(data, BattleRoundUpdateState.None);
        }

        private IEnumerator DeliverDamage(BattleRoundDamage damage, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            Damage?.Invoke(damage);
        }
        private IEnumerator DeliverDeath(int id, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            Die?.Invoke(0, 0, id, 0);
            RemoveData(id);
            pendingDeaths.Remove(id);
        }
        private void RemoveData(int id)
        {
            Driver.DataList.RemoveAll(data => data.EntityId == id);
            Driver.NotifyRemove(id);
        }
        private void OnDestroy()
        {
            if (manager != null) manager.enabled = false;
            playerPortraitAtlas?.Dispose();
            playerPortraitAtlas = null;
        }
        private static bool IsSpawned(FightBattleCheckpoint checkpoint, int id)
        {
            if (checkpoint.Waves == null) return true;
            foreach (var wave in checkpoint.Waves)
                if (wave?.UnitIds != null && wave.UnitIds.Contains(id)) return wave.Spawned;
            return true;
        }
        private static bool ShowOnRoundProgress(FightBattleUnitState unit)
        {
            var summon = Resources.Load<OfficialSummonSkeletonData>("Combat/Summons/" + unit.MonsterId + "-data");
            return summon == null || summon.Source.showOnRoundProgress;
        }
    }
}

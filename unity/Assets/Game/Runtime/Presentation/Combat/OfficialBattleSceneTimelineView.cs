using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.BattleRoundUI;
using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    [DefaultExecutionOrder(100)]
    public sealed class OfficialBattleSceneTimelineView : MonoBehaviour
    {
        private sealed class Card
        {
            public GameObject Root;
            public GameRoundCharacterItemScene Native;
            public OfficialBattleRoundCardContext Context;
            public bool Acting;
            public Vector3 From, Move, Target;
        }
        private readonly Dictionary<int, Card> cards = new Dictionary<int, Card>();
        private OfficialBattleTimelineAssets assets;
        private GameObject cardPrefab;
        private Transform slot;
        private float start, end, moveTime;
        private bool moving;
        private FightDynamicTextureETC2 playerPortraitAtlas;
        private BattleRoundPortraitAssets portraitAssets;
        private Transform inactiveCards;

        public static OfficialBattleSceneTimelineView Mount(GameObject hudRoot, Camera sceneCamera = null)
        {
            var assets = OfficialBattleTimelineAssets.Load();
            RectTransform anchor = hudRoot.transform.Find(assets.Anchor).GetComponent<RectTransform>();
            var prefab = Resources.Load<GameObject>("Combat/RoundUI/GameRoundScene");
            var cardPrefab = Resources.Load<GameObject>("Combat/RoundUI/GameRoundCharacterItem_Scene");
            if (prefab == null || prefab.GetComponent<GameRoundScene>() == null)
                throw new InvalidOperationException("Restore the official native GameRoundScene before entering battle.");
            if (cardPrefab == null || cardPrefab.GetComponent<GameSceneObjectOrderLayer>() == null)
                throw new InvalidOperationException("Restore the official timeline card sorting bindings before entering battle.");
            GameObject root = Instantiate(prefab);
            GameRoundScene scene = root.GetComponent<GameRoundScene>();
            scene.SetAttachUI(anchor, sceneCamera);
            var view = root.AddComponent<OfficialBattleSceneTimelineView>();
            view.assets = assets;
            view.cardPrefab = cardPrefab;
            view.slot = scene.IconRoot;
            view.portraitAssets = Resources.Load<BattleRoundPortraitAssets>("Combat/RoundUI/PortraitAssets");
            if (view.portraitAssets == null) throw new InvalidOperationException("Restore the original battle portrait configuration.");
            view.playerPortraitAtlas = new FightDynamicTextureETC2();
            view.playerPortraitAtlas.Initialize(view.portraitAssets.Configuration, view.portraitAssets.LoadTexture);
            var inactive = new GameObject("Native card injection");
            inactive.SetActive(false); inactive.transform.SetParent(hudRoot.transform, false);
            view.inactiveCards = inactive.transform;
            SpriteRenderer line = scene.Line;
            float width = line.size.x * line.transform.localScale.x;
            view.start = width / 2; view.end = -width / 2;
            hudRoot.AddComponent<OfficialBattleTimelineLifetime>().Initialize(root);
            return view;
        }

        public void Refresh(FightBattleCheckpoint checkpoint, Func<FightBattleUnitState, Sprite> portrait,
            Func<FightBattleUnitState, Sprite> faction, bool animate, FightBattleStepEvent step)
        {
            var all = checkpoint.Units.Where(u => u != null).ToDictionary(u => u.Id);
            var states = all.Values.Where(u => u.Hp > 0 && !u.Ended && IsSpawned(checkpoint, u.Id)
                && ShowOnRoundProgress(u)).ToDictionary(u => u.Id);
            var entryList = checkpoint.Timeline == null || checkpoint.Timeline.Entries == null
                ? new List<FightTurnEntry>()
                : checkpoint.Timeline.Entries.Where(e => e != null && states.ContainsKey(e.Id))
                    .OrderBy(e => e.Next).ToList();
            // FightRoundUI inserts CurActingEntityId before the sorted queue. The local
            // executor persists the same identity in Timeline.Acting while the completed
            // action is being presented, so the first card cannot be inferred from Next.
            // Keeping this order also preserves the official spacing when the acting entry's
            // Next value is later than another queued entry.
            if (checkpoint.Timeline != null && checkpoint.Timeline.Acting > 0)
            {
                int actingIndex = entryList.FindIndex(e => e.Id == checkpoint.Timeline.Acting);
                if (actingIndex > 0)
                {
                    FightTurnEntry acting = entryList[actingIndex];
                    entryList.RemoveAt(actingIndex);
                    entryList.Insert(0, acting);
                }
            }
            var entries = entryList.ToArray();
            // OnRoundDataInsert updates all positions immediately in the client.
            if (entries.Any(e => !cards.ContainsKey(e.Id))) animate = false;
            // Damage notifications precede role removal in GameRoundUI. Keep the
            // dying card available for its final Hurt notification as well.
            if (step?.DamageEvents != null)
                foreach (FightBattleDamageEvent damage in step.DamageEvents)
                    if (!damage.IsSkillCost && (damage.Damage < damage.ShieldDamage
                        || damage.Damage == damage.ShieldDamage && damage.HitEntityId > 0 && damage.DamageType != 0)
                        && cards.TryGetValue(damage.TargetId, out Card hitCard))
                        StartCoroutine(Hurt(hitCard, damage.Delay));
            var keep = new HashSet<int>(entries.Select(e => e.Id));
            foreach (int id in cards.Keys.Where(id => !keep.Contains(id)).ToArray())
            {
                Card old = cards[id]; cards.Remove(id);
                if (all.TryGetValue(id, out FightBattleUnitState dead) && dead.Hp <= 0)
                {
                    float delay = step?.DamageEvents == null ? 0 : step.DamageEvents.Where(d => d.TargetId == id && d.AfterHp == 0)
                        .Select(d => d.Delay).DefaultIfEmpty(0).Max();
                    StartCoroutine(Die(old, delay));
                }
                else Destroy(old.Root);
            }
            double total = entries.Length == 0 ? 0 : entries.Max(e =>
            {
                FightBattleUnitState unit = states[e.Id];
                return Math.Max(0, unit.Turn == null ? 0 : unit.Turn.IntervalTicks);
            });
            var times = new double[entries.Length];
            for (int i = 1; i < times.Length; i++) times[i] = times[i - 1] + Math.Max(entries[i].Next - entries[i - 1].Next, total * assets.MinDistance);
            double duration = times.Length == 0 ? total : Math.Max(times[times.Length - 1], total);
            for (int i = 0; i < entries.Length; i++)
            {
                FightBattleUnitState unit = states[entries[i].Id];
                bool created = !cards.TryGetValue(unit.Id, out Card card);
                if (created)
                {
                    GameObject root = Instantiate(cardPrefab, inactiveCards, false);
                    root.name = "GameRoundCharacterItem_" + unit.Id;
                    card = new Card { Root = root, Native = root.GetComponent<GameRoundCharacterItemScene>() };
                    cards.Add(unit.Id, card);
                    var summon = Resources.Load<OfficialSummonSkeletonData>("Combat/Summons/" + unit.MonsterId + "-data");
                    var context = new OfficialBattleRoundCardContext(unit,
                        unit.NpcClassId > 0 ? OfficialBattleNpcPortraitCatalog.Load().Find(unit.NpcClassId)
                        : summon != null ? summon.Portrait : portrait(unit), faction?.Invoke(unit), portraitAssets);
                    card.Context = context;
                    card.Native.Bind(context);
                    foreach (var selector in root.GetComponentsInChildren<GameRoundPlayerPortrait>(true))
                        selector.BindConfiguration(portraitAssets.Configuration);
                    foreach (var head in root.GetComponentsInChildren<HeadCharacterPortraitETC2Scene>(true))
                    { head.Bind(context); head.BindAtlas(playerPortraitAtlas); }
                    foreach (var handler in root.GetComponentsInChildren<GameRoundPortraitETC2SceneHandler>(true))
                        handler.BindAtlas(playerPortraitAtlas);
                    root.SetActive(false);
                    root.transform.SetParent(slot, false);
                    root.SetActive(true);
                    card.Native.Init(entries[i], i == 0, context.Role);
                    card.Native.Animator.Rebind(); card.Native.Animator.Update(0);
                }
                bool acting = i == 0;
                card.Native.UpdateRoundData(entries[i]);
                if (created || card.Acting != acting)
                {
                    if (!created) card.Native.Refresh(entries[i], acting, card.Context.Role);
                    if (acting) card.Native.SetActing(); else card.Native.SetIdle();
                    card.Acting = acting;
                }
                card.From = card.Root.transform.localPosition;
                card.Target = new Vector3(Mathf.Lerp(start, end, duration < .00001 ? 0 : (float)(times[i] / duration)), 0, 0);
                card.Move = card.From.x <= card.Target.x ? card.Target - card.From
                    : card.Target - new Vector3(end, 0, 0) + new Vector3(start, 0, 0) - card.From;
                if (created || !animate) card.Root.transform.localPosition = card.Target;
                if (created) { card.From = card.Target; card.Move = Vector3.zero; }
            }
            // All cards must exist before assigning the final reverse order.
            // Unity clamps an index beyond the current child count during insertion.
            for (int i = 0; i < entries.Length; i++)
                cards[entries[i].Id].Native.SetSiblingIndex(entries.Length - i - 1);
            moving = animate && entries.Length > 0; moveTime = 0;
        }

        private void OnDestroy()
        {
            playerPortraitAtlas?.Dispose();
            playerPortraitAtlas = null;
        }

        private static bool IsSpawned(FightBattleCheckpoint checkpoint, int id)
        {
            if (checkpoint.Waves == null) return true;
            foreach (FightBattleWaveState wave in checkpoint.Waves)
                if (wave?.UnitIds != null && wave.UnitIds.Contains(id)) return wave.Spawned;
            return true;
        }

        private static bool ShowOnRoundProgress(FightBattleUnitState unit)
        {
            var summon = Resources.Load<OfficialSummonSkeletonData>("Combat/Summons/" + unit.MonsterId + "-data");
            return summon == null || summon.Source.showOnRoundProgress;
        }

        private IEnumerator Hurt(Card card, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (card.Root != null) card.Native.SetHurt();
        }
        private IEnumerator Die(Card card, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (card.Root == null) yield break;
            card.Native.SetDead(() => { if (card.Root != null) Destroy(card.Root); });
        }
        private void Update()
        {
            if (!moving) return;
            moveTime = Mathf.Min(assets.MoveDuration, moveTime + Time.deltaTime);
            float percent = moveTime / assets.MoveDuration;
            foreach (Card card in cards.Values)
            {
                Vector3 value = card.From + card.Move * percent;
                if (value.x > start) value.x -= start - end;
                card.Root.transform.localPosition = percent >= 1 ? card.Target : value;
            }
            if (percent >= 1) moving = false;
        }
    }
}

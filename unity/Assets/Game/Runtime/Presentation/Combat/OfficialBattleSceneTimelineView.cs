using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
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
            public Animator Animator;
            public bool Acting;
            public Vector3 From, Move, Target;
        }
        private readonly Dictionary<int, Card> cards = new Dictionary<int, Card>();
        private OfficialBattleTimelineAssets assets;
        private RectTransform attach;
        private Canvas canvas;
        private Transform slot;
        private float start, end, moveTime;
        private bool moving;

        public static OfficialBattleSceneTimelineView Mount(GameObject hudRoot)
        {
            var assets = OfficialBattleTimelineAssets.Load();
            RectTransform anchor = hudRoot.transform.Find(assets.Anchor).GetComponent<RectTransform>();
            GameObject root = Instantiate(assets.ScenePrefab);
            var view = root.AddComponent<OfficialBattleSceneTimelineView>();
            view.assets = assets;
            view.attach = anchor;
            view.canvas = anchor.GetComponentInParent<Canvas>();
            view.slot = root.transform.Find(assets.SceneBindings.slot);
            SpriteRenderer line = root.transform.Find(assets.SceneBindings.line).GetComponent<SpriteRenderer>();
            float width = line.size.x * line.transform.localScale.x;
            view.start = width / 2; view.end = -width / 2;
            hudRoot.AddComponent<OfficialBattleTimelineLifetime>().Initialize(root);
            view.LateUpdate();
            return view;
        }

        public void Refresh(FightBattleCheckpoint checkpoint, Func<FightBattleUnitState, Sprite> portrait,
            Func<FightBattleUnitState, Sprite> faction, bool animate, FightBattleStepEvent step)
        {
            var all = checkpoint.Units.Where(u => u != null).ToDictionary(u => u.Id);
            var states = all.Values.Where(u => u.Hp > 0 && !u.Ended && IsSpawned(checkpoint, u.Id)
                && ShowOnRoundProgress(u)).ToDictionary(u => u.Id);
            var entries = checkpoint.Timeline.Entries.Where(e => e != null && states.ContainsKey(e.Id)).ToArray();
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
            double total = states.Count == 0 ? 0 : states.Values.Max(u => Math.Max(0, u.Turn.IntervalTicks));
            var times = new double[entries.Length];
            for (int i = 1; i < times.Length; i++) times[i] = times[i - 1] + Math.Max(entries[i].Next - entries[i - 1].Next, total * assets.MinDistance);
            double duration = times.Length == 0 ? total : Math.Max(times[times.Length - 1], total);
            for (int i = 0; i < entries.Length; i++)
            {
                FightBattleUnitState unit = states[entries[i].Id];
                bool created = !cards.TryGetValue(unit.Id, out Card card);
                if (created)
                {
                    GameObject root = Instantiate(assets.CardPrefab, slot, false);
                    root.name = "GameRoundCharacterItem_" + unit.Id;
                    card = new Card { Root = root, Animator = root.GetComponent<Animator>() };
                    cards.Add(unit.Id, card);
                    var summon = Resources.Load<OfficialSummonSkeletonData>("Combat/Summons/" + unit.MonsterId + "-data");
                    BindPortrait(root, unit, unit.NpcClassId > 0 ? OfficialBattleNpcPortraitCatalog.Load().Find(unit.NpcClassId)
                        : summon != null ? summon.Portrait : portrait(unit), faction?.Invoke(unit));
                    card.Animator.Rebind(); card.Animator.Update(0);
                }
                bool acting = i == 0;
                if (created || card.Acting != acting)
                {
                    card.Animator.ResetTrigger(acting ? "Idle" : "Acting");
                    card.Animator.SetTrigger(acting ? "Acting" : "Idle");
                    card.Acting = acting;
                }
                card.From = card.Root.transform.localPosition;
                card.Target = new Vector3(Mathf.Lerp(start, end, duration < .00001 ? 0 : (float)(times[i] / duration)), 0, 0);
                card.Move = card.From.x <= card.Target.x ? card.Target - card.From
                    : card.Target - new Vector3(end, 0, 0) + new Vector3(start, 0, 0) - card.From;
                if (created || !animate) card.Root.transform.localPosition = card.Target;
                if (created) { card.From = card.Target; card.Move = Vector3.zero; }
                card.Root.transform.SetSiblingIndex(entries.Length - i - 1);
            }
            moving = animate && entries.Length > 0; moveTime = 0;
        }

        private void BindPortrait(GameObject card, FightBattleUnitState unit, Sprite portrait, Sprite faction)
        {
            bool npc = unit.NpcClassId > 0, monster = !npc && unit.MonsterId > 0;
            var binding = assets.CardBindings;
            card.transform.Find(binding.mainPlayerEffect).gameObject.SetActive(unit.Id == 1);
            card.transform.Find(binding.playerRoot).gameObject.SetActive(!npc && !monster);
            card.transform.Find(binding.monster).gameObject.SetActive(monster);
            card.transform.Find(binding.npcRoot).gameObject.SetActive(npc);
            if (portrait == null) throw new InvalidOperationException("Official battle timeline portrait is missing for unit " + unit.Id);
            if (monster)
            {
                card.transform.Find(binding.monster).GetComponent<SpriteRenderer>().sprite = portrait;
                card.transform.Find(binding.monsterHurt).GetComponent<SpriteRenderer>().sprite = portrait;
            }
            else
            {
                string path = npc ? binding.npc : binding.player;
                card.transform.Find(path).GetComponent<WorldCircleSpriteRenderer>().SetSprite(portrait);
            }
            if (faction != null) card.transform.Find(binding.faction).GetComponent<SpriteRenderer>().sprite = faction;
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
            if (card.Root != null) card.Animator.SetTrigger("Hurt");
        }
        private IEnumerator Die(Card card, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (card.Root == null) yield break;
            card.Animator.SetTrigger("Dead");
            yield return new WaitForSeconds(assets.DeadTime);
            if (card.Root != null) Destroy(card.Root);
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
        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (attach == null || canvas == null || camera == null) return;
            // Overlay canvases use a null UI camera on Windows. The official
            // screen-to-world anchor and camera-size scaling remain unchanged.
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, attach.position);
            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(camera.transform.position.z)));
            world.z = assets.Z;
            transform.position = world;
            var scaler = canvas.GetComponent<CanvasScaler>();
            RectTransform canvasRect = canvas.transform as RectTransform;
            float canvasHeight = canvasRect == null ? 0f : canvasRect.rect.height;
            if (scaler == null || scaler.referenceResolution.y <= 0f || canvasHeight <= Mathf.Epsilon
                || assets.SceneSize <= Mathf.Epsilon || camera.orthographicSize <= Mathf.Epsilon) return;
            float canvasScale = scaler.referenceResolution.y / canvasHeight;
            float scale = camera.orthographicSize / assets.SceneSize * canvasScale;
            if (float.IsNaN(scale) || float.IsInfinity(scale)) return;
            transform.localScale = Vector3.one * scale;
        }
    }
}

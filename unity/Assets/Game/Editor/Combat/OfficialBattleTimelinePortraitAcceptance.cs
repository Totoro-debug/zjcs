using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleTimelinePortraitAcceptance
    {
        private sealed class Fixture : IDisposable
        {
            private readonly GameObject canvas;
            private readonly Texture2D temporaryTexture;
            private readonly Sprite temporarySprite;
            private readonly OfficialBattleSceneTimelineView timeline;
            private readonly OfficialBattleHudView view;
            private readonly FightBattleCheckpoint checkpoint;
            private readonly Func<FightBattleUnitState, Sprite> portrait;

            public Fixture(Sprite playerPortrait = null)
            {
                if (playerPortrait == null)
                {
                    temporaryTexture = new Texture2D(4, 4);
                    temporarySprite = Sprite.Create(temporaryTexture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
                    playerPortrait = temporarySprite;
                }
                canvas = new GameObject("BattleTimelinePortraitAcceptance", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 1;
                var prior = UnityEngine.Object.FindObjectsOfType<OfficialBattleSceneTimelineView>(true);
                view = new OfficialBattleHudView(OfficialBattleHudCatalog.LoadDefault());
                view.Mount(canvas.transform);
                foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
                timeline = UnityEngine.Object.FindObjectsOfType<OfficialBattleSceneTimelineView>(true).Except(prior).Single();
                checkpoint = new FightBattleCheckpoint();
                AddUnit(checkpoint, 1, 0, 0);
                AddUnit(checkpoint, 2, 3206, 0);
                AddUnit(checkpoint, 3, 0, 8401);
                AddUnit(checkpoint, 4, 0, 8411);
                AddUnit(checkpoint, 5, 0, 11001);
                OfficialBattleMonsterPortraitCatalog monsters = OfficialBattleMonsterPortraitCatalog.TryLoadDefault();
                OfficialBattleNpcPortraitCatalog npcs = OfficialBattleNpcPortraitCatalog.Load();
                // The official NPC branch resolves NpcInfo.Icon independently of player/monster callbacks.
                portrait = unit => unit.MonsterId > 0 ? monsters.Find(unit.MonsterId) : playerPortrait;
                view.RefreshTimeline(checkpoint, portrait, null, false);
                Transform player = Card(1).Find("IconGo/PlayerHeadETC2Go/HeadImg");
                if (player.GetComponent<WorldCircleSpriteRenderer>().Sprite != playerPortrait)
                    throw new InvalidOperationException("Player circle did not bind its resolved head texture.");
                if (Card(2).Find("IconGo/MonsterIconImg").GetComponent<SpriteRenderer>().sprite.name != "G_3206")
                    throw new InvalidOperationException("Monster IconId 3206 did not bind G_3206.");
                foreach (int id in new[] { 3, 4, 5 })
                {
                    Transform card = Card(id);
                    Transform node = card.Find("IconGo/NpcIconGO/NpcIconImg");
                    int npcId = checkpoint.Units.Single(u => u.Id == id).NpcClassId;
                    var circle = node.GetComponent<WorldCircleSpriteRenderer>();
                    var block = new MaterialPropertyBlock();
                    node.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                    if (!node.gameObject.activeInHierarchy || circle.Sprite != npcs.Find(npcId)
                        || block.GetTexture("_MainTex") != circle.Sprite.texture
                        || node.GetComponent<MeshFilter>().sharedMesh.vertexCount != 33
                        || card.Find("IconGo/PlayerHeadETC2Go").gameObject.activeSelf
                        || card.Find("IconGo/MonsterIconImg").gameObject.activeSelf)
                        throw new InvalidOperationException("NPC class " + npcId + " did not bind its separate original circular portrait.");
                }
            }

            public IEnumerator VerifyActionOrder()
            {
                var timelineAssets = OfficialBattleTimelineAssets.Load();
                float width = timelineAssets.ScenePrefab.transform.Find(timelineAssets.SceneBindings.line)
                    .GetComponent<SpriteRenderer>().size.x * timelineAssets.ScenePrefab.transform
                    .Find(timelineAssets.SceneBindings.line).localScale.x;
                void CheckFirst(int id)
                {
                    Transform card = Card(id);
                    if (Mathf.Abs(card.localPosition.x - width / 2f) > .00001f
                        || card.GetSiblingIndex() != card.parent.childCount - 1
                        || !card.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0)
                            .IsName("UIGameRound_Acting"))
                        throw new InvalidOperationException("Current action portrait is not first/Acting: " + id);
                }
                // Settled step: the actor was already reinserted behind its opponent.
                checkpoint.Timeline.Acting = 3;
                checkpoint.Timeline.Entries.Single(entry => entry.Id == 3).Next = 20000;
                view.RefreshTimeline(checkpoint, portrait, null, true);
                yield return new WaitForSecondsRealtime(.4f);
                CheckFirst(3);
                string output = Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_OUTPUT");
                if (!string.IsNullOrEmpty(output))
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "timeline-current-actor.png"));
                    yield return new WaitForSecondsRealtime(.3f);
                }
                if (checkpoint.Timeline.Entries.First().Id != 1
                    || checkpoint.Timeline.Entries.Single(entry => entry.Id == 3).Next != 20000)
                    throw new InvalidOperationException("Timeline presentation changed the authoritative queue.");
                checkpoint.Timeline.Acting = 2;
                view.RefreshTimeline(checkpoint, portrait, null, true);
                yield return new WaitForSecondsRealtime(.4f);
                CheckFirst(2);
                // Restore the serialized identity while its future entry remains later.
                var restored = JsonUtility.FromJson<FightBattleCheckpoint>(JsonUtility.ToJson(checkpoint));
                restored.Timeline.Acting = 3;
                view.RefreshTimeline(restored, portrait, null, false);
                yield return new WaitForSecondsRealtime(.4f);
                CheckFirst(3);
                restored.Units.Single(unit => unit.Id == 3).Hp = 0;
                view.RefreshTimeline(restored, portrait, null, true);
                yield return new WaitForSecondsRealtime(.8f);
                CheckFirst(1);
                if (Card(3) != null) throw new InvalidOperationException("Dead acting portrait was retained.");
                restored.Timeline.Acting = 0;
                restored.Timeline.Entries.Clear();
                view.RefreshTimeline(restored, portrait, null, false);
                yield return null;
                if (timeline.transform.Find("Slot").childCount != 0)
                    throw new InvalidOperationException("Empty timeline retained portraits.");
                Debug.Log("Battle timeline action order passed: settled actor, switch, original Acting state, immutable queue, serialized restore, dead actor fallback and empty queue.");
            }

            private Transform Card(int id) => timeline.transform.Find("Slot/GameRoundCharacterItem_" + id);
            public void Dispose()
            {
                Remove(timeline == null ? null : timeline.gameObject);
                Remove(canvas); Remove(temporarySprite); Remove(temporaryTexture);
            }
        }

        [MenuItem("Zjcs/Combat/Validate Timeline Portrait Binding")]
        public static void Run()
        {
            OfficialBattleNpcPortraitTools.Validate();
            using (var fixture = new Fixture()) { }
            Debug.Log("Battle timeline portrait binding passed: player, G_3206 and NPC 8401/8411/11001.");
        }

        public static IEnumerator VerifyNpcInPlayMode()
        {
            var existing = UnityEngine.Object.FindObjectsOfType<OfficialBattleSceneTimelineView>();
            Sprite portrait = existing.SelectMany(v => v.GetComponentsInChildren<WorldCircleSpriteRenderer>())
                .First(c => c.name == "HeadImg").Sprite;
            foreach (var view in existing) view.gameObject.SetActive(false);
            try
            {
                using (var fixture = new Fixture(portrait))
                {
                    yield return new WaitForSecondsRealtime(.5f);
                    string output = Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_OUTPUT");
                    if (!string.IsNullOrEmpty(output))
                    {
                        Directory.CreateDirectory(output);
                        ScreenCapture.CaptureScreenshot(Path.Combine(output, "timeline-npcs.png"));
                    }
                    yield return new WaitForSecondsRealtime(.3f);
                    yield return fixture.VerifyActionOrder();
                }
                yield return null;
                Debug.Log("NPC timeline portrait Play Mode verification passed: three original NPC icons, circular meshes and exclusive branches.");
            }
            finally
            {
                foreach (var view in existing) if (view != null) view.gameObject.SetActive(true);
            }
        }

        private static void AddUnit(FightBattleCheckpoint checkpoint, int id, int monsterId, int npcClassId)
        {
            var unit = new FightBattleUnitState { Id = id, MonsterId = monsterId, NpcClassId = npcClassId, Side = 1, Hp = 1, MaxHp = 1 };
            unit.Turn.Id = id; unit.Turn.Hp = 1; unit.Turn.Speed = 100; unit.Turn.IntervalTicks = 10000;
            checkpoint.Units.Add(unit);
            checkpoint.Timeline.Entries.Add(new FightTurnEntry { Id = id, Next = (id - 1) * 2500 });
        }

        private static void Remove(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}

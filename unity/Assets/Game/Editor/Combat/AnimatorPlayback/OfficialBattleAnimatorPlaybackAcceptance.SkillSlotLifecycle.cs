using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Presentation.Combat;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        private static int skillSlotProbe;

        private static IEnumerator CheckSkillSlotLifecycle(GameObject actor, GameObject target,
            GameObject prefab, Func<int, float, IEnumerator> pump)
        {
            const float delta = 1f / 64;
            int probe = ++skillSlotProbe;
            var world = new GameObject("Shared skill slot lifecycle");
            var canvas = new GameObject("Original skill slot HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var output = new GameObject("Skill slot HUD output", typeof(Camera)).GetComponent<Camera>();
            var texture = new RenderTexture(675, 1463, 24);
            output.orthographic = true; output.clearFlags = CameraClearFlags.SolidColor;
            output.backgroundColor = new Color(.15f, .18f, .2f); output.cullingMask = ~0;
            output.transform.position = new Vector3(0, 0, -10); output.targetTexture = texture;
            var ui = canvas.GetComponent<Canvas>(); ui.renderMode = RenderMode.ScreenSpaceCamera;
            ui.worldCamera = output; ui.planeDistance = 1;
            var scaler = canvas.GetComponent<CanvasScaler>();
            // Same window adaptation as the production HomeHudView parent canvas.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var catalog = OfficialBattleHudCatalog.LoadDefault();
            var view = new OfficialBattleHudView(catalog);
            var lateView = new OfficialBattleHudView(catalog);
            var checkpoint = new FightBattleCheckpoint();
            var source = new FightBattleUnitState { Id = 1, CharacterType = 1, Hp = 100, MaxHp = 100, Direction = 3 };
            source.Skills.Add(new FightBattleSkillDefinition { Slot = 0, ItemId = 10032 });
            source.Skills.Add(new FightBattleSkillDefinition { Slot = 2, ItemId = 10032 });
            source.SkillState.IsDriving = true; source.SkillState.Round = 1;
            source.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, Cooldown = 3, LastRound = 1 });
            checkpoint.Units.Add(source);
            checkpoint.Units.Add(new FightBattleUnitState { Id = 2, MonsterId = 10001, Direction = 3 });
            Sprite icon = catalog.FindSprite("C_none-fa3e084e40.png");
            Require(icon, "existing official HUD icon is available to the isolated timing fixture");
            view.Mount(canvas.transform);
            view.RefreshPhase(FightBattlePhase.Play, null);
            view.Refresh(checkpoint);
            view.RefreshPetFunction(false);
            view.RefreshSkillSlots(checkpoint, _ => icon);
            var bindings = OfficialBattleSkillSlotBindings.Load();
            var borrowed = target.GetComponent<MyFightSkillAgent>(); bool ownsBorrowed = !borrowed;
            if (!borrowed) borrowed = target.AddComponent<MyFightSkillAgent>();
            int externalNotifications = 0;
            Action<MyFightSkill> external = _ => externalNotifications++;
            borrowed.OnSkillApplied += external;
            var mapper = new OfficialBattleGridMapper(world.transform, new FightBattleGrid { Width = 8, Height = 8 });
            using var scope = new OfflineBattleNativePlayableScope(world.transform, null);
            using var entities = new OfficialBattleSkillEntityPlayback(world.transform, mapper, scope,
                id => checkpoint.Units.SingleOrDefault(unit => unit.Id == id));
            using var playback = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), scope, entities);
            var notifications = new List<string>();
            entities.SkillStarted += view.OnSkillStarted; entities.SkillEnded += view.OnSkillEnded;
            entities.SkillStarted += lateView.OnSkillStarted; entities.SkillEnded += lateView.OnSkillEnded;
            entities.SkillStarted += (unit, slot) => notifications.Add($"{unit}/{slot}/start");
            entities.SkillEnded += (unit, slot) => notifications.Add($"{unit}/{slot}/end");
            FightBattleSkillCastEvent Cast(int unit, int slot, float duration) => new FightBattleSkillCastEvent {
                ActorId = unit, Slot = slot, SkillEntityId = 11170, TargetIds = new List<int> { unit == 1 ? 2 : 1 },
                Presentation = new FightBattleSkillCastPresentation { TargetId = unit == 1 ? 2 : 1,
                    TargetX = 1, FacingIndex = 3, SkillTargetType = 1, Duration = duration } };
            IEnumerator Pump(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    playback.Tick(delta); playback.LateTick();
                    yield return pump(1, delta);
                }
            }
            void State(OfficialBattleHudView hud, int slot, int state)
            {
                var binding = bindings.slots[slot];
                string[] selected = binding.states.FirstOrDefault(value => value.state == state)?.objects ?? Array.Empty<string>();
                foreach (string path in binding.states.SelectMany(value => value.objects).Distinct())
                    Require(hud.Find(path).activeSelf == selected.Contains(path), $"live original slot {slot} state {state}: {path}");
            }
            void Capture(string phase)
            {
                Canvas.ForceUpdateCanvases(); output.Render();
                var previous = RenderTexture.active; RenderTexture.active = texture;
                var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                try
                {
                    pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                        $"skill-slot-{probe}-{phase}.png"), pixels.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; Object.Destroy(pixels); }
            }
            try
            {
                // Let the original HUD entrance animation finish before checking
                // cast timing; otherwise its skill row is still below the camera.
                yield return Pump(64);
                // A player trigger can belong to another unit's step. Only the
                // actual source agent and its creation/removal times drive the UI.
                var cast = Cast(1, 0, .5f); cast.Delay = .25f;
                var step = new FightBattleStepEvent { ActorId = 2 }; step.SkillCasts.Add(cast);
                playback.Play(step, checkpoint, id => id == 1 ? actor.transform : target.transform);
                playback.Tick(0); State(view, 0, 3);
                yield return Pump(15); State(view, 0, 3); Capture("waiting-cd");
                yield return Pump(1); State(view, 0, 2);
                Require(notifications.SequenceEqual(new[] { "1/0/start" }), "delayed original start forwarded exactly once");
                view.RefreshSkillSlots(checkpoint, _ => icon); State(view, 0, 2);
                GameObject lateRoot = lateView.Mount(canvas.transform);
                lateView.RefreshPhase(FightBattlePhase.Play, null);
                lateView.Refresh(checkpoint);
                lateView.RefreshPetFunction(false);
                lateView.RefreshSkillSlots(checkpoint, _ => icon); State(lateView, 0, 2); lateRoot.SetActive(false);
                Capture("applied");
                // Original empty skill 10100 isolates the agent filtering and
                // negative-slot branches. The primary cast above uses 11170.
                var foreign = Cast(2, 2, .0625f); foreign.SkillEntityId = 10100;
                entities.Start(foreign, target.transform, actor.transform);
                MyFightSkill other = world.GetComponentsInChildren<MyFightSkill>().Last();
                var triggered = Cast(1, -1, .0625f); triggered.SkillEntityId = 10100;
                entities.Start(triggered, actor.transform, target.transform);
                State(view, 2, 1);
                yield return Pump(31); State(view, 0, 2); State(view, 2, 1);
                yield return Pump(1); State(view, 0, 4); State(lateView, 0, 4); Capture("ended");
                Require(notifications.SequenceEqual(new[] { "1/0/start", "2/2/start", "2/2/end", "1/0/end" }),
                    "original finite ends, foreign owner and negative slot branches preserve notification order");
                view.RefreshSkillSlots(checkpoint, _ => icon); State(view, 0, 3);
                // Original entity 10100 declares no directional/default graph.
                // It must still traverse the shared dispatcher and original agent.
                var empty = Cast(1, 0, .03125f); empty.SkillEntityId = 10100; empty.Delay = .03125f;
                var emptyStep = new FightBattleStepEvent { ActorId = 1 }; emptyStep.SkillCasts.Add(empty);
                int beforeEmpty = notifications.Count;
                playback.Play(emptyStep, checkpoint, id => id == 1 ? actor.transform : target.transform);
                yield return Pump(1); State(view, 0, 3);
                Require(notifications.Count == beforeEmpty, "known empty graph retains its delay");
                yield return Pump(1); State(view, 0, 2);
                Require(world.GetComponentsInChildren<MyFightSkill>().Any(value => value.myEntity.Entity.ClassId == 10100 && value.myPlayablePrefab == null),
                    "known empty graph created its original skill entity");
                yield return Pump(2); State(view, 0, 4);
                Require(notifications.Skip(beforeEmpty).SequenceEqual(new[] { "1/0/start", "1/0/end" }),
                    "known empty graph forwards original finite lifecycle exactly once");
                int forwarded = notifications.Count;
                playback.Dispose();
                borrowed.SendSkillAppliedEvent(other);
                Require(notifications.Count == forwarded && externalNotifications == 2,
                    "release detaches borrowed-agent subscriptions and preserves its external observer");
                yield return null;
                Require(target.GetComponent<MyFightSkillAgent>() == borrowed, "shared teardown retained the borrowed original agent");
                Debug.Log($"[Original skill slot lifecycle] probe {probe}: delayed cast, exact finite end, trigger owner, child slot exclusion, CD, active refresh, delayed HUD mount known empty graph dispatch and borrowed-agent release passed.");
            }
            finally
            {
                playback.Dispose(); borrowed.OnSkillApplied -= external;
                if (ownsBorrowed) Object.Destroy(borrowed);
                Object.Destroy(canvas); output.targetTexture = null; Object.Destroy(output.gameObject);
                texture.Release(); Object.Destroy(texture); Object.Destroy(world);
            }
        }
    }
}

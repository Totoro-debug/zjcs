using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattlePreparationAcceptance
    {
        public static IEnumerator Verify(OfficialBattleRuntimeController controller)
        {
            var checkpoint = controller.Checkpoint;
            Require(controller.Phase == FightBattlePhase.Preparing && checkpoint.PreparationPending,
                "Preparation was not recorded in the checkpoint.");
            var pointer = UnityEngine.Object.FindObjectsOfType<BoxCollider>()
                .FirstOrDefault(value => value.transform.root != null && value.GetComponentInParent<Transform>() != null
                    && value.transform.parent != null && value.transform.parent.name.StartsWith("PrepareBattlePlayerPointer"));
            Require(pointer != null, "The original player preparation collider is not active.");
            Transform ground = pointer.transform.parent.parent;
            var mapper = new OfficialBattleGridMapper(ground, checkpoint.Grid);
            var player = checkpoint.Units.First(value => value.Id == 1);
            var start = new FightGridPoint(player.X, player.Y);
            var candidates = new[] { new FightGridPoint(start.X + 1, start.Y),
                new FightGridPoint(start.X - 1, start.Y), new FightGridPoint(start.X, start.Y + 1),
                new FightGridPoint(start.X, start.Y - 1) };
            // Use the exact runtime range and occupancy acceptance, without calling its mutation.
            var view = typeof(OfficialBattleRuntimeController).GetField("preparationView",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller);
            var positions = (FightBattlePreparationPositions)view.GetType().GetField("positions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(view);
            FightGridPoint target = start;
            foreach (var point in candidates)
                if (positions.TryPreview(1, point, out FightGridPoint preview) && !preview.Equals(start))
                { target = preview; break; }
            Require(!target.Equals(start), "No second legal preparation grid is available for input validation.");
            Camera camera = Camera.main;
            Require(camera != null, "Preparation has no scene camera.");
            Vector2 down = camera.WorldToScreenPoint(pointer.transform.TransformPoint(pointer.center));
            Vector2 drop = camera.WorldToScreenPoint(mapper.GridToWorld(target));
            int changed = 0;
            Action onChanged = () => changed++;
            controller.PreparationPositionChanged += onChanged;
            try
            {
                var frame = new OfficialBattlePointerFrame { Position = down, Down = true, Held = true };
                controller.PreparationPointerProvider = () => frame;
                yield return null;
                frame.Down = false;
                frame.Position = drop;
                yield return new WaitForSecondsRealtime(.12f);
                frame.Held = false; frame.Up = true;
                yield return null;
                frame.Up = false;
                yield return null;
                Require(player.X == target.X && player.Y == target.Y && changed == 1,
                    "Simulated raycast drag did not commit exactly one legal preparation move.");
                var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                Require(restored.PreparationPending && restored.Units.First(value => value.Id == 1).X == target.X
                    && restored.Units.First(value => value.Id == 1).Y == target.Y,
                    "Preparation checkpoint lost its phase or changed position.");
                Debug.Log("Official preparation input passed: original collider raycast, held drag, legal drop, one save event and preparation checkpoint.");
            }
            finally
            {
                controller.PreparationPointerProvider = null;
                controller.PreparationPositionChanged -= onChanged;
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}

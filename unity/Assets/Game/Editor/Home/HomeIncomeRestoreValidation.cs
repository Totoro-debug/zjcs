using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Home;
using Zjcs.Offline.State;

namespace Zjcs.Editor.Home
{
    public static class HomeIncomeRestoreValidation
    {
        private static readonly Dictionary<int, long> Properties = new Dictionary<int, long>();
        private static readonly HashSet<int> FloatProperties = new HashSet<int>();
        private static readonly OfflineHomeRandomRewardDefinition[] NoRewards = Array.Empty<OfflineHomeRandomRewardDefinition>();

        public static void Run()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(workspace)) throw new InvalidOperationException("An isolated validation workspace is required.");
            string root = Path.Combine(workspace, "income-saves");
            if (Directory.Exists(root)) throw new IOException("Income validation requires fresh saves.");
            Directory.CreateDirectory(root);
            try
            {
                var slots = new ProfileSlotCatalog(root);
                string slot = slots.CreateSlot();
                const long startedAt = 1000000;
                OfflineHomeIncomeSession initial = Open(slots, slot, startedAt);
                Require(initial.Mutate("seed", save =>
                {
                    save.Furniture.Add(new OfflineHomeFurnitureEntry { Type = 0, Owned = true });
                    save.HangUps.Add(new OfflineHomeHangUpEntry { HangUpId = 1, Unlocked = true, Level = 3 });
                }), "seed saved unlock and level");
                Require(initial.Preview(1).Count == 0, "new save has no epoch income");

                OfflineHomeIncomeSession restored = Open(slots, slot, startedAt + 3601);
                Require(restored.Preview(1).Count == 360, "offline hour uses restored unlock and level");
                Require(restored.Collect(1, "collect") == 360, "offline reward is collectible");
                Require(restored.Save.Inventory.Single().Count == 360, "reward enters durable ledger");
                Require(Math.Abs(restored.Save.Income.Single().StoredSeconds - 1) < 0.00001, "fractional time survives collection");
                Require(restored.Collect(1, "collect") == 0, "replayed receipt grants nothing");
                OfflineHomeIncomeSession reopened = Open(slots, slot, startedAt + 3601);
                Require(reopened.Save.Inventory.Single().Count == 360 && reopened.Preview(1).Count == 0,
                    "same checkpoint cannot duplicate offline reward");
                Require(Open(slots, slot, startedAt).Save.LastLocalUnixSeconds == startedAt + 3601,
                    "clock rollback cannot regress checkpoint");

                string cappedSlot = slots.CreateSlot();
                OfflineHomeIncomeSession capped = Open(slots, cappedSlot, startedAt);
                Require(capped.Mutate("seed", save => save.HangUps.Add(
                    new OfflineHomeHangUpEntry { HangUpId = 1, Unlocked = true, Level = 1 })), "seed capacity case");
                capped = Open(slots, cappedSlot, startedAt + 40 * 3600);
                Require(capped.Preview(1).Count == 36 * 120, "offline restore respects 36-hour capacity");

                string savePath = slots.GetHomePath(slot);
                string before = File.ReadAllText(savePath);
                bool rejected = false;
                try
                {
                    OfflineHomeIncomeSession.Open(slots, slot, new[] { Placeholder() }, Properties, FloatProperties,
                        NoRewards, startedAt + 7200, _ => throw new InvalidOperationException("configuration failed"));
                }
                catch (InvalidOperationException error) when (error.Message == "configuration failed") { rejected = true; }
                Require(rejected && File.ReadAllText(savePath) == before, "configuration failure preserves checkpoint and disk save");
                Debug.Log("[HomeIncomeRestore] PASS: restored unlock/level, offline accumulation, capacity, remainder, receipt, reopen, rollback and configuration failure.");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }

        private static OfflineHomeIncomeSession Open(ProfileSlotCatalog slots, string slot, long now)
        {
            return OfflineHomeIncomeSession.Open(slots, slot, new[] { Placeholder() }, Properties, FloatProperties,
                NoRewards, now, restored => restored.Reconfigure(new[] { new OfflineHomeIncomeDefinition
                {
                    HangUpId = 1, OutputItemId = 50, Unlocked = restored.IsHangUpUnlocked(1),
                    Rule = new HangUpIncomeRule { HourlyOutput = 120, LevelNum = restored.HangUpLevel(1), MaxStoreHours = 36 }
                } }, Properties, FloatProperties, NoRewards));
        }

        private static OfflineHomeIncomeDefinition Placeholder() => new OfflineHomeIncomeDefinition
        {
            HangUpId = 1, OutputItemId = 50,
            Rule = new HangUpIncomeRule { LevelNum = 1, MaxStoreHours = 36 }
        };

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Home income restore: " + message);
        }
    }
}

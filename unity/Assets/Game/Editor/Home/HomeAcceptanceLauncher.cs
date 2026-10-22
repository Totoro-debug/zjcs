using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Editor.Home
{
    /// <summary>
    /// Launches the home Play Mode acceptance run:
    /// Unity.exe -projectPath unity -executeMethod Zjcs.Editor.Home.HomeAcceptanceLauncher.Run -homeAcceptance
    /// The run creates an isolated profile, enters Play Mode, captures screenshots
    /// and exits the editor with the result code.
    /// </summary>
    public static class HomeAcceptanceLauncher
    {
        private static string Root => Path.Combine(Path.GetTempPath(), "zjcs-home-acceptance");

        public static void Run()
        {
            try
            {
                PrepareProfile();
                Debug.Log("[HomeAcceptance] entering play mode with " + Root);
                EditorApplication.isPlaying = true;
            }
            catch (Exception error)
            {
                Debug.LogError("[HomeAcceptance] launcher failed: " + error);
                EditorApplication.Exit(1);
            }
        }

        private static void PrepareProfile()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
            Directory.CreateDirectory(Root);
            var slots = new ProfileSlotCatalog(Root);
            string slotId = slots.CreateSlot();
            OfflineMainlineProgressSession session = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            ProgressionResult result = session.ApplyCharacterMutation(player =>
            {
                player.CharacterName = "验收角色";
                player.CharacterCreated = true;
                player.InitialWeaponType = 200;
                player.Experience = 100000;
                player.Level = 20;
                player.SubRank = 3;
                player.Rank = 1;
                player.MaxProfessionRank = 1;
                player.Wardrobe.Profession = 1;
                if (player.UnlockedMapClassIds != null && !player.UnlockedMapClassIds.Contains(11))
                    player.UnlockedMapClassIds.Add(11);
            }, "acceptance:character:" + slotId);
            if (result.Status != ProgressionResultStatus.Applied
                && result.Status != ProgressionResultStatus.Unchanged
                && result.Status != ProgressionResultStatus.Ready)
                throw new InvalidOperationException("The acceptance profile could not be created: " + result.Status);
            Debug.Log("[HomeAcceptance] profile " + slotId + " ready at " + Root);
        }
    }
}

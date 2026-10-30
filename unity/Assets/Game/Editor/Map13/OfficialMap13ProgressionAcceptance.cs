using System;
using System.IO;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.World;
using UnityEngine;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13ProgressionAcceptance
    {
        public static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "zjcs-map13-progress-" + Guid.NewGuid().ToString("N"));
            try
            {
                OfficialMap13StoryCatalog stories = OfficialMap13StoryCatalog.Load();
                Require(stories.Data.stories.Length == 74 && stories.Data.subgraphs.Length == 23,
                    "Map13 story source closure could not be loaded.");

                Require(stories.Data.mapTalkEntries.Length == 64 && stories.Data.entityTemplates.Length == 165,
                    "Map13 shared MapTalk/follower template dependencies could not be loaded.");
                Require(stories.Data.mainlineTasks.Length == 20
                    && stories.Data.mainlineTasks.Any(value => value.ClassId == 1355),
                    "Map13 mainline task chain omitted the intervening profession task.");
                Require(OfficialMainlineMapProfile.Get(13).RequiredAccessConditions.Count == 13,
                    "Map13 region access conditions were not registered.");
                Require(OfficialMainlineMapRuntime.OfficialStoryEnteredMapRegion(35, 36, 36, true)
                    && !OfficialMainlineMapRuntime.OfficialStoryEnteredMapRegion(36, 36, 36, true)
                    && OfficialMainlineMapRuntime.OfficialStoryEnteredMapRegion(36, 36, 36, false)
                    && OfficialMainlineMapRuntime.OfficialStoryEnteredMapRegion(35, 36, -1, true),
                    "Map13 official movement-based region-entry condition differs from its node flags.");

                var triggers = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
                Require(!OfficialMainlineMapRuntime.OfficialStoryTriggerIsSet(triggers, "Talk1")
                    && OfficialMainlineMapRuntime.OfficialStoryTriggerSet(triggers, "Talk1")
                    && OfficialMainlineMapRuntime.OfficialStoryTriggerIsSet(triggers, "Talk1")
                    && !OfficialMainlineMapRuntime.OfficialStoryTriggerSet(triggers, "Talk1"),
                    "Map13 story graph blackboard triggers did not retain true values during one graph run.");
                Require(OfficialMainlineMapRuntime.OfficialStoryAny(new[] { false, true, false })
                    && !OfficialMainlineMapRuntime.OfficialStoryAny(Array.Empty<bool>())
                    && OfficialMainlineMapRuntime.OfficialStoryAll(new[] { true, true })
                    && !OfficialMainlineMapRuntime.OfficialStoryAll(Array.Empty<bool>()),
                    "Map13 official AND/OR semantics differ from the client nodes.");
                Require(OfficialMainlineMapRuntime.OfficialStoryProfessionMatches(10, 10)
                    && !OfficialMainlineMapRuntime.OfficialStoryProfessionMatches(10, 11)
                    && !OfficialMainlineMapRuntime.OfficialStoryProfessionMatches(0, 10),
                    "Map13 official profession condition semantics differ from the client node.");

                foreach (Map10RuntimeMapTalkEntry talk in stories.Data.mapTalkEntries)
                    if (!string.IsNullOrEmpty(talk.TalkId))
                        Require(stories.TryGetStory(talk.TalkId, out _), "Map13 direct/subgraph MapTalk entry is missing: " + talk.TalkId);

                var slots = new ProfileSlotCatalog(root);
                string slot = slots.CreateSlot();
                OfflineMainlineProgressSession session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                ProgressionResult added = session.SetNpcFollowerClassIds(new[] { 8442, 8402, 8442, 8440 });
                Require(added.Status == ProgressionResultStatus.Applied, "Follower IDs were not written.");

                OfflineMainlineProgressSession restored = OfflineMainlineProgressSession.InspectCharacter(slots, slot);
                Require(restored.Save.Player.NpcFollowerClassIds.SequenceEqual(new[] { 8402, 8440, 8442 }),
                    "Follower IDs were not deduplicated and restored from disk.");
                Require(restored.SetNpcFollowerClassIds(Array.Empty<int>()).IsApplied,
                    "Follower clear did not persist.");
                OfflineMainlineProgressSession cleared = OfflineMainlineProgressSession.InspectCharacter(slots, slot);
                Require(cleared.Save.Player.NpcFollowerClassIds.Count == 0,
                    "Follower clear was not restored from disk.");
                Debug.Log("Map13 story catalog and NPC follower cold-save acceptance passed.");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}

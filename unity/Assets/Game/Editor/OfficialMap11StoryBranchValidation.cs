using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static class OfficialMap11StoryBranchValidation
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly Type Runtime = typeof(OfficialMainlineMapRuntime);

        public static void Validate(OfficialMainlineMapRuntime runtime)
        {
            var stories = (IDictionary)Runtime.GetField("stories", Private).GetValue(runtime);
            var subgraphs = (IDictionary)Runtime.GetField("storySubgraphs", Private).GetValue(runtime);
            var worldTree = (Map10RuntimeStory)stories["WorldTreeGuideToNPC"];
            var assistance = (Map10RuntimeStory)subgraphs["CombatAssistance"];
            Map10RuntimeStoryNode treeSelector = worldTree.nodes.Single(value => value.id == 14);
            Map10RuntimeStoryNode aidSelector = assistance.nodes.Single(value => value.id == 51);
            string countKey = worldTree.nodes.Single(value => value.id == 11).storyName;
            Require(countKey == assistance.nodes.Single(value => value.id == 48).storyName,
                "Pinned world tree and assistance use the same story count");
            for (int count = 0; count <= 2; count++)
            {
                Call(runtime, "SetStoryProgress", countKey, count, 2);
                Require((int)Call(runtime, "SelectStoryBranch", worldTree, treeSelector) == (count == 0 ? 5 : 0),
                    "World tree first match or original Exit at count " + count);
                Require((int)Call(runtime, "SelectStoryBranch", assistance, aidSelector) == (count == 0 ? 28 : count == 1 ? 33 : 0),
                    "Assistance first match or original Exit at count " + count);
            }

            var witch = (Map10RuntimeStory)stories["WitchStory2"];
            Map10RuntimeStoryNode witchSwitch = witch.nodes.Single(value => value.id == 33);
            MethodInfo selectSwitch = Runtime.GetMethod("SelectStorySwitch", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (object input in new object[] { 0, 1, 2, 1L })
                Require((int)selectSwitch.Invoke(null, new[] { witchSwitch, input }) == (Convert.ToInt32(input) == 1 ? 22 : 3),
                    "Witch switch matches the original primitive comparison or Exit");

            Map10RuntimeStoryNode storyState = witch.nodes
                .First(value => value.type == "IsStoryStateNode");
            foreach (int status in new[] { 0, 1, 2 })
            {
                Call(runtime, "SetStoryProgress", storyState.storyName, 0, status);
                Require((bool)Call(runtime, "EvaluateStoryBoolean", witch, storyState, 0)
                    == (status == storyState.storyStatus), "Official story status condition " + status);
            }
            var save = (OfflineMainlineProgressSession)Runtime.GetField("progressionSave", Private).GetValue(runtime);
            Map10RuntimeStoryNode witchQuery = witch.nodes.Single(value => value.type == "GetMapWitchExploreInfoNode");
            Require(save.ActiveWitchCount == 0, "Existing saves without a witch ledger start at zero");
            Require(save.RecordWitchActivated(11, 2801, "witch-count-fixture:11:2801").Status == ProgressionResultStatus.Applied,
                "First activation commits the placed witch");
            Require((int)Call(runtime, "EvaluateStoryValue", witch, witchQuery, 0) == 1
                && (int)Call(runtime, "StorySwitchValue", witch, witchSwitch) == 1,
                "Actual witch query supplies the original Switch input");
            Require(save.RecordWitchActivated(11, 2801, "witch-count-fixture:duplicate").Status == ProgressionResultStatus.Unchanged
                && save.ActiveWitchCount == 1, "Different receipt cannot count the same witch twice");
            save.InitializeMap(10, new[] { 0 });
            Require(save.RecordWitchActivated(10, 2801, "witch-count-fixture:10:2801").Status == ProgressionResultStatus.Applied
                && save.ActiveWitchCount == 2, "Activation count spans maps and placement IDs are scoped by map");
            Require((int)Call(runtime, "StorySwitchValue", witch, witchSwitch) == 2,
                "Second map activation updates the original query output");
            Debug.Log("Map11 story branches validated against pinned graphs: first-match selectors and original Exit; primitive Switch comparison; None/Playing/End; global witch activation ledger, duplicate protection and actual query output. Witch UI and native input remain outside this check.");
        }

        public static void ValidateColdLoad(OfficialMainlineMapRuntime runtime)
        {
            var stories = (IDictionary)Runtime.GetField("stories", Private).GetValue(runtime);
            var story = (Map10RuntimeStory)stories["WorldTreeGuideToNPC"];
            Require((int)Call(runtime, "GetStoryPlayCount", story.nodes.Single(value => value.id == 11).storyName) == 2,
                "Story branch count survives cold load");
            Require((int)Call(runtime, "SelectStoryBranch", story, story.nodes.Single(value => value.id == 14)) == 0,
                "Cold load retains the unmatched original Exit");
            var save = (OfflineMainlineProgressSession)Runtime.GetField("progressionSave", Private).GetValue(runtime);
            Require(save.ActiveWitchCount == 2
                && save.RecordWitchActivated(11, 2801, "witch-count-fixture:after-reload").Status == ProgressionResultStatus.Unchanged,
                "Cold load preserves global witch records and duplicate protection");
        }

        private static object Call(object target, string name, params object[] arguments) =>
            Runtime.GetMethod(name, Private).Invoke(target, arguments);
        private static void Require(bool value, string name)
        {
            if (!value) throw new InvalidDataException(name);
        }
    }
}

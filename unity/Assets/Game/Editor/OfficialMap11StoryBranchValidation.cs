using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static class OfficialMap11StoryBranchValidation
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly Type Runtime = typeof(OfficialMap10GameRuntime);

        public static void Validate(OfficialMap10GameRuntime runtime)
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
            Debug.Log("Map11 story branches validated against pinned graphs: world tree and assistance prioritize the first simultaneous match; unmatched selectors use Exit; witch switch preserves primitive comparison and Exit; story status condition preserves None/Playing/End. Witch activation values and native input are outside this check.");
        }

        public static void ValidateColdLoad(OfficialMap10GameRuntime runtime)
        {
            var stories = (IDictionary)Runtime.GetField("stories", Private).GetValue(runtime);
            var story = (Map10RuntimeStory)stories["WorldTreeGuideToNPC"];
            Require((int)Call(runtime, "GetStoryPlayCount", story.nodes.Single(value => value.id == 11).storyName) == 2,
                "Story branch count survives cold load");
            Require((int)Call(runtime, "SelectStoryBranch", story, story.nodes.Single(value => value.id == 14)) == 0,
                "Cold load retains the unmatched original Exit");
        }

        private static object Call(object target, string name, params object[] arguments) =>
            Runtime.GetMethod(name, Private).Invoke(target, arguments);
        private static void Require(bool value, string name)
        {
            if (!value) throw new InvalidDataException(name);
        }
    }
}

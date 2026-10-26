using System;
using System.Globalization;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        [Serializable] private sealed class StorySelectorFields { public int[] Ins; }
        [Serializable] private sealed class StorySwitchFields
        {
            public StorySerializableValue Input;
            public StorySerializableValue[] OutputList;
        }

        private bool FinishStoryFrame(out int nextNodeId)
        {
            nextNodeId = 0;
            if (storyFrames.Count > 0)
            {
                Map10RuntimeStoryFrame frame = storyFrames.Pop();
                activeStory = frame.Parent;
                nextNodeId = frame.NextNodeId;
                return false;
            }
            CompleteStory(activeStory.key);
            IncrementStoryPlayCount(activeStory.key);
            SaveLocalProgress();
            FinishStoryGraph();
            return true;
        }

        // The client consumes SelectedIndex supplied by the server. The confirmed
        // offline rule selects the first match in the original serialized order.
        private int SelectStoryBranch(Map10RuntimeStory story, Map10RuntimeStoryNode node)
        {
            int[] inputs = JsonUtility.FromJson<StorySelectorFields>(node.sourceJson).Ins ?? Array.Empty<int>();
            for (int index = 0; index < inputs.Length; index++)
            {
                int source = StoryNext(node, "Ins " + index);
                if (source > 0 ? EvaluateStoryBoolean(story, FindStoryNode(story, source)) : inputs[index] != 0)
                    return StoryNext(node, "Outs " + index);
            }
            return StoryNext(node, "Exit");
        }

        private object StorySwitchValue(Map10RuntimeStory story, Map10RuntimeStoryNode node)
        {
            int source = StoryNext(node, "Input");
            return source > 0 ? EvaluateStoryValue(story, FindStoryNode(story, source))
                : StoryConstant(JsonUtility.FromJson<StorySwitchFields>(node.sourceJson).Input);
        }

        private static int SelectStorySwitch(Map10RuntimeStoryNode node, object input)
        {
            StorySerializableValue[] outputs = JsonUtility.FromJson<StorySwitchFields>(node.sourceJson).OutputList
                ?? Array.Empty<StorySerializableValue>();
            for (int index = 0; index < outputs.Length; index++)
            {
                object target = StoryConstant(outputs[index]);
                // SerializableObj.Compare converts the origin to the target's
                // primitive type before invoking IComparable.CompareTo.
                object converted = input;
                try
                {
                    if (input != null && target != null && input.GetType() != target.GetType())
                        converted = Convert.ChangeType(input, target.GetType(), CultureInfo.CurrentCulture);
                }
                catch (Exception error) when (error is InvalidCastException || error is FormatException || error is OverflowException)
                {
                    continue;
                }
                if (target != null && converted is IComparable comparable && comparable.CompareTo(target) == 0)
                    return StoryNext(node, "OutputList " + index);
            }
            return StoryNext(node, "Exit");
        }
    }
}

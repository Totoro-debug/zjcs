using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    [Serializable] public sealed class BattleFunctionEntryData
    {
        public string manifest;
        public BattleFunctionEntry[] entries;
    }
    [Serializable] public sealed class BattleFunctionEntry
    {
        public string owner, controllerName, parameter, emojiPanel;
        public string[] entryButtons, selfRects;
        public bool ignoreWindows;
        public int defaultInt, foldedInt, expandedInt, defaultState;
        public BattleFunctionEntryClip[] clips;
        public BattleFunctionEntryState[] states;
    }
    [Serializable] public sealed class BattleFunctionEntryClip
    {
        public string name;
        public float duration, frameRate;
        public bool loop;
        public BattleFunctionEntryTrack[] tracks;
        public BattleFunctionEntryEvent[] events;
    }
    [Serializable] public sealed class BattleFunctionEntryTrack
    {
        public string path, property;
        public BattleFunctionEntryKey[] keys;
    }
    [Serializable] public sealed class BattleFunctionEntryKey
    {
        public float time, value, inTangent, outTangent;
    }
    [Serializable] public sealed class BattleFunctionEntryEvent
    {
        public string functionName, data;
        public float time, floatParameter;
        public int intParameter, messageOptions;
    }
    [Serializable] public sealed class BattleFunctionEntryState
    {
        public string name;
        public int clip;
        public float speed;
        public bool writeDefaultValues;
        public BattleFunctionEntryTransition[] transitions;
    }
    [Serializable] public sealed class BattleFunctionEntryTransition
    {
        public int destination, interruptionSource;
        public float duration, exitTime, offset;
        public bool fixedDuration, hasExitTime, canTransitionToSelf, orderedInterruption;
        public BattleFunctionEntryCondition[] conditions;
    }
    [Serializable] public sealed class BattleFunctionEntryCondition
    {
        public int mode;
        public string parameter;
        public float threshold;
    }

    public static class OfficialBattleFunctionEntryBindings
    {
        public static void Mount(OfficialBattleHudRenderer renderer, Func<bool> hasPanelOrDialog)
        {
            TextAsset asset = Resources.Load<TextAsset>("Combat/battle-function-entry");
            if (asset == null) throw new InvalidOperationException("Official function-entry bindings are missing.");
            var data = JsonUtility.FromJson<BattleFunctionEntryData>(asset.text);
            if (data.manifest != "854_156383" || data.entries == null || data.entries.Length != 1)
                throw new InvalidOperationException("Unexpected official function-entry prefab version.");
            foreach (BattleFunctionEntry entry in data.entries)
            {
                GameObject owner = Require(renderer, entry.owner);
                var controller = Resources.Load<RuntimeAnimatorController>("Combat/" + entry.controllerName);
                if (controller == null) throw new InvalidOperationException("Restore the official function-entry Animator.");
                var animator = owner.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var panel = owner.AddComponent<WorldChatQuickMsgEntryPanel>();
                panel.InitializeEmojiPanel(Require(renderer, entry.emojiPanel));
                panel.Initialize(animator, entry.parameter, entry.foldedInt, entry.expandedInt,
                    entry.selfRects.Select(path => Require(renderer, path).GetComponent<RectTransform>()).ToArray(),
                    entry.entryButtons.Select(path => Require(renderer, path).GetComponent<Button>()).ToArray(),
                    entry.ignoreWindows);
                var input = owner.AddComponent<OfficialBattleFunctionEntryInput>();
                input.Initialize(panel, hasPanelOrDialog);
                animator.Rebind();
                animator.Update(0f);
            }
        }

        private static GameObject Require(OfficialBattleHudRenderer renderer, string path)
        {
            GameObject result = renderer.Find(path);
            return result != null ? result : throw new InvalidOperationException("Official function entry reference is missing: " + path);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleFunctionEntryTools
    {
        private const string Output = "Assets/Official/Combat/HUD/Resources/Combat";
        private const string Receipt = "../assets/reference/battle-hud/function-entry-native-source-manifest.json";

        [MenuItem("Zjcs/Combat/Restore Official Function Entry Animator")]
        public static void Restore()
        {
            TextAsset payload = Resources.Load<TextAsset>("Combat/battle-function-entry");
            if (payload == null) throw new InvalidDataException("Extract the official function entry before importing.");
            var data = JsonUtility.FromJson<BattleFunctionEntryData>(payload.text);
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            foreach (BattleFunctionEntry entry in data.entries)
            {
                var clips = new AnimationClip[entry.clips.Length];
                for (int i = 0; i < clips.Length; i++) clips[i] = RestoreClip(entry.clips[i]);
                string path = Output + "/" + entry.controllerName + ".controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                controller.parameters = new[] { new AnimatorControllerParameter
                    { name = entry.parameter, type = AnimatorControllerParameterType.Int, defaultInt = entry.defaultInt } };
                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                foreach (ChildAnimatorState old in machine.states)
                {
                    foreach (AnimatorStateTransition transition in old.state.transitions)
                        UnityEngine.Object.DestroyImmediate(transition, true);
                    machine.RemoveState(old.state);
                    UnityEngine.Object.DestroyImmediate(old.state, true);
                }
                var states = new AnimatorState[entry.states.Length];
                for (int i = 0; i < states.Length; i++)
                {
                    BattleFunctionEntryState source = entry.states[i];
                    AnimatorState state = machine.AddState(source.name);
                    state.motion = clips[source.clip];
                    state.speed = source.speed;
                    state.writeDefaultValues = source.writeDefaultValues;
                    states[i] = state;
                }
                machine.defaultState = states[entry.defaultState];
                for (int i = 0; i < states.Length; i++)
                    foreach (BattleFunctionEntryTransition source in entry.states[i].transitions)
                    {
                        AnimatorStateTransition transition = states[i].AddTransition(states[source.destination]);
                        transition.duration = source.duration;
                        transition.hasFixedDuration = source.fixedDuration;
                        transition.exitTime = source.exitTime;
                        transition.hasExitTime = source.hasExitTime;
                        transition.offset = source.offset;
                        transition.canTransitionToSelf = source.canTransitionToSelf;
                        transition.interruptionSource = (TransitionInterruptionSource)source.interruptionSource;
                        transition.orderedInterruption = source.orderedInterruption;
                        foreach (BattleFunctionEntryCondition condition in source.conditions)
                            transition.AddCondition((AnimatorConditionMode)condition.mode, condition.threshold, condition.parameter);
                    }
                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(machine);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateAssets();
            var assets = Directory.GetFiles(Output).Where(path => path.EndsWith(".anim") || path.EndsWith(".controller")
                    || path.EndsWith(".anim.meta") || path.EndsWith(".controller.meta"))
                .OrderBy(path => path, StringComparer.Ordinal).Select(path => new NativeOutput
                { path = "unity/" + path.Replace('\\', '/'), sha256 = Sha(File.ReadAllBytes(path)) }).ToArray();
            File.WriteAllText(Receipt, JsonUtility.ToJson(new NativeReceipt
            {
                manifest = data.manifest, input_sha256 = Sha(File.ReadAllBytes(AssetDatabase.GetAssetPath(payload))),
                importer_sha256 = Sha(File.ReadAllBytes("Assets/Game/Editor/Combat/OfficialBattleFunctionEntryTools.cs")),
                outputs = assets
            }, true) + "\n");
            Validate();
            Debug.Log("Official function-entry Animator restored: three source clips, three states, original Fold parameter and events.");
        }

        private static AnimationClip RestoreClip(BattleFunctionEntryClip source)
        {
            var clip = new AnimationClip { name = source.name, legacy = false, frameRate = source.frameRate };
            foreach (BattleFunctionEntryTrack track in source.tracks)
            {
                bool discrete = track.property == "m_IsActive";
                Type type = discrete ? typeof(GameObject) : typeof(Transform);
                string property = track.property.StartsWith("scale.", StringComparison.Ordinal)
                    ? "m_LocalScale." + track.property.Substring(6) : track.property;
                if (!discrete && !property.StartsWith("m_LocalScale.", StringComparison.Ordinal))
                    throw new InvalidDataException("Unsupported official function-entry property: " + property);
                var curve = new AnimationCurve(track.keys.Select(k => new Keyframe(k.time, k.value,
                    discrete ? float.PositiveInfinity : k.inTangent,
                    discrete ? float.PositiveInfinity : k.outTangent)).ToArray());
                // The original GameObject m_IsActive bindings are scalar float
                // curves (isIntCurve=0), with stepped tangents for visibility.
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(track.path, type, property), curve);
            }
            AnimationUtility.SetAnimationEvents(clip, source.events.Select(e => new AnimationEvent
            {
                time = e.time, functionName = e.functionName, stringParameter = e.data,
                floatParameter = e.floatParameter, intParameter = e.intParameter,
                messageOptions = (SendMessageOptions)e.messageOptions
            }).ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = source.loop;
            settings.startTime = 0f;
            settings.stopTime = source.duration;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            string path = Output + "/" + source.name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) AssetDatabase.CreateAsset(clip, path);
            else
            {
                EditorUtility.CopySerialized(clip, existing);
                UnityEngine.Object.DestroyImmediate(clip);
                clip = existing;
            }
            EditorUtility.SetDirty(clip);
            return clip;
        }

        public static void Validate()
        {
            ValidateAssets();
            var receipt = JsonUtility.FromJson<NativeReceipt>(File.ReadAllText(Receipt));
            string payload = AssetDatabase.GetAssetPath(Resources.Load<TextAsset>("Combat/battle-function-entry"));
            if (receipt.manifest != "854_156383" || receipt.input_sha256 != Sha(File.ReadAllBytes(payload))
                || receipt.importer_sha256 != Sha(File.ReadAllBytes("Assets/Game/Editor/Combat/OfficialBattleFunctionEntryTools.cs")))
                throw new InvalidDataException("Official function-entry native provenance does not match its inputs.");
            foreach (NativeOutput output in receipt.outputs)
                if (Sha(File.ReadAllBytes("../" + output.path)) != output.sha256)
                    throw new InvalidDataException("Official function-entry native output changed: " + output.path);
        }

        private static void ValidateAssets()
        {
            var data = JsonUtility.FromJson<BattleFunctionEntryData>(Resources.Load<TextAsset>("Combat/battle-function-entry").text);
            foreach (BattleFunctionEntry entry in data.entries)
            {
                var controller = Resources.Load<RuntimeAnimatorController>("Combat/" + entry.controllerName) as AnimatorController;
                if (controller == null || controller.parameters.Length != 1 || controller.parameters[0].defaultInt != entry.defaultInt)
                    throw new InvalidDataException("Official Fold parameter was not restored.");
                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                if (machine.states.Length != entry.states.Length || machine.defaultState.name != entry.states[entry.defaultState].name)
                    throw new InvalidDataException("Official function-entry states were not restored.");
                foreach (BattleFunctionEntryState sourceState in entry.states)
                {
                    AnimatorState state = machine.states.Single(value => value.state.name == sourceState.name).state;
                    if (state.motion.name != entry.clips[sourceState.clip].name || state.speed != sourceState.speed
                        || state.writeDefaultValues != sourceState.writeDefaultValues || state.transitions.Length != sourceState.transitions.Length)
                        throw new InvalidDataException("Official state motion or transition count changed: " + sourceState.name);
                    for (int i = 0; i < sourceState.transitions.Length; i++)
                    {
                        BattleFunctionEntryTransition source = sourceState.transitions[i];
                        AnimatorStateTransition target = state.transitions[i];
                        if (target.destinationState.name != entry.states[source.destination].name || target.duration != source.duration
                            || target.hasFixedDuration != source.fixedDuration || target.hasExitTime != source.hasExitTime
                            || target.exitTime != source.exitTime || target.offset != source.offset
                            || target.canTransitionToSelf != source.canTransitionToSelf
                            || (int)target.interruptionSource != source.interruptionSource
                            || target.orderedInterruption != source.orderedInterruption || target.conditions.Length != source.conditions.Length)
                            throw new InvalidDataException("Official function-entry transition changed: " + sourceState.name);
                        for (int j = 0; j < source.conditions.Length; j++)
                            if ((int)target.conditions[j].mode != source.conditions[j].mode
                                || target.conditions[j].parameter != source.conditions[j].parameter
                                || target.conditions[j].threshold != source.conditions[j].threshold)
                                throw new InvalidDataException("Official Fold transition condition changed.");
                    }
                }
                foreach (BattleFunctionEntryClip source in entry.clips)
                {
                    AnimationClip clip = controller.animationClips.Single(value => value.name == source.name);
                    if (clip.legacy || clip.frameRate != source.frameRate || AnimationUtility.GetCurveBindings(clip).Length != source.tracks.Length
                        || AnimationUtility.GetAnimationEvents(clip).Length != source.events.Length)
                        throw new InvalidDataException("Official function-entry native clip is incomplete: " + source.name);
                    foreach (BattleFunctionEntryTrack track in source.tracks)
                    {
                        string property = track.property.StartsWith("scale.") ? "m_LocalScale." + track.property.Substring(6) : track.property;
                        EditorCurveBinding binding = AnimationUtility.GetCurveBindings(clip)
                            .Single(value => value.path == track.path && value.propertyName == property);
                        if (binding.isDiscreteCurve)
                            throw new InvalidDataException("The source visibility binding is a float curve, not an integer curve.");
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                        for (int i = 0; i < track.keys.Length; i++)
                            if (!Mathf.Approximately(curve.Evaluate(track.keys[i].time), track.keys[i].value))
                                throw new InvalidDataException("Official animation key changed: " + source.name + "/" + track.path);
                    }
                }
            }
        }

        private static string Sha(byte[] raw)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(raw)).Replace("-", "").ToLowerInvariant();
        }
        [Serializable] private sealed class NativeReceipt
        {
            public string manifest, input_sha256, importer_sha256;
            public NativeOutput[] outputs;
        }
        [Serializable] private sealed class NativeOutput { public string path, sha256; }
    }
}

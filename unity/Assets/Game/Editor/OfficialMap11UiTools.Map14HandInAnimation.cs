using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11UiTools
    {
        private static void ValidateHandInButtonController(Dictionary<string, object> record, string destination)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(destination + "/Btn_Base_Click.controller");
            if (controller == null) throw new InvalidDataException("Original hand-in button controller is absent.");
            Dictionary<string, object> Dict(object value) => (Dictionary<string, object>)value;
            IEnumerable<Dictionary<string, object>> Rows(object value) => ((List<object>)value).Cast<Dictionary<string, object>>();
            var native = Dict(record["native"]);
            var names = ((List<object>)native["m_TOS"]).Cast<List<object>>().ToDictionary(v => Convert.ToUInt32(v[0]),v => (string)v[1]);
            string Name(object id) => names[Convert.ToUInt32(id)];
            var constant = Dict(native["m_Controller"]);
            var parameters = Rows(Dict(Dict(constant["m_Values"])["data"])["m_ValueArray"]).ToArray();
            if (parameters.Length != controller.parameters.Length || parameters.Any(p => !controller.parameters.Any(a =>
                a.name == Name(p["m_ID"]) && (int)a.type == Convert.ToInt32(p["m_Type"]))))
                throw new InvalidDataException("Original hand-in button parameters differ from the compiled APK controller.");
            var machines = Rows(constant["m_StateMachineArray"]).ToArray();
            if (machines.Length != 1 || controller.layers.Length != 1)
                throw new InvalidDataException("Original hand-in button controller layer count differs.");
            var machine = Dict(machines[0]["data"]);
            var states = Rows(machine["m_StateConstantArray"]).Select(r => Dict(r["data"])).ToArray();
            AnimatorStateMachine actual = controller.layers[0].stateMachine;
            string StateName(int index) => Name(states[index]["m_NameID"]);
            if (states.Length != actual.states.Length || actual.defaultState.name != StateName(Convert.ToInt32(machine["m_DefaultState"])))
                throw new InvalidDataException("Original hand-in button states or default state differ.");
            void Near(float value, object expected)
            { if (Mathf.Abs(value - Convert.ToSingle(expected, CultureInfo.InvariantCulture)) > 0.000001f) throw new InvalidDataException("Original button animation value differs."); }
            void Transitions(AnimatorStateTransition[] imported, object source)
            {
                var rows = Rows(source).Select(r => Dict(r["data"])).ToArray();
                if (rows.Length != imported.Length) throw new InvalidDataException("Original button transition count differs.");
                for (int index = 0; index < rows.Length; index++)
                {
                    var expected = rows[index];var transition = imported[index];
                    if (transition.destinationState == null || transition.destinationState.name != StateName(Convert.ToInt32(expected["m_DestinationState"]))
                        || transition.hasExitTime != Convert.ToBoolean(expected["m_HasExitTime"])
                        || transition.hasFixedDuration != Convert.ToBoolean(expected["m_HasFixedDuration"])
                        || transition.orderedInterruption != Convert.ToBoolean(expected["m_OrderedInterruption"])
                        || transition.canTransitionToSelf != Convert.ToBoolean(expected["m_CanTransitionToSelf"])
                        || (int)transition.interruptionSource != Convert.ToInt32(expected["m_InterruptionSource"]))
                        throw new InvalidDataException("Original button transition branch differs.");
                    Near(transition.duration, expected["m_TransitionDuration"]);Near(transition.offset, expected["m_TransitionOffset"]);Near(transition.exitTime, expected["m_ExitTime"]);
                    var conditions = Rows(expected["m_ConditionConstantArray"]).Select(r => Dict(r["data"])).ToArray();
                    if (conditions.Length != transition.conditions.Length) throw new InvalidDataException("Original button transition conditions differ.");
                    for (int c=0;c<conditions.Length;c++)
                    {
                        if (transition.conditions[c].parameter != Name(conditions[c]["m_EventID"])
                            || (int)transition.conditions[c].mode != Convert.ToInt32(conditions[c]["m_ConditionMode"]))
                            throw new InvalidDataException("Original button transition condition differs.");
                        Near(transition.conditions[c].threshold, conditions[c]["m_EventThreshold"]);
                    }
                }
            }
            Transitions(actual.anyStateTransitions,machine["m_AnyStateTransitionConstantArray"]);
            foreach (var state in states)
            {
                AnimatorState imported = actual.states.Single(s => s.state.name == Name(state["m_NameID"])).state;
                Near(imported.speed,state["m_Speed"]);Near(imported.cycleOffset,state["m_CycleOffset"]);
                if (imported.iKOnFeet != Convert.ToBoolean(state["m_IKOnFeet"]) || imported.writeDefaultValues != Convert.ToBoolean(state["m_WriteDefaultValues"]) || imported.mirror != Convert.ToBoolean(state["m_Mirror"]))
                    throw new InvalidDataException("Original button state flags differ.");
                Transitions(imported.transitions,state["m_TransitionConstantArray"]);
            }
            string root = (string)record["root"];
            foreach (var clipRecord in Rows(record["clips"]))
            {
                string name = (string)clipRecord["name"];
                var clip = controller.animationClips.Single(c => c.name == name);
                Near(clip.frameRate,clipRecord["frameRate"]);
                if (AnimationUtility.GetAnimationClipSettings(clip).loopTime != Convert.ToBoolean(clipRecord["loop"]))
                    throw new InvalidDataException("Original button loop flag differs.");
                var tracks = Rows(clipRecord["tracks"]).ToArray();
                if (AnimationUtility.GetCurveBindings(clip).Length != tracks.Length)
                    throw new InvalidDataException("Original button animation channels differ.");
                foreach (var track in tracks)
                {
                    string path = ((string)track["path"]).Substring(root.Length+1);
                    string property = (string)track["property"];
                    if (!property.StartsWith("scale.",StringComparison.Ordinal)) throw new InvalidDataException("Unverified original button animation channel.");
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalScale."+property.Substring(6)));
                    var keys = Rows(track["keyframes"]).ToArray();
                    if (curve == null || curve.length != keys.Length) throw new InvalidDataException("Original button animation keys differ.");
                    for (int k=0;k<keys.Length;k++)
                    { Near(curve.keys[k].time,keys[k]["time"]);Near(curve.keys[k].value,keys[k]["value"]);Near(curve.keys[k].inTangent,keys[k]["inTangent"]);Near(curve.keys[k].outTangent,keys[k]["outTangent"]); }
                }
            }
        }

        private static void RestoreHandInAnimatorSettings(Dictionary<string, object> document, string panel,
            Func<string, Transform> require, string destination)
        {
            foreach (var record in ((List<object>)document["animators"]).Cast<Dictionary<string, object>>().Where(r => (string)r["panel"] == panel))
            {
                Animator animator = require((string)record["path"]).GetComponent<Animator>();
                if (animator == null) throw new InvalidDataException("Original hand-in Animator is missing.");
                var settings = (Dictionary<string, object>)record["settings"];
                string raw = "{" + string.Join(",",settings.Select(p => "\"" + p.Key + "\":" + (p.Value is bool boolean ? boolean.ToString().ToLowerInvariant() : Convert.ToString(p.Value,CultureInfo.InvariantCulture)))) + "}";
                EditorJsonUtility.FromJsonOverwrite(raw,animator);
                animator.runtimeAnimatorController = record["controller"] == null ? null :
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(destination+"/"+(string)record["controller"]+".controller");
            }
        }
    }
}

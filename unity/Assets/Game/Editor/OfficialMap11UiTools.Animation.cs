using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11UiTools
    {
        private static void RestoreNativeAnimation(OfficialMap11UiComponent source, Func<string, Transform> require)
        {
            GameObject root = require(source.path).gameObject;
            Animation player = root.GetComponent<Animation>();
            if (player == null) player = root.AddComponent<Animation>();
            player.playAutomatically = source.fields.playAutomatically || source.type == "NativeAnimator";
            player.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (OfficialMap11UiClip record in source.fields.clips)
            {
                if (record.unknown.Any(value => source.type != "NativeAnimatorInitialState" || value.reason != "target_absent"))
                    throw new System.IO.InvalidDataException("Unresolved original UI animation: " + record.name);
                var clip = new AnimationClip { name = record.name, legacy = true, frameRate = 30,
                    wrapMode = record.loop ? WrapMode.Loop : WrapMode.ClampForever };
                foreach (OfficialMap11UiTrack track in record.tracks)
                {
                    string relative = track.path == source.path ? "" : track.path.Substring(source.path.Length + 1);
                    string property = track.property;
                    Type target;
                    if (property.StartsWith("scale.")) { target = typeof(Transform); property = "m_LocalScale." + property.Substring(6); }
                    else if (property.StartsWith("position.")) { target = typeof(Transform); property = "m_LocalPosition." + property.Substring(9); }
                    else if (property.StartsWith("rotation.")) { target = typeof(Transform); property = "localEulerAnglesRaw." + property.Substring(9); }
                    else if (property.StartsWith("quaternion.")) { target = typeof(Transform); property = "m_LocalRotation." + property.Substring(11); }
                    else if (property == "m_IsActive") target = typeof(GameObject);
                    else if (property == "m_Alpha") target = typeof(CanvasGroup);
                    else if (property.StartsWith("m_Color.")) target = require(track.path).GetComponent<SpriteRenderer>() != null
                        ? typeof(SpriteRenderer) : require(track.path).GetComponent<Graphic>().GetType();
                    else if (property.StartsWith("m_fontColor.")) target = typeof(TextMeshProUGUI);
                    else if (property.StartsWith("m_AnchoredPosition.") || property.StartsWith("m_SizeDelta.")
                        || property.StartsWith("m_Anchor") || property.StartsWith("m_Pivot.")) target = typeof(RectTransform);
                    else throw new System.IO.InvalidDataException("Unsupported native UI animation property: " + property);
                    var curve = new AnimationCurve(track.keyframes.Select(key => new Keyframe(key.time, key.value,
                        property == "m_IsActive" ? float.PositiveInfinity : key.inTangent,
                        property == "m_IsActive" ? float.PositiveInfinity : key.outTangent)).ToArray());
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(relative, target, property), curve);
                }
                var dialogueEvents = (record.events ?? Array.Empty<OfficialMap11UiAnimationEvent>())
                    .Where(value => value.functionName == "OnTalkDialogContentSwitchHalf" || value.functionName == "OnSwitchAnimHalf"
                        || value.functionName == "ReturnPool").ToArray();
                if (dialogueEvents.Length > 0)
                {
                    if (dialogueEvents.Any(value => value.functionName == "ReturnPool"))
                    {
                        if (root.GetComponent<WorldAwardAnimationEvents>() == null) root.AddComponent<WorldAwardAnimationEvents>();
                    }
                    else if (root.GetComponent<WorldDialogueAnimationEvents>() == null) root.AddComponent<WorldDialogueAnimationEvents>();
                    AnimationUtility.SetAnimationEvents(clip, dialogueEvents.Select(value => new AnimationEvent
                    {
                        functionName = value.functionName, time = value.time, stringParameter = value.data,
                        floatParameter = value.floatParameter, intParameter = value.intParameter,
                        messageOptions = (SendMessageOptions)value.messageOptions
                    }).ToArray());
                }
                string id = Hash128.Compute(source.panel + source.path + record.name).ToString();
                string path = ResourceRoot + "/Native-" + id + ".anim";
                AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing == null) AssetDatabase.CreateAsset(clip, path);
                else { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); clip = existing; }
                player.AddClip(clip, record.name);
                // A null default clip in the source Animation must stay null.
                if (record.name == source.fields.defaultClip
                    || source.type == "NativeAnimator" && player.clip == null) player.clip = clip;
            }
            // The function-entry subclass opens its separate popup. Its unused
            // inline panel still needs the original controller's default pose.
            if (source.type == "NativeAnimatorInitialState") player.clip.SampleAnimation(root, 0);
            if (source.type == "NativeAnimator" && source.fields.clips.Any(value => value.name == "Pressed"))
                root.AddComponent<WorldOptionButtonAnimation>();
        }
    }
}

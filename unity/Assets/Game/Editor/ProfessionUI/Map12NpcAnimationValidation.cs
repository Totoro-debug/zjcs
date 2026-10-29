using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    internal static class Map12NpcAnimationValidation
    {
        [Serializable] private sealed class Catalog { public Row[] models; }
        [Serializable] private sealed class Row { public string resource; }
        internal static void Verify()
        {
            VerifyCurves();
            VerifyAnimatedPose();
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Map12/TeamNpcSkeletons/catalog").text);
            int animations = 0, tracks = 0, inherit = 0, colors = 0, sampled = 0;
            foreach (var model in catalog.models)
            {
                var data = ProfessionNativeSkeletonData.LoadNpc(Resources.Load<TextAsset>(model.resource).bytes);
                var pose = data.CreateBonePose(); pose.SetSkinBones(Enumerable.Range(0, data.BoneCount).ToArray());
                var slots = data.CreateSlotPose(pose); var rgba = new Color[data.SlotCount];
                foreach (string name in data.AnimationNames)
                {
                    var clip = new ProfessionNativeAnimationChannels(data, name);
                    foreach (var track in clip.BoneTracks)
                        foreach (float time in new[] { 0f, clip.Duration * .37f, clip.Duration })
                            if (track.Curve.TryEvaluate(time, out float value))
                            { Require(!float.IsNaN(value) && !float.IsInfinity(value), "Nonfinite original local channel: " + name); sampled++; }
                    foreach (float time in new[] { 0f, clip.Duration * .37f, clip.Duration })
                    {
                        clip.ApplyLocalPose(pose, slots, rgba, time, false);
                        foreach (Color color in rgba)
                            for (int axis = 0; axis < 4; axis++)
                                Require(!float.IsNaN(color[axis]) && !float.IsInfinity(color[axis]), "Nonfinite original color channel: " + name);
                    }
                    VerifyKeys(data, clip, pose, slots, rgba);
                    tracks += clip.BoneTracks.Count; inherit += clip.InheritanceTrackCount; colors += clip.ColorTrackCount; animations++;
                }
                GC.Collect();
            }
            Require(animations == 1952 && inherit == 27 && colors == 1327, "Original NPC local timeline inventory differs.");
            Debug.Log("Map12 original NPC local channels passed: " + animations + " animations, " + tracks +
                " scalar bone tracks, " + inherit + " inheritance tracks, " + colors + " RGBA tracks, " + sampled +
                " scalar samples; source first/last bone and color keys verified. Constraint solving/rendering remain separate.");
        }

        private static void VerifyAnimatedPose()
        {
            var pose = new ProfessionNativeBonePose(new[] {
                new ProfessionNativeBonePose.Bone { name = "root", rotation = 10, shearX = 2, shearY = 3,
                    scaleX = 2, scaleY = 3 },
                new ProfessionNativeBonePose.Bone { name = "child", parent = 0, x = 1 } });
            ProfessionNativeBonePose.Track Track(int channel, float value) => new ProfessionNativeBonePose.Track(0, channel,
                new ProfessionNativeCurve(new[] { new ProfessionNativeCurve.Key { time = 0, value = value } }));
            pose.Apply(new[] { Track(1, 4), Track(5, 20), Track(6, -5) }, 0);
            Require(pose.LocalPosition(0) == new Vector2(4, 0) && pose.Shear(0) == new Vector2(22, -2),
                "Independent axis/shear channels did not retain setup offsets.");
            var world = pose.World(0);
            Require(Mathf.Abs(world.m00 - Mathf.Cos(32 * Mathf.Deg2Rad) * 2) < .000001f &&
                Mathf.Abs(world.m11 - Mathf.Sin(98 * Mathf.Deg2Rad) * 3) < .000001f,
                "Animated shear did not reach both world axes.");
            pose.SetInheritance(1, 1);
            Require(pose.Inheritance(1) == 1, "Animated inheritance was not applied.");
            pose.Apply(System.Array.Empty<ProfessionNativeBonePose.Track>(), 0);
            Require(pose.Inheritance(1) == 0 && pose.Shear(0) == new Vector2(2, 3) && pose.LocalPosition(0) == Vector2.zero,
                "A subsequent local animation leaked the previous inheritance/shear/axis channels.");
        }

        private static void VerifyKeys(ProfessionNativeSkeletonData data, ProfessionNativeAnimationChannels clip,
            ProfessionNativeBonePose pose, ProfessionNativeSlotPose slots, Color[] rgba)
        {
            var boneRows = Children(clip.OriginalFields, "bones"); int trackIndex = 0;
            foreach (var bone in boneRows)
                foreach (var channel in Map(bone.Value))
                {
                    var rows = (List<object>)channel.Value;
                    if (channel.Key == "inherit")
                    {
                        string[] modes = { "normal", "onlyTranslation", "noRotationOrReflection", "noScale", "noScaleOrReflection" };
                        foreach (var item in new[] { rows.First(), rows.Last() })
                        {
                            var row = Map(item); clip.ApplyLocalPose(pose, slots, rgba, Number(row, "time"), false);
                            Require(pose.Inheritance(data.FindBone(bone.Key)) == System.Array.IndexOf(modes, (string)row["value"]), "Original inherited mode differs.");
                        }
                        continue;
                    }
                    bool paired = channel.Key == "translate" || channel.Key == "scale" || channel.Key == "shear";
                    for (int axis = 0; axis < (paired ? 2 : 1); axis++)
                    {
                        var track = clip.BoneTracks[trackIndex++];
                        string key = paired ? axis == 0 ? "x" : "y" : "value";
                        foreach (var item in new[] { rows.First(), rows.Last() })
                        {
                            var row = Map(item); float expected = Number(row, key, track.Channel == 3 || track.Channel == 4 ? 1 : 0);
                            Require(track.Curve.TryEvaluate(Number(row, "time"), out float actual) && actual == expected,
                                "Original scalar key differs: " + clip.Name + "/" + bone.Key + "/" + channel.Key);
                        }
                    }
                }
            Require(trackIndex == clip.BoneTracks.Count, "Original bone channel order differs.");
            foreach (var slot in Children(clip.OriginalFields, "slots"))
                foreach (var channel in Map(slot.Value))
                    if (channel.Key == "rgba")
                    {
                        var rows = (List<object>)channel.Value;
                        foreach (var item in new[] { rows.First(), rows.Last() })
                        {
                            var row = Map(item); clip.ApplyLocalPose(pose, slots, rgba, Number(row, "time"), false);
                            string value = (string)row["color"]; Color actual = rgba[data.FindSlot(slot.Key)];
                            for (int axis = 0; axis < 4; axis++)
                                Require(Mathf.Abs(actual[axis] - byte.Parse(value.Substring(axis * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f) < .0000001f,
                                    "Original RGBA key differs: " + data.Sex + "/" + clip.Name + "/" + slot.Key +
                                    ", axis=" + axis + ", time=" + Number(row, "time").ToString("R") + ", actual=" + actual[axis].ToString("R") +
                                    ", expected=" + value + ", active=" + pose.IsActive(slots.Bone(data.FindSlot(slot.Key))));
                        }
                    }
        }

        private static void VerifyCurves()
        {
            void Cubic(Vector4 controls, float time, float expected)
            {
                var curve = new ProfessionNativeCurve(new[] {
                    new ProfessionNativeCurve.Key { time = 0, value = 0, mode = 2, controls = controls },
                    new ProfessionNativeCurve.Key { time = 1, value = 1 } });
                Require(!curve.TryEvaluate(-.01f, out _), "Original before-first-key behavior differs.");
                Require(curve.TryEvaluate(time, out float value) && Mathf.Abs(value - expected) < .000001f, "Original source cubic root differs.");
                Require(curve.TryEvaluate(1, out value) && value == 1, "Original endpoint differs.");
            }
            Cubic(new Vector4(.8f, 1f / 3, .2f, 2f / 3), .5f, .5f);
            Cubic(new Vector4(0, 1f / 3, .5f, 2f / 3), .0859375f, .25f);
            Cubic(new Vector4(1.4f, 1f / 3, -.4f, 2f / 3), .5f, (1 - Mathf.Sqrt(.375f)) * .5f);
            var stepped = new ProfessionNativeCurve(new[] {
                new ProfessionNativeCurve.Key { time = 0, value = 3, mode = 1 }, new ProfessionNativeCurve.Key { time = 1, value = 7 } });
            Require(stepped.TryEvaluate(.999f, out float step) && step == 3, "Original stepped channel differs.");
        }
        private static Dictionary<string, object> Map(object value) => (Dictionary<string, object>)value;
        private static IEnumerable<KeyValuePair<string, object>> Children(IReadOnlyDictionary<string, object> value, string key) =>
            value.TryGetValue(key, out object child) ? Map(child) : Enumerable.Empty<KeyValuePair<string, object>>();
        private static float Number(IReadOnlyDictionary<string, object> row, string key, float fallback = 0) =>
            row.TryGetValue(key, out object value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    }
}

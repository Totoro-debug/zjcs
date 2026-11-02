// Optional public-call oracle only. Never loaded by the game/native pipeline.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Spine;

public static class PartnerPlayerPoseReference
{
    public static int Main(string[] args)
    {
        if (args[0] == "--curve") return CaptureCurve(args[1], args[2]);
        var data = new SkeletonJson(new AtlasAttachmentLoader(new Atlas[0]))
            .ReadSkeletonData(new StringReader(File.ReadAllText(args[0])));
        var lines = File.ReadAllLines(args[1]);
        var skin = new Skin("actual-original-avatar-selection");
        foreach (int index in Indices(lines[0])) skin.Bones.Add(data.Bones.Items[index]);
        var constraints = new List<ConstraintData>();
        constraints.AddRange(data.IkConstraints); constraints.AddRange(data.TransformConstraints);
        constraints.AddRange(data.PathConstraints); constraints.AddRange(data.PhysicsConstraints);
        foreach (int index in Indices(lines[1])) skin.Constraints.Add(constraints[index]);
        var skeleton = new Skeleton(data); skeleton.SetSkin(skin); skeleton.UpdateCache();
        var animation = data.FindAnimation("map/idle_01");
        float time = 0, basis = 0, position = 0;
        float initialBasis = 0, initialPosition = 0;
        string firstDifference = "";
        int frame = -1, checks = 0;
        string worstBasis = "", worstPosition = "";
        foreach (string line in lines.Skip(2))
        {
            string[] cells = line.Split('|'); int next = int.Parse(cells[0]), index = int.Parse(cells[1]);
            if (frame != next)
            {
                float delta = next == 0 ? 0 : 1f / 60; time += delta;
                skeleton.SetBonesToSetupPose();
                animation.Apply(skeleton, 0, time, true, null, 1, MixBlend.Setup, MixDirection.In);
                skeleton.Update(delta); skeleton.UpdateWorldTransform(Skeleton.Physics.Update);
                frame = next;
            }
            var bone = skeleton.Bones.Items[index];
            if (!bone.Active) throw new Exception("Native pose contains inactive original bone: " + bone.Data.Name);
            float[] actual = cells[2].Split(',').Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray();
            float[] expected = { bone.A, bone.B, bone.C, bone.D, bone.WorldX, bone.WorldY };
            for (int axis = 0; axis < expected.Length; axis++)
            {
                float error = Math.Abs(actual[axis] - expected[axis]);
                if (float.IsNaN(error) || float.IsInfinity(error)) throw new Exception("Nonfinite native pose");
                string detail = bone.Data.Name + "/" + frame + "/" + axis;
                if (axis < 4 && error > basis) { basis = error; worstBasis = detail; }
                if (axis >= 4 && error > position) { position = error; worstPosition = detail; }
                if (frame == 0 && axis < 4) initialBasis = Math.Max(initialBasis, error);
                if (frame == 0 && axis >= 4) initialPosition = Math.Max(initialPosition, error);
                if (firstDifference.Length == 0 && error > (axis < 4 ? .00002f : .002f))
                    firstDifference = detail + ":native=" + actual[axis].ToString("R", CultureInfo.InvariantCulture)
                        + ",reference=" + expected[axis].ToString("R", CultureInfo.InvariantCulture);
            }
            checks++;
        }
        string result = "{\"frames\":" + (frame + 1) + ",\"checks\":" + checks
            + ",\"maximum_basis_error\":" + basis.ToString("R", CultureInfo.InvariantCulture)
            + ",\"maximum_position_error\":" + position.ToString("R", CultureInfo.InvariantCulture)
            + ",\"worst_basis\":\"" + worstBasis + "\",\"worst_position\":\"" + worstPosition + "\"}\n";
        result = result.Replace("}\n", ",\"initial_basis_error\":" + initialBasis.ToString("R", CultureInfo.InvariantCulture)
            + ",\"initial_position_error\":" + initialPosition.ToString("R", CultureInfo.InvariantCulture)
            + ",\"first_difference\":\"" + firstDifference + "\"}\n");
        File.WriteAllText(args[2], result); Console.WriteLine(result);
        return 0;
    }
    private static IEnumerable<int> Indices(string line) => line.Substring(2).Split(',')
        .Where(value => value.Length > 0).Select(int.Parse);

    private static int CaptureCurve(string input, string output)
    {
        var data = new SkeletonJson(new AtlasAttachmentLoader(new Atlas[0]))
            .ReadSkeletonData(new StringReader(File.ReadAllText(input)));
        var skeleton = new Skeleton(data);
        using (var writer = new StreamWriter(output))
            for (int frame = 0; frame <= 60; frame++)
            {
                float time = frame / 60f;
                skeleton.SetBonesToSetupPose();
                data.FindAnimation("a").Apply(skeleton, 0, time, false, null, 1, MixBlend.Setup, MixDirection.In);
                writer.WriteLine("F|" + time.ToString("R", CultureInfo.InvariantCulture) + "|"
                    + skeleton.Bones.Items[0].Rotation.ToString("R", CultureInfo.InvariantCulture));
            }
        return 0;
    }
}

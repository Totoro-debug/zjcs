// Optional binary reference check of independent dynamics. The reference supplies
// each constraint's input world matrix; this is not a native hierarchy/UI test.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Spine;
using Zjcs.Offline.ProfessionUI;

public static class PartnerPlayerPhysicsAssetReference
{
    private static ProfessionNativeSpringConstraint.Affine Read(Bone bone) =>
        new ProfessionNativeSpringConstraint.Affine { A = bone.A, B = bone.B, C = bone.C, D = bone.D,
            X = bone.WorldX, Y = bone.WorldY, Length = bone.Data.Length };

    public static int Main(string[] args)
    {
        var reader = new SkeletonJson(new AtlasAttachmentLoader(new Atlas[0]));
        var data = reader.ReadSkeletonData(new StringReader(File.ReadAllText(args[0])));
        var skeleton = new Skeleton(data);
        var skin = new Skin("all-original-constraints");
        foreach (var bone in data.Bones) skin.Bones.Add(bone);
        foreach (var value in data.IkConstraints) skin.Constraints.Add(value);
        foreach (var value in data.TransformConstraints) skin.Constraints.Add(value);
        foreach (var value in data.PhysicsConstraints) skin.Constraints.Add(value);
        skeleton.Skin = skin; skeleton.UpdateCache();
        skeleton.UpdateWorldTransform(Skeleton.Physics.Reset);
        var candidates = new Dictionary<PhysicsConstraint, ProfessionNativeSpringConstraint>();
        foreach (var value in skeleton.PhysicsConstraints)
        {
            var solver = new ProfessionNativeSpringConstraint();
            solver.Reset(Read(value.Bone), 0); candidates.Add(value, solver);
        }
        float maximum = 0;
        int checks = 0;
        for (int frame = 0; frame < 120; frame++)
        {
            skeleton.SetBonesToSetupPose();
            data.FindAnimation("map/idle_01").Apply(skeleton, 0, skeleton.Time, true, null, 1, MixBlend.Setup, MixDirection.In);
            skeleton.Update(1f / 60);
            skeleton.UpdateWorldTransform(Skeleton.Physics.None);
            foreach (var value in skeleton.PhysicsConstraints)
            {
                if (!value.Active) continue;
                value.Bone.UpdateWorldTransform();
                var parameters = new ProfessionNativeSpringConstraint.Parameters {
                    Step = value.Data.Step, X = value.Data.X, Y = value.Data.Y, Turn = value.Data.Rotate,
                    Limit = value.Data.Limit, Inertia = value.Inertia, Strength = value.Strength,
                    Damping = value.Damping, InverseMass = value.MassInverse, Wind = value.Wind,
                    Gravity = value.Gravity, Mix = value.Mix, Reference = data.ReferenceScale };
                var expected = candidates[value].Evaluate(Read(value.Bone), parameters, skeleton.Time);
                value.Update(Skeleton.Physics.Update);
                var actual = Read(value.Bone);
                float[] left = { expected.A, expected.B, expected.C, expected.D, expected.X, expected.Y };
                float[] right = { actual.A, actual.B, actual.C, actual.D, actual.X, actual.Y };
                for (int index = 0; index < left.Length; index++)
                {
                    float error = Math.Abs(left[index] - right[index]);
                    if (float.IsNaN(error) || error > .00002f)
                        throw new Exception("Original asset physics differs: " + value.Data.Name + " frame=" + frame);
                    maximum = Math.Max(maximum, error);
                }
                checks++;
            }
        }
        string result = "{\"constraints\":" + skeleton.PhysicsConstraints.Count + ",\"frames\":120,\"checks\":"
            + checks + ",\"maximum_error\":" + maximum.ToString("R", CultureInfo.InvariantCulture) + "}\n";
        File.WriteAllText(args[1], result);
        Console.WriteLine("Original player asset physics passed: " + checks + " input poses.");
        return 0;
    }
}

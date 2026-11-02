// Isolated numerical reference only. Never compiled into the game or the
// independent regression test. Uses public calls on an existing binary.
using System;
using System.Globalization;
using System.IO;
using Spine;

public static class PartnerPlayerPhysicsReferenceCapture
{
    public static int Main(string[] args)
    {
        var reader = new SkeletonJson(new AtlasAttachmentLoader(new Atlas[0]));
        var data = reader.ReadSkeletonData(new StringReader(File.ReadAllText(args[0])));
        var skeleton = new Skeleton(data);
        skeleton.UpdateWorldTransform(Skeleton.Physics.Reset);
        var bone = skeleton.RootBone;
        var p = skeleton.PhysicsConstraints.Items[0];
        float dt = float.Parse(args[1], CultureInfo.InvariantCulture);
        using (var writer = new StreamWriter(args[3]))
        {
            writer.WriteLine((p.Mix == 0 ? "D|" : "C|") + Format(bone.A, bone.B, bone.C, bone.D,
                bone.WorldX, bone.WorldY, bone.Data.Length, skeleton.Time));
            for (int frame = 0; frame < 120; frame++)
            {
                if (args[2] == "harmonic")
                {
                    bone.X = (float)Math.Sin(frame * .13) * 20;
                    bone.Y = (float)Math.Cos(frame * .17) * 30;
                    bone.Rotation = frame * 7;
                    bone.ScaleX = 1 + (float)Math.Sin(frame * .23) * .3f;
                    bone.ScaleY = 1 + (float)Math.Cos(frame * .29) * .2f;
                }
                else if (args[2] == "mix-flip")
                {
                    bone.X = frame * 3; bone.Y = frame * 2; bone.Rotation = frame * 7;
                    p.Mix = frame % 10 < 5 ? 0 : 1;
                }
                else throw new ArgumentException("Unknown numerical observation motion.");
                skeleton.Update(dt);
                bone.UpdateWorldTransform();
                float a = bone.A, b = bone.B, c = bone.C, d = bone.D, x = bone.WorldX, y = bone.WorldY;
                skeleton.UpdateWorldTransform(Skeleton.Physics.Update);
                writer.WriteLine("F|" + Format(p.Data.Step, p.Data.X, p.Data.Y, p.Data.Rotate, p.Data.Limit,
                    p.Inertia, p.Strength, p.Damping, p.MassInverse, p.Wind, p.Gravity, p.Mix, data.ReferenceScale,
                    a, b, c, d, x, y, bone.Data.Length, skeleton.Time,
                    bone.A, bone.B, bone.C, bone.D, bone.WorldX, bone.WorldY));
            }
        }
        return 0;
    }

    private static string Format(params float[] values) => string.Join("|",
        Array.ConvertAll(values, value => value.ToString("R", CultureInfo.InvariantCulture)));
}

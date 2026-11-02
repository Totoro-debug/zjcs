using System;
using System.Globalization;
using System.IO;
using Zjcs.Offline.ProfessionUI;

public static class PartnerPlayerPhysicsValidation
{
    public static int Main(string[] args)
    {
        try
        {
            Validate(args[0]);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    public static void Validate(string path)
    {
        var solver = new ProfessionNativeSpringConstraint();
        var identity = new ProfessionNativeSpringConstraint.Affine { A = 1, D = 1, Length = 100 };
        var p = new ProfessionNativeSpringConstraint.Parameters { Step = 1f / 60, Limit = 5000,
            Inertia = .5f, Strength = 100, Damping = .85f, InverseMass = 1, Mix = 1, Turn = 1, Reference = 100 };
        Reject<InvalidOperationException>(() => solver.Evaluate(identity, p, 0));
        solver.Reset(identity, 0);
        Reject<ArgumentException>(() => solver.Evaluate(identity, p, float.NaN));
        var invalid = p; invalid.Step = 0;
        Reject<ArgumentOutOfRangeException>(() => solver.Evaluate(identity, invalid, 0));
        invalid = p; invalid.Reference = 0;
        Reject<ArgumentOutOfRangeException>(() => solver.Evaluate(identity, invalid, 0));
        var invalidPose = identity; invalidPose.A = float.PositiveInfinity;
        Reject<ArgumentException>(() => solver.Reset(invalidPose, 0));

        int cases = 0, frames = 0;
        bool awaitingFirstActivePose = false;
        float initializationClock = 0;
        float maximum = 0;
        foreach (string row in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(row) || row.StartsWith("#", StringComparison.Ordinal)) continue;
            string[] fields = row.Split('|');
            var values = new float[fields.Length - 1];
            for (int index = 0; index < values.Length; index++)
                values[index] = float.Parse(fields[index + 1], CultureInfo.InvariantCulture);
            if (fields[0] == "C" || fields[0] == "D")
            {
                if (values.Length != 8) throw new FormatException("Original reset observation is incomplete.");
                awaitingFirstActivePose = fields[0] == "D";
                initializationClock = values[7];
                if (!awaitingFirstActivePose) solver.Reset(Pose(values, 0), values[7]);
                cases++;
            }
            else
            {
                if (fields[0] != "F" || values.Length != 27 || cases == 0)
                    throw new FormatException("Original physics observation is incomplete.");
                p = new ProfessionNativeSpringConstraint.Parameters { Step = values[0], X = values[1],
                    Y = values[2], Turn = values[3], Limit = values[4], Inertia = values[5], Strength = values[6],
                    Damping = values[7], InverseMass = values[8], Wind = values[9], Gravity = values[10],
                    Mix = values[11], Reference = values[12] };
                var pose = Pose(values, 13);
                bool bindThisFrame = awaitingFirstActivePose && p.Mix != 0;
                if (bindThisFrame)
                {
                    // The host binds a new inactive constraint when it first
                    // becomes active, using that frame's actual world pose.
                    solver.Reset(pose, initializationClock);
                    awaitingFirstActivePose = false;
                }
                if (!awaitingFirstActivePose && !bindThisFrame) pose = solver.Evaluate(pose, p, values[20]);
                float[] actual = { pose.A, pose.B, pose.C, pose.D, pose.X, pose.Y };
                for (int index = 0; index < actual.Length; index++)
                {
                    float error = Math.Abs(actual[index] - values[21 + index]);
                    // Large translated coordinates have a coarser single-
                    // precision grid than the dimensionless affine axes.
                    float expected = values[21 + index];
                    int bits = BitConverter.ToInt32(BitConverter.GetBytes(expected), 0);
                    float adjacent = BitConverter.ToSingle(BitConverter.GetBytes(bits + 1), 0);
                    float tolerance = .00002f + 2 * Math.Abs(adjacent - expected);
                    if (float.IsNaN(error) || error > tolerance)
                        throw new Exception("Physics observation differs: case " + cases + ", frame " + frames
                            + ", component " + index + ", error " + error.ToString("R", CultureInfo.InvariantCulture));
                    maximum = Math.Max(maximum, error);
                }
                frames++;
            }
        }
        if (cases != 34 || frames != 4080) throw new Exception("Original physics observation coverage differs.");
        Console.WriteLine("Player physics observations passed: " + cases + " cases, " + frames
            + " frames, max error " + maximum.ToString("R", CultureInfo.InvariantCulture));
    }

    private static ProfessionNativeSpringConstraint.Affine Pose(float[] values, int offset) =>
        new ProfessionNativeSpringConstraint.Affine { A = values[offset], B = values[offset + 1],
            C = values[offset + 2], D = values[offset + 3], X = values[offset + 4],
            Y = values[offset + 5], Length = values[offset + 6] };

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
}

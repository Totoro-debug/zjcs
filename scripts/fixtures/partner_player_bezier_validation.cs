using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Zjcs.Offline.ProfessionUI;

public static class PartnerPlayerBezierValidation
{
    public static int Main(string[] args)
    {
        ProfessionNativeBezierPolyline curve = null;
        int cases = 0, checks = 0;
        float maximum = 0;
        foreach (string row in File.ReadLines(args[0]))
        {
            if (row.StartsWith("#") || row.Length == 0) continue;
            var cells = row.Split('|');
            float[] values = cells.Skip(1).Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray();
            if (cells[0] == "C")
            {
                if (values.Length != 8) throw new Exception("Original numerical curve layout differs");
                curve = new ProfessionNativeBezierPolyline(values[0], values[1], values[2], values[3],
                    values[4], values[5], values[6], values[7]); cases++;
            }
            else
            {
                if (cells[0] != "F" || values.Length != 2 || curve == null) throw new Exception("Invalid numerical observation");
                float error = Math.Abs(curve.Evaluate(values[0]) - values[1]);
                maximum = Math.Max(maximum, error);
                if (float.IsNaN(error) || error > .00002f) throw new Exception("Original player curve differs at case=" + cases + ", time=" + values[0] + ", error=" + error);
                checks++;
            }
        }
        if (cases != 4 || checks != 244) throw new Exception("Original player numerical observations are incomplete");
        Console.WriteLine("Independent player Bezier observations passed: " + checks + ", maximum error=" + maximum.ToString("R", CultureInfo.InvariantCulture));
        return 0;
    }
}

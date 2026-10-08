using System.Globalization;

namespace Stillhouse.Components;

/// <summary>Small helpers shared by the SVG components.</summary>
public static class Draw
{
    static readonly (double t, (int r, int g, int b) c)[] Ramp =
    [
        (30, (52, 86, 112)),
        (50, (82, 120, 128)),
        (65, (128, 138, 96)),
        (78, (204, 152, 62)),
        (92, (205, 96, 44)),
        (108, (150, 34, 30)),
    ];

    /// <summary>Rickhouse heat ramp: winter slate, spring moss, July amber, top-floor rust.</summary>
    public static string Heat(double t)
    {
        if (t <= Ramp[0].t) return Hex(Ramp[0].c);
        for (int i = 1; i < Ramp.Length; i++)
            if (t <= Ramp[i].t)
            {
                var (t0, a) = Ramp[i - 1]; var (t1, b) = Ramp[i];
                double k = (t - t0) / (t1 - t0);
                return Hex(((int)(a.r + (b.r - a.r) * k), (int)(a.g + (b.g - a.g) * k), (int)(a.b + (b.b - a.b) * k)));
            }
        return Hex(Ramp[^1].c);
    }

    static string Hex((int r, int g, int b) c) => $"#{c.r:x2}{c.g:x2}{c.b:x2}";

    /// <summary>Invariant number for SVG attributes.</summary>
    public static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Poly(IEnumerable<(double x, double y)> pts) =>
        string.Join(' ', pts.Select(p => $"{N(p.x)},{N(p.y)}"));

    public static string Years(double y) => y < 1 ? $"{y * 12:0} mo" : $"{y:0.0} yrs";
}

namespace Stillhouse.Model;

/// <summary>Weekly snapshot of what is inside one barrel.</summary>
public readonly record struct Snap(
    int Day,            // climate day index
    float Volume,       // gallons of liquid you could still dump
    float Proof,
    float Soaked,       // gallons held in the staves
    float Depth,        // mm the spirit is currently pushed into the wood
    float Cycles,       // cumulative °F of breathing (day/night swing + weather)
    float WeekTemp,     // mean °F this week
    Flavor Flavor,
    float Score);       // 0..100 how good it would taste dumped today

/// <summary>0..10 on each axis, concentrations as they would be in the glass.</summary>
public readonly record struct Flavor(float Vanilla, float Oak, float Spice, float Fruit, float Heat, float Harsh)
{
    public static Flavor Blend(IEnumerable<(Flavor f, double weight)> parts)
    {
        double w = 0, v = 0, o = 0, s = 0, fr = 0, h = 0, hr = 0;
        foreach (var (f, k) in parts)
        {
            w += k; v += f.Vanilla * k; o += f.Oak * k; s += f.Spice * k;
            fr += f.Fruit * k; h += f.Heat * k; hr += f.Harsh * k;
        }
        if (w <= 0) return default;
        return new((float)(v / w), (float)(o / w), (float)(s / w), (float)(fr / w), (float)(h / w), (float)(hr / w));
    }
}

public sealed class Barrel
{
    public const double FillGallons = 53;
    public const int ProjectYears = 20;

    public required string Serial { get; init; }
    public required int Floor { get; init; }          // 0 = ground
    public required int Slot { get; init; }
    public required DateOnly FillDate { get; init; }
    public required double EntryProof { get; init; }
    public required int CharLevel { get; init; }      // 1..4
    public Recipe Recipe { get; init; } = Recipe.Traditional;
    public Snap[] Path { get; private set; } = [];

    public int FillDay(Climate c) => c.DayIndex(FillDate);

    public void Age(Climate c) => Path = Physics.Run(c, Floor, FillDate, EntryProof, CharLevel, Recipe.Mods);

    /// <summary>Snapshot at a calendar date, or null if not filled yet.</summary>
    public Snap? At(Climate c, DateOnly date)
    {
        int d = c.DayIndex(date) - FillDay(c);
        if (d < 0 || Path.Length == 0) return null;
        return Path[Math.Min(d / 7, Path.Length - 1)];
    }

    public double AgeYears(DateOnly date) => (date.DayNumber - FillDate.DayNumber) / 365.25;

    /// <summary>Distilling season for bottled-in-bond: Jan–Jun or Jul–Dec of one year.</summary>
    public string Season => $"{(FillDate.Month <= 6 ? "Spring" : "Fall")} {FillDate.Year}";

    public (int flavorPeakWeek, int valuePeakWeek) Peaks() => Physics.Peaks(Path);
}

public static class Physics
{
    // Permeation through the staves. Water leaves in proportion to how dry the air is;
    // ethanol leaves regardless. They balance at ~62% relative humidity: drier floors
    // lose more water (proof climbs), damper floors lose more ethanol (proof falls).
    const double Kw = 2.05e-4, Ke = Kw * 0.38;

    // Char #1..#4: thicker char means a thicker red layer underneath, more soak, faster sulfur stripping.
    static readonly double[] CharVanilla = [0.82, 0.90, 1.0, 1.08];
    static readonly double[] CharSpice = [0.85, 0.92, 1.0, 1.15];
    static readonly double[] CharSoak = [2.8, 2.95, 3.1, 3.5];
    static readonly double[] CharStrip = [0.7, 0.85, 1.0, 1.1];

    public static Snap[] Run(Climate c, int floor, DateOnly fill, double entryProof, int charLevel, AgingMods? mods = null)
    {
        var m = mods ?? AgingMods.Neutral;
        int ci = Math.Clamp(charLevel, 1, 4) - 1;
        int start = c.DayIndex(fill);
        int days = Math.Min(Barrel.ProjectYears * 365 + 3, c.Days - start);
        var path = new Snap[days / 7 + 1];

        double v = Barrel.FillGallons;
        double e = v * entryProof / 200;                 // gallons of ethanol
        double soaked = 0, soakMax = CharSoak[ci];
        double depth = 1.0, cycles = 0;
        double vanPool = 1, tanPool = 1, spicePool = 1, esters = m.EsterStart, harsh = m.Harsh;
        double vanSize = CharVanilla[ci] * m.Sweet;      // corn sugars read as caramel once the oak arrives
        double spiceSize = CharSpice[ci] * m.Spice;      // rye spice builds on the char's clove and pepper
        double prevT = c.FloorTemp[floor, start], weekT = 0;

        for (int i = 0; i < days; i++)
        {
            int d = start + i;
            double t = c.FloorTemp[floor, d], swing = c.FloorSwing[floor, d], rh = c.FloorRh[floor, d];
            double fT = Math.Exp(0.032 * (t - 60));         // vapor pressure ~ doubles per 22°F
            double fE = Math.Exp(0.035 * (t - 60));         // extraction chemistry

            // 1. The staves drink first: ~3 gallons in the first months.
            double soak = (soakMax - soaked) / 100 * Math.Sqrt(fT);
            double abv = e / v;
            soaked += soak; e -= soak * abv; v -= soak;

            // 2. Angel's share, split into water and ethanol by humidity.
            abv = e / v;
            double waterOut = Kw * fT * (1 - rh) * (1 - abv) * v;
            double alcOut = Ke * fT * abv * v;
            v -= waterOut + alcOut; e -= alcOut;

            // 3. Breathing: heat expands the spirit into the char, cold pulls it back.
            //    Every degree of swing is spirit moving through the red layer.
            double breath = 2 * swing + Math.Abs(t - prevT);
            cycles += breath;
            double depthEq = Math.Clamp(1.5 + 0.07 * (t - 40), 0.8, 6.0);
            depth += (depthEq - depth) * 0.2;
            double access = depth / 3;
            double pump = 0.6 + breath / 30;

            // 4. Extraction from finite pools in the wood, driven by the breathing.
            vanPool -= 1.05e-4 * pump * access * fE * vanPool;
            tanPool -= 2.6e-4 * pump * access * fE * tanPool;
            spicePool -= 1.9e-4 * pump * access * fE * spicePool;
            harsh -= 1.15e-3 * CharStrip[ci] * harsh * fE * (0.5 + access / 2);       // char strips sulfur
            double lostFrac = 1 - v / Barrel.FillGallons;
            esters += 1.9e-4 * m.Fruit * Math.Sqrt(fE) * (1 + 6 * lostFrac);   // slow oxidation; more air, more fruit

            prevT = t;
            weekT += t;
            if (i % 7 == 6 || i == days - 1)
            {
                int n = i % 7 + 1;
                double conc = Barrel.FillGallons / v;                  // evaporation concentrates flavor
                double proof = 200 * e / v;
                var fl = new Flavor(
                    Vanilla: Soft(9.0 * vanSize * (1 - vanPool) * conc),
                    Oak: Soft(6.2 * m.Grip * (1 - tanPool) * conc),
                    Spice: Soft(5.5 * spiceSize * (1 - spicePool) * conc),
                    Fruit: Soft(2.5 * esters * conc),
                    Heat: Cl((proof - 80) / 7),
                    Harsh: Cl(10 * harsh));
                path[i / 7] = new Snap(d, (float)v, (float)proof, (float)soaked, (float)depth,
                    (float)cycles, (float)(weekT / n), fl, (float)Score(fl, v));
                weekT = 0;
            }
        }
        return path;
    }

    /// <summary>
    /// How good it would taste if dumped now. Rewards vanilla, fruit and spice,
    /// tolerates oak up to a point, punishes new-make and tannin that outruns sweetness.
    /// </summary>
    public static double Score(Flavor f, double volume)
    {
        double good = 0.34 * f.Vanilla + 0.26 * f.Fruit + 0.2 * f.Spice + 0.2 * Math.Min(f.Oak, 5.5);
        double bad = 0.55 * f.Harsh
                   + 0.9 * Math.Max(0, f.Oak - 0.9 * f.Vanilla - 0.8)
                   + 1.4 * Math.Max(0, f.Oak - 7.0)
                   + 0.25 * Math.Max(0, f.Heat - 8.2)
                   + 18 * Math.Max(0, 0.55 - volume / Barrel.FillGallons);
        return Math.Clamp(11 * (good - bad) + 17, 0, 100);
    }

    public static (int flavor, int value) Peaks(Snap[] p)
    {
        int bf = 0, bv = 0;
        for (int i = 0; i < p.Length; i++)
        {
            if (p[i].Score > p[bf].Score) bf = i;
            if (p[i].Score * p[i].Volume > p[bv].Score * p[bv].Volume) bv = i;
        }
        return (bf, bv);
    }

    static float Cl(double x) => (float)Math.Clamp(x, 0, 10);

    /// <summary>The palate saturates: doubling vanillin doesn't taste twice as vanilla.</summary>
    static float Soft(double x) => (float)(10 * (1 - Math.Exp(-Math.Max(0, x) / 7)));
}

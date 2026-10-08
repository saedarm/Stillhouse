namespace Stillhouse.Model;

/// <summary>
/// Synthetic central-Kentucky weather and the seven-floor rickhouse it drives.
/// Everything is precomputed once per day from Epoch to End so every barrel on
/// a floor sees exactly the same air.
/// </summary>
public sealed class Climate
{
    public static readonly DateOnly Epoch = new(2008, 1, 1);
    public static readonly DateOnly End = new(2046, 12, 31);
    public const int Floors = 7;

    public int Days { get; }
    public float[] OutTemp { get; }          // °F daily mean outside
    public float[,] FloorTemp { get; }       // [floor, day] °F daily mean
    public float[,] FloorSwing { get; }      // [floor, day] ±°F day/night swing
    public float[,] FloorRh { get; }         // [floor, day] relative humidity 0..1

    public Climate(int seed = 1792)
    {
        Days = End.DayNumber - Epoch.DayNumber + 1;
        OutTemp = new float[Days];
        FloorTemp = new float[Floors, Days];
        FloorSwing = new float[Floors, Days];
        FloorRh = new float[Floors, Days];

        var rng = new Random(seed);
        double anomaly = 0, yearAnomaly = 0;
        var lag = new double[Floors];
        for (int f = 0; f < Floors; f++) lag[f] = 40;

        for (int d = 0; d < Days; d++)
        {
            var date = Epoch.AddDays(d);
            double doy = date.DayOfYear;
            if (doy == 1) yearAnomaly = Gauss(rng) * 1.6;          // warm years / cold years

            // Louisville-ish normals: ~35°F mid-January, ~78°F mid-July.
            double normal = 56.5 - 21.5 * Math.Cos(2 * Math.PI * (doy - 18) / 365.25);
            anomaly = 0.82 * anomaly + Gauss(rng) * 3.6;           // fronts come and go
            double tOut = normal + anomaly + yearAnomaly;
            OutTemp[d] = (float)tOut;

            // Sun on the tin roof: 1 at the solstice, 0 in late December.
            double sun = 0.5 + 0.5 * Math.Cos(2 * Math.PI * (doy - 172) / 365.25);
            double rhOut = 0.71 + 0.04 * Math.Cos(2 * Math.PI * (doy - 15) / 365.25) + Gauss(rng) * 0.04;
            double vapor = Math.Clamp(rhOut, 0.35, 0.98) * SatVapor(tOut);

            for (int f = 0; f < Floors; f++)
            {
                double h = f / 6.0;                                  // 0 = ground, 1 = top
                double tau = 20 - 12 * h;                            // ground floor has the most thermal mass
                lag[f] += (tOut - lag[f]) / tau;
                double strat = h * (5 + 20 * sun);                   // hot air rises, roof bakes
                double ground = 0.35 * (1 - h) * (1 - h);            // earth floor pulls toward 57°F
                double t = (1 - ground) * (lag[f] + strat) + ground * 57;
                FloorTemp[f, d] = (float)t;
                FloorSwing[f, d] = (float)(1.5 + 9 * h * (0.5 + 0.5 * sun) + 1.0 * h * Math.Abs(Gauss(rng)));

                // Same moisture, warmer air => drier. Gravel floor adds damp at the bottom.
                double damp = 1 + 0.10 * (1 - h) * (1 - h);
                FloorRh[f, d] = (float)Math.Clamp(vapor * damp / SatVapor(t), 0.15, 0.97);
            }
        }
    }

    public int DayIndex(DateOnly date) => Math.Clamp(date.DayNumber - Epoch.DayNumber, 0, Days - 1);

    /// <summary>Saturation vapor pressure (hPa), Magnus formula, input °F.</summary>
    static double SatVapor(double tf)
    {
        double c = (tf - 32) / 1.8;
        return 6.112 * Math.Exp(17.62 * c / (243.12 + c));
    }

    static double Gauss(Random r)
    {
        double u1 = 1 - r.NextDouble(), u2 = r.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }
}

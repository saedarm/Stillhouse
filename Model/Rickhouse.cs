namespace Stillhouse.Model;

public sealed class Rickhouse
{
    public const int SlotsPerFloor = 12;
    public Climate Climate { get; } = new();
    public List<Barrel> Barrels { get; } = [];

    int labFills;

    public Rickhouse(int seed = 1964)
    {
        var rng = new Random(seed);
        var mashRng = new Random(seed + 1);
        Recipe trad = Recipe.Traditional, highRye = Recipe.HighRye, wheated = Recipe.Wheated;
        int[] proofs = [110, 114, 118, 120, 125];
        for (int f = 0; f < Climate.Floors; f++)
            for (int s = 0; s < SlotsPerFloor; s++)
            {
                if (rng.NextDouble() < 0.12) continue;                    // a few empty ricks
                var fill = new DateOnly(2010, 1, 1).AddDays(rng.Next(0, 365 * 16 + 120));
                var b = new Barrel
                {
                    Serial = $"{fill.Year % 100:00}-{rng.Next(1000, 9999)}",
                    Floor = f,
                    Slot = s,
                    FillDate = fill,
                    EntryProof = proofs[rng.Next(proofs.Length)],
                    CharLevel = rng.NextDouble() < 0.6 ? 4 : 3,
                    Recipe = mashRng.NextDouble() switch { < 0.6 => trad, < 0.85 => highRye, _ => wheated },
                };
                b.Age(Climate);
                Barrels.Add(b);
            }
    }

    public Barrel? At(int floor, int slot) => Barrels.FirstOrDefault(b => b.Floor == floor && b.Slot == slot);

    /// <summary>Rolls a new barrel of this recipe into the first empty rick on a floor. Null if the floor is full.</summary>
    public Barrel? Fill(Recipe recipe, int floor, DateOnly date)
    {
        int slot = Enumerable.Range(0, SlotsPerFloor).FirstOrDefault(s => At(floor, s) is null, -1);
        if (slot < 0) return null;
        var b = new Barrel
        {
            Serial = $"{date.Year % 100:00}-L{++labFills:000}",
            Floor = floor,
            Slot = slot,
            FillDate = date,
            EntryProof = recipe.EntryProof,
            CharLevel = recipe.CharLevel,
            Recipe = recipe.Clone(),
        };
        b.Age(Climate);
        Barrels.Add(b);
        return b;
    }

    public int EmptyRicks(int floor) => Enumerable.Range(0, SlotsPerFloor).Count(s => At(floor, s) is null);

    /// <summary>A recipe filled today, aged on every floor.</summary>
    public Snap[][] Preview(Recipe r, DateOnly fill)
    {
        var mods = r.Mods;
        return Enumerable.Range(0, Climate.Floors)
            .Select(f => Physics.Run(Climate, f, fill, r.EntryProof, r.CharLevel, mods)).ToArray();
    }

    /// <summary>The same fill, aged on each floor, for "what if it had been up there".</summary>
    public Snap[][] EveryFloor(Barrel b) =>
        Enumerable.Range(0, Climate.Floors)
            .Select(f => f == b.Floor ? b.Path : Physics.Run(Climate, f, b.FillDate, b.EntryProof, b.CharLevel, b.Recipe.Mods))
            .ToArray();
}

public sealed record BlendResult(
    int Barrels,
    double FilledGallons,
    double Gallons,
    double Soaked,
    double BarrelProof,
    double ProofGallons,
    double AngelsSharePct,
    double YoungestYears,
    double OldestYears,
    Flavor Flavor,
    double Score,
    int Bottles,
    double BottleProof,
    bool Straight,
    bool BondEligible,
    string? BondBlocker,
    string Classification,
    string ColorHex,
    string Notes,
    IReadOnlyList<int> Floors)
{
    public const double BottleGallons = 0.198129;     // 750 mL

    public static BlendResult? Compute(Rickhouse house, IEnumerable<Barrel> picks, DateOnly date, double? bottleProof)
    {
        var rows = picks.Select(b => (b, s: b.At(house.Climate, date))).Where(x => x.s is not null)
                        .Select(x => (x.b, s: x.s!.Value)).ToList();
        if (rows.Count == 0) return null;

        double gal = rows.Sum(r => r.s.Volume);
        double eth = rows.Sum(r => r.s.Volume * r.s.Proof / 200);
        double proof = 200 * eth / gal;
        double pg = gal * proof / 100;
        double filled = rows.Count * Barrel.FillGallons;
        double young = rows.Min(r => r.b.AgeYears(date)), old = rows.Max(r => r.b.AgeYears(date));
        var flavor = Flavor.Blend(rows.Select(r => (r.s.Flavor, (double)r.s.Volume)));
        double bp = Math.Clamp(bottleProof ?? proof, 80, proof);
        int bottles = (int)Math.Floor(pg * 100 / bp / BottleGallons);
        flavor = flavor with { Heat = (float)Math.Clamp((bp - 80) / 7, 0, 10) };   // water tames the burn

        bool straight = young >= 2;
        string? blocker = null;
        if (young < 4) blocker = "needs 4 years";
        else if (rows.Select(r => r.b.Season).Distinct().Count() > 1) blocker = "mixes distilling seasons";
        else if (Math.Abs(bp - 100) > 0.05) blocker = "must be bottled at 100 proof";
        bool bond = blocker is null;

        string cls = bond ? "Bottled-in-Bond Kentucky Straight Bourbon Whiskey"
                   : straight ? "Kentucky Straight Bourbon Whiskey"
                   : "Kentucky Bourbon Whiskey";
        if (rows.Count == 1) cls = "Single Barrel " + cls;

        return new BlendResult(rows.Count, filled, gal, rows.Sum(r => r.s.Soaked), proof, pg,
            100 * (1 - gal / filled), young, old, flavor, Physics.Score(flavor, gal / rows.Count),
            bottles, bp, straight, bond, blocker, cls, Color(flavor, bp),
            TastingNotes(flavor, proof), rows.Select(r => r.b.Floor).Distinct().Order().ToList());
    }

    /// <summary>Straw to deep mahogany, from extraction depth; dilution lightens it slightly.</summary>
    static string Color(Flavor f, double bottleProof)
    {
        double x = Math.Clamp((f.Oak + f.Vanilla) / 18.0, 0, 1) * (0.85 + 0.15 * bottleProof / 140);
        (double r, double g, double b) a = (226, 176, 92), z = (118, 46, 18);
        int R = (int)(a.r + (z.r - a.r) * x), G = (int)(a.g + (z.g - a.g) * x), B = (int)(a.b + (z.b - a.b) * x);
        return $"#{R:x2}{G:x2}{B:x2}";
    }

    static string TastingNotes(Flavor f, double proof)
    {
        var parts = new List<string>();
        var ranked = new (string word, float v)[]
        {
            (f.Vanilla > 6 ? "caramel and baked vanilla" : "light vanilla", f.Vanilla),
            (f.Oak > 7 ? "dry, tannic oak" : "toasted oak", f.Oak),
            (f.Spice > 6 ? "clove and black pepper" : "a little baking spice", f.Spice),
            (f.Fruit > 6 ? "dark cherry and orange peel" : "green apple", f.Fruit),
        }.OrderByDescending(x => x.v).ToList();
        parts.Add(ranked[0].word);
        parts.Add(ranked[1].word);
        string lead = $"Leads with {parts[0]}, then {parts[1]}.";
        string tail = f.Harsh > 3 ? " Still some grainy new-make on the finish."
                    : f.Oak > 7.6 ? " Long, drying finish; the wood is close to taking over."
                    : proof > 130 ? " Hot at full strength; a few drops of water open it."
                    : " Finish is warm and clean.";
        return lead + tail;
    }
}

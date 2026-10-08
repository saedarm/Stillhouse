namespace Stillhouse.Model;

/// <summary>What a grain brings to new-make, per 100% of the bill. Axes are 0..1 strengths.</summary>
public sealed record Grain(
    string Key, string Name, string Note, string Color,
    double Sweet, double Spice, double Bread, double Nutty, double Fruit, double Smoke, double Creamy,
    double ProofGallonsPerBushel, double Enzymes, GrainClass Class);

public enum GrainClass { Corn, Rye, Wheat, Malt, Other }

public sealed record Yeast(string Key, string Name, string Note, double Fruit, double Floral, double Spice, double Yield, double Harsh);

/// <summary>Multipliers the barrel physics applies. Neutral = the traditional recipe.</summary>
public sealed record AgingMods(double Sweet, double Spice, double Fruit, double Grip, double Harsh, double EsterStart)
{
    public static readonly AgingMods Neutral = new(1, 1, 1, 1, 1, 0);
}

/// <summary>New-make straight off the still: 0..10 per axis, plus the paperwork.</summary>
public sealed record NewMake(
    double Sweet, double Spice, double Bread, double Nutty, double Fruit, double Floral, double Smoke, double Bite,
    double ProofGallonsPerBushel, double BarrelsPer1000Bushels, double EnzymeFactor,
    string Classification, string Style, bool IsLegalBourbon, IReadOnlyList<(string rule, bool ok)> Checks,
    IReadOnlyList<string> Warnings, string Notes, AgingMods Mods);

public static class Grains
{
    public static readonly Grain[] All =
    [
        new("corn", "Yellow corn", "Sweet, full body. Bourbon needs 51% or more.", "#e2b445",
            1.0, 0.05, 0.10, 0.10, 0.05, 0, 0.30, 5.0, 0, GrainClass.Corn),
        new("bluecorn", "Blue corn", "Nuttier and earthier than yellow. Counts as corn.", "#5d73ad",
            0.75, 0.10, 0.10, 0.65, 0.10, 0, 0.25, 4.6, 0, GrainClass.Corn),
        new("rye", "Rye", "Pepper, mint, baking spice, dark fruit.", "#8b5a3a",
            0.10, 1.0, 0.20, 0.05, 0.20, 0, 0.05, 4.4, 0.05, GrainClass.Rye),
        new("wheat", "Wheat", "Soft, bready, honeyed. Takes the edge off.", "#d8c286",
            0.40, 0.02, 0.80, 0.10, 0.05, 0, 0.45, 4.8, 0.05, GrainClass.Wheat),
        new("barley", "Malted barley", "Nutty and cereal. Its enzymes turn starch into sugar.", "#b8894a",
            0.20, 0.05, 0.50, 0.80, 0.05, 0, 0.10, 4.3, 1.0, GrainClass.Malt),
        new("oats", "Oats", "Creamy, oatmeal-cookie softness. Thick, sticky mash.", "#cfc5a5",
            0.20, 0.02, 0.30, 0.40, 0.05, 0, 1.0, 3.4, 0, GrainClass.Other),
        new("smoked", "Smoked malt", "Wood smoke and bacon. A little goes a long way.", "#6a4636",
            0.10, 0.10, 0.20, 0.40, 0, 1.0, 0.05, 4.2, 0.6, GrainClass.Malt),
    ];

    public static Grain Get(string key) => All.First(g => g.Key == key);
}

public static class Yeasts
{
    public static readonly Yeast[] All =
    [
        new("classic", "Classic bourbon", "Balanced fruit and a little spice.", 1.0, 0.5, 0.5, 1.0, 1.0),
        new("fruity", "Fruit-forward", "Banana, pear and stone fruit.", 1.5, 0.6, 0.3, 0.98, 1.05),
        new("floral", "Floral", "Rose, honeysuckle, light citrus.", 0.9, 1.4, 0.3, 0.98, 1.0),
        new("spicy", "Herbal and spicy", "Clove, black tea, herbs.", 0.8, 0.4, 1.0, 1.0, 1.05),
        new("clean", "Clean, high yield", "Neutral. More alcohol, less character.", 0.6, 0.3, 0.3, 1.06, 0.9),
    ];

    public static Yeast Get(string key) => All.First(y => y.Key == key);
}

/// <summary>A full still-house recipe: grain bill through barrel entry.</summary>
public sealed class Recipe
{
    public string Name { get; set; } = "Traditional";
    public Dictionary<string, int> Bill { get; set; } = new();
    public string YeastKey { get; set; } = "classic";
    public int FermentDays { get; set; } = 4;
    public int FermentTempF { get; set; } = 84;
    public int SourMashPct { get; set; } = 25;
    public bool PotStill { get; set; }
    public int StillProof { get; set; } = 135;
    public int EntryProof { get; set; } = 120;
    public int CharLevel { get; set; } = 4;

    public int Pct(string key) => Bill.GetValueOrDefault(key);

    public Recipe Clone() => new()
    {
        Name = Name, Bill = new(Bill), YeastKey = YeastKey, FermentDays = FermentDays, FermentTempF = FermentTempF,
        SourMashPct = SourMashPct, PotStill = PotStill, StillProof = StillProof, EntryProof = EntryProof, CharLevel = CharLevel,
    };

    /// <summary>Short mash bill, largest grain first: "60% corn, 36% rye, 4% malted barley".</summary>
    public string BillText => string.Join(", ", Bill.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value)
        .Select(kv => $"{kv.Value}% {Grains.Get(kv.Key).Name.ToLowerInvariant()}"));

    public static Recipe Make(string name, params (string key, int pct)[] bill) =>
        new() { Name = name, Bill = bill.ToDictionary(b => b.key, b => b.pct) };

    public static readonly Recipe Traditional = Make("Traditional", ("corn", 75), ("rye", 13), ("barley", 12));

    public static Recipe HighRye => With(Make("High-rye", ("corn", 60), ("rye", 36), ("barley", 4)), r => r.CharLevel = 4);
    public static Recipe Wheated => With(Make("Wheated", ("corn", 70), ("wheat", 20), ("barley", 10)), r => { r.EntryProof = 114; r.CharLevel = 3; });
    public static Recipe FourGrain => Make("Four-grain", ("corn", 60), ("rye", 15), ("wheat", 15), ("barley", 10));
    public static Recipe CornForward => With(Make("Corn-forward", ("corn", 88), ("rye", 4), ("barley", 8)), r => r.YeastKey = "fruity");

    public static IReadOnlyList<Recipe> Presets => [Traditional.Clone(), HighRye, Wheated, FourGrain, CornForward];

    static Recipe With(Recipe r, Action<Recipe> a) { a(r); return r; }

    /// <summary>A random but plausible recipe, usually with something unusual in it.</summary>
    public static Recipe Surprise(Random rng)
    {
        var r = new Recipe { Bill = new() };
        int corn = rng.Next(51, 81);
        string[] extras = ["rye", "wheat", "oats", "smoked", "bluecorn", "rye", "wheat"];
        int left = 100 - corn;
        int malt = Math.Min(left, rng.Next(5, 13));
        left -= malt;
        bool blue = rng.NextDouble() < 0.3;
        if (blue) { r.Bill["bluecorn"] = corn / 2; r.Bill["corn"] = corn - corn / 2; } else r.Bill["corn"] = corn;
        r.Bill["barley"] = malt;
        while (left > 0)
        {
            var k = extras[rng.Next(extras.Length)];
            if (k == "bluecorn") k = "rye";
            int cap = k == "smoked" ? Math.Min(left, 6) : k == "oats" ? Math.Min(left, 12) : left;
            int take = left <= 4 ? left : Math.Max(2, rng.Next(2, cap + 1));
            r.Bill[k] = r.Pct(k) + take;
            left -= take;
        }
        r.YeastKey = Yeasts.All[rng.Next(Yeasts.All.Length)].Key;
        r.FermentDays = rng.Next(3, 8);
        r.FermentTempF = rng.Next(78, 93);
        r.SourMashPct = rng.Next(0, 9) * 5;
        r.PotStill = rng.NextDouble() < 0.35;
        r.StillProof = rng.Next(23, 33) * 5;          // 115..160
        r.EntryProof = rng.Next(21, 26) * 5;          // 105..125
        r.CharLevel = rng.Next(2, 5);
        r.Name = Mash.Nickname(r);
        return r;
    }

    // Aging modifiers are cached per recipe state; cheap to recompute but called per barrel.
    public AgingMods Mods => Mash.Analyze(this).Mods;
}

public static class Mash
{
    /// <summary>Raw sensory sums, before normalizing against the traditional recipe.</summary>
    record Raw(double Sweet, double Spice, double Bread, double Nutty, double Fruit, double Floral, double Smoke, double Creamy, double Bite);

    static Raw Sum(Recipe r)
    {
        double sweet = 0, spice = 0, bread = 0, nutty = 0, fruit = 0, smoke = 0, creamy = 0;
        foreach (var (k, pct) in r.Bill)
        {
            var g = Grains.Get(k); double f = pct / 100.0;
            sweet += g.Sweet * f; spice += g.Spice * f; bread += g.Bread * f; nutty += g.Nutty * f;
            fruit += g.Fruit * f; smoke += g.Smoke * f; creamy += g.Creamy * f;
        }
        var y = Yeasts.Get(r.YeastKey);

        // Fermentation: longer and warmer makes more esters (fruit) and more fusel oils (bite).
        double esterF = (0.75 + 0.07 * (r.FermentDays - 3)) * (1 + 0.025 * (r.FermentTempF - 82));
        double fusel = 1 + 0.03 * Math.Max(0, r.FermentTempF - 84) + 0.04 * Math.Max(0, r.FermentDays - 5);
        double sour = 1 + 0.004 * r.SourMashPct;     // backset adds a little tang and keeps the ferment clean

        // The still: lower proof off the still keeps more of the grain and the congeners.
        double keep = 1 + (135 - r.StillProof) / 80.0;
        double pot = r.PotStill ? 1.15 : 1.0;

        return new Raw(
            Sweet: sweet * keep,
            Spice: (spice + 0.06 * y.Spice) * keep,
            Bread: bread * keep * pot,
            Nutty: nutty * keep * pot,
            Fruit: (fruit + 0.4) * y.Fruit * esterF * sour * keep * pot,
            Floral: 0.4 * y.Floral * esterF * keep,
            Smoke: smoke * keep,
            Creamy: creamy * keep * pot,
            Bite: y.Harsh * fusel * (1 + (135 - r.StillProof) / 90.0) * pot * (1 - 0.004 * r.SourMashPct));
    }

    static readonly Raw Base = Sum(Recipe.Traditional);

    public static NewMake Analyze(Recipe r)
    {
        var raw = Sum(r);
        int corn = r.Pct("corn") + r.Pct("bluecorn"), rye = r.Pct("rye"), wheat = r.Pct("wheat");
        int malt = r.Pct("barley") + r.Pct("smoked");

        // Conversion: malted barley's enzymes turn starch into sugar. Below ~6% it starts to stall.
        double enzymes = r.Bill.Sum(kv => Grains.Get(kv.Key).Enzymes * kv.Value);
        double enzymeF = Math.Min(1, 0.6 + 0.4 * enzymes / 6);
        double pgb = r.Bill.Sum(kv => Grains.Get(kv.Key).ProofGallonsPerBushel * kv.Value / 100.0)
                     * enzymeF * Yeasts.Get(r.YeastKey).Yield * (r.PotStill ? 0.95 : 1.0);
        double barrels = 1000 * pgb / (Barrel.FillGallons * r.EntryProof / 100.0);

        var checks = new List<(string, bool)>
        {
            ($"At least 51% corn (this has {corn}%)", corn >= 51),
            ($"Off the still at 160 proof or lower ({r.StillProof})", r.StillProof <= 160),
            ($"Into the barrel at 125 proof or lower ({r.EntryProof})", r.EntryProof <= 125),
            ("Aged in new, charred oak", true),
        };
        bool bourbon = checks.All(c => c.Item2);
        bool distilled = r.StillProof <= 160 && r.EntryProof <= 125;

        string cls = bourbon ? "Bourbon whiskey"
            : !distilled ? "American whiskey (off-spec proof)"
            : rye >= 51 ? "Rye whiskey"
            : wheat >= 51 ? "Wheat whiskey"
            : malt >= 51 ? "Malt whiskey"
            : "American whiskey";

        string style = Style(r, corn, rye, wheat, malt);

        var warn = new List<string>();
        if (enzymes < 4) warn.Add($"Only {enzymes:0}% enzyme power from malt. The starch won't fully convert, so yield drops.");
        if (r.Pct("oats") > 15) warn.Add("Over 15% oats makes a thick, sticky mash that's hard to pump and scorches easily.");
        if (r.Pct("smoked") > 8) warn.Add("Over 8% smoked malt will taste like a campfire for a decade.");
        if (r.FermentTempF > 90) warn.Add("Fermenting above 90°F stresses the yeast and throws off solvent notes.");
        if (r.StillProof < 120 && !r.PotStill) warn.Add("Column stills rarely run this low; you'll lose efficiency.");

        // Relative to traditional: these drive the barrel physics. Traditional is exactly neutral.
        static double Rel(double x, double b, double k, double lo, double hi) => Math.Clamp(1 + k * (x / b - 1), lo, hi);
        double soft = raw.Creamy + 0.5 * raw.Bread;
        double baseSoft = Base.Creamy + 0.5 * Base.Bread;
        var mods = new AgingMods(
            Sweet: Rel(raw.Sweet, Base.Sweet, 0.55, 0.6, 1.5),
            Spice: Rel(raw.Spice + 0.5 * raw.Smoke, Base.Spice, 0.45, 0.6, 1.9),
            Fruit: Rel(raw.Fruit + 0.5 * raw.Floral, Base.Fruit + 0.5 * Base.Floral, 0.7, 0.5, 1.9),
            Grip: Rel(soft, baseSoft, -0.35, 0.7, 1.15),
            Harsh: Rel(raw.Bite, Base.Bite, 1.0, 0.6, 1.6),
            EsterStart: Math.Max(0, 0.6 * (raw.Fruit / Base.Fruit - 1)));

        double S(double x, double b, double mid) => Math.Clamp(mid * x / b, 0, 10);
        var nm = new NewMake(
            Sweet: S(raw.Sweet, Base.Sweet, 6.5),
            Spice: S(raw.Spice, Base.Spice, 3.0),
            Bread: S(raw.Bread, Base.Bread, 3.0),
            Nutty: S(raw.Nutty, Base.Nutty, 3.0),
            Fruit: S(raw.Fruit, Base.Fruit, 4.0),
            Floral: S(raw.Floral, Base.Floral, 2.4),
            Smoke: Math.Clamp(raw.Smoke * 60, 0, 10),
            Bite: S(raw.Bite, Base.Bite, 5.0),
            pgb, barrels, enzymeF, cls, style, bourbon, checks, warn, "", mods);
        return nm with { Notes = NewMakeNotes(nm) };
    }

    static string Style(Recipe r, int corn, int rye, int wheat, int malt)
    {
        var bits = new List<string>();
        if (r.Pct("smoked") >= 2) bits.Add("smoked");
        if (r.Pct("bluecorn") >= 15) bits.Add("blue corn");
        if (r.Pct("oats") >= 5) bits.Add("oat");
        string core =
            rye >= 51 ? "rye" :
            wheat >= 51 ? "wheat whiskey" :
            corn >= 5 && rye >= 5 && wheat >= 5 && malt >= 5 ? "four-grain" :
            wheat >= 8 && wheat > rye ? "wheated" :
            rye >= 20 ? "high-rye" :
            corn >= 85 ? "corn-forward" :
            "traditional";
        bits.Add(core);
        var s = string.Join(' ', bits);
        return char.ToUpperInvariant(s[0]) + s[1..];
    }

    static string NewMakeNotes(NewMake n)
    {
        var axes = new (string word, double v)[]
        {
            ("sweet corn and cornbread", n.Sweet - 4), ("cracked pepper and rye bread", n.Spice),
            ("fresh dough", n.Bread), ("toasted nuts and cereal", n.Nutty), ("pear and banana", n.Fruit),
            ("rose and honeysuckle", n.Floral), ("campfire smoke", n.Smoke * 1.3),
        }.OrderByDescending(a => a.v).ToList();
        string bite = n.Bite > 6.5 ? "Hot and oily, with a solvent edge the barrel will have to work on."
                    : n.Bite < 4 ? "Clean and gentle for new-make."
                    : "Typical white-dog bite.";
        return $"Off the still: {axes[0].word}, then {axes[1].word}. {bite}";
    }

    static readonly string[] Nouns =
        ["Tin Roof", "Gravel Floor", "Backset", "Doubler", "Thief", "Low Wines", "Dog Days", "Head House",
         "Rick Yard", "Cistern", "Hogshead", "Stave Yard", "Mash Paddle", "Cooper's Mark", "Spirit Safe", "Slop Line"];

    /// <summary>Stable fun name from the recipe: an adjective from its loudest trait plus still-house slang.</summary>
    public static string Nickname(Recipe r)
    {
        var n = Analyze(r);
        var trait = new (double v, string[] adj)[]
        {
            (n.Sweet - 5, ["Honeyed", "Sorghum", "Sweet Feed"]),
            (n.Spice, ["Peppered", "Firebrand", "Hot Box"]),
            (n.Bread, ["Biscuit", "Hearth", "Sunday Bread"]),
            (n.Nutty, ["Pecan", "Hickory Nut", "Toasted"]),
            (n.Fruit, ["Orchard", "Bruised Pear", "Banana Boat"]),
            (n.Floral, ["Bluegrass", "Honeysuckle", "Clover"]),
            (n.Smoke * 1.4, ["Smokehouse", "Brush Fire", "Burnt Barn"]),
        }.OrderByDescending(t => t.v).First();
        // String.GetHashCode is randomized per run, so hash by hand to keep names stable.
        int h = 17;
        foreach (char ch in $"{r.BillText}|{r.YeastKey}|{r.FermentDays}|{r.FermentTempF}|{r.StillProof}|{r.PotStill}")
            h = unchecked(h * 31 + ch);
        h &= int.MaxValue;
        return $"{trait.adj[h % trait.adj.Length]} {Nouns[(h / 7) % Nouns.Length]}";
    }
}

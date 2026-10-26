using System.Text.RegularExpressions;

namespace CalculatorPuzzle;

/// <summary>What a left-side transform reads besides the display.</summary>
public enum Reads { None, Goal, Money }

/// <summary>
/// A left-side button that maps the display to a new display. X+k, X++, X*2, &lt;&lt; and sqr clamp results above 999 999 to
/// 999 999 (the game does); p and round don't. Apply returns null only for a display that isn't a number. Preimages lists every display that Apply maps to the given one,
/// used by the backward search (number transforms list plain numbers only, no leading-zero variants).
/// Equality is by Token.
/// </summary>
public sealed class LeftTransform : IEquatable<LeftTransform>
{
    public string Token { get; }                                   // "X++", "X+16", "2->4", "to25", ...
    public Func<string, string?> Apply { get; }
    public Func<string, IEnumerable<string>> Preimages { get; }

    /// <summary>True when Preimages yields plain numbers in ascending order, so a search can stop at the first one that is too long.</summary>
    public bool Ascending { get; }

    /// <summary>What the transform reads besides the display: None, Goal (near, median) or Money (money).</summary>
    public Reads Reads { get; }

    /// <summary>The same transform for another goal or money amount; null when Reads is None.</summary>
    public Func<long, LeftTransform>? Bind { get; }

    internal LeftTransform(string token, Func<string, string?> apply, Func<string, IEnumerable<string>> preimages,
                           bool ascending = false, Reads reads = Reads.None, Func<long, LeftTransform>? bind = null)
    {
        Ascending = ascending;
        Reads = reads;
        Bind = bind;
        Token = token;
        Apply = apply;
        Preimages = preimages;
    }

    public bool Equals(LeftTransform? other) => other != null && other.Token == Token;
    public override bool Equals(object? obj) => Equals(obj as LeftTransform);
    public override int GetHashCode() => Token.GetHashCode();
    public override string ToString() => Token;
}

public static class LeftTransforms
{
    const long Max = EngineeringCalculator.Max;

    /// <summary>Sanity bound for preimages of transforms that don't clamp: 2^24 (the biggest % result) rounded up.</summary>
    const long Sane = 100_000_000;

    const long MaxStep = 1_000_000_000;

    static readonly Dictionary<string, LeftTransform?> Cache = [];

    /// <summary>The transform for a token (any case, spaces removed, "−" read as "-"), or null.</summary>
    public static LeftTransform? Parse(string token)
    {
        var text = Regex.Replace(token, @"\s+", "").Replace('−', '-').ToLowerInvariant();
        lock (Cache)
        {
            if (!Cache.TryGetValue(text, out var found))
                Cache[text] = found = Create(text);
            return found;
        }
    }

    static LeftTransform? Create(string text)
    {
        switch (text)
        {
            case "x++": return Number("X++", v => Math.Min(v + 1, Max), v => v >= 1 && v <= Max ? [v - 1] : []);
            case "x--": return Minus("X--", 1);
            case "x*2": return Number("X*2", Twice, Half);
            case "<<": return Number("<<", Twice, Half);
            case "x/2": return Number("X/2", v => v / 2, Double);
            case ">>": return Number(">>", v => v / 2, Double);
            case "sqr":
                return Number("sqr", v => v > 999 ? Max : v * v, v =>
                {
                    var r = Root(v);
                    return r * r == v ? [r] : [];
                });
            case "sqrt":
                return Number("sqrt", v => RoundedRoot(v), SquareRange, ascending: true);
            case "round":
                return Number("round", v => v - v % 10 + (v % 10 >= 5 ? 10 : 0), v => v % 10 == 0 ? Enumerable.Range(-5, 10).Select(o => v + o).Where(p => p >= 0) : []);
            case "100-x":
                return Number("100-X", v => Math.Max(0, 100 - v),
                              v => v > 0 ? (v <= 100 ? [100 - v] : []) : HundredUp(), ascending: true);
            case "25|69":
                return Number("25|69", v => Swap2569(v), v => [Swap2569(v)]);
            case "p":
                return Number("p", v => Primes.Nearest(v), PrimePreimages, ascending: true);
            case "to25":
                return new LeftTransform("to25", _ => "25", _ => []);
            case "sup":
                return Sup();
        }

        if (Regex.Match(text, @"^x\+(\d+)$") is { Success: true } plus && Step(plus.Groups[1].Value) is { } up)
            return Number($"X+{up}", v => Math.Min(v + up, Max), v => v >= up && v <= Max ? [v - up] : []);
        if (Regex.Match(text, @"^x-(\d+)$") is { Success: true } minus && Step(minus.Groups[1].Value) is { } down)
            return Minus($"X-{down}", down);
        if (Regex.Match(text, @"^(\d)->(\d)$") is { Success: true } map && map.Groups[1].Value != map.Groups[2].Value)
            return DigitMap(map.Groups[1].Value[0], map.Groups[2].Value[0]);
        return null;
    }

    /// <summary>The "last" button: the left side becomes the previous round's goal. Not cached or parsed by token, because the value changes from puzzle to puzzle.</summary>
    public static LeftTransform Last(long value) => new("last", _ => value.ToString(), _ => []);

    /// <summary>The "near" button: the goal when it is at most 10 away, else + 10. Bound to the goal shown at the press; not cached or parsed by token.</summary>
    public static LeftTransform Near(long goal) =>
        Bound(Number("near", v => Math.Abs(goal - v) <= 10 ? goal : v + 10, v =>
        {
            if (v == goal)
                return Enumerable.Range(0, 21).Select(o => goal - 10 + o).Where(u => u >= 0);
            return v - 10 >= 0 && Math.Abs(v - 10 - goal) > 10 ? [v - 10] : [];
        }, ascending: true), Reads.Goal, Near);

    /// <summary>The "median" button: ceil((v + goal) / 2). Bound to the goal shown at the press; not cached or parsed by token.</summary>
    public static LeftTransform Median(long goal) =>
        Bound(Number("median", v => (v + goal + 1) / 2,
                     v => new[] { 2 * v - 1 - goal, 2 * v - goal }.Where(u => u >= 0 && u <= Sane).Distinct().Order(),
                     ascending: true), Reads.Goal, Median);

    /// <summary>The "money" button: v + the player's money, only on a display of fewer than 5 characters. Bound to the money at the press.</summary>
    public static LeftTransform Money(long money) =>
        Bound(new LeftTransform("money",
            display => display.Length < 5 && long.TryParse(display, out var v) ? (v + money).ToString() : null,
            w => IsPlain(w) && long.TryParse(w, out var v) && v - money is >= 0 and <= 9_999 ? [(v - money).ToString()] : [],
            ascending: true), Reads.Money, Money);

    /// <summary>The "sup" button: the digit sum, only on a display of fewer than 6 characters.</summary>
    static LeftTransform Sup() =>
        new("sup",
            display => display.Length < 6 && long.TryParse(display, out var v) ? DigitSum(v).ToString() : null,
            w => IsPlain(w) && long.TryParse(w, out var v) ? DigitSumPreimages(v).Select(u => u.ToString()) : [],
            ascending: true);

    static long DigitSum(long v)
    {
        long sum = 0;
        for (; v > 0; v /= 10)
            sum += v % 10;
        return sum;
    }

    /// <summary>Every u up to 99 999 with digit sum `sum`, ascending and lazy (none above 45).</summary>
    static IEnumerable<long> DigitSumPreimages(long sum)
    {
        if (sum > 45)
            yield break;
        if (sum == 0)
        {
            yield return 0;
            yield break;
        }
        for (int length = 1; length <= 5; length++)
            foreach (var u in FillDigits(length, (int)sum, 0, true))
                yield return u;
    }

    static IEnumerable<long> FillDigits(int length, int sum, long prefix, bool first)
    {
        if (length == 0)
        {
            if (sum == 0)
                yield return prefix;
            yield break;
        }
        if (sum > 9 * length)
            yield break;
        for (int d = first ? 1 : 0; d <= Math.Min(9, sum); d++)
            foreach (var u in FillDigits(length - 1, sum - d, prefix * 10 + d, false))
                yield return u;
    }

    /// <summary>The same transform with a Reads kind and a Bind to rebuild it for another value.</summary>
    static LeftTransform Bound(LeftTransform t, Reads reads, Func<long, LeftTransform> bind) =>
        new(t.Token, t.Apply, t.Preimages, t.Ascending, reads, bind);

    /// <summary>Every v whose nearest prime (ties down) is p: ((q + p) / 2, (p + r) / 2], or 0..(2 + 3) / 2 for p = 2.</summary>
    static IEnumerable<long> PrimePreimages(long p)
    {
        if (!Primes.IsPrime(p))
            yield break;
        long from = Primes.Previous(p) is { } q ? (q + p) / 2 + 1 : 0;
        long to = (p + Primes.Next(p)) / 2;
        for (long v = from; v <= to && v <= Sane; v++)
            yield return v;
    }

    /// <summary>100, 101, ... up to the sanity bound (every v of 100 or more makes 100-X give 0).</summary>
    static IEnumerable<long> HundredUp()
    {
        for (long v = 100; v <= Sane; v++)
            yield return v;
    }

    /// <summary>Swaps the digits 2↔5 and 6↔9 (its own inverse).</summary>
    static long Swap2569(long v) =>
        long.Parse(string.Concat(v.ToString().Select(c => c switch { '2' => '5', '5' => '2', '6' => '9', '9' => '6', _ => c })));

    /// <summary>k as a number when it is 1..10^9 with no leading zero.</summary>
    static long? Step(string digits) =>
        digits[0] != '0' && digits.Length <= 10 && long.Parse(digits) is var k && k <= MaxStep ? k : null;

    static long? Twice(long v) => Math.Min(v * 2, Max);
    static IEnumerable<long> Half(long v) => v % 2 == 0 && v <= Max ? [v / 2] : [];
    static IEnumerable<long> Double(long v) => 2 * v + 1 <= Sane ? [2 * v, 2 * v + 1] : [];

    /// <summary>floor(sqrt(v)).</summary>
    static long Root(long v)
    {
        var r = (long)Math.Sqrt(v);
        while (r * r > v) r--;
        while ((r + 1) * (r + 1) <= v) r++;
        return r;
    }

    /// <summary>sqrt(v) rounded to the nearest whole number (.5 rounds up).</summary>
    static long RoundedRoot(long v)
    {
        var r = Root(v);
        return v > r * r + r ? r + 1 : r;
    }

    /// <summary>v² − v + 1, …, v² + v (just 0 for v = 0) up to the sanity bound, lazily upward.</summary>
    static IEnumerable<long> SquareRange(long v)
    {
        if (v > 10_000)
            yield break;
        if (v == 0)
        {
            yield return 0;
            yield break;
        }
        for (long p = v * v - v + 1; p <= v * v + v && p <= Sane; p++)
            yield return p;
    }

    static LeftTransform Number(string token, Func<long, long?> apply, Func<long, IEnumerable<long>> preimages,
                                bool ascending = false) =>
        new(token,
            display => long.TryParse(display, out var v) && apply(v) is { } result ? result.ToString() : null,
            w => IsPlain(w) && long.TryParse(w, out var v) ? preimages(v).Select(p => p.ToString()) : [],
            ascending);

    static LeftTransform Minus(string token, long k) =>
        new(token,
            display => long.TryParse(display, out var v) ? Math.Max(0, v - k).ToString() : null,
            w =>
            {
                if (!IsPlain(w) || !long.TryParse(w, out var v))
                    return [];
                if (v > 0)
                    return v + k <= Sane ? [(v + k).ToString()] : [];
                return Enumerable.Range(0, (int)Math.Min(k, int.MaxValue - 1) + 1).Select(p => p.ToString());
            },
            ascending: true);

    static LeftTransform DigitMap(char a, char b) =>
        new($"{a}->{b}",
            display => display.Replace(a, b),
            w =>
            {
                if (w.Contains(a))
                    return [];
                var at = Enumerable.Range(0, w.Length).Where(i => w[i] == b).ToArray();
                return at.Length > 12 ? [] : Subsets(w, at, a);
            });

    static IEnumerable<string> Subsets(string w, int[] at, char a)
    {
        for (int mask = 0; mask < 1 << at.Length; mask++)
        {
            var chars = w.ToCharArray();
            for (int i = 0; i < at.Length; i++)
                if ((mask >> i & 1) == 1)
                    chars[at[i]] = a;
            yield return new string(chars);
        }
    }

    /// <summary>No leading zero (a number transform's output never has one).</summary>
    static bool IsPlain(string w) => w.Length == 1 || (w.Length > 1 && w[0] != '0');
}

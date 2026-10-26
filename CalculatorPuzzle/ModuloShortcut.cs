namespace CalculatorPuzzle;

/// <summary>
/// The "10d" Modulo Shortcut, for puzzles where S &lt; G or S &gt; G.
///
/// Given start S, goal G, iterator A (&gt;= 1) and k:
///   k &gt;= number of digits in S. Extra places (k > digits of S) need zeros
///   between the prepended digits and S. 0X can't make them (dX drops leading zeros); flip X0 ... flip
///   can (31 -> 13 -> 130 -> 1304 -> 4031).
///   L = A when S &lt; G, A - 1 when S &gt; G   (lift: how many 10^k are added)
///   d = L * 10^k + S - G            (must be positive)
///   d = M * N                       (any factor pair)
///   (prepend (M + L) to S) mod (10^k + N) = G
///
/// For S &gt; G at A = 1 this is d = S - G, prepend M to S, mod (10^k + N).
/// Example: 31 to 11, d = 20 = 4 * 5, 431 mod 105 = 11 and 531 mod 104 = 11.
///
/// Why it works: prepending M + L to S gives X = (M + L) * 10^k + S, and
/// X - M * (10^k + N) = L * 10^k + S - M * N = L * 10^k + S - d = G.
/// So G is the remainder as long as 0 &lt;= G &lt; 10^k + N.
/// </summary>
public static class ModuloShortcut
{
    public record Formula(long Prepend, long Value, long Modulus, long Result, bool Valid);

    /// <summary>
    /// One factor pair of d (Small &lt;= Large). First uses M = Large, N = Small;
    /// Second swaps them.
    /// </summary>
    public record FactorPair(long Small, long Large, Formula First, Formula Second);

    public record Result(int K, long Delta, string? Error, IReadOnlyList<FactorPair> Pairs);

    public static int MinK(long start) => start.ToString().Length;

    /// <summary>How many 10^k are added to d: A for S &lt; G, A - 1 for S &gt; G.</summary>
    public static long Lift(long start, long goal, long a) => start > goal ? a - 1 : a;

    public static Result Solve(long start, long goal, long a, int k)
    {
        if (k < MinK(start))
            return new Result(k, 0, $"k must be at least {MinK(start)}.", []);
        if (start == goal)
            return new Result(k, 0, "S already equals G.", []);

        long lift = Lift(start, goal, a);
        long pow;
        long delta;
        try
        {
            pow = checked((long)Math.Pow(10, k));
            delta = checked(lift * pow + start - goal);
        }
        catch (OverflowException)
        {
            return new Result(k, 0, "Numbers are too large.", []);
        }

        if (delta <= 0)
            return new Result(k, delta, $"d = {delta} is not positive. Increase A.", []);

        var pairs = new List<FactorPair>();
        try
        {
            for (long small = 1; small * small <= delta; small++)
            {
                if (delta % small != 0)
                    continue;
                long large = delta / small;
                pairs.Add(new FactorPair(small, large,
                    Build(start, goal, lift, pow, large, small),
                    Build(start, goal, lift, pow, small, large)));
            }
        }
        catch (OverflowException)
        {
            return new Result(k, delta, "Numbers are too large.", []);
        }

        return new Result(k, delta, null, pairs);
    }

    static Formula Build(long start, long goal, long lift, long pow, long m, long n)
    {
        long prepend = checked(m + lift);
        long value = checked(prepend * pow + start);
        long modulus = checked(pow + n);
        long result = value % modulus;
        return new Formula(prepend, value, modulus, result, result == goal);
    }
}

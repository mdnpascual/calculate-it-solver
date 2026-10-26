namespace CalculatorPuzzle;

/// <summary>Primality and nearest prime for 0 ≤ v &lt; 2^63, as the game's operator_prime button computes it.</summary>
public static class Primes
{
    static readonly long[] Bases = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37];

    /// <summary>Deterministic Miller–Rabin (bases 2..37 cover every 64-bit number).</summary>
    public static bool IsPrime(long v)
    {
        if (v < 2)
            return false;
        foreach (var b in Bases)
        {
            if (v == b)
                return true;
            if (v % b == 0)
                return false;
        }

        long d = v - 1;
        int s = 0;
        while ((d & 1) == 0)
        {
            d >>= 1;
            s++;
        }

        foreach (var b in Bases)
        {
            var x = PowMod(b, d, v);
            if (x == 1 || x == v - 1)
                continue;
            bool composite = true;
            for (int i = 1; i < s && composite; i++)
            {
                x = MulMod(x, x, v);
                if (x == v - 1)
                    composite = false;
            }
            if (composite)
                return false;
        }
        return true;
    }

    /// <summary>MainCalc.FindNearestPrime: v &lt; 2 → 2; else check v−d before v+d for d = 0, 1, 2, … (ties go down).</summary>
    public static long Nearest(long v)
    {
        if (v < 2)
            return 2;
        for (long d = 0; ; d++)
        {
            if (IsPrime(v - d))
                return v - d;
            if (IsPrime(v + d))
                return v + d;
        }
    }

    /// <summary>The largest prime below p, or null for p ≤ 2.</summary>
    public static long? Previous(long p)
    {
        for (long v = p - 1; v >= 2; v--)
            if (IsPrime(v))
                return v;
        return null;
    }

    /// <summary>The smallest prime above p.</summary>
    public static long Next(long p)
    {
        long v = Math.Max(p, 1) + 1;
        while (!IsPrime(v))
            v++;
        return v;
    }

    static long MulMod(long a, long b, long m) => (long)((UInt128)(ulong)a * (ulong)b % (ulong)m);

    static long PowMod(long b, long e, long m)
    {
        long result = 1;
        b %= m;
        while (e > 0)
        {
            if ((e & 1) == 1)
                result = MulMod(result, b, m);
            b = MulMod(b, b, m);
            e >>= 1;
        }
        return result;
    }
}

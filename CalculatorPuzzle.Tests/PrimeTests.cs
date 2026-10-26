namespace CalculatorPuzzle.Tests;

public class PrimeTests
{
    [Theory]
    [InlineData(8084, 8081)]
    [InlineData(41, 41)]
    [InlineData(93, 89)]
    [InlineData(33, 31)]
    [InlineData(55, 53)]
    [InlineData(88, 89)]
    [InlineData(6, 5)]
    [InlineData(80, 79)]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(999999, 1000003)]
    public void Nearest_matches_game_tests(long v, long expected) => Assert.Equal(expected, Primes.Nearest(v));

    [Fact]
    public void IsPrime_matches_trial_division()
    {
        for (long v = 0; v <= 100_000; v++)
        {
            bool prime = v >= 2;
            for (long i = 2; i * i <= v && prime; i++)
                if (v % i == 0) prime = false;
            Assert.Equal(prime, Primes.IsPrime(v));
        }
    }

    // The game's loop: trial division while (float)i <= Mathf.Sqrt(n).
    static bool GameIsPrime(long n)
    {
        if (n < 2) return false;
        for (long i = 2; (float)i <= MathF.Sqrt((float)n); i++)
            if (n % i == 0) return false;
        return true;
    }

    [Fact]
    public void IsPrime_matches_game_float_loop()
    {
        var primes = Enumerable.Range(2, 50_000).Where(i => Primes.IsPrime(i)).Select(i => (long)i).ToList();
        foreach (long p in primes.Where(p => p >= 4_097))
            Assert.Equal(GameIsPrime(p * p), Primes.IsPrime(p * p));
        for (int i = 1; i < primes.Count; i++)
            if (primes[i] >= 4_097 && primes[i] - primes[i - 1] == 2)
            {
                long n = primes[i - 1] * primes[i];
                Assert.Equal(GameIsPrime(n), Primes.IsPrime(n));
            }
    }

    [Theory]
    [InlineData(999_999_999_989, true)]
    [InlineData(100_000_000_000_000_003, true)]
    [InlineData(100_000_000_000_000_001, false)]
    [InlineData(1_000_000_007, true)]
    [InlineData(1_000_000_007L * 1_000_000_007L, false)]
    public void IsPrime_large(long v, bool expected) => Assert.Equal(expected, Primes.IsPrime(v));

    [Fact]
    public void Previous_and_next()
    {
        Assert.Null(Primes.Previous(2));
        Assert.Equal(2, Primes.Previous(3));
        Assert.Equal(89, Primes.Previous(97));
        Assert.Equal(3, Primes.Next(2));
        Assert.Equal(97, Primes.Next(89));
    }
}

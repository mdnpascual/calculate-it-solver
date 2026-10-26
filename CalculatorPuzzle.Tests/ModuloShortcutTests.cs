namespace CalculatorPuzzle.Tests;

public class ModuloShortcutTests
{
    [Fact]
    public void Example_41_to_48_gives_both_documented_formulas()
    {
        var result = ModuloShortcut.Solve(41, 48, 1, 2);

        Assert.Null(result.Error);
        Assert.Equal(2, result.K);
        Assert.Equal(93, result.Delta);

        var pair = Assert.Single(result.Pairs, p => p.Small == 3 && p.Large == 31);
        Assert.Equal((3241L, 103L, 48L), (pair.First.Value, pair.First.Modulus, pair.First.Result));
        Assert.Equal((441L, 131L, 48L), (pair.Second.Value, pair.Second.Modulus, pair.Second.Result));
        Assert.True(pair.First.Valid);
        Assert.True(pair.Second.Valid);
    }

    [Fact]
    public void Factor_pairs_are_listed_once_without_flipped_duplicates()
    {
        // d = 1 * 100 + 41 - 105 = 36, a perfect square.
        var result = ModuloShortcut.Solve(41, 105, 1, 2);

        Assert.Equal(36, result.Delta);
        Assert.Equal(
            [(1L, 36L), (2L, 18L), (3L, 12L), (4L, 9L), (6L, 6L)],
            result.Pairs.Select(p => (p.Small, p.Large)));
    }

    [Fact]
    public void Non_positive_delta_asks_for_a_larger_A()
    {
        // k = 1, d = 1 * 10 + 5 - 48 = -33.
        var result = ModuloShortcut.Solve(5, 48, 1, 1);

        Assert.NotNull(result.Error);
        Assert.Empty(result.Pairs);
    }

    [Fact]
    public void Zero_padding_lets_goal_have_more_digits_than_start()
    {
        // k = 2 means 5 is padded to 05. d = 1 * 100 + 5 - 48 = 57 = 3 * 19.
        var result = ModuloShortcut.Solve(5, 48, 1, 2);

        Assert.Null(result.Error);
        Assert.Equal(57, result.Delta);
        var pair = Assert.Single(result.Pairs, p => p.Small == 3 && p.Large == 19);
        Assert.Equal((2005L, 103L, 48L), (pair.First.Value, pair.First.Modulus, pair.First.Result));
        Assert.True(pair.First.Valid);
    }

    [Fact]
    public void K_shorter_than_start_is_rejected()
    {
        var result = ModuloShortcut.Solve(41, 48, 1, 1);

        Assert.NotNull(result.Error);
        Assert.Empty(result.Pairs);
    }

    [Fact]
    public void Start_equal_to_goal_is_rejected()
    {
        var result = ModuloShortcut.Solve(41, 41, 1, 2);

        Assert.NotNull(result.Error);
        Assert.Empty(result.Pairs);
    }

    [Fact]
    public void Example_31_to_11_gives_both_documented_formulas()
    {
        // S > G, A = 1: d = 31 - 11 = 20 = 4 * 5, prepend M to S, mod 10^k + N.
        var result = ModuloShortcut.Solve(31, 11, 1, 2);

        Assert.Null(result.Error);
        Assert.Equal(20, result.Delta);

        var pair = Assert.Single(result.Pairs, p => p.Small == 4 && p.Large == 5);
        Assert.Equal((531L, 104L, 11L), (pair.First.Value, pair.First.Modulus, pair.First.Result));
        Assert.Equal((431L, 105L, 11L), (pair.Second.Value, pair.Second.Modulus, pair.Second.Result));
        Assert.True(pair.First.Valid);
        Assert.True(pair.Second.Valid);
    }

    [Fact]
    public void Start_above_goal_adds_one_less_power_of_ten_per_A()
    {
        // S > G, A = 2: d = 1 * 100 + 31 - 11 = 120 = 10 * 12, prepend M + 1.
        var result = ModuloShortcut.Solve(31, 11, 2, 2);

        Assert.Null(result.Error);
        Assert.Equal(120, result.Delta);
        var pair = Assert.Single(result.Pairs, p => p.Small == 10 && p.Large == 12);
        Assert.Equal((1331L, 110L, 11L), (pair.First.Value, pair.First.Modulus, pair.First.Result));
        Assert.Equal((1131L, 112L, 11L), (pair.Second.Value, pair.Second.Modulus, pair.Second.Result));
    }

    [Fact]
    public void Every_formula_reaches_goal_whenever_goal_is_below_modulus()
    {
        for (long start = 0; start <= 120; start++)
        for (long goal = 0; goal <= 200; goal++)
        for (long a = 1; a <= 4; a++)
        for (int k = ModuloShortcut.MinK(start); k <= ModuloShortcut.MinK(start) + 2; k++)
        {
            var result = ModuloShortcut.Solve(start, goal, a, k);
            if (start == goal || result.Delta <= 0)
            {
                Assert.NotNull(result.Error);
                continue;
            }
            Assert.Null(result.Error);

            long pow = (long)Math.Pow(10, k);
            foreach (var pair in result.Pairs)
            {
                Assert.Equal(result.Delta, pair.Small * pair.Large);
                foreach (var (f, m, n) in new[] { (pair.First, pair.Large, pair.Small), (pair.Second, pair.Small, pair.Large) })
                {
                    Assert.Equal(m + ModuloShortcut.Lift(start, goal, a), f.Prepend);
                    Assert.Equal(long.Parse($"{f.Prepend}{start.ToString().PadLeft(k, '0')}"), f.Value);
                    Assert.Equal(pow + n, f.Modulus);
                    Assert.Equal(goal < f.Modulus, f.Valid);
                    if (f.Valid)
                        Assert.Equal(goal, f.Value % f.Modulus);
                }
            }
        }
    }
}

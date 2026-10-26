namespace CalculatorPuzzle.Tests;

public class LeftSearchTests
{
    static Buttons B(string text) => TestButtons.Parse(text).Buttons!;
    static string Show(IEnumerable<Press> presses) => string.Join(' ', presses);
    static LeftSearch.Limits L(int presses) => new(presses, 8, 200_000);

    static IReadOnlyList<IReadOnlyList<Press>> Connect(long s, long g, string buttons, int presses = 6) =>
        LeftSearch.Connect(s, g.ToString(), B(buttons), L(presses), 10);

    [Theory]
    [InlineData(88, 89)]
    [InlineData(120, 113)]
    public void Prime_is_one_press(long s, long g) =>
        Assert.Contains(Connect(s, g, "p:1"), l => Show(l) == "p");

    [Fact]
    public void Prime_tie_goes_down_so_it_is_not_offered_for_42_to_43()
    {
        var lines = Connect(42, 43, "p:1, X++:1");
        Assert.Contains(lines, l => Show(l) == "X++");
        Assert.DoesNotContain(lines, l => Show(l) == "p");
    }

    [Fact]
    public void Forward_counts_presses()
    {
        var f = LeftSearch.Forward("41", B("X++"), L(2));
        Assert.Single(f["42"]);
        Assert.Equal(2, f["43"].Count);
        Assert.DoesNotContain("44", f.Keys);
    }

    [Fact]
    public void Backward_finds_the_halves() =>
        Assert.Equal("X*2", Show(LeftSearch.Backward("84", B("X*2"), L(2))["42"]));

    [Fact]
    public void Forward_respects_charges() =>
        Assert.DoesNotContain("43", LeftSearch.Forward("41", B("X++:1"), L(4)).Keys);

    [Theory]
    [InlineData(41, 42, "X++:1", "X++")]
    [InlineData(7, 49, "sqr:1", "sqr")]
    [InlineData(35, 55, "3->0:1, 5X:1", "3->0 5X")]
    [InlineData(41, 84, "X++:1, X*2:1", "X++ X*2")]
    [InlineData(41, 25, "to25:1", "to25")]
    [InlineData(55, 27, ">>:1", ">>")]
    [InlineData(41, 43, "X++:2", "X++ X++")]
    public void Connect_cheapest(long s, long g, string buttons, string expected) =>
        Assert.Equal(expected, Show(Connect(s, g, buttons)[0]));

    [Fact]
    public void Connect_cannot_prepend_to_a_leading_zero() =>
        Assert.DoesNotContain(Connect(35, 505, "3->0:1, 5X:1"), l => Show(l) == "3->0 5X");

    [Fact]
    public void Connect_without_enough_charges_is_empty() =>
        Assert.Empty(Connect(41, 43, "X++:1"));

    [Fact]
    public void Connected_lines_run_to_the_goal()
    {
        var b = B("X++:3, X*2:3, 4X:2, flip:2, sqr:1");
        var lines = LeftSearch.Connect(41, "1764", b, L(8), 20);
        Assert.NotEmpty(lines);
        foreach (var line in lines)
        {
            Assert.Equal("1764", EngineeringCalculator.Run(41, line, b)![^1]);
            Assert.True(b.Fits(line));
        }
    }

    [Fact]
    public void Cancelled_token_throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            LeftSearch.Forward("41", B("X++:9, 1X:9, X1:9, X*2:9, flip:9, X2:9, X3:9, X4:9"),
                               new(12, 18, 10_000_000), cts.Token));
    }
}

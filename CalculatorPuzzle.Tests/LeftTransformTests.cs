namespace CalculatorPuzzle.Tests;

public class LeftTransformTests
{
    static string? Apply(string token, string display) => LeftTransforms.Parse(token)!.Apply(display);

    [Theory]
    [InlineData("3->0", "35", "05")]
    [InlineData("0->8", "05", "85")]
    [InlineData("X++", "021", "22")]
    [InlineData("X--", "0", "0")]
    [InlineData("X-10", "5", "0")]
    [InlineData("X/2", "55", "27")]
    [InlineData(">>", "55", "27")]
    [InlineData("<<", "6", "12")]
    [InlineData("X*2", "41", "82")]
    [InlineData("sqr", "9", "81")]
    [InlineData("sqrt", "10", "3")]
    [InlineData("sqrt", "99", "10")]
    [InlineData("sqrt", "6", "2")]
    [InlineData("sqrt", "7", "3")]
    [InlineData("sqrt", "2", "1")]
    [InlineData("sqrt", "3", "2")]
    [InlineData("sqrt", "0", "0")]
    [InlineData("sqrt", "100", "10")]
    [InlineData("p", "05", "5")]
    [InlineData("p", "8084", "8081")]
    [InlineData("round", "54", "50")]
    [InlineData("round", "55", "60")]
    [InlineData("round", "50", "50")]
    [InlineData("round", "4", "0")]
    [InlineData("round", "95", "100")]
    [InlineData("to25", "41", "25")]
    [InlineData("sqr", "1000", "999999")]
    [InlineData("sqr", "999", "998001")]
    [InlineData("X++", "999999", "999999")]
    public void Apply_cases(string token, string display, string? expected) =>
        Assert.Equal(expected, Apply(token, display));

    [Theory]
    [InlineData("X++")]
    [InlineData("X--")]
    [InlineData("X+16")]
    [InlineData("X-4")]
    [InlineData("X*2")]
    [InlineData("X/2")]
    [InlineData("<<")]
    [InlineData(">>")]
    [InlineData("sqr")]
    [InlineData("sqrt")]
    [InlineData("round")]
    [InlineData("2->4")]
    [InlineData("3->0")]
    public void Preimages_map_back(string token)
    {
        var t = LeftTransforms.Parse(token)!;
        var targets = Enumerable.Range(0, 2001).Select(i => i.ToString()).Concat(["05", "0042"]);
        foreach (var w in targets)
            foreach (var p in t.Preimages(w).Take(2000))
                Assert.Equal(w, t.Apply(p));
    }

    [Fact]
    public void Preimage_spot_checks()
    {
        var prime = LeftTransforms.Parse("p")!;
        Assert.Equal(["0", "1", "2"], prime.Preimages("2").Order());
        Assert.Equal(["3", "4"], prime.Preimages("3").Order());
        Assert.Equal(["5", "6"], prime.Preimages("5").Order());
        Assert.Equal(Enumerable.Range(87, 7).Select(i => i.ToString()), prime.Preimages("89"));
        Assert.Equal(Enumerable.Range(8076, 9).Select(i => i.ToString()), prime.Preimages("8081"));
        Assert.Empty(prime.Preimages("90"));
        Assert.Empty(prime.Preimages("089"));

        Assert.Equal(["0", "1"], LeftTransforms.Parse("X--")!.Preimages("0").Take(2).Order());
        Assert.Equal(Enumerable.Range(55, 10).Select(i => i.ToString()).Order(), LeftTransforms.Parse("round")!.Preimages("60").Order());
        Assert.Equal(["22", "24", "42", "44"], LeftTransforms.Parse("2->4")!.Preimages("44").Order());
        Assert.Empty(LeftTransforms.Parse("2->4")!.Preimages("24"));
    }

    [Theory]
    [InlineData("X++", "X++")]
    [InlineData("x++", "X++")]
    [InlineData("X+16", "X+16")]
    [InlineData("x−4", "X-4")]
    [InlineData("2->4", "2->4")]
    [InlineData("TO25", "to25")]
    public void Parse_canonical(string text, string token) => Assert.Equal(token, LeftTransforms.Parse(text)!.Token);

    [Theory]
    [InlineData("X+0")]
    [InlineData("X+04")]
    [InlineData("3->3")]
    [InlineData("X*3")]
    public void Parse_rejects(string text) => Assert.Null(LeftTransforms.Parse(text));

    [Fact]
    public void Parse_returns_one_instance_per_token() =>
        Assert.Same(LeftTransforms.Parse("x++"), LeftTransforms.Parse("X++"));
}

public class PrimeTransformTests
{
    [Fact]
    public void Preimages_map_back_and_the_edges_do_not()
    {
        var prime = LeftTransforms.Parse("p")!;
        for (long p = 2; p <= 10_000; p++)
        {
            if (!Primes.IsPrime(p)) continue;
            var pre = prime.Preimages(p.ToString()).Select(long.Parse).ToList();
            Assert.All(pre, v => Assert.Equal(p, Primes.Nearest(v)));
            if (pre[0] > 0) Assert.NotEqual(p, Primes.Nearest(pre[0] - 1));
            Assert.NotEqual(p, Primes.Nearest(pre[^1] + 1));
        }
    }
}

public class TransformSimulatorTests
{
    static Buttons B(string text) => TestButtons.Parse(text).Buttons!;
    static Press T(string token) => Press.Of(LeftTransforms.Parse(token)!);
    static readonly Buttons Everything = B("X++, 3->0, 5X, flip, sqr, ÷, 10");

    [Fact]
    public void Digit_map_keeps_leading_zero_and_prepend_drops_it() =>
        Assert.Equal(["05", "55"], EngineeringCalculator.Run(35, [T("3->0"), new Press(Key.Prepend, 5)], Everything));

    [Fact]
    public void Digit_map_then_flip() =>
        Assert.Equal(["05", "5"], EngineeringCalculator.Run(35, [T("3->0"), new Press(Key.Flip)], Everything));

    [Fact]
    public void Flip_then_number_transform_drops_zero() =>
        Assert.Equal(["21", "22"], EngineeringCalculator.Run(120, [new Press(Key.Flip), T("X++")], Everything));

    [Fact]
    public void In_game_digit_map_chain_then_prepend_47() =>
        Assert.Equal(["77", "33", "00", "80"], EngineeringCalculator.Run(47,
            [T("4->7"), T("7->3"), T("3->0"), new Press(Key.Prepend, 8)], B("4->7, 7->3, 3->0, 8X")));

    [Fact]
    public void In_game_digit_map_chain_then_prepend_76() =>
        Assert.Equal(["36", "06", "86"], EngineeringCalculator.Run(76,
            [T("7->3"), T("3->0"), new Press(Key.Prepend, 8)], B("7->3, 3->0, 8X")));

    [Fact]
    public void Transform_on_the_right_side_is_invalid() =>
        Assert.Null(EngineeringCalculator.Run(41, [new Press(Key.Divide), T("X++")], Everything));

    [Fact]
    public void Missing_transform_is_invalid() =>
        Assert.Null(EngineeringCalculator.Run(41, [T("X--")], Everything));

    [Fact]
    public void Square_near_the_limit() =>
        Assert.Equal("998001", EngineeringCalculator.Run(999, [T("sqr")], Everything)![^1]);

    [Fact]
    public void Square_over_the_limit_clamps_and_play_goes_on() =>
        Assert.Equal(["999999", "999999"], EngineeringCalculator.Run(1000, [T("sqr"), T("X++")], Everything));

    [Fact]
    public void Parse_with_charges()
    {
        var (b, error) = Buttons.Parse("X++:2, X--:1, X+16:1, X-64:1, X*2:1, X/2:1, <<:1, >>:1, sqr:1, sqrt:1, 2->4:1, to25:1, round:1");
        Assert.Null(error);
        Assert.Equal(13, b!.Transforms.Count);
        Assert.Equal(2, b.Charges["X++"]);
        Assert.Equal(1, b.Charges["2->4"]);
        Assert.Equal(1, b.Charges["X+16"]);
        Assert.Equal(1, b.Charges["to25"]);
    }

    [Fact]
    public void Zero_charges_leave_the_transform_out()
    {
        var (b, _) = Buttons.Parse("X++:0, sqr:1");
        Assert.Equal(["sqr"], b!.Transforms.Select(t => t.Token));
    }

    [Fact]
    public void Transform_without_a_count_is_an_error() => Assert.NotNull(Buttons.Parse("X++").Error);

    [Fact]
    public void Existing_tokens_still_parse()
    {
        var (b, error) = Buttons.Parse("x4:1, +:1");
        Assert.Null(error);
        Assert.Contains(4, b!.AppendDigits);
        Assert.True(b.Add);
        Assert.Empty(b.Transforms);
    }
}

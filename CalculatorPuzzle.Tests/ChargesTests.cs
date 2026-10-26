namespace CalculatorPuzzle.Tests;

public class ButtonsChargesParseTests
{
    [Fact]
    public void Colon_n_records_the_charge()
    {
        var (b, error) = TestButtons.Parse("X1:2, 10:13, /:6");

        Assert.Null(error);
        Assert.Contains(1, b!.AppendDigits);
        Assert.Equal(2, b.Charges["X1"]);
        Assert.Equal(13, b.Charges["10"]);
        Assert.Equal(6, b.Charges["÷"]);
    }

    [Fact]
    public void Zero_charges_leaves_the_button_out()
    {
        var (b, error) = TestButtons.Parse("X1:0, X2:1, 4X:0, 5:0, flip:0, ÷:0");

        Assert.Null(error);
        Assert.DoesNotContain(1, b!.AppendDigits);
        Assert.Contains(2, b.AppendDigits);
        Assert.Empty(b.PrependDigits);
        Assert.Empty(b.DigitKeys);
        Assert.False(b.Flip);
        Assert.False(b.Divide);
        Assert.False(b.Charges.ContainsKey("X1"));
        Assert.Equal(1, b.Charges["X2"]);
    }

    [Theory]
    [InlineData("X1:-1")]
    [InlineData("X1:a")]
    [InlineData("X1:")]
    [InlineData("X1:1:2")]
    public void Bad_charge_counts_give_an_error(string text)
    {
        var (b, error) = Buttons.Parse(text);

        Assert.Null(b);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("X1, 10:3", "X1")]
    [InlineData("X1:1, X2", "X2")]
    [InlineData("mod", "mod")]
    public void A_token_without_a_colon_is_an_error(string text, string token)
    {
        var (b, error) = Buttons.Parse(text);

        Assert.Null(b);
        Assert.Contains($"Missing charge count in \"{token}\" (write {token}:n)", error);
    }

    [Fact]
    public void Equals_needs_no_count_and_a_count_on_it_is_ignored()
    {
        var (b, error) = Buttons.Parse("X1:2, =");
        Assert.Null(error);
        Assert.Single(b!.Charges);

        (b, error) = Buttons.Parse("X1:2, =:3");
        Assert.Null(error);
        Assert.Single(b!.Charges);
    }

    [Fact]
    public void Spare_adds_the_charges_of_the_distinct_buttons_pressed()
    {
        var (b, _) = Buttons.Parse("4X:2, mod:3, 10:5, 5:1");
        Assert.Equal(11, b!.Spare(P("4X mod 10 5 =")));
        Assert.Equal(2 + 3 + 5, b.Spare(P("4X 4X mod 10 10 =")));
    }

    [Fact]
    public void Drained_counts_distinct_buttons_pressed_as_often_as_their_charges()
    {
        var (b, _) = Buttons.Parse("4X:2, mod:3, 10:5, 5:1");
        Assert.Equal(1, b!.Drained(P("4X mod 10 5 =")));

        var (two, _) = Buttons.Parse("X1:2");
        Assert.Equal(1, two!.Drained(P("X1 X1")));
        var (three, _) = Buttons.Parse("X1:3");
        Assert.Equal(0, three!.Drained(P("X1 X1")));
        Assert.Equal(0, Buttons.All.Drained(P("X1 X1")));
    }

    static IReadOnlyList<Press> P(string text) => text.Split(' ').Select(t => t switch
    {
        "mod" => new Press(Key.Mod),
        "10" => new Press(Key.Ten),
        "=" => new Press(Key.Equals),
        _ when t.Length == 1 => new Press(Key.Digit, t[0] - '0'),
        _ when t[1] == 'X' => new Press(Key.Prepend, t[0] - '0'),
        _ => new Press(Key.Append, t[1] - '0'),
    }).ToList();
}

public class FitsTests
{
    static IReadOnlyList<Press> P(params Press[] presses) => presses;

    [Fact]
    public void X2_flip_fits_one_charge_each()
    {
        var (b, _) = TestButtons.Parse("X2:1, flip:1");
        Assert.True(b!.Fits(P(new Press(Key.Append, 2), new Press(Key.Flip))));
    }

    [Fact]
    public void Pressing_X1_twice_does_not_fit_one_charge()
    {
        var (b, _) = TestButtons.Parse("X1:1, X2");
        Assert.False(b!.Fits(P(new Press(Key.Append, 1), new Press(Key.Append, 1))));
        Assert.True(b.Fits(P(new Press(Key.Append, 2), new Press(Key.Append, 2))));
    }

    [Fact]
    public void Equals_is_never_counted()
    {
        var (b, _) = TestButtons.Parse("10:1, ÷:1");
        Assert.True(b!.Fits(P(new Press(Key.Divide), new Press(Key.Ten), new Press(Key.Equals), new Press(Key.Equals))));
    }

    [Fact]
    public void Buttons_without_charges_are_unlimited()
    {
        Assert.True(Buttons.All.Fits(Enumerable.Repeat(new Press(Key.Flip), 50).ToList()));
    }
}

public class DivisionShortcutChargesTests
{
    const string Rest = "5X, X2, X7, X4, 4X, 6X, 3X, 8X, X9, flip, reset";

    static (List<DivisionShortcut.Line> Lines, List<DivisionShortcut.Line> Rejected) Run(long s, long g, string text)
    {
        var (buttons, error) = TestButtons.Parse(text);
        Assert.Null(error);
        var lines = new List<DivisionShortcut.Line>();
        var rejected = new List<DivisionShortcut.Line>();
        Assert.Null(DivisionShortcut.Search(s, g, buttons!, 12, lines.Add, onRejected: rejected.Add));
        return (lines, rejected);
    }

    static string Show(DivisionShortcut.Line l) => string.Join(' ', l.Presses);

    [Fact]
    public void Top_line_for_41_to_48_with_one_charge_each_is_divide_by_10_then_X8()
    {
        var (lines, _) = Run(41, 48, "X8:1, 10:1, ÷:1");
        Assert.Equal("÷ 10 = X8", Show(lines.OrderBy(l => l, DivisionShortcut.Ranking).First()));
    }

    [Fact]
    public void Zero_charges_on_X8_means_no_line_uses_X8()
    {
        var (lines, rejected) = Run(41, 48, "X8:0, " + Rest + ", X1, 10, ÷");
        Assert.NotEmpty(lines);
        Assert.DoesNotContain(lines.Concat(rejected), l => l.Presses.Any(p => p.Key == Key.Append && p.Digit == 8));
    }

    [Fact]
    public void Lines_over_a_charge_go_to_onRejected_and_are_not_reported()
    {
        var (lines, rejected) = Run(4, 24, "X1:1, " + Rest + ", 10:1, ÷:1");
        Assert.NotEmpty(rejected);
        var (buttons, _) = TestButtons.Parse("X1:1, " + Rest + ", 10:1, ÷:1");
        Assert.All(lines, l => Assert.True(buttons!.Fits(l.Presses), Show(l)));
        Assert.All(rejected, l => Assert.False(buttons!.Fits(l.Presses), Show(l)));
    }

    [Fact]
    public void Logged_charges_for_4_to_24_only_report_lines_that_fit()
    {
        const string text = "X1:1, X2:1, flip:7, ÷:6, 10:13, reset:2, 4X:2, X4:2";
        var (lines, _) = Run(4, 24, text);
        var (buttons, _) = TestButtons.Parse(text);
        Assert.All(lines, l => Assert.True(buttons!.Fits(l.Presses), Show(l)));
    }
}

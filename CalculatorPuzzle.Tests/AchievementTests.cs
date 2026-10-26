namespace CalculatorPuzzle.Tests;

/// <summary>Achievement modes: School mathematics (+ - × ÷ in one line) and Just a one (an = that gives S + 1).</summary>
public class AchievementTests
{
    static readonly Press Add = new(Key.Add), Sub = new(Key.Subtract), Mul = new(Key.Multiply), Div = new(Key.Divide),
        Mod = new(Key.Mod), Pct = new(Key.Percent), Eq = new(Key.Equals), Ten = new(Key.Ten);
    static Press D(int d) => new(Key.Digit, d);
    static Press Xd(int d) => new(Key.Append, d);
    static Press Pre(int d) => new(Key.Prepend, d);
    static IReadOnlyList<Press> P(params Press[] presses) => presses;
    static Buttons B(string text) => TestButtons.Parse(text).Buttons!;

    static IReadOnlyList<ResultLine> JustOne(long start, string buttons, out JustOneSearch.Result result)
    {
        var lines = new List<ResultLine>();
        result = JustOneSearch.Search(start, B(buttons), 12, lines.Add, CancellationToken.None);
        return lines;
    }

    // ---- helpers -------------------------------------------------------------------------------

    [Fact]
    public void FourOps_needs_all_four_with_equals()
    {
        Assert.True(Achievements.FourOps(P(Add, D(7), Eq, Mul, D(1), Eq, Div, D(1), Eq, Sub, D(0), Eq)));
        Assert.False(Achievements.FourOps(P(Add, D(7), Eq, Mul, D(1), Eq, Div, D(1), Eq)));
        Assert.False(Achievements.FourOps(P(Add, D(7), Eq, Mul, D(1), Eq, Div, D(1), Eq, Mod, D(3), Eq)));
        // An operator without its = counts nothing.
        Assert.False(Achievements.FourOps(P(Add, D(7), Eq, Mul, D(1), Eq, Div, D(1), Eq, Sub, D(0))));
    }

    [Fact]
    public void Credit_is_the_last_press_with_a_game_id()
    {
        Assert.Equal("digit", Achievements.Credit(P(Add, D(1), Eq)));
        Assert.Equal("÷", Achievements.Credit(P(Pre(5), Div, Ten, Eq)));
        Assert.Equal("mod", Achievements.Credit(P(Xd(5), Mod, Ten, Eq)));
        Assert.Equal("digit", Achievements.Credit(P(Xd(5), Sub, D(4), D(0), Eq)));
        Assert.Equal("+", Achievements.Credit(P(Add, Ten, Ten, Eq)));
        Assert.Equal("c", Achievements.Credit(P(Add, new Press(Key.Charge, 9), Eq)));
        Assert.Equal("%", Achievements.Credit(P(Pct, Ten, Eq)));
        Assert.Null(Achievements.Credit(P(Xd(5))));
    }

    // ---- School mathematics --------------------------------------------------------------------

    static SchoolSearch.Result School(long start, long goal, string buttons, int maxCost = 30, Action? onRejected = null) =>
        SchoolSearch.Find(start, goal, B(buttons), maxCost, onRejected: onRejected);

    static void AssertValid(SchoolSearch.Result result, long start, long goal, Buttons buttons)
    {
        Assert.NotEmpty(result.Lines);
        foreach (var line in result.Lines)
        {
            Assert.Equal(goal, EngineeringCalculator.Sent(start, line.Presses, buttons));
            Assert.True(Achievements.FourOps(line.Presses));
            Assert.True(buttons.Fits(line.Presses));
        }
    }

    [Fact]
    public void School_all_digits_costs_eight()
    {
        var buttons = "+, -, ×, ÷, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9";
        var result = School(41, 48, buttons);
        Assert.Equal(8, result.Lines[0].Cost);
        AssertValid(result, 41, 48, B(buttons));
    }

    [Fact]
    public void School_with_zero_one_seven()
    {
        var buttons = "+, -, ×, ÷, 0, 1, 7";
        var result = School(41, 48, buttons);
        Assert.Equal(8, result.Lines[0].Cost);
        AssertValid(result, 41, 48, B(buttons));
    }

    [Fact]
    public void School_early_game_with_ten_and_X8()
    {
        var buttons = "+, -, ×, ÷, 10, X8";
        var result = School(41, 48, buttons);
        Assert.True(result.Lines[0].Cost <= 11);
        AssertValid(result, 41, 48, B(buttons));
    }

    [Fact]
    public void School_without_divide_names_it()
    {
        var result = School(41, 48, "+, -, ×, 10");
        Assert.Empty(result.Lines);
        Assert.Contains("÷", result.Error);
    }

    [Fact]
    public void School_tight_charges_use_a_key_at_most_once()
    {
        var buttons = "+:1, -:1, ×:1, ÷:1, 0:1, 1:1, 2:9, 3:9, 4:9, 5:9, 6:9, 7:9, 8:9, 9:9";
        var result = School(41, 48, buttons);
        AssertValid(result, 41, 48, B(buttons));
        foreach (var line in result.Lines)
        {
            Assert.True(line.Presses.Count(p => p.Key == Key.Digit && p.Digit == 1) <= 1);
            Assert.True(line.Presses.Count(p => p.Key == Key.Digit && p.Digit == 0) <= 1);
        }
    }

    [Fact]
    public void School_lines_have_at_most_twelve_operators()
    {
        var result = School(41, 48, "+, -, ×, ÷, 10, X8");
        Assert.All(result.Lines, l => Assert.InRange(l.Operators, 4, Achievements.MaxSchoolOperators));
    }

    [Fact]
    public void School_cancel_throws()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            SchoolSearch.Find(41, 48, B("+, -, ×, ÷, 10, X8"), 30, cancel.Token));
    }

    // ---- Just a one ----------------------------------------------------------------------------

    [Fact]
    public void JustOne_plus_one_credits_a_digit()
    {
        var lines = JustOne(4, "+, 1", out _);
        var line = Assert.Single(lines, l => string.Join(' ', l.Presses) == "+ 1 =");
        Assert.Equal("digit", line.Credit);
    }

    [Fact]
    public void JustOne_ten_credits_the_operator()
    {
        var lines = JustOne(4, "5X, ÷, 10", out _);
        var line = Assert.Single(lines, l => string.Join(' ', l.Presses) == "5X ÷ 10 =");
        Assert.Equal("÷", line.Credit);
    }

    [Fact]
    public void JustOne_lists_a_line_for_each_credit()
    {
        var lines = JustOne(4, "5X, ÷, 10, +, 1", out var result);
        Assert.Contains("+ 1 =", lines.Select(l => string.Join(' ', l.Presses)));
        Assert.Contains("5X ÷ 10 =", lines.Select(l => string.Join(' ', l.Presses)));
        Assert.Equal(["digit", "÷"], result.Credits.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void JustOne_mod_credit()
    {
        var lines = JustOne(4, "X5, mod, 10", out _);
        var line = Assert.Single(lines, l => string.Join(' ', l.Presses) == "X5 mod 10 =");
        Assert.Equal("mod", line.Credit);
    }

    [Fact]
    public void JustOne_lines_have_one_equals_at_the_end()
    {
        // S 41 has no line (÷ 10 = 4 and X8 can't make 42); S 4 has 5X ÷ 10 =.
        Assert.Empty(JustOne(41, "÷, 10, X8", out _));
        var lines = JustOne(4, "5X, ÷, 10, X8, +, 1", out _);
        Assert.NotEmpty(lines);
        Assert.All(lines, l =>
        {
            Assert.Equal(Key.Equals, l.Presses[^1].Key);
            Assert.Equal(1, l.Presses.Count(p => p.Key == Key.Equals));
            Assert.Equal(5, EngineeringCalculator.Sent(4, l.Presses));
        });
    }

    [Fact]
    public void JustOne_order_puts_each_credit_first()
    {
        ResultLine Line(string credit, int cost) =>
            new([], cost, 0, 0, null, null, Credit: credit);
        var lines = new List<ResultLine> { Line("digit", 3), Line("÷", 4), Line("digit", 2) };
        JustOneSearch.Order(lines);
        Assert.Equal([("digit", 2), ("÷", 4), ("digit", 3)], lines.Select(l => (l.Credit!, l.Cost)));
    }

    [Fact]
    public void JustOne_cannot_go_above_the_maximum()
    {
        var lines = JustOne(999_999, "+, 1", out var result);
        Assert.Empty(lines);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ResultLine_hides_the_credit()
    {
        var line = new ResultLine([Div, Ten, Eq], 2, 0, 5, null, null, Credit: "÷");
        Assert.DoesNotContain("[", line.ToString());
    }
}

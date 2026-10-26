namespace CalculatorPuzzle.Tests;

/// <summary>Boss rule 12: every = adds 1 to its result.</summary>
public class SendPlusOneTests
{
    const string Keys = "0, 1, 2, 3, 4, 5, 6, 7, 8, 9";
    static readonly Press Add = new(Key.Add), Sub = new(Key.Subtract), Div = new(Key.Divide), Eq = new(Key.Equals), Ten = new(Key.Ten);
    static Press D(int d) => new(Key.Digit, d);
    static Press Xd(int d) => new(Key.Append, d);
    static IReadOnlyList<Press> P(params Press[] presses) => presses;
    static Buttons B(string text) => TestButtons.Parse(text).Buttons!;
    static string Text(IReadOnlyList<Press> presses) => string.Join(' ', presses);

    // ---- Simulator ----

    [Fact]
    public void Mid_line_equals_adds_one_and_the_send_adds_another()
    {
        var line = P(Div, Ten, Eq, Xd(8));
        Assert.Equal("58", EngineeringCalculator.Run(41, line, null, true)![^1]);
        Assert.Equal(59, EngineeringCalculator.Sent(41, line, null, true));
        Assert.Equal("48", EngineeringCalculator.Run(41, line)![^1]);
        Assert.Equal(48, EngineeringCalculator.Sent(41, line));
    }

    [Fact]
    public void A_line_ending_with_equals_is_sent_by_that_equals() =>
        Assert.Equal(49, EngineeringCalculator.Sent(41, P(Add, D(7), Eq), null, true));

    [Fact]
    public void Bare_equals_is_allowed_only_as_the_last_press_with_the_rule()
    {
        Assert.Equal(48, EngineeringCalculator.Sent(47, P(Eq), null, true));
        Assert.Null(EngineeringCalculator.Run(47, P(Eq)));
        Assert.Null(EngineeringCalculator.Run(47, P(Eq, Xd(8)), null, true));
    }

    [Fact]
    public void Subtract_stops_at_zero_then_adds_one() =>
        Assert.Equal("1", EngineeringCalculator.Run(59, P(Sub, D(6), D(0), Eq), null, true)![^1]);

    [Fact]
    public void Remorph_key_has_no_equals_so_only_the_send_adds_one() =>
        Assert.Equal(74, EngineeringCalculator.Sent(483, P(new Press(Key.RemorphA), D(7)), null, true));

    // ---- Phases ----

    static void AssertAllSend(long start, long goal, Buttons buttons, IEnumerable<IReadOnlyList<Press>> lines)
    {
        foreach (var line in lines)
        {
            Assert.Equal(goal, EngineeringCalculator.Sent(start, line, buttons, true));
            Assert.True(buttons.Fits(line));
        }
    }

    [Fact]
    public void Arithmetic_plus_6_sends_48()
    {
        var b = B($"+, {Keys}, =");
        var lines = ArithmeticSearch.Find(41, 48, b, 6, plusOne: true);
        AssertAllSend(41, 48, b, lines.Select(l => l.Presses));
        Assert.Equal("+ 6 =", Text(lines[0].Presses));
        Assert.DoesNotContain(lines, l => Text(l.Presses) == "+ 7 =");
    }

    [Fact]
    public void Arithmetic_edits_after_the_equals_aim_one_lower()
    {
        var b = B("+:9, 1:9, X8:9, =");
        var lines = ArithmeticSearch.Find(41, 439, b, 6, plusOne: true);
        AssertAllSend(41, 439, b, lines.Select(l => l.Presses));
        Assert.Contains(lines, l => Text(l.Presses) == "+ 1 = X8");
    }

    [Fact]
    public void Arithmetic_start_one_below_goal_is_the_bare_equals()
    {
        var b = B($"+, {Keys}, =");
        var lines = ArithmeticSearch.Find(47, 48, b, 6, plusOne: true);
        Assert.Equal("=", Text(lines[0].Presses));
        Assert.Equal(0, lines[0].Cost);
    }

    [Fact]
    public void Arithmetic_subtract_20_sends_40()
    {
        var b = B($"-, {Keys}, =");
        var lines = ArithmeticSearch.Find(59, 40, b, 6, plusOne: true);
        AssertAllSend(59, 40, b, lines.Select(l => l.Presses));
        Assert.Equal("- 2 0 =", Text(lines[0].Presses));
        Assert.DoesNotContain(lines, l => Text(l.Presses) == "- 1 9 =");
    }

    [Fact]
    public void Division_with_edits_after_the_equals()
    {
        var b = B("÷, 10, X8, =");
        var r = DivisionShortcut.Solve(41, 59, b, 6, plusOne: true);
        Assert.Null(r.Error);
        AssertAllSend(41, 59, b, r.Lines.Select(l => l.Presses));
        Assert.Contains(r.Lines, l => Text(l.Presses) == "÷ 10 = X8");
        Assert.Equal("÷ 10 = X8", Text(r.Lines[0].Presses));
        Assert.Equal(3, r.Lines[0].Cost);
        Assert.Empty(ArithmeticSearch.Find(41, 59, b, 6, plusOne: false));

        var normalGoal = DivisionShortcut.Solve(41, 48, b, 6, plusOne: true);
        Assert.DoesNotContain(normalGoal.Lines, l => Text(l.Presses) == "÷ 10 = X8");
    }

    [Fact]
    public void Division_ending_with_equals_is_sent_by_it()
    {
        var b = B("÷, 10, =");
        var r = DivisionShortcut.Solve(41, 5, b, 6, plusOne: true);
        Assert.Contains(r.Lines, l => Text(l.Presses) == "÷ 10 =");
    }

    [Fact]
    public void Modulo_sends_goal_through_its_last_equals()
    {
        var b = B("2X, 3X, mod, 10, 3, =");
        var result = ModuloSearch.Find(41, 49, b, 8, plusOne: true);
        AssertAllSend(41, 49, b, result.Lines.Select(l => l.Presses));
        Assert.Contains(result.Lines, l => Text(l.Presses) == "2X 3X mod 10 3 =");
    }

    [Fact]
    public void Two_operator_first_equals_adds_one()
    {
        var b = B($"-, +, {Keys}, =");
        var lines = TwoOperatorSearch.Find(59, 42, b, 8, plusOne: true);
        AssertAllSend(59, 42, b, lines.Select(l => l.Presses));
        Assert.Contains(lines, l => Text(l.Presses) == "- 2 0 = + 1 =");
    }

    [Fact]
    public void Goal_zero_gives_no_lines_in_every_phase()
    {
        var b = B($"+, -, ×, ÷, mod, 10, {Keys}, =");
        Assert.Empty(ArithmeticSearch.Find(41, 0, b, 6, plusOne: true));
        Assert.Empty(TwoOperatorSearch.Find(41, 0, b, 6, plusOne: true));
        Assert.Empty(DivisionShortcut.Solve(41, 0, b, 6, plusOne: true).Lines);
        Assert.Empty(ModuloSearch.Find(41, 0, b, 6, plusOne: true).Lines);
    }
}

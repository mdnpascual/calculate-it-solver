namespace CalculatorPuzzle.Tests;

/// <summary>Boss rule 13: keep only lines whose only = is the last press.</summary>
public class OneSendTests
{
    static readonly Press Add = new(Key.Add), Sub = new(Key.Subtract), Div = new(Key.Divide), Mod = new(Key.Mod),
        Eq = new(Key.Equals), Ten = new(Key.Ten);
    static Press D(int d) => new(Key.Digit, d);
    static Press Xd(int d) => new(Key.Append, d);
    static Press Pre(int d) => new(Key.Prepend, d);
    static IReadOnlyList<Press> P(params Press[] presses) => presses;
    static Buttons B(string text) => TestButtons.Parse(text).Buttons!;

    [Fact]
    public void One_send_lines_are_kept()
    {
        Assert.True(EngineeringCalculator.OneSend(P()));
        Assert.True(EngineeringCalculator.OneSend(P(Xd(8))));
        Assert.True(EngineeringCalculator.OneSend(P(Div, Ten, Eq)));
        Assert.True(EngineeringCalculator.OneSend(P(Pre(2), Pre(3), Mod, Ten, D(3), Eq)));
    }

    [Fact]
    public void A_middle_equals_is_dropped()
    {
        Assert.False(EngineeringCalculator.OneSend(P(Div, Ten, Eq, Xd(8))));
        Assert.False(EngineeringCalculator.OneSend(P(Sub, D(2), D(0), Eq, Add, D(1), Eq)));
        Assert.False(EngineeringCalculator.OneSend(P(Eq, Xd(8))));
    }

    [Fact]
    public void Plan_drops_two_operator_and_middle_equals()
    {
        var plan = RequiredPlan.For(null, false).OneSend();
        Assert.False(plan.TwoOperator);
        Assert.False(plan.Accept!(P(Div, Ten, Eq, Xd(8))));
        Assert.True(plan.Accept!(P(Div, Ten, Eq)));
    }

    [Fact]
    public void Plan_with_a_required_button_needs_both()
    {
        var plan = RequiredPlan.For("X8", true).OneSend();
        Assert.True(plan.Accept!(P(Xd(8))));
        Assert.False(plan.Accept!(P(Div, Ten, Eq)));
        Assert.False(plan.Accept!(P(Div, Ten, Eq, Xd(8))));
    }

    static List<IReadOnlyList<Press>> Arith(long s, long g, string buttons)
    {
        var plan = RequiredPlan.For(null, false).OneSend();
        return ArithmeticSearch.Find(s, g, B(buttons), 8, CancellationToken.None, accept: plan.Accept).Select(l => l.Presses).ToList();
    }

    [Fact]
    public void Arithmetic_middle_equals_line_is_not_found()
    {
        foreach (var line in Arith(41, 438, "+:9, 1:9, X8:9"))
            Assert.True(EngineeringCalculator.OneSend(line));
    }

    [Fact]
    public void Arithmetic_edit_only_line_is_first() =>
        Assert.Equal("X8", string.Join(' ', Arith(41, 418, "X8:9, ÷:9, 10:9")[0]));

    [Fact]
    public void Arithmetic_minus_nineteen_is_first() =>
        Assert.Equal("- 1 9 =", string.Join(' ', Arith(59, 40, "-:9, +:9, 0:9, 1:9, 2:9, 3:9, 4:9, 5:9, 6:9, 7:9, 8:9, 9:9")[0]));

    static List<IReadOnlyList<Press>> Division(long s, long g, string buttons)
    {
        var plan = RequiredPlan.For(null, false).OneSend();
        var kept = new List<IReadOnlyList<Press>>();
        DivisionShortcut.Search(s, g, B(buttons), 8, l => { if (plan.Accept!(l.Presses)) kept.Add(l.Presses); });
        return kept;
    }

    [Fact]
    public void Division_keeps_the_line_ending_with_equals() =>
        Assert.Contains("÷ 10 =", Division(41, 4, "÷:9, 10:9").Select(l => string.Join(' ', l)));

    [Fact]
    public void Division_with_a_left_edit_after_equals_is_dropped() =>
        Assert.Empty(Division(41, 48, "÷:9, 10:9, X8:9"));

    [Fact]
    public void Modulo_line_is_kept()
    {
        var plan = RequiredPlan.For(null, false).OneSend();
        var r = ModuloSearch.Find(41, 48, B("2X:9, 3X:9, mod:9, 10:9, 3:9"), 8, accept: plan.Accept);
        Assert.Contains("2X 3X mod 10 3 =", r.Lines.Select(l => string.Join(' ', l.Presses)));
    }
}

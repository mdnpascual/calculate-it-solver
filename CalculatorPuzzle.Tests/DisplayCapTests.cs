namespace CalculatorPuzzle.Tests;

/// <summary>spec/display-cap: results above 999 999 clamp, Xd / dX stop at 6 characters, operand keys stop at 5.</summary>
public class DisplayCapTests
{
    static Buttons B(string text) => TestButtons.Parse(text).Buttons!;
    static readonly Buttons Everything = B("X++, X--, X*2, <<, sqr, p, round, 4X, 7X, X6, X7, X0, X1, 0, 1, 2, 5, 9, 10, +, -, ×, ÷, mod, flip, reset");
    static readonly Press Eq = new(Key.Equals);
    static Press D(int d) => new(Key.Digit, d);
    static Press T(string token) => Press.Of(LeftTransforms.Parse(token)!);
    static string? Last(long start, params Press[] presses) => EngineeringCalculator.Run(start, presses, Everything)?[^1];

    static readonly string AllText = string.Join(",", Enumerable.Range(0, 10).SelectMany(d => new[] { $"{d}X", $"X{d}", $"{d}" })
        .Concat(["10", "reset", "÷", "mod", "flip", "+", "-", "×"]));

    // 1
    [Fact]
    public void Add_and_multiply_clamp_to_999999()
    {
        Assert.Equal("999999", Last(999000, new(Key.Add), D(5), D(0), D(0), D(0), Eq));
        Assert.Equal("999999", Last(1000, new(Key.Multiply), D(1), D(0), D(0), D(0), Eq));
        Assert.Equal("1200", Last(600, new(Key.Multiply), D(2), Eq));
        Assert.Equal("0", Last(5, new(Key.Subtract), D(9), Eq));
        Assert.Equal("4", Last(41, new(Key.Divide), new(Key.Ten), Eq));
    }

    // 2
    [Fact]
    public void X_times_2_clamps_and_play_goes_on() =>
        Assert.Equal(["999999", "999998"], EngineeringCalculator.Run(600000, [T("X*2"), T("X--")], Everything));

    // 3
    [Fact]
    public void Square_clamps()
    {
        Assert.Equal("999999", Last(1000, T("sqr")));
        Assert.Equal("998001", Last(999, T("sqr")));
    }

    // 4
    [Fact]
    public void Prime_and_round_do_not_clamp()
    {
        Assert.Equal("1000003", Last(999999, T("p")));
        Assert.Equal("1000000", Last(999995, T("round")));
    }

    // 5
    [Fact]
    public void Xd_and_dX_do_nothing_on_6_characters()
    {
        Assert.Equal("123456", Last(12345, new Press(Key.Append, 6)));
        Assert.Null(Last(123456, new Press(Key.Append, 7)));
        Assert.Null(Last(123456, new Press(Key.Prepend, 7)));
        var zeros = Enumerable.Repeat(new Press(Key.Append, 0), 5).ToArray();
        Assert.Equal("000000", Last(0, zeros));
        Assert.Null(Last(0, [.. zeros, new Press(Key.Append, 1)]));
    }

    // 6
    [Fact]
    public void Operand_stops_at_5_characters_or_6_with_a_two_character_key()
    {
        Press Ten = new(Key.Ten);
        Assert.Equal("0", Last(41, new(Key.Divide), Ten, Ten, Ten, Eq));
        Assert.Null(Last(41, new(Key.Divide), Ten, Ten, Ten, Ten, Eq));
        Assert.Equal("0", Last(41, new(Key.Divide), D(9), D(9), D(9), D(9), D(9), Eq));
        Assert.Null(Last(41, new(Key.Divide), D(9), D(9), D(9), D(9), D(9), D(9), Eq));
    }

    // 7
    [Fact]
    public void TypeOperand_follows_the_operand_rule()
    {
        var ten = B("10");
        Assert.Equal(3, DivisionShortcut.TypeOperand("101010", ten)!.Count);
        Assert.Null(DivisionShortcut.TypeOperand("1010101", ten));
        var nine = B("9");
        Assert.Equal(5, DivisionShortcut.TypeOperand("99999", nine)!.Count);
        Assert.Null(DivisionShortcut.TypeOperand("999999", nine));
        Assert.Equal("9 9 9 9 10", string.Join(' ', DivisionShortcut.TypeOperand("999910", B("10, 9"))!));
    }

    // 8
    [Fact]
    public void LeftEdits_do_not_build_7_characters()
    {
        Assert.Null(LeftEdits.Plan("41", "1234567", Buttons.All));
        var plan = LeftEdits.Plan("41", "123441", Buttons.All);
        Assert.NotNull(plan);
        Assert.Equal("123441", EngineeringCalculator.Run(41, plan, Buttons.All)![^1]);
    }

    // 9
    [Fact]
    public void LeftSearch_forward_stops_at_6_characters()
    {
        var buttons = B("X6, X7");
        var found = LeftSearch.Forward("12345", buttons, LeftSearch.DefaultLimits("12345", "123456", 3));
        Assert.Contains("123456", found.Keys);
        Assert.DoesNotContain(found.Keys, k => k.Length > 6);
    }

    /// <summary>No Xd / dX result above 6 characters, no operand key result above 6, no four-<c>10</c> operand.</summary>
    static void AssertWithinTheCap(long start, IReadOnlyList<Press> presses, Buttons buttons)
    {
        var text = string.Join(' ', presses);
        var log = EngineeringCalculator.Run(start, presses, buttons);
        Assert.True(log != null, text);
        for (int i = 0; i < presses.Count; i++)
        {
            bool edit = presses[i].Key is Key.Prepend or Key.Append || Press.IsOperandKey(presses[i].Key);
            if (edit)
                Assert.True(log[i].Length <= EngineeringCalculator.MaxDisplay, $"{text}: {log[i]}");
        }
        Assert.DoesNotContain("10 10 10 10", text);
    }

    // 10
    [Fact]
    public void Division_and_modulo_lines_stay_within_the_cap()
    {
        var buttons = B(AllText);
        var division = DivisionShortcut.Solve(41, 483, buttons, 7);
        Assert.NotEmpty(division.Lines);
        foreach (var line in division.Lines)
            AssertWithinTheCap(41, line.Presses, buttons);
        Assert.DoesNotContain(division.Lines, l => l.Built.Length > EngineeringCalculator.MaxDisplay);

        var modulo = ModuloSearch.Find(41, 483, buttons, 8);
        Assert.NotEmpty(modulo.Lines);
        foreach (var line in modulo.Lines)
            AssertWithinTheCap(41, line.Presses, buttons);
    }

    [Fact]
    public void Division_path_A_never_uses_four_tens()
    {
        var buttons = B("10, ÷, X1, X2, X3, X4, X5, X6, X7, X8, X9, 1X, 2X, 3X, 4X, 5X, 6X, 7X, 8X, 9X");
        var result = DivisionShortcut.Solve(41, 483, buttons, 12);
        foreach (var line in result.Lines)
            AssertWithinTheCap(41, line.Presses, buttons);
    }

    // 11
    [Fact]
    public void Two_operator_lines_stay_within_the_cap()
    {
        var buttons = B(AllText);
        var lines = TwoOperatorSearch.Find(41, 483, buttons, 6);
        Assert.NotEmpty(lines);
        foreach (var line in lines)
            AssertWithinTheCap(41, line.Presses, buttons);
    }
}

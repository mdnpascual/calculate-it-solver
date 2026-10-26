namespace CalculatorPuzzle.Tests;

public class TakeTwoTests
{
    static Buttons B(string text) => TestButtons.Parse(text).Buttons!;
    static string Text(IReadOnlyList<Press>? p) => p == null ? "null" : string.Join(' ', p);

    static readonly Press[] Line =
    [
        new(Key.Append, 1), new(Key.Prepend, 8), new(Key.Divide), new(Key.Ten), new(Key.Ten),
        new(Key.Equals), new(Key.Prepend, 4),
    ];

    [Theory]
    [InlineData("X8:1", 1)]
    [InlineData("X8:2", 1)]
    [InlineData("X8:3", 2)]
    [InlineData("X8:4", 2)]
    public void Charges_become_presses(string text, int presses) =>
        Assert.Equal(presses, B(text).TakeTwo().Charges["X8"]);

    [Fact]
    public void Glass_keeps_its_one_press_and_zero_stays_absent()
    {
        var b = B("X8:1g, 4X:3, ÷:0").TakeTwo();
        Assert.Equal(1, b.Charges["X8"]);
        Assert.Equal(2, b.Charges["4X"]);
        Assert.Contains("X8", b.Glass);
        Assert.False(b.Charges.ContainsKey("÷"));
        Assert.False(b.Charges.ContainsKey("="));
    }

    [Fact]
    public void TakeTwo_keeps_the_rest()
    {
        var a = B("X8:3, 3X:3, 7:3, 10:3, ÷:3, mod:3, flip:3, sqr:3, c12:3, q5:3, X1:2g");
        var b = a.TakeTwo();
        Assert.Equal(a.PrependDigits, b.PrependDigits);
        Assert.Equal(a.AppendDigits, b.AppendDigits);
        Assert.Equal(a.DigitKeys, b.DigitKeys);
        Assert.Equal(a.Ten, b.Ten);
        Assert.Equal(a.Divide, b.Divide);
        Assert.Equal(a.Mod, b.Mod);
        Assert.Equal(a.Flip, b.Flip);
        Assert.Equal(a.Glass, b.Glass);
        Assert.Equal(a.Transforms, b.Transforms);
        Assert.Equal(a.ChargeStart, b.ChargeStart);
        Assert.Equal(a.QuestDigit, b.QuestDigit);
        Assert.Equal(2, b.ChargeStep);
    }

    [Fact]
    public void Fits_counts_presses_after_TakeTwo()
    {
        var two = B("X1:2, 8X:2, 4X:2, ÷:2, 10:2");
        Assert.True(two.Fits(Line));
        Assert.False(two.TakeTwo().Fits(Line));
        Assert.True(B("X1:2, 8X:2, 4X:2, ÷:2, 10:3").TakeTwo().Fits(Line));
    }

    [Fact]
    public void Drained_counts_presses_after_TakeTwo()
    {
        var b = B("10:3").TakeTwo();
        Assert.Equal(1, b.Drained([new(Key.Ten), new(Key.Ten)]));
        Assert.Equal(0, b.Drained([new(Key.Ten)]));
    }

    [Theory]
    [InlineData(12, 6)]
    [InlineData(13, 7)]
    public void ChargeValue_steps_by_two(int start, int presses)
    {
        var b = B($"c:{start}").TakeTwo();
        for (int used = 0; used < presses; used++)
            Assert.Equal(start - 2 * used, b.ChargeValue(used));
        Assert.Null(b.ChargeValue(presses));
        Assert.Equal(start % 2 == 0 ? 2 : 1, b.ChargeValue(presses - 1));
    }

    [Fact]
    public void ChargeValue_step_one_is_unchanged()
    {
        var b = B("c:12");
        Assert.Equal(11, b.ChargeValue(1));
        Assert.Equal(1, b.ChargeValue(11));
        Assert.Null(b.ChargeValue(12));
    }

    [Fact]
    public void Simulator_follows_the_step()
    {
        var b = B("8X:2, ÷:2, c12:12");
        var take = b.TakeTwo();
        Press[] ok = [new(Key.Prepend, 8), new(Key.Divide), new(Key.Charge, 12), new(Key.Charge, 10), new(Key.Equals)];
        Press[] bad = [new(Key.Prepend, 8), new(Key.Divide), new(Key.Charge, 12), new(Key.Charge, 11), new(Key.Equals)];
        Assert.NotNull(EngineeringCalculator.Run(411, ok, take));
        Assert.Null(EngineeringCalculator.Run(411, bad, take));
        Assert.NotNull(EngineeringCalculator.Run(411, bad, b));
    }

    [Fact]
    public void TypeOperand_uses_the_step()
    {
        var take = B("c:12").TakeTwo();
        Assert.Equal("c12 c10", Text(DivisionShortcut.TypeOperand("1210", take)));
        Assert.Null(DivisionShortcut.TypeOperand("1211", take));
    }

    [Fact]
    public void Search_never_reports_a_line_that_does_not_fit()
    {
        var take = B("X1:2, 8X:2, 4X:2, ÷:2, 10:3, X8:1").TakeTwo();
        var result = DivisionShortcut.Solve(41, 48, take, 8);
        Assert.NotEmpty(result.Lines);
        foreach (var line in result.Lines)
        {
            Assert.True(take.Fits(line.Presses));
            Assert.Equal("48", EngineeringCalculator.Run(41, line.Presses, take)![^1]);
        }
    }
}

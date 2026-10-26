namespace CalculatorPuzzle.Tests;

public class BlockedButtonTests
{
    static string File(string buttons) =>
        "{ \"version\": 1, \"seq\": 1, \"time\": \"\", \"round\": 1, \"start\": 41, \"goal\": 48, "
        + $"\"bossRule\": 2, \"buttons\": [ {buttons} ] }}";

    static string Button(string id, int uses, string extra = "") =>
        $"{{ \"id\": \"{id}\", \"uses\": {uses}{extra} }}";

    [Fact]
    public void Blocked_button_with_charges_is_zero_and_listed()
    {
        var p = GameLink.Parse(File(Button("operator_x8", 2, ", \"blocked\": true")))!;
        Assert.Equal("X8:0", p.ButtonsText);
        Assert.Equal(["X8"], p.Blocked);
    }

    [Fact]
    public void Blocked_empty_button_is_not_listed()
    {
        var p = GameLink.Parse(File(Button("operator_x8", 0, ", \"blocked\": true")))!;
        Assert.Equal("X8:0", p.ButtonsText);
        Assert.Empty(p.Blocked!);
    }

    [Theory]
    [InlineData(", \"blocked\": false")]
    [InlineData("")]
    public void Not_blocked_keeps_the_charges(string extra)
    {
        var p = GameLink.Parse(File(Button("operator_x8", 2, extra)))!;
        Assert.Equal("X8:2", p.ButtonsText);
        Assert.Empty(p.Blocked!);
    }

    [Fact]
    public void Blocked_wins_over_glass()
    {
        var p = GameLink.Parse(File(Button("operator_x8", 1, ", \"glass\": true, \"blocked\": true")))!;
        Assert.Equal("X8:0", p.ButtonsText);
        Assert.Empty(p.Glass);
        Assert.Equal(["X8"], p.Blocked);
    }

    [Fact]
    public void Blocked_charge_key_is_c_zero()
    {
        var p = GameLink.Parse(File(Button("number_charge", 12, ", \"blocked\": true")))!;
        Assert.Equal("volt:0", p.ButtonsText);
        Assert.Equal(["c"], p.Blocked);
    }

    [Fact]
    public void Blocked_unmapped_button_stays_ignored()
    {
        var p = GameLink.Parse(File(Button("operator_random", 1, ", \"blocked\": true")))!;
        Assert.Equal(["random"], p.Ignored);
        Assert.Empty(p.Blocked!);
    }

    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, 3)]
    public void Solver_avoids_the_blocked_button(bool blocked, int presses)
    {
        string flag = blocked ? ", \"blocked\": true" : "";
        var p = GameLink.Parse(File(string.Join(", ",
            Button("operator/", 3), Button("number10", 4), Button("operator_x8", 2, flag),
            Button("operator_8x", 2), Button("operator_reverse", 1))))!;
        var buttons = Buttons.Parse(p.ButtonsText).Buttons!;
        var result = DivisionShortcut.Solve(41, 48, buttons, 6);
        var best = result.Lines.OrderBy(l => l.Cost).First();
        Assert.Equal(presses, best.Cost);
        if (blocked)
            Assert.All(result.Lines, l => Assert.DoesNotContain("X8", l.Presses.Select(x => x.ToString())));
    }
}

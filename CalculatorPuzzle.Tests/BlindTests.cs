namespace CalculatorPuzzle.Tests;

public class BlindTests
{
    static Buttons B(string text, long goal, bool blind)
    {
        var buttons = Buttons.Parse(text, goal: goal).Buttons!;
        return buttons.WithContext(new LineContext(goal, true, Blind: blind));
    }

    static readonly RequiredPlan Everything = RequiredPlan.For(null, false);

    static List<ResultLine> Search(long start, long goal, string text, bool blind, int maxSteps = 6)
    {
        var lines = new List<ResultLine>();
        GoalStepSearch.Search(start, goal, B(text, goal, blind), Everything, null, l => { lock (lines) lines.Add(l); },
            CancellationToken.None, 10, maxSteps);
        return lines;
    }

    static string Text(ResultLine l) => string.Join(' ', l.Presses);

    // 1: near reads the goal shown at its press.
    [Fact]
    public void Near_first_press_reads_the_unraised_goal_with_blind()
    {
        var near = LeftTransforms.Near(46);
        var b = B("near:1, X++:1", 46, true);
        Assert.Equal("46", EngineeringCalculator.Run(41, [Press.Of(near)], b)![^1]);   // 41 + 5 within 10 -> goal
        var plus = LeftTransforms.Parse("X++")!;
        // After X++ (free) the goal is still 46; a second press reads 47.
        var b2 = B("near:1, X++:1", 46, true);
        Assert.Equal("47", EngineeringCalculator.Run(41, [Press.Of(plus), Press.Of(LeftTransforms.Near(46))], b2)![^1]);
    }

    // 2: a glass press doesn't use the free one.
    [Fact]
    public void Glass_press_leaves_the_free_press()
    {
        var b = B("X++:1g, near:1", 46, true);
        var plus = LeftTransforms.Parse("X++")!;
        // glass X++ (no raise), near (free, no raise) -> reads 46
        Assert.Equal("46", EngineeringCalculator.Run(41, [Press.Of(plus), Press.Of(LeftTransforms.Near(46))], b)![^1]);
    }

    // 3
    [Fact]
    public void Raises_drops_the_free_press()
    {
        var b = B("÷:9, 10:9, X8:9, =", 46, true);
        Press[] line = [new(Key.Divide), new(Key.Ten), new(Key.Equals), new(Key.Append, 8)];
        Assert.Equal(3, GoalStepSearch.Charged(line, b));
        Assert.Equal(2, GoalStepSearch.Raises(line, b));
        Assert.Equal(0, GoalStepSearch.Raises([], b));
        Assert.Equal(3, GoalStepSearch.Raises(line, B("÷:9, 10:9, X8:9, =", 46, false)));
    }

    // 4
    [Fact]
    public void Rule_14_with_blind_aims_at_goal_plus_k_minus_1()
    {
        var withBlind = Search(41, 46, "÷:9, 10:9, X8:9, =", true);
        var top = withBlind.First(l => Text(l) == "÷ 10 = X8");
        Assert.Equal(48, top.Reaches);
        Assert.DoesNotContain(Search(41, 46, "÷:9, 10:9, X8:9, =", false), l => Text(l) == "÷ 10 = X8");
        Assert.Contains(Search(41, 45, "÷:9, 10:9, X8:9, =", false), l => Text(l) == "÷ 10 = X8");
        Assert.DoesNotContain(Search(41, 45, "÷:9, 10:9, X8:9, =", true), l => Text(l) == "÷ 10 = X8");
    }

    // 5
    [Fact]
    public void One_press_line_can_end_at_the_goal_with_blind()
    {
        var lines = Search(47, 48, "X++:1, =", true);
        var line = Assert.Single(lines, l => Text(l) == "X++");
        Assert.Equal(48, line.Reaches);
        Assert.DoesNotContain(Search(47, 48, "X++:1, =", false), l => Text(l) == "X++");
    }

    // 6
    [Fact]
    public void Glass_button_does_not_use_the_free_press()
    {
        var line = Search(41, 47, "÷:9, 10:9, X8:1g, =", true).First(l => Text(l) == "÷ 10 = X8");
        Assert.Equal(48, line.Reaches);
    }

    // 7
    [Fact]
    public void Swap_is_the_free_press()
    {
        Assert.Equal(48, SwapSearch.FlippedStart(48, B("swap:1", 48, true), 14));
        Assert.Equal(49, SwapSearch.FlippedStart(48, B("swap:1", 48, false), 14));
        Assert.Equal(48, SwapSearch.FlippedStart(48, B("swap:1g", 48, true), 14));
        Assert.Equal(49, SwapSearch.FlippedStart(48, B("swap:1", 48, true), 12));

        var plain = SwapSearch.Flip(B("swap:1, ÷:1", 48, true), 4);
        Assert.False(plain.Context!.Blind);
        var glass = SwapSearch.Flip(B("swap:1g, ÷:1", 48, true), 4);
        Assert.True(glass.Context!.Blind);
    }

    [Fact]
    public void Swap_check_uses_raises()
    {
        // S 46, G 479: swap is free (F = 479), the flipped leg has no blind, so ÷ 10 = needs S + 2 = 48 and 479 ÷ 10 = 47 misses.
        // Without blind F = 480 and 480 ÷ 10 = 48 hits.
        Press[] line = [new(Key.Swap), new(Key.Divide), new(Key.Ten), new(Key.Equals)];
        Assert.False(SwapSearch.Check(46, 479, line, B("swap:1, ÷:1, 10:1", 479, true), 14));
        Assert.True(SwapSearch.Check(46, 479, line, B("swap:1, ÷:1, 10:1", 479, false), 14));
        Assert.True(SwapSearch.Check(46, 480, line, B("swap:1, ÷:1, 10:1", 480, true), 14));
    }

    // 8
    [Theory]
    [InlineData(", \"blind\": true", true)]
    [InlineData(", \"blind\": false", false)]
    [InlineData("", false)]
    public void GameLink_reads_blind(string json, bool expected)
    {
        var text = "{ \"version\": 1, \"seq\": 1, \"time\": \"\", \"round\": 1, \"start\": 41, \"goal\": 48 " + json
            + ", \"buttons\": [ { \"id\": \"operator/\", \"uses\": 3 } ] }";
        Assert.Equal(expected, GameLink.Parse(text)!.Blind);
    }
}

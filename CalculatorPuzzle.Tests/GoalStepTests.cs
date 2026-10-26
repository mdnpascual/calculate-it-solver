namespace CalculatorPuzzle.Tests;

public class GoalStepTests
{
    const string Plain = "÷:9, 10:9, X8:9, =";
    static readonly RequiredPlan Everything = RequiredPlan.For(null, false);

    static Buttons B(string text) => Buttons.Parse(text).Buttons!;

    static (List<ResultLine> Lines, GoalStepSearch.Result Result) Run(long start, long goal, string buttons,
                                                                        int wanted = 10, int maxSteps = 12)
    {
        var b = B(buttons);
        var lines = new List<ResultLine>();
        var result = GoalStepSearch.Search(start, goal, b, Everything, null, l => { lock (lines) lines.Add(l); },
            CancellationToken.None, wanted, maxSteps);
        return (lines, result);
    }

    static string Text(ResultLine l) => string.Join(' ', l.Presses);

    // ---- Glass syntax ----

    [Fact]
    public void Glass_syntax_marks_the_button()
    {
        var b = B("X8:1g");
        Assert.Equal(["X8"], b.Glass);
        Assert.Equal(1, b.Charges["X8"]);
        Assert.Equal(1, B("X8:4g").Charges["X8"]);
        Assert.Equal(1, B("X8:1G").Charges["X8"]);
    }

    [Fact]
    public void Glass_zero_charges_leaves_the_button_out()
    {
        var b = B("X8:0g");
        Assert.Empty(b.Glass);
        Assert.DoesNotContain("X8", b.Charges.Keys);
        Assert.Empty(b.AppendDigits);
    }

    [Theory]
    [InlineData("X8:g")]
    [InlineData("X8:1x")]
    [InlineData("X8:1gg")]
    public void Bad_glass_counts_are_errors(string text)
    {
        var (b, error) = Buttons.Parse(text);
        Assert.Null(b);
        Assert.StartsWith("Bad charge count", error);
    }

    [Fact]
    public void Glass_uses_charge_keys()
    {
        var b = B("÷:1g, c12:1g, sqr:1g");
        Assert.Equal(new[] { "c", "sqr", "÷" }.OrderBy(x => x), b.Glass.OrderBy(x => x));
    }

    [Fact]
    public void Plain_buttons_are_not_glass() => Assert.Empty(B("X8:3").Glass);

    // ---- Charged ----

    [Fact]
    public void Charged_skips_equals_and_glass()
    {
        var presses = new[] { new Press(Key.Divide), new Press(Key.Ten), new Press(Key.Equals), new Press(Key.Append, 8) };
        Assert.Equal(3, GoalStepSearch.Charged(presses, B(Plain)));
        Assert.Equal(2, GoalStepSearch.Charged(presses, B("÷:9, 10:9, X8:1g, =")));
    }

    // ---- Search ----

    [Fact]
    public void Division_line_is_found_at_k3()
    {
        var (lines, r) = Run(41, 45, Plain);
        Assert.NotEmpty(lines);
        Assert.All(lines.Where(l => Text(l) == "÷ 10 = X8"), l => Assert.Equal(48, l.Reaches));
        Assert.Contains(lines, l => Text(l) == "÷ 10 = X8");
        Assert.All(lines, l => Assert.True(l.Reaches >= 48));
        Assert.True(r.LastStep >= 3);
    }

    [Fact]
    public void Glass_button_lowers_the_charged_count()
    {
        var (lines, _) = Run(41, 46, "÷:9, 10:9, X8:1g, =");
        var line = Assert.Single(lines, l => Text(l) == "÷ 10 = X8");
        Assert.Equal(48, line.Reaches);
    }

    [Fact]
    public void Without_glass_the_line_is_not_kept_at_the_old_goal()
    {
        var (lines, _) = Run(41, 46, Plain);
        Assert.DoesNotContain(lines, l => Text(l) == "÷ 10 = X8" && l.Reaches == 48);
    }

    [Fact]
    public void Every_line_ends_at_goal_plus_charged()
    {
        const string text = "÷:9, 10:9, X8:1g, 8X:9, X4:9, flip:9, mod:9, 3:9, +:9, =";
        var b = B(text);
        var (lines, _) = Run(41, 46, text);
        Assert.NotEmpty(lines);
        foreach (var l in lines)
        {
            var log = EngineeringCalculator.Run(41, l.Presses, b);
            Assert.NotNull(log);
            Assert.Equal(l.Reaches.ToString(), log![^1]);
            Assert.Equal(46 + GoalStepSearch.Charged(l.Presses, b), l.Reaches);
        }
    }

    [Fact]
    public void Stops_after_the_k_that_reaches_wanted()
    {
        var (lines, r) = Run(41, 45, Plain, wanted: 1);
        Assert.Equal(3, r.LastStep);  // k = 1, 2 find nothing; k = 3 finds ÷ 10 = X8
        Assert.Single(lines);
    }

    [Fact]
    public void Runs_to_max_steps_when_wanted_is_not_reached()
    {
        var (lines, r) = Run(41, 45, Plain, wanted: 10, maxSteps: 5);
        Assert.Equal(5, r.LastStep);
        Assert.Single(lines);
    }

    [Fact]
    public void Cancelled_token_throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var lines = new List<ResultLine>();
        Assert.Throws<OperationCanceledException>(() =>
            GoalStepSearch.Search(41, 45, B(Plain), Everything, null, lines.Add, cts.Token, 10, 12));
        Assert.Empty(lines);
    }

    [Fact]
    public void Target_equal_to_start_is_skipped()
    {
        var (lines, r) = Run(47, 45, Plain, maxSteps: 2);
        Assert.Equal(2, r.LastStep);
        Assert.DoesNotContain(lines, l => l.Reaches == 47);
    }

    // ---- Parsing and display ----

    [Fact]
    public void GameLink_reads_boss_rule()
    {
        const string head = "{ \"version\": 1, \"seq\": 1, \"time\": \"\", \"round\": 1, \"start\": 41, \"goal\": 48, ";
        Assert.Equal(14, GameLink.Parse(head + "\"bossRule\": 14, \"buttons\": [] }")!.BossRule);
        Assert.Equal(0, GameLink.Parse(head + "\"buttons\": [] }")!.BossRule);
    }

    [Fact]
    public void GameLink_writes_glass_buttons_with_g()
    {
        const string json = "{ \"version\": 1, \"seq\": 1, \"time\": \"\", \"round\": 1, \"start\": 41, \"goal\": 48, \"buttons\": [ "
            + "{ \"id\": \"operator_x1\", \"uses\": 3, \"glass\": true }, { \"id\": \"operator_x2\", \"uses\": 0, \"glass\": true } ] }";
        Assert.Equal("X1:1g, X2:0", GameLink.Parse(json)!.ButtonsText);
    }

    [Fact]
    public void ResultLine_shows_the_goal_it_reaches()
    {
        var (lines, _) = Run(41, 45, Plain);
        var text = lines.First(l => Text(l) == "÷ 10 = X8").ToString();
        Assert.Contains("goal 48", text);
        Assert.StartsWith("/ | 10 | = | X8   (3 press, goal 48", text);
    }

    // ---- Two-operator lines ----

    [Fact]
    public void Two_operator_line_reaches_goal_plus_k()
    {
        // No one-operator line fits any k up to 8; 41 + 4 + 7 = 52 = 48 + 4 presses.
        var (lines, result) = Run(41, 48, "+:2, 7:2, 4:2", wanted: 1);
        Assert.Equal(0, result.Arithmetic);
        Assert.Equal(lines.Count, result.TwoOperator);
        Assert.NotEmpty(lines);
        var b = B("+:2, 7:2, 4:2");
        Assert.All(lines, l =>
        {
            Assert.NotNull(l.TwoOperator);
            Assert.Equal(GoalStepSearch.Charged(l.Presses, b), l.Cost);
            Assert.Equal(48 + l.Cost, l.Reaches);
            Assert.Equal((48 + l.Cost).ToString(), EngineeringCalculator.Run(41, l.Presses, b)![^1]);
        });
        Assert.Contains(lines, l => Text(l) == "+ 4 = + 7 =");
    }
}

namespace CalculatorPuzzle.Tests;

/// <summary>The near-one passive: a send within 1 of the goal wins.</summary>
public class NearOneTests
{
    static readonly RequiredPlan Everything = RequiredPlan.For(null, false);
    static Buttons B(string text) => Buttons.Parse(text).Buttons!;
    static string Text(ResultLine l) => string.Join(' ', l.Presses);

    [Fact]
    public void Targets_basic()
    {
        Assert.Equal([48L, 47L, 49L], NearGoals.Targets(48, true, false));
        Assert.Equal([48L], NearGoals.Targets(48, false, false));
    }

    [Fact]
    public void Targets_zero()
    {
        Assert.Equal([0L, 1L], NearGoals.Targets(0, true, false));
        Assert.Equal([1L, 2L], NearGoals.Targets(1, true, true));
    }

    [Fact]
    public void BareSend_when_start_is_one_off()
    {
        var b = B("+:9, 7:9, =");
        var lines = ArithmeticSearch.Find(49, 49, b, 6, bareSend: true);
        Assert.Single(lines);
        Assert.Equal("=", string.Join(' ', lines[0].Presses));
        Assert.Equal(0, lines[0].Cost);
        Assert.Empty(ArithmeticSearch.Find(49, 49, b, 6));
    }

    static List<ResultLine> Near(long start, long goal, string buttons)
    {
        var b = B(buttons);
        var lines = new List<ResultLine>();
        foreach (var t in NearGoals.Targets(goal, true, false))
        {
            if (t == start) continue;
            foreach (var l in DivisionShortcut.Solve(start, t, b, 6).Lines)
                lines.Add(ResultLine.From(l, b) with { Reaches = t == goal ? null : t });
        }
        lines.Sort(ResultLine.Ranking);
        return lines;
    }

    [Fact]
    public void Near_line_is_found()
    {
        var lines = Near(41, 48, "X7:1, ÷:1, 10:1, =");
        var line = Assert.Single(lines, l => Text(l) == "÷ 10 = X7");
        Assert.Equal(47, line.Reaches);
        Assert.Contains("goal 47", line.ToString());
    }

    [Fact]
    public void Ranking_unchanged()
    {
        var b = B("+:5, 9:5, 10:5, =");
        var exact = ResultLine.From(ArithmeticSearch.Find(41, 50, b, 6)[0], b);
        var near = ResultLine.From(ArithmeticSearch.Find(41, 51, b, 6)[0], b) with { Reaches = 51 };
        Assert.Equal(exact.Cost, near.Cost);
        var longer = exact with { Cost = exact.Cost + 1 };
        Assert.True(ResultLine.Ranking.Compare(near, longer) < 0);
    }

    [Fact]
    public void GoalStep_near1()
    {
        var b = B("+:9, 1:9, 2:9, 3:9, =");
        var lines = new List<ResultLine>();
        GoalStepSearch.Search(41, 48, b, Everything, null, l => { lock (lines) lines.Add(l); },
            CancellationToken.None, 10, 3, near1: true);
        Assert.NotEmpty(lines);
        foreach (var l in lines)
        {
            long k = GoalStepSearch.Charged(l.Presses, b);
            Assert.InRange(l.Reaches!.Value, 48 + k - 1, 48 + k + 1);
        }
        Assert.Contains(lines, l => l.Reaches != 48 + GoalStepSearch.Charged(l.Presses, b));
        var exactOnly = new List<ResultLine>();
        GoalStepSearch.Search(41, 48, b, Everything, null, exactOnly.Add, CancellationToken.None, 10, 3);
        Assert.All(exactOnly, l => Assert.Equal(48 + GoalStepSearch.Charged(l.Presses, b), l.Reaches));
    }

    [Fact]
    public void PlusOne_near1()
    {
        var b = B("+:9, 7:9, =");
        // Rule 12 sends 41 + 7 + 1 = 49 = G + 1 for G = 48.
        var lines = ArithmeticSearch.Find(41, 49, b, 6, plusOne: true);
        Assert.Contains(lines, l => string.Join(' ', l.Presses) == "+ 7 =");
        Assert.Equal(49, EngineeringCalculator.Sent(41, lines.First(l => string.Join(' ', l.Presses) == "+ 7 =").Presses, b, true));
        Assert.Equal([1L, 2L], NearGoals.Targets(1, true, true));
    }

    [Fact]
    public void GameLink_near1()
    {
        const string Json = """{"version":1,"seq":1,"time":"t","round":1,"start":1,"goal":2,"buttons":[],"near1":true}""";
        Assert.True(GameLink.Parse(Json)!.Near1);
        Assert.False(GameLink.Parse(Json.Replace(",\"near1\":true", ""))!.Near1);
    }
}

namespace CalculatorPuzzle.Tests;

public class SwapTests
{
    static Buttons B(string text) => Buttons.Parse(text).Buttons!;

    static readonly RequiredPlan Everything = RequiredPlan.For(null, false);

    static string Text(ResultLine l) => string.Join(' ', l.Presses);

    /// <summary>Arithmetic and division for each leg, the flipped lines wrapped (what MainForm does, without caps).</summary>
    static List<ResultLine> Solve(long start, long goal, Buttons b, int bossRule = 0, string? required = null, bool boss = false)
    {
        var plan = RequiredPlan.For(boss ? required : null, boss);
        var lines = new List<ResultLine>();
        bool plusOne = bossRule == EngineeringCalculator.SendPlusOneRule;
        foreach (var leg in SwapSearch.Legs(start, goal, b, plan, required, boss, bossRule))
        {
            var found = new List<ResultLine>();
            foreach (var l in ArithmeticSearch.Find(leg.Start, leg.Goal, leg.Buttons, 8, CancellationToken.None,
                         accept: leg.Plan.Accept, plusOne: plusOne))
                found.Add(ResultLine.From(l, leg.Buttons, required));
            DivisionShortcut.Search(leg.Start, leg.Goal, leg.Buttons, 8,
                l => { lock (found) found.Add(ResultLine.From(l, leg.Buttons, required)); }, CancellationToken.None, plusOne: plusOne);
            lines.AddRange(leg.Flipped
                ? found.Select(f => SwapSearch.Wrap(f, start, b, required)).Where(w => w != null).Select(w => w!)
                : found);
        }
        return lines;
    }

    [Fact]
    public void Parse_swap()
    {
        var b = B("swap:2, ÷:1");
        Assert.Equal(2, b.Charges["swap"]);
        Assert.Equal("swap", Buttons.Token("swap"));
        Assert.Contains("swap", B("swap:1g").Glass);
        Assert.Equal(1, B("swap:1g").Charges["swap"]);
        Assert.Equal("swap", new Press(Key.Swap).ToString());
        Assert.DoesNotContain("swap", B("swap:0, ÷:1").Charges.Keys);
        Assert.Null(Buttons.OperatorKey("swap"));
    }

    [Fact]
    public void Run_rejects_swap()
    {
        var b = B("swap:1, X2:1");
        Assert.Null(EngineeringCalculator.Run(12, [new Press(Key.Swap), new Press(Key.Append, 2)], b));
        Assert.Null(EngineeringCalculator.Run(12, [new Press(Key.Swap)], b));
    }

    [Fact]
    public void Without_clears_one_charge()
    {
        var b = B("swap:2, ÷:1").Without("swap");
        Assert.Equal(0, b.Charges["swap"]);
        Assert.Equal(1, b.Charges["÷"]);
    }

    [Fact]
    public void FlippedStart_rules()
    {
        var b = B("swap:1");
        Assert.Equal(48, SwapSearch.FlippedStart(48, b, 0));
        Assert.Equal(49, SwapSearch.FlippedStart(48, b, 12));
        Assert.Equal(49, SwapSearch.FlippedStart(48, b, 14));
        Assert.Equal(48, SwapSearch.FlippedStart(48, B("swap:1g"), 14));
    }

    [Fact]
    public void Check_first_only()
    {
        var b = B("swap:1, ÷:1, 10:1");
        Press[] line = [new(Key.Swap), new(Key.Divide), new(Key.Ten), new(Key.Equals)];
        Assert.True(SwapSearch.Check(4, 48, line, b, 0));
        Assert.False(SwapSearch.Check(4, 48, [line[1], line[0], line[2], line[3]], b, 0));
        Assert.False(SwapSearch.Check(4, 48, [line[0], line[0], line[1], line[2], line[3]], B("swap:2, ÷:1, 10:1"), 0));
        Assert.False(SwapSearch.Check(4, 48, line, B("÷:1, 10:1"), 0));
        Assert.False(SwapSearch.Check(4, 48, line[1..], b, 0));
    }

    [Fact]
    public void Flipped_line_found()
    {
        var b = B("swap:1, ÷:1, 10:1");
        var legs = SwapSearch.Legs(4, 48, b, Everything, null, false, 0);
        Assert.Equal([false, true], legs.Select(l => l.Flipped));
        Assert.Empty(ArithmeticSearch.Find(4, 48, b, 8, CancellationToken.None));
        var lines = Solve(4, 48, b);
        var line = Assert.Single(lines, l => Text(l) == "swap ÷ 10 =");
        Assert.Equal(3, line.Cost);
        Assert.Equal(4, line.Reaches);
        Assert.True(line.Swapped);
        Assert.Contains("goal 4", line.ToString());
        Assert.True(SwapSearch.Check(4, 48, line.Presses, b, 0));
    }

    [Fact]
    public void Edits_only_after_swap()
    {
        var b = B("swap:1, X2:1");
        var line = Assert.Single(Solve(12, 1, b));
        Assert.Equal("swap X2", Text(line));
        Assert.Equal(2, line.Cost);
        Assert.True(SwapSearch.Check(12, 1, line.Presses, b, 0));
    }

    [Fact]
    public void No_flipped_leg()
    {
        var b = B("swap:1, ÷:1, 10:1");
        Assert.Single(SwapSearch.Legs(4, 48, b, Everything, null, false, 13));
        Assert.Single(SwapSearch.Legs(4, 48, B("÷:1, 10:1"), Everything, null, false, 0));
        Assert.Single(SwapSearch.Legs(48, 48, b, Everything, null, false, 0));
    }

    [Fact]
    public void Required_swap_boss()
    {
        var b = B("swap:1, ÷:1, 10:1");
        var legs = SwapSearch.Legs(4, 48, b, RequiredPlan.For("swap", true), "swap", true, 11);
        var only = Assert.Single(legs);
        Assert.True(only.Flipped);
        Assert.Null(only.Plan.Accept);
    }

    [Fact]
    public void Market_swap()
    {
        var b = B("swap:1, +:1, -:1, 7:1, ÷:1, 10:1");
        var lines = Solve(41, 48, b, required: "swap");
        Assert.Contains(lines, l => !l.Swapped);
        Assert.Contains(lines, l => l.Swapped);
        Assert.All(lines.Where(l => !l.Swapped), l => Assert.True(l.Missing));
        Assert.All(lines.Where(l => l.Swapped), l => Assert.False(l.Missing));
    }

    [Fact]
    public void Swap_alone()
    {
        var b = B("swap:1");
        var legs = SwapSearch.Legs(49, 48, b, Everything, null, false, 12);
        Assert.Single(legs);
        var line = SwapSearch.SwapAlone(49, 48, b, null, 12);
        Assert.NotNull(line);
        Assert.Equal(1, line!.Cost);
        Assert.True(SwapSearch.Check(49, 48, line.Presses, b, 12));
        Assert.Null(SwapSearch.SwapAlone(49, 48, b, null, 0));
        Assert.False(SwapSearch.Check(49, 48, line.Presses, b, 0));
    }

    [Fact]
    public void Drained_counts_swap()
    {
        var inner = B("÷:5, 10:5");
        var b = B("swap:1, ÷:5, 10:5");
        var row = ResultLine.From(DivisionShortcut.Solve(48, 4, inner, 8).Lines.First(l => l.Cost == 2), inner);
        var wrapped = SwapSearch.Wrap(row, 4, b, null)!;
        Assert.Equal(row.Drained + 1, wrapped.Drained);
        Assert.Equal(row.Cost + 1, wrapped.Cost);
        Assert.Equal(row.Spare + 1, wrapped.Spare);
    }

    [Fact]
    public void Rule12_flipped()
    {
        // S 5, G 48, every = adds 1: swap shows 49, ÷ 10 = gives 4, and the send adds 1.
        var b = B("swap:1, ÷:1, 10:1");
        var lines = Solve(5, 48, b, bossRule: 12);
        var line = Assert.Single(lines, l => Text(l) == "swap ÷ 10 =");
        Assert.True(SwapSearch.Check(5, 48, line.Presses, b, 12));
    }

    [Fact]
    public void Rule14_flipped()
    {
        var b = B("swap:1, -:1, 4:1, 2:1");
        var legs = SwapSearch.Legs(4, 48, b, Everything, null, true, 14);
        var flipped = Assert.Single(legs, l => l.Flipped);
        Assert.Equal(49, flipped.Start);
        var lines = new List<ResultLine>();
        GoalStepSearch.Search(flipped.Start, flipped.Goal, flipped.Buttons, flipped.Plan, null, l => { lock (lines) lines.Add(l); },
            CancellationToken.None, 10, 6);
        Assert.NotEmpty(lines);
        foreach (var l in lines)
        {
            var wrapped = SwapSearch.Wrap(l, 4, b, null)!;
            Assert.Equal(4 + GoalStepSearch.Charged(l.Presses, flipped.Buttons), wrapped.Reaches);
            Assert.True(SwapSearch.Check(4, 48, wrapped.Presses, b, 14));
        }
    }

    [Fact]
    public void GameLink_maps_swap() => Assert.Equal("swap", GameLink.MapButton("operator_swap"));
}

namespace CalculatorPuzzle.Tests;

public class BossRequiredTests
{
    static Buttons B(string text)
    {
        var (buttons, error) = Buttons.Parse(text);
        Assert.Null(error);
        return buttons!;
    }

    static string Json(string required, string? kind) =>
        "{ \"version\": 1, \"seq\": 1, \"time\": \"\", \"round\": 10, \"start\": 41, \"goal\": 48, "
        + $"\"required\": {required}, {(kind == null ? "" : $"\"requiredKind\": {kind}, ")}"
        + "\"buttons\": [ { \"id\": \"operator/\", \"uses\": 3 } ] }";

    [Theory]
    [InlineData("\"operator_reverse\"", "\"boss\"", "flip", "boss")]
    [InlineData("\"operator/\"", "\"market\"", "÷", "market")]
    [InlineData("\"operator/\"", null, "÷", null)]
    [InlineData("null", "\"boss\"", null, null)]
    public void Parse_reads_the_required_kind(string required, string? kind, string? token, string? expectedKind)
    {
        var p = GameLink.Parse(Json(required, kind))!;
        Assert.Equal(token, p.Required);
        Assert.Equal(expectedKind, p.RequiredKind);
    }

    [Fact]
    public void Market_runs_everything()
    {
        var plan = RequiredPlan.For("flip", boss: false);
        Assert.Null(plan.Accept);
        Assert.Null(plan.OnlyOps);
        Assert.True(plan.Arithmetic && plan.Division && plan.Modulo);
    }

    [Fact]
    public void Boss_unsupported_runs_everything()
    {
        var plan = RequiredPlan.For(null, boss: true);
        Assert.Null(plan.Accept);
        Assert.True(plan.Arithmetic && plan.Division && plan.Modulo);
    }

    [Theory]
    [InlineData("÷", false, true, false, null)]
    [InlineData("mod", false, false, true, null)]
    [InlineData("+", true, false, false, Key.Add)]
    [InlineData("%", true, false, false, Key.Percent)]
    [InlineData("a->c", true, false, false, Key.RemorphA)]
    [InlineData("b->c", true, false, false, Key.RemorphB)]
    public void Boss_operator_runs_one_phase(string token, bool arithmetic, bool division, bool modulo, Key? only)
    {
        var plan = RequiredPlan.For(token, boss: true);
        Assert.Equal((arithmetic, division, modulo), (plan.Arithmetic, plan.Division, plan.Modulo));
        Assert.Equal(only == null ? null : [only.Value], plan.OnlyOps);
        Assert.NotNull(plan.Accept);
    }

    [Theory]
    [InlineData("+", Key.Add)]
    [InlineData("-", Key.Subtract)]
    [InlineData("÷", Key.Divide)]
    [InlineData("mod", Key.Mod)]
    [InlineData("%", Key.Percent)]
    [InlineData("a->c", Key.RemorphA)]
    public void Boss_operator_adds_the_two_operator_phase(string token, Key op)
    {
        var plan = RequiredPlan.For(token, boss: true);
        Assert.True(plan.TwoOperator);
        Assert.Equal(op, plan.RequiredOp);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("+", false)]
    [InlineData("flip", true)]
    public void Two_operator_phase_has_no_required_operator_without_one(string? token, bool boss)
    {
        var plan = RequiredPlan.For(token, boss);
        Assert.True(plan.TwoOperator);
        Assert.Null(plan.RequiredOp);
    }

    [Fact]
    public void Boss_left_button_filters()
    {
        var plan = RequiredPlan.For("flip", boss: true);
        Assert.True(plan.Arithmetic && plan.Division && plan.Modulo);
        Assert.Null(plan.OnlyOps);
        Assert.True(plan.Accept!([new(Key.Divide), new(Key.Flip)]));
        Assert.False(plan.Accept!([new(Key.Divide), new(Key.Ten), new(Key.Equals)]));
    }

    [Fact]
    public void Arithmetic_accept_counts_only_accepted_lines()
    {
        var all = ArithmeticSearch.Find(12, 36, Buttons.All, 8, maxLines: 3);
        Assert.Contains(all, l => !Buttons.Uses(l.Presses, "+"));
        var onlyAdd = ArithmeticSearch.Find(12, 36, Buttons.All, 8, maxLines: 3, accept: p => Buttons.Uses(p, "+"));
        Assert.Equal(3, onlyAdd.Count);
        Assert.All(onlyAdd, l => Assert.True(Buttons.Uses(l.Presses, "+")));
    }

    [Fact]
    public void Arithmetic_onlyOps_skips_other_operators_and_edits_only_lines()
    {
        var lines = ArithmeticSearch.Find(12, 36, Buttons.All, 8, onlyOps: [Key.Multiply]);
        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Equal(Key.Multiply, l.Op));
    }

    [Fact]
    public void Modulo_accept_returns_only_accepted_lines()
    {
        var buttons = B("flip:4, X0:4, X1:4, X4:2, X5:2, mod:3, 10:5, 3:2, 4:2, 5:2");
        var unfiltered = ModuloSearch.Find(31, 11, buttons, 12);
        Assert.NotEmpty(unfiltered.Lines);
        var accepted = ModuloSearch.Find(31, 11, buttons, 12, accept: p => Buttons.Uses(p, "X4"));
        Assert.All(accepted.Lines, l => Assert.True(Buttons.Uses(l.Presses, "X4")));
        Assert.True(accepted.Lines.Count < unfiltered.Lines.Count);
    }

    [Fact]
    public void Boss_flip_end_to_end()
    {
        var buttons = B("÷:3, 10:13, X8:2, 8X:2, flip:2");
        var plan = RequiredPlan.For("flip", boss: true);
        var lines = new List<IReadOnlyList<Press>>();
        lines.AddRange(ArithmeticSearch.Find(41, 48, buttons, 12, accept: plan.Accept, onlyOps: plan.OnlyOps).Select(l => l.Presses));
        DivisionShortcut.Search(41, 48, buttons, 12, l => { if (plan.Accept!(l.Presses)) lines.Add(l.Presses); }, default);
        lines.AddRange(ModuloSearch.Find(41, 48, buttons, 12, accept: plan.Accept).Lines.Select(l => l.Presses));

        Assert.NotEmpty(lines);
        Assert.All(lines, p => Assert.True(Buttons.Uses(p, "flip")));
        Assert.Contains(lines, p => string.Join(' ', p) == "8X ÷ 10 = flip");
    }
}

namespace CalculatorPuzzle.Tests;

public class TwoOperatorSearchTests
{
    // The 2026-10-24 boss rule 11 puzzle: 59 → 40, no dX / Xd, required +.
    const string GamePuzzle = "1:1, 2:2, 3:2, 4:2, 5:2, 6:2, 7:2, 8:3, 9:2, 0:2, +:3, -:2, ×:2, ÷:2, round:1g, 0->8:2";

    static Buttons B(string text)
    {
        var (buttons, error) = Buttons.Parse(text);
        Assert.Null(error);
        return buttons!;
    }

    static string Text(TwoOperatorSearch.Line l) => string.Join(' ', l.Presses);

    static List<string> Texts(IEnumerable<TwoOperatorSearch.Line> lines) => lines.Select(Text).ToList();

    [Fact]
    public void Game_puzzle_59_to_40_needs_plus()
    {
        var lines = TwoOperatorSearch.Find(59, 40, B(GamePuzzle), 12, accept: p => Buttons.Uses(p, "+"), requiredOp: Key.Add);
        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.True(Buttons.Uses(l.Presses, "+")));
        Assert.Equal(5, lines.Min(l => l.Cost));
        Assert.Contains("- 2 0 = + 1 =", Texts(lines));
        Assert.Contains("÷ 2 = + 9 = round", Texts(lines));
    }

    [Fact]
    public void Game_puzzle_end_to_end_in_the_search_order()
    {
        var buttons = B(GamePuzzle);
        var plan = RequiredPlan.For("+", boss: true);
        Assert.Empty(ArithmeticSearch.Find(59, 40, buttons, 12, accept: plan.Accept, onlyOps: plan.OnlyOps));
        Assert.True(plan.TwoOperator);
        var lines = TwoOperatorSearch.Find(59, 40, buttons, 12, accept: plan.Accept, requiredOp: plan.RequiredOp);
        Assert.Contains("- 2 0 = + 1 =", Texts(lines));
    }

    [Fact]
    public void Lines_replay_to_the_goal()
    {
        var buttons = B(GamePuzzle);
        foreach (var l in TwoOperatorSearch.Find(59, 40, buttons, 6))
        {
            Assert.Equal("40", EngineeringCalculator.Run(59, l.Presses, buttons)![^1]);
            Assert.True(buttons.Fits(l.Presses));
        }
    }

    [Fact]
    public void Plus_and_minus_pairs_for_41_to_48()
    {
        var buttons = B("+:2, -:2, 9:2, 2:2");
        Assert.Empty(ArithmeticSearch.Find(41, 48, buttons, 12, accept: p => Buttons.Uses(p, "-"), onlyOps: [Key.Subtract]));
        var lines = TwoOperatorSearch.Find(41, 48, buttons, 12);
        Assert.Equal(4, lines.Min(l => l.Cost));
        var cheapest = Texts(lines.Where(l => l.Cost == 4));
        Assert.Contains("+ 9 = - 2 =", cheapest);
        Assert.Contains("- 2 = + 9 =", cheapest);

        var withSubtract = TwoOperatorSearch.Find(41, 48, buttons, 12, requiredOp: Key.Subtract);
        Assert.Contains("+ 9 = - 2 =", Texts(withSubtract));
        Assert.Contains("- 2 = + 9 =", Texts(withSubtract));
        Assert.Empty(TwoOperatorSearch.Find(41, 48, buttons, 12, requiredOp: Key.Multiply));
    }

    [Fact]
    public void Charges_reject_a_second_press_of_the_same_button()
    {
        var buttons = B("+:1, -:2, 9:2, 2:2");
        var lines = TwoOperatorSearch.Find(41, 48, buttons, 12);
        Assert.All(lines, l => Assert.True(buttons.Fits(l.Presses)));
        Assert.Contains("- 2 = + 9 =", Texts(lines));
        Assert.Contains("+ 9 = - 2 =", Texts(lines));
        Assert.DoesNotContain(lines, l => l.Presses.Count(p => p.Key == Key.Add) > 1);
    }

    // Each case: the second operator is the one under test, and the first is a plain ×.
    [Theory]
    [InlineData(7, 3, "× 9 = ÷ 2 0 =", "×:2, ÷:2, 9:2, 2:2, 0:2")]
    [InlineData(7, 3, "× 9 = mod 6 =", "×:2, mod:2, 9:2, 6:2")]
    [InlineData(7, 1, "× 9 = % 2 =", "×:2, %:2, 9:2, 2:2")]
    public void Second_operator_forms(long start, long goal, string line, string buttonText)
    {
        var buttons = B(buttonText);
        var simulated = EngineeringCalculator.Run(start, ParseLine(line, buttons), buttons)![^1];
        // 63 ÷ 20 = 3, 63 mod 6 = 3, 63 % 2 = 63 * 2 / 100 = 1
        Assert.Equal(goal.ToString(), simulated);
        var lines = TwoOperatorSearch.Find(start, goal, buttons, 6);
        Assert.Contains(line, Texts(lines));
        Assert.All(lines, l => Assert.Equal(goal.ToString(), EngineeringCalculator.Run(start, l.Presses, buttons)![^1]));
    }

    [Fact]
    public void Remorph_as_second_operator()
    {
        // 59 → 63 → 3 would need more; here × 2 = gives 118, then a->c 5 gives (118 mod 10) + 50 = 58.
        var buttons = B("×:2, a->c:2, 2:2, 5:2");
        var lines = TwoOperatorSearch.Find(59, 58, buttons, 6);
        Assert.Contains("× 2 = a->c 5", Texts(lines));
        var b = TwoOperatorSearch.Find(59, 114, B("×:2, b->c:2, 2:2, 4:2, 5:2"), 6);
        Assert.Contains("× 2 = b->c 4", Texts(b));
    }

    [Fact]
    public void Remorph_as_first_operator()
    {
        var buttons = B("a->c:2, +:2, 5:2, 1:2");
        // 59 a->c 5 = 59 (no change: skipped); 59 a->c 1 → 19, then + 5 … no; use 12 → 52 → 53
        var lines = TwoOperatorSearch.Find(12, 53, buttons, 6);
        Assert.Contains("a->c 5 + 1 =", Texts(lines));
    }

    [Fact]
    public void No_op_operands_are_never_produced()
    {
        var buttons = B(GamePuzzle);
        foreach (var l in TwoOperatorSearch.Find(59, 40, buttons, 7))
        {
            if (l.Op1 is Key.Add or Key.Subtract)
                Assert.NotEqual(0, l.Operand1);
            if (l.Op2 is Key.Add or Key.Subtract)
                Assert.NotEqual(0, l.Operand2);
            if (l.Op1 is Key.Multiply or Key.Divide)
                Assert.NotEqual(1, l.Operand1);
            if (l.Op2 is Key.Multiply or Key.Divide)
                Assert.NotEqual(1, l.Operand2);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Fewer_than_four_presses_returns_nothing(int maxCost) =>
        Assert.Empty(TwoOperatorSearch.Find(41, 48, B("+:2, -:2, 9:2, 2:2"), maxCost));

    [Fact]
    public void MaxLines_counts_only_accepted_lines()
    {
        var buttons = B(GamePuzzle);
        var all = TwoOperatorSearch.Find(59, 40, buttons, 7, maxLines: 3);
        Assert.Equal(3, all.Count);
        var onlyPlus = TwoOperatorSearch.Find(59, 40, buttons, 7, maxLines: 3, accept: p => Buttons.Uses(p, "+"));
        Assert.Equal(3, onlyPlus.Count);
        Assert.All(onlyPlus, l => Assert.True(Buttons.Uses(l.Presses, "+")));
    }

    [Fact]
    public void Lines_are_ordered_cheapest_first()
    {
        var lines = TwoOperatorSearch.Find(59, 40, B(GamePuzzle), 7);
        Assert.Equal(lines.OrderBy(l => l.Cost).Select(l => l.Cost), lines.Select(l => l.Cost));
        Assert.Equal(Enumerable.Range(0, lines.Count), lines.Select(l => l.Order));
    }

    [Fact]
    public void A_cancelled_token_throws()
    {
        var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            TwoOperatorSearch.Find(59, 40, B(GamePuzzle), 12, cancelled.Token));
    }

    /// <summary>Reads "× 9 = ÷ 2 0 =" into presses (digits 0-9, operators, =).</summary>
    static List<Press> ParseLine(string text, Buttons buttons) => text.Split(' ').Select(t => t switch
    {
        "=" => new Press(Key.Equals),
        "+" => new Press(Key.Add),
        "-" => new Press(Key.Subtract),
        "×" => new Press(Key.Multiply),
        "÷" => new Press(Key.Divide),
        "mod" => new Press(Key.Mod),
        "%" => new Press(Key.Percent),
        "a->c" => new Press(Key.RemorphA),
        "b->c" => new Press(Key.RemorphB),
        _ => new Press(Key.Digit, int.Parse(t)),
    }).ToList();
}

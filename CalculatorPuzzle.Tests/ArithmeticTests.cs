namespace CalculatorPuzzle.Tests;

public class ArithmeticSimulatorTests
{
    static IReadOnlyList<Press> P(params Press[] presses) => presses;
    static readonly Press Add = new(Key.Add), Sub = new(Key.Subtract), Mul = new(Key.Multiply), Eq = new(Key.Equals);
    static Press D(int d) => new(Key.Digit, d);
    static Buttons B(string text) => TestButtons.Parse(text).Buttons!;

    [Fact]
    public void Add_55_plus_22() =>
        Assert.Equal("77", EngineeringCalculator.Run(55, P(Add, D(2), D(2), Eq), Buttons.All)![^1]);

    [Fact]
    public void Subtract_91_minus_84() =>
        Assert.Equal("7", EngineeringCalculator.Run(91, P(Sub, D(8), D(4), Eq), Buttons.All)![^1]);

    [Fact]
    public void Subtract_below_zero_gives_zero() =>
        Assert.Equal("0", EngineeringCalculator.Run(5, P(Sub, D(9), Eq), Buttons.All)![^1]);

    [Fact]
    public void Multiply_12_times_4() =>
        Assert.Equal("48", EngineeringCalculator.Run(12, P(Mul, D(4), Eq), Buttons.All)![^1]);

    [Fact]
    public void Operator_on_the_right_side_is_invalid() =>
        Assert.Null(EngineeringCalculator.Run(5, P(Add, Add), Buttons.All));

    [Fact]
    public void Operator_missing_from_buttons_is_invalid() =>
        Assert.Null(EngineeringCalculator.Run(5, P(Add, D(2), Eq), B("2, -, ×")));

    [Fact]
    public void Adding_zero_keeps_the_value() =>
        Assert.Equal("5", EngineeringCalculator.Run(5, P(Add, D(0), Eq), Buttons.All)![^1]);

    [Fact]
    public void Divide_by_zero_is_still_invalid() =>
        Assert.Null(EngineeringCalculator.Run(5, P(new(Key.Divide), D(0), Eq), Buttons.All));

    [Fact]
    public void Overflow_clamps_to_999999_and_a_6_character_display_blocks_Xd()
    {
        var presses = P(new(Key.Add), D(5), D(0), D(0), D(0), Eq);
        Assert.Equal("999999", EngineeringCalculator.Run(999000, presses, Buttons.All)![^1]);
        Assert.Null(EngineeringCalculator.Run(999000, [.. presses, new Press(Key.Append, 1)], Buttons.All));
    }
}

public class ArithmeticParseTests
{
    [Fact]
    public void Operators_parse_with_charges()
    {
        var (b, error) = Buttons.Parse("+:2, -:1, ×:3");
        Assert.Null(error);
        Assert.True(b!.Add && b.Subtract && b.Multiply);
        Assert.Equal(2, b.Charges["+"]);
        Assert.Equal(1, b.Charges["-"]);
        Assert.Equal(3, b.Charges["×"]);
    }

    [Theory]
    [InlineData("*:2")]
    [InlineData("x:2")]
    public void Multiply_aliases(string text)
    {
        var (b, _) = Buttons.Parse(text);
        Assert.True(b!.Multiply);
        Assert.Equal(2, b.Charges["×"]);
    }

    [Fact]
    public void Unicode_minus_means_subtract()
    {
        var (b, _) = Buttons.Parse("−:2");
        Assert.True(b!.Subtract);
        Assert.Equal(2, b.Charges["-"]);
    }

    [Fact]
    public void Zero_charges_remove_the_operator()
    {
        var (b, _) = Buttons.Parse("-:0, +:1");
        Assert.False(b!.Subtract);
        Assert.True(b.Add);
    }

    [Fact]
    public void X4_is_still_an_append()
    {
        var (b, _) = Buttons.Parse("x4:1");
        Assert.Contains(4, b!.AppendDigits);
        Assert.False(b.Multiply);
    }
}

public class ArithmeticSearchTests
{
    static Buttons B(string text) => TestButtons.Parse(text).Buttons!;
    static string Show(IEnumerable<Press> presses) => string.Join(' ', presses);

    static IReadOnlyList<ArithmeticSearch.Line> Find(long s, long g, string buttons, int cap = 12, int max = 100)
    {
        var b = B(buttons);
        var lines = ArithmeticSearch.Find(s, g, b, cap, default, max);
        foreach (var l in lines)
        {
            Assert.Equal(g.ToString(), EngineeringCalculator.Run(s, l.Presses, b)![^1]);
            Assert.True(b.Fits(l.Presses));
            Assert.True(l.Cost <= cap);
        }
        Assert.Equal(lines.Count, lines.Select(l => Show(l.Presses)).Distinct().Count());
        return lines;
    }

    [Fact]
    public void Goal_zero_70_to_0_by_subtracting_or_multiplying()
    {
        var lines = Find(70, 0, "0:2,1:2,2:2,3:2,4:2,5:2,6:2,7:2,8:2,9:2,+:2,-:2,×:2,/:2");
        var all = lines.Select(l => Show(l.Presses)).ToList();
        Assert.Contains("× 0 =", all);
        Assert.Contains("- 7 0 =", all);
        Assert.Equal(2, lines[0].Cost);
    }

    [Fact]
    public void Plain_calculator_55_to_77()
    {
        var lines = Find(55, 77, "0:2,1:2,2:2,3:2,4:2,5:2,6:2,7:2,8:2,9:2,+:2,-:2,×:2,/:2");
        Assert.Equal("+ 2 2 =", Show(lines[0].Presses));
        Assert.Equal(3, lines[0].Cost);
        Assert.All(lines, l => Assert.True(l.Presses[0].Key is Key.Add or Key.Subtract or Key.Multiply));
    }

    [Fact]
    public void Transform_before_and_after_the_operator()
    {
        var lines = Find(12, 97, "×:1, 8:1, X++:1");
        Assert.Equal("× 8 = X++", Show(lines[0].Presses));
        Assert.Equal(3, lines[0].Cost);
    }

    [Fact]
    public void Edits_only_transform_line_comes_first()
    {
        var lines = Find(41, 42, "X++:1, +:1, 1:1");
        Assert.Equal("X++", Show(lines[0].Presses));
        Assert.Equal(1, lines[0].Cost);
    }

    [Fact]
    public void Two_transforms_edits_only()
    {
        var lines = Find(41, 84, "X++:2, X*2:2");
        Assert.Equal("X++ X*2", Show(lines[0].Presses));
    }

    [Fact]
    public void Add_41_to_48() => Assert.Equal("+ 7 =", Show(Find(41, 48, "+:1, 7:1")[0].Presses));

    [Fact]
    public void Subtract_91_to_7() => Assert.Equal("- 8 4 =", Show(Find(91, 7, "-:1, 8:1, 4:1")[0].Presses));

    [Fact]
    public void Multiply_12_to_48() => Assert.Equal("× 4 =", Show(Find(12, 48, "×:1, 4:1")[0].Presses));

    [Fact]
    public void Multiply_then_edit_6_to_242() =>
        Assert.Equal("× 4 = X2", Show(Find(6, 242, "×:1, 4:1, X2:1")[0].Presses));

    [Fact]
    public void Edit_then_add_3_to_50() =>
        Assert.Equal("1X + 3 7 =", Show(Find(3, 50, "1X:1, +:1, 3:1, 7:1")[0].Presses));

    [Fact]
    public void Edits_only_36_to_63()
    {
        var line = Find(36, 63, "flip:1")[0];
        Assert.Equal("flip", Show(line.Presses));
        Assert.Null(line.Op);
    }

    [Fact]
    public void Charges_limit_lines() =>
        Assert.DoesNotContain(Find(55, 77, "+:1, 2:1"), l => Show(l.Presses) == "+ 2 2 =");

    [Fact]
    public void No_operators_and_no_edit_path_is_empty() => Assert.Empty(Find(41, 48, "10:1, /:1"));

    [Fact]
    public void Same_number_is_empty() => Assert.Empty(Find(5, 5, "+:2, 2:2"));

    [Fact]
    public void Max_lines_is_respected() =>
        Assert.True(Find(55, 77, "0:2,1:2,2:2,3:2,4:2,5:2,6:2,7:2,8:2,9:2,+:2,-:2,×:2", max: 2).Count <= 2);

    [Fact]
    public void Cancelled_token_throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => ArithmeticSearch.Find(55, 77, B("+:2, 2:2"), 12, cts.Token));
    }
}

public class ArithmeticResultLineTests
{
    [Fact]
    public void Arithmetic_ranks_before_division_before_modulo_at_equal_cost()
    {
        var presses = new[] { new Press(Key.Digit, 1) };
        var arith = new ResultLine(presses, 3, 0, 5, null, null, new ArithmeticSearch.Line(Key.Add, "1", 1, 1, presses, 3, 0));
        var div = new ResultLine(presses, 3, 0, 5, new DivisionShortcut.Line("1", 1, 10, 1, presses, 3), null);
        var mod = new ResultLine(presses, 3, 0, 5, null, new ModuloSearch.Line(1, 1, 1, 1, presses, 3, 0));
        var sorted = new[] { mod, div, arith }.Order(ResultLine.Ranking).ToList();
        Assert.Same(arith, sorted[0]);
        Assert.Same(div, sorted[1]);
        Assert.Same(mod, sorted[2]);
    }
}

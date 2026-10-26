namespace CalculatorPuzzle.Tests;

public class ModSimulatorTests
{
    static IReadOnlyList<Press> P(params Press[] presses) => presses;
    static readonly Press Mod = new(Key.Mod), Eq = new(Key.Equals), Ten = new(Key.Ten);

    [Fact]
    public void Mod_runs_41_to_48()
    {
        var log = EngineeringCalculator.Run(41,
            P(new(Key.Prepend, 2), new(Key.Prepend, 3), Mod, Ten, new(Key.Digit, 3), Eq), Buttons.All);
        Assert.Equal("48", log![^1]);
    }

    [Fact]
    public void Mod_by_zero_is_invalid() =>
        Assert.Null(EngineeringCalculator.Run(41, P(Mod, new(Key.Digit, 0), Eq), Buttons.All));

    [Fact]
    public void Mod_on_the_right_side_is_invalid() =>
        Assert.Null(EngineeringCalculator.Run(41, P(Mod, Mod), Buttons.All));

    [Fact]
    public void Mod_missing_from_buttons_is_invalid()
    {
        var (b, _) = TestButtons.Parse("10, 3, 2X");
        Assert.Null(EngineeringCalculator.Run(41, P(Mod, Ten, Eq), b));
    }
}

public class ModuloSearchTests
{
    static Buttons B(string text) => TestButtons.Parse(text).Buttons!;
    static string Show(IEnumerable<Press> presses) => string.Join(' ', presses);

    [Fact]
    public void Presses_31_to_11_with_pair_4_x_5()
    {
        var f = ModuloShortcut.Solve(31, 11, 1, 2).Pairs.First(p => p.Small == 4).Second;
        var presses = ModuloSearch.Presses(31, 2, f, B("4X, mod, 10, 5"));
        Assert.Equal("4X mod 10 5 =", Show(presses!));
    }

    [Fact]
    public void Presses_5_to_48_cannot_pad_with_zeros()
    {
        var f = ModuloShortcut.Solve(5, 48, 1, 2).Pairs.Select(p => p.First).Concat(
                ModuloShortcut.Solve(5, 48, 1, 2).Pairs.Select(p => p.Second))
                .First(x => x.Valid && x.Value == 2005 && x.Modulus == 103);
        var presses = ModuloSearch.Presses(5, 2, f, B("0X, 2X, mod, 10, 3"));
        Assert.Null(presses);
    }

    [Fact]
    public void Find_31_to_11_pads_with_flip_X0_flip_and_never_with_0X()
    {
        var result = ModuloSearch.Find(31, 11, B("flip:4, X0:4, X1:4, X4:2, X5:2, mod:3, 10:5, 3:2, 4:2, 5:2"), 12);
        Assert.Null(result.Error);
        Assert.Contains(result.Lines, l => Show(l.Presses).StartsWith("flip X0") && Show(l.Presses).Contains("flip mod"));
        Assert.DoesNotContain(result.Lines, l => Show(l.Presses).Contains("0X"));
    }

    [Fact]
    public void Presses_are_null_without_mod_or_a_digit_key_for_the_modulus()
    {
        var f = ModuloShortcut.Solve(31, 11, 1, 2).Pairs.First(p => p.Small == 4).Second;
        Assert.Null(ModuloSearch.Presses(31, 2, f, B("4X, 10, 5")));
        var big = ModuloShortcut.Solve(41, 48, 1, 2).Pairs.SelectMany(p => new[] { p.First, p.Second })
                  .First(x => x.Valid && x.Modulus == 131);
        Assert.Null(ModuloSearch.Presses(41, 2, big, B("4X, mod, 10, 1")));
    }

    const string Buttons31 = "4X:2, 5X:2, mod:3, 10:5, 4:1, 5:1";

    [Fact]
    public void Find_31_to_11_contains_4X_mod_10_5()
    {
        var buttons = B(Buttons31);
        var result = ModuloSearch.Find(31, 11, buttons, 4);
        Assert.Null(result.Error);
        Assert.Contains(result.Lines, l => Show(l.Presses) == "4X mod 10 5 =" && l.Cost == 4);
        foreach (var l in result.Lines)
        {
            Assert.True(l.Cost <= 4);
            Assert.True(buttons.Fits(l.Presses));
            Assert.Equal("11", EngineeringCalculator.Run(31, l.Presses, buttons)![^1]);
        }
        Assert.Equal(result.Lines.Count, result.Lines.Select(l => Show(l.Presses)).Distinct().Count());
    }

    [Fact]
    public void Find_stops_at_maxLines()
    {
        var result = ModuloSearch.Find(31, 11, Buttons.All, 8, maxLines: 3);
        Assert.Equal(3, result.Lines.Count);
    }

    [Fact]
    public void Find_never_goes_past_maxA()
    {
        var result = ModuloSearch.Find(31, 11, Buttons.All, 8, maxA: 2);
        Assert.All(result.Lines, l => Assert.True(l.A <= 2));
        Assert.True(result.LastA <= 2);
    }

    [Fact]
    public void Find_with_a_cap_below_every_line_is_empty()
    {
        var result = ModuloSearch.Find(31, 11, B(Buttons31), 1);
        Assert.Null(result.Error);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Find_without_mod_is_an_error() =>
        Assert.NotNull(ModuloSearch.Find(31, 11, B("4X, 10, 5"), 8).Error);

    [Fact]
    public void Find_with_S_equal_G_is_empty()
    {
        var result = ModuloSearch.Find(11, 11, B(Buttons31), 8);
        Assert.Null(result.Error);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Find_throws_when_cancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => ModuloSearch.Find(31, 11, B(Buttons31), 8, cts.Token));
    }
}

public class ResultLineTests
{
    static DivisionShortcut.Line Div(long divisor = 10) =>
        new("1", 1, divisor, 1, [new Press(Key.Divide)], 1);

    static ResultLine Make(int cost, int drained, int spare, bool division, int order = 0) =>
        new([], cost, drained, spare, division ? Div() : null,
            division ? null : new ModuloSearch.Line(1, 1, 1, 1, [], cost, order));

    static List<ResultLine> Sorted(params ResultLine[] lines)
    {
        var list = lines.ToList();
        list.Sort(ResultLine.Ranking);
        return list;
    }

    [Fact]
    public void Cost_comes_before_drained_and_spare()
    {
        var cheap = Make(4, 3, 1, false);
        var dear = Make(5, 0, 99, true);
        Assert.Same(cheap, Sorted(dear, cheap)[0]);
    }

    [Fact]
    public void Fewer_drained_first_even_with_a_lower_spare()
    {
        var safe = Make(4, 0, 2, false);
        var drains = Make(4, 1, 50, true);
        Assert.Same(safe, Sorted(drains, safe)[0]);
    }

    [Fact]
    public void Higher_spare_first_at_the_same_cost_and_drained()
    {
        var high = Make(4, 0, 20, false);
        var low = Make(4, 0, 10, true);
        Assert.Same(high, Sorted(low, high)[0]);
    }

    [Fact]
    public void Division_before_modulo_when_all_else_is_equal()
    {
        var division = Make(4, 0, 10, true);
        var modulo = Make(4, 0, 10, false);
        Assert.Same(division, Sorted(modulo, division)[0]);
    }

    [Fact]
    public void Modulo_ties_keep_their_found_order()
    {
        var first = Make(4, 0, 10, false, order: 0);
        var second = Make(4, 0, 10, false, order: 1);
        Assert.Same(first, Sorted(second, first)[0]);
    }
}

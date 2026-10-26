namespace CalculatorPuzzle.Tests;

public class DivisionShortcutTests
{
    const string DefaultText = "5X, X1, X2, X7, X4, 4X, 6X, 3X, 8X, 10, /, X9, flip, mod, =, reset";
    const string NoFlipText = "5X, X1, X2, X7, X4, 4X, 6X, 3X, 8X, 10, /, X9, mod, =, reset";
    const int MaxPresses = 12;

    static string AllButFlip(params string[] removed)
    {
        var tokens = new List<string> { "10", "÷", "reset" };
        for (int d = 0; d <= 9; d++)
        {
            tokens.Add($"{d}X");
            tokens.Add($"X{d}");
        }
        tokens.RemoveAll(removed.Contains);
        return string.Join(", ", tokens);
    }

    static DivisionShortcut.Result Solve(long start, long goal, string text, int max = MaxPresses)
    {
        var (buttons, error) = TestButtons.Parse(text);
        Assert.Null(error);
        return DivisionShortcut.Solve(start, goal, buttons!, max);
    }

    static string Show(DivisionShortcut.Line line) => string.Join(' ', line.Presses);

    static void AssertHasLine(DivisionShortcut.Result result, string presses, int cost)
    {
        var line = result.Lines.FirstOrDefault(l => Show(l) == presses);
        Assert.True(line != null, $"Missing line \"{presses}\". Got: {string.Join(" | ", result.Lines.Take(15).Select(Show))}");
        Assert.Equal(cost, line.Cost);
    }

    // ---- Example tables --------------------------------------------------------------------

    [Fact]
    public void Cheapest_41_to_48_without_flip_is_divide_by_10_then_X8()
    {
        var result = Solve(41, 48, AllButFlip());

        Assert.Null(result.Error);
        Assert.Equal("÷ 10 = X8", Show(result.Lines[0]));
        Assert.Equal(3, result.Lines[0].Cost);
    }

    [Fact]
    public void Example_41_to_48_with_D_1010_and_a_suffix()
    {
        AssertHasLine(Solve(41, 48, AllButFlip()), "X1 8X ÷ 10 10 = 4X", 6);
    }

    [Fact]
    public void Example_41_to_48_with_D_1010_and_a_three_digit_prefix()
    {
        AssertHasLine(Solve(41, 48, AllButFlip()), "5X 8X 4X ÷ 10 10 =", 6);
    }

    [Fact]
    public void Example_41_to_48_without_8X_or_X8_prepends_three_digits()
    {
        var result = Solve(41, 48, AllButFlip("8X", "X8"));

        AssertHasLine(result, "1X 9X 4X ÷ 10 10 =", 6);
        Assert.DoesNotContain(result.Lines, l => l.ToString().StartsWith("0X"));
        Assert.Equal(6, result.Lines[0].Cost);
    }

    [Fact]
    public void Example_531_to_11()
    {
        AssertHasLine(Solve(531, 11, AllButFlip()), "1X 1X ÷ 10 10 =", 5);
    }

    [Fact]
    public void Example_7_to_250()
    {
        AssertHasLine(Solve(7, 250, AllButFlip()), "5X 2X ÷ 10 = X0", 5);
    }

    [Fact]
    public void Limited_buttons_41_to_48_with_4X_8X_X4()
    {
        var result = Solve(41, 48, "4X, 8X, X4, 10, ÷");

        Assert.Equal(6, result.Lines[0].Cost);
        AssertHasLine(result, "4X 8X ÷ 10 10 = 4X", 6);
        AssertHasLine(result, "X4 8X ÷ 10 10 = 4X", 6);
        AssertHasLine(result, "8X 8X 4X ÷ 10 10 =", 6);
    }

    [Fact]
    public void Limited_buttons_without_an_8_give_no_line()
    {
        var result = Solve(41, 48, "4X, X4, 10, ÷");

        Assert.Empty(result.Lines);
        Assert.Equal("No division line fits the available buttons.", result.Error);
    }

    [Fact]
    public void Limited_buttons_531_to_11_with_only_1X()
    {
        var result = Solve(531, 11, "1X, 10, ÷");

        Assert.Equal(5, result.Lines[0].Cost);
        AssertHasLine(result, "1X ÷ 10 10 = 1X", 5);
        AssertHasLine(result, "1X 1X ÷ 10 10 =", 5);
    }

    // ---- flip ------------------------------------------------------------------------------

    [Fact]
    public void Default_buttons_41_to_48_cost_4_and_never_use_X8()
    {
        var result = Solve(41, 48, DefaultText);

        Assert.All(result.Lines.TakeWhile(l => l.Cost == 4), l => Assert.Equal(4, l.Cost));
        Assert.Equal(4, result.Lines[0].Cost);
        AssertHasLine(result, "8X ÷ 10 = flip", 4);
        AssertHasLine(result, "÷ 10 = 8X flip", 4);
        Assert.DoesNotContain(result.Lines, l => l.Presses.Any(p => p.Key == Key.Append && p.Digit == 8));
        var firstTwo = result.Lines.Take(2).Select(Show).Order().ToList();
        Assert.Equal(new[] { "8X ÷ 10 = flip", "÷ 10 = 8X flip" }.Order(), firstTwo);
    }

    [Fact]
    public void Default_buttons_without_flip_cost_6()
    {
        var result = Solve(41, 48, NoFlipText);

        Assert.All(result.Lines.TakeWhile(l => l.Cost == 6), l => Assert.Equal(6, l.Cost));
        Assert.Equal(6, result.Lines[0].Cost);
        AssertHasLine(result, "X1 8X ÷ 10 10 = 4X", 6);
    }

    [Fact]
    public void Flip_makes_7_to_250_possible_without_prepends()
    {
        var withFlip = Solve(7, 250, "X0, X2, X5, flip, 10, ÷");
        var withoutFlip = Solve(7, 250, "X0, X2, X5, 10, ÷");

        Assert.Equal(6, withFlip.Lines[0].Cost);
        AssertHasLine(withFlip, "X2 flip ÷ 10 = X5 X0", 6);
        Assert.Empty(withoutFlip.Lines);
        Assert.Equal("No division line fits the available buttons.", withoutFlip.Error);
    }

    [Fact]
    public void Flip_does_not_help_531_to_11()
    {
        var result = Solve(531, 11, "1X, flip, 10, ÷");

        Assert.Equal(5, result.Lines[0].Cost);
        AssertHasLine(result, "1X 1X ÷ 10 10 =", 5);
    }

    // ---- Rules -----------------------------------------------------------------------------

    [Fact]
    public void Missing_divide_or_ten_is_an_error()
    {
        Assert.Equal("The Division Shortcut needs ÷ and 10 or a digit key.", Solve(41, 48, "4X, 10").Error);
        Assert.Equal("The Division Shortcut needs ÷ and 10 or a digit key.", Solve(41, 48, "4X, ÷").Error);
        Assert.Equal("The Division Shortcut needs ÷ and 10 or a digit key.", Solve(41, 48, "").Error);
    }

    [Fact]
    public void S_equals_G_gives_no_lines_and_no_error()
    {
        var result = Solve(41, 41, DefaultText);

        Assert.Null(result.Error);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Every_press_is_on_its_own_side_and_available()
    {
        var cases = new[]
        {
            (41L, 48L, DefaultText), (41L, 48L, NoFlipText), (41L, 48L, "4X, 8X, X4, 10, ÷"),
            (41L, 48L, DefaultText + ", 3"),
            (41L, 105L, "1X, X1, X0, X5, 10, 3, ÷, flip"),
        };
        foreach (var (start, goal, text) in cases)
        {
            var (buttons, _) = TestButtons.Parse(text);
            foreach (var line in Solve(start, goal, text).Lines)
            {
                bool right = false;
                foreach (var p in line.Presses)
                {
                    if (p.Key == Key.Divide) right = true;
                    else if (p.Key == Key.Equals) right = false;
                    else if (right) Assert.True(p.Key is Key.Ten or Key.Digit);
                    else Assert.False(p.Key is Key.Ten or Key.Digit);

                    switch (p.Key)
                    {
                        case Key.Prepend: Assert.Contains(p.Digit, buttons!.PrependDigits); break;
                        case Key.Append: Assert.Contains(p.Digit, buttons!.AppendDigits); break;
                        case Key.Flip: Assert.True(buttons!.Flip); break;
                        case Key.Ten: Assert.True(buttons!.Ten); break;
                        case Key.Digit: Assert.Contains(p.Digit, buttons!.DigitKeys); break;
                        case Key.Reset: Assert.True(buttons!.Reset); break;
                    }
                }
            }
        }
    }

    [Fact]
    public void No_quotient_has_a_leading_zero()
    {
        // G = 105 could split as q = "05" or "0", which are skipped.
        var result = Solve(41, 105, AllButFlip());

        Assert.NotEmpty(result.Lines);
        Assert.All(result.Lines, l => Assert.NotEqual('0', l.Quotient.ToString()[0]));
        Assert.All(result.Lines, l => Assert.True(l.Quotient > 0));
    }

    [Theory]
    [InlineData(41, 48, DefaultText)]
    [InlineData(41, 48, NoFlipText)]
    [InlineData(531, 11, DefaultText)]
    [InlineData(7, 250, DefaultText)]
    [InlineData(120, 31, DefaultText)]
    public void Every_line_runs_to_G(long start, long goal, string text)
    {
        var (buttons, _) = TestButtons.Parse(text);
        var result = Solve(start, goal, text);

        Assert.NotEmpty(result.Lines);
        foreach (var line in result.Lines)
        {
            var log = EngineeringCalculator.Run(start, line.Presses, buttons);
            Assert.NotNull(log);
            Assert.Equal(goal.ToString(), log[^1]);
            Assert.Equal(line.Cost, EngineeringCalculator.Cost(line.Presses));
            Assert.True(line.Cost <= MaxPresses);
        }
    }

    [Fact]
    public void Lines_are_sorted_by_cost()
    {
        var costs = Solve(41, 48, DefaultText).Lines.Select(l => l.Cost).ToList();

        Assert.Equal(costs.Order(), costs);
    }

    [Fact]
    public void Prefix_range_holds_a_value_when_D_is_at_least_10_to_the_m()
    {
        var random = new Random(12345);
        for (int i = 0; i < 20000; i++)
        {
            int r = random.Next(1, 5);
            long divisor = long.Parse(string.Concat(Enumerable.Repeat("10", r)));
            long q = random.NextInt64(1, 1000);
            string tail = random.NextInt64(0, 100000).ToString();
            int m = tail.Length;
            var (low, high) = DivisionShortcut.PrefixRange(q, divisor, tail);
            long pow = (long)Math.Pow(10, m);

            if (divisor >= pow)
                Assert.True(low <= high, $"q={q} D={divisor} tail={tail}");
            for (long a = low; a <= Math.Min(high, low + 20); a++)
                Assert.Equal(q, (a * pow + long.Parse(tail)) / divisor);
            if (low > 0)
                Assert.NotEqual(q, ((low - 1) * pow + long.Parse(tail)) / divisor);
            if (low <= high)
                Assert.NotEqual(q, ((high + 1) * pow + long.Parse(tail)) / divisor);
        }
    }

    // ---- Digit keys ------------------------------------------------------------------------

    static Buttons B(string text) => TestButtons.Parse(text).Buttons!;

    static string Top(DivisionShortcut.Result result) => Show(result.Lines[0]);

    [Fact]
    public void Digit_example_531_to_11_without_digit_keys()
    {
        var result = Solve(531, 11, "1X, 10, ÷");

        Assert.Equal("1X 1X ÷ 10 10 =", Top(result));
        Assert.Equal(5, result.Lines[0].Cost);
    }

    [Fact]
    public void Digit_example_531_to_11_with_4_and_5()
    {
        var result = Solve(531, 11, "1X, 10, 4, 5, ÷");

        Assert.Equal("÷ 4 5 =", Top(result));
        Assert.Equal(3, result.Lines[0].Cost);
    }

    [Fact]
    public void Digit_example_41_to_48_with_digit_8_has_no_line()
    {
        var result = Solve(41, 48, "4X, X4, 10, 8, ÷");

        Assert.Equal("No division line fits the available buttons.", result.Error);
    }

    [Fact]
    public void Digit_key_and_X8_41_to_48()
    {
        var result = Solve(41, 48, "X8, 8, 10, ÷");

        Assert.Equal("÷ 10 = X8", Top(result));
        Assert.Equal(3, result.Lines[0].Cost);
    }

    [Fact]
    public void Digit_example_41_to_48_with_no_8_has_no_line()
    {
        var result = Solve(41, 48, "4X, X4, 10, ÷");

        Assert.Equal("No division line fits the available buttons.", result.Error);
    }

    [Fact]
    public void Digit_example_7_to_250()
    {
        var result = Solve(7, 250, "X0, X2, X5, 10, 3, ÷");

        Assert.Equal("X5 X0 ÷ 3 =", Top(result));
        Assert.Equal(4, result.Lines[0].Cost);
    }

    [Fact]
    public void Digit_example_1234_to_7()
    {
        var result = Solve(1234, 7, "1, 7, 10, ÷");

        Assert.Equal("÷ 1 7 1 =", Top(result));
        Assert.Equal(4, result.Lines[0].Cost);
    }

    [Fact]
    public void Digit_example_41_to_48_default_plus_3_ties_at_4()
    {
        var result = Solve(41, 48, DefaultText + ", 3");

        Assert.Equal(4, result.Lines[0].Cost);
        AssertHasLine(result, "5X flip ÷ 3 =", 4);
        AssertHasLine(result, "8X ÷ 10 = flip", 4);
    }

    [Fact]
    public void Digit_keys_alone_are_enough_for_validation()
    {
        Assert.Null(Solve(531, 11, "4, 5, ÷").Error);
    }

    [Fact]
    public void Every_digit_line_ends_on_the_goal()
    {
        var buttons = B("1X, 10, 4, 5, ÷");
        var result = DivisionShortcut.Solve(531, 11, buttons, MaxPresses);

        Assert.NotEmpty(result.Lines);
        foreach (var line in result.Lines)
        {
            var log = EngineeringCalculator.Run(531, line.Presses, buttons);
            Assert.NotNull(log);
            Assert.Equal("11", log[^1]);
        }
    }

    [Fact]
    public void TypeOperand_picks_the_fewest_presses()
    {
        Assert.Equal(2, DivisionShortcut.TypeOperand("1010", B("10"))!.Count);
        Assert.Equal("10 0", string.Join(' ', DivisionShortcut.TypeOperand("100", B("10, 0"))!));
        Assert.Null(DivisionShortcut.TypeOperand("45", B("4")));
        Assert.Null(DivisionShortcut.TypeOperand("05", B("0, 5")));
    }

    [Fact]
    public void CheapestDivisor_finds_171_with_1_7_and_10()
    {
        var found = DivisionShortcut.CheapestDivisor(155, 176, B("1, 7, 10"), 6);

        Assert.Equal(171, found!.Value.Divisor);
        Assert.Equal(3, found.Value.Presses.Count);
    }

    [Fact]
    public void CheapestDivisor_returns_null_when_nothing_is_typeable()
    {
        Assert.Null(DivisionShortcut.CheapestDivisor(20, 29, B("3, 10"), 6));
    }

    [Fact]
    public void CheapestDivisor_never_returns_1()
    {
        var found = DivisionShortcut.CheapestDivisor(1, 5, B("1, 3"), 6);

        Assert.Equal(3, found!.Value.Divisor);
    }

    [Fact]
    public void CheapestDivisor_matches_brute_force()
    {
        var buttons = B("1, 3, 7, 10");
        var random = new Random(5);
        for (int i = 0; i < 200; i++)
        {
            long x = random.Next(10, 100000), q = random.Next(1, 50);
            long low = x / (q + 1) + 1, high = x / q;
            var found = DivisionShortcut.CheapestDivisor(low, high, buttons, 8);
            if (found is not { } f)
                continue;
            Assert.InRange(f.Divisor, Math.Max(low, 2), high);
            Assert.Equal(q, x / f.Divisor);
            var typed = DivisionShortcut.TypeOperand(f.Divisor.ToString(), buttons)!;
            Assert.Equal(typed.Count, f.Presses.Count);
        }
    }

    // ---- Search ----------------------------------------------------------------------------

    static (string? Error, List<DivisionShortcut.Line> Lines) Search(
        long start, long goal, string text, CancellationToken cancel = default, Action<DivisionShortcut.Line>? also = null)
    {
        var (buttons, error) = TestButtons.Parse(text);
        Assert.Null(error);
        var lines = new List<DivisionShortcut.Line>();
        var err = DivisionShortcut.Search(start, goal, buttons!, MaxPresses, l => { lines.Add(l); also?.Invoke(l); }, cancel);
        return (err, lines);
    }

    [Theory]
    [InlineData(41, 48, DefaultText)]
    [InlineData(41, 48, NoFlipText)]
    [InlineData(531, 11, DefaultText)]
    public void Search_reports_the_lines_Solve_returns_with_no_duplicates(long s, long g, string text)
    {
        var solved = Solve(s, g, text);
        var (error, lines) = Search(s, g, text);
        Assert.Null(error);
        Assert.Equal(solved.Lines.Select(Show).Order(StringComparer.Ordinal), lines.Select(Show).Order(StringComparer.Ordinal));
        Assert.Equal(lines.Count, lines.Select(Show).Distinct().Count());
        Assert.Equal(solved.Lines.Select(Show), lines.OrderBy(l => l, DivisionShortcut.Ranking).Select(Show));
    }

    [Fact]
    public void Search_cancelled_inside_onLine_reports_exactly_one_line()
    {
        using var cts = new CancellationTokenSource();
        var (error, lines) = Search(41, 48, DefaultText, cts.Token, _ => cts.Cancel());
        Assert.Null(error);
        Assert.Single(lines);
    }

    [Fact]
    public void Search_with_a_cancelled_token_reports_nothing()
    {
        var (error, lines) = Search(41, 48, DefaultText, new CancellationToken(true));
        Assert.Null(error);
        Assert.Empty(lines);
    }

    [Fact]
    public void Goal_zero_is_accepted()
    {
        var (error, _) = Search(41, 0, DefaultText);
        Assert.Null(error);
    }

    [Fact]
    public void Search_returns_the_same_input_errors_as_Solve()
    {
        foreach (var (s, g, text) in new[] { (41L, -1L, DefaultText), (41L, 48L, "10, X1, =") })
        {
            var (error, lines) = Search(s, g, text);
            Assert.NotNull(error);
            Assert.Equal(Solve(s, g, text).Error, error);
            Assert.Empty(lines);
        }
    }

    // ---- Parallel search -------------------------------------------------------------------

    [Theory]
    [InlineData(41, 48, DefaultText)]
    [InlineData(41, 48, NoFlipText)]
    [InlineData(531, 11, DefaultText)]
    public void Solve_gives_the_same_ranked_lines_with_1_and_8_workers(long s, long g, string text)
    {
        var (buttons, error) = TestButtons.Parse(text);
        Assert.Null(error);
        var one = DivisionShortcut.Solve(s, g, buttons!, MaxPresses, workers: 1);
        var eight = DivisionShortcut.Solve(s, g, buttons!, MaxPresses, workers: 8);
        Assert.Equal(one.Error, eight.Error);
        Assert.Equal(one.Lines.Select(Show), eight.Lines.Select(Show));
    }

    [Fact]
    public void Search_with_8_workers_never_reports_the_same_line_twice()
    {
        var (buttons, _) = TestButtons.Parse(DefaultText);
        var lines = new List<string>();
        DivisionShortcut.Search(41, 48, buttons!, MaxPresses, l => lines.Add(Show(l)), workers: 8);
        Assert.NotEmpty(lines);
        Assert.Equal(lines.Count, lines.Distinct().Count());
    }

    [Fact]
    public void Search_with_8_workers_cancelled_inside_onLine_reports_exactly_one_line()
    {
        var (buttons, _) = TestButtons.Parse(DefaultText);
        using var cts = new CancellationTokenSource();
        int count = 0;
        var error = DivisionShortcut.Search(41, 48, buttons!, MaxPresses, _ => { count++; cts.Cancel(); }, cts.Token, workers: 8);
        Assert.Null(error);
        Assert.Equal(1, count);
    }
}

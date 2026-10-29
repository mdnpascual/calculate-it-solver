namespace CalculatorPuzzle.Tests;

public class CoinSubTests
{
    static readonly Press Coin = new(Key.CoinSub);

    static Buttons B(string text, long goal, long money, bool step = false, bool rule10 = false, bool blind = false, bool drain = false)
    {
        var buttons = Buttons.Parse(text, goal: goal, money: money).Buttons!;
        return buttons.WithContext(new LineContext(goal, step, money, new MoneyRules(drain, rule10, []), blind));
    }

    static string Last(long start, IReadOnlyList<Press> presses, Buttons b) => EngineeringCalculator.Run(start, presses, b)![^1];

    // 1: parsing and mapping
    [Fact]
    public void Parse_reads_token_label_and_glass()
    {
        Assert.Equal(5, Buttons.Parse("coinsub:5").Buttons!.Charges["coinsub"]);
        Assert.Equal(5, Buttons.Parse("$++:5").Buttons!.Charges["coinsub"]);
        var glass = Buttons.Parse("coinsub:1g").Buttons!;
        Assert.Contains("coinsub", glass.Glass);
        Assert.True(glass.Has(Key.CoinSub));
        Assert.False(Buttons.Parse("coinsub:0").Buttons!.Has(Key.CoinSub));
    }

    [Fact]
    public void Dollar_labels_do_not_clash()
    {
        var money = Buttons.Parse("$:2", money: 5).Buttons!;
        Assert.Equal(2, money.Charges["money"]);
        Assert.DoesNotContain("coinsub", money.Charges.Keys);
        Assert.Equal("coinsub", ButtonLabels.Token("$++"));
        Assert.Equal("money", ButtonLabels.Token("$"));
        Assert.Equal("$++", ButtonLabels.Label("coinsub"));
        Assert.Equal("coinsub", Buttons.Token("$++"));
    }

    [Fact]
    public void GameLink_maps_operator_coinsub() => Assert.Equal("coinsub", GameLink.MapButton("operator_coinsub"));

    // 2: the simulator
    [Fact]
    public void Press_adds_one_and_pays_a_coin()
    {
        var b = B("coinsub:5", 50, 2);
        Assert.Equal("42", Last(41, [Coin], b));
        var state = CoinSubSearch.After(41, 50, 1, b, 0)!;
        Assert.Equal(1, state.Rest.Context!.Money);
    }

    [Fact]
    public void Press_without_coins_does_nothing()
    {
        var b = B("coinsub:5", 50, 0);
        Assert.Equal("41", Last(41, [Coin], b));
        Assert.Null(CoinSubSearch.After(41, 50, 1, b, 0));
    }

    [Fact]
    public void Press_is_left_side_only_and_clamps()
    {
        var b = B("coinsub:5, ÷:1, 10:1", 50, 5);
        Assert.Null(EngineeringCalculator.Run(41, [new Press(Key.Divide), Coin], b));
        Assert.Equal("999999", Last(999_999, [Coin], b));
        Assert.Null(CoinSubSearch.After(999_999, 50, 1, b, 0));
        // PlusNumber reads the number, so leading zeros drop: "04" + 1 = 5.
        var withAppend = B("coinsub:5, X4:1", 50, 5);
        Assert.Equal("5", Last(0, [new Press(Key.Append, 4), Coin], withAppend));
    }

    // 3: boss rule 10
    [Fact]
    public void Rule_10_takes_two_coins()
    {
        var b = B("coinsub:5", 50, 5, rule10: true);
        var state = CoinSubSearch.After(41, 50, 1, b, 10)!;
        Assert.Equal(42, state.Start);
        Assert.Equal(3, state.Rest.Context!.Money);
        // $1: the rule takes it, then nothing happens.
        Assert.Equal("41", Last(41, [Coin], B("coinsub:5", 50, 1, rule10: true)));
    }

    // 4: After
    [Fact]
    public void After_counts_presses_and_coins()
    {
        var b = B("coinsub:5, ÷:1", 48, 5);
        var state = CoinSubSearch.After(41, 48, 3, b, 0)!;
        Assert.Equal(44, state.Start);
        Assert.Equal(2, state.Rest.Context!.Money);
        Assert.Equal(0, state.Rest.Charges["coinsub"]);
        Assert.Equal(3, state.Rest.Context.ChargedBefore);
        Assert.Null(CoinSubSearch.After(41, 48, 3, B("coinsub:5", 48, 2), 0));
        Assert.Null(CoinSubSearch.After(41, 48, 3, B("coinsub:2", 48, 5), 0));
        Assert.Null(CoinSubSearch.After(41, 48, 3, B("coinsub:5", 48, 5, rule10: true), 10));
        Assert.Equal(44, CoinSubSearch.After(41, 48, 3, B("coinsub:5", 48, 6, rule10: true), 10)!.Start);
        Assert.Equal(41, CoinSubSearch.After(41, 48, 0, b, 0)!.Start);
    }

    [Fact]
    public void Needed_adds_the_drains()
    {
        Assert.Equal(1, CoinSubSearch.Needed(new MoneyRules(false, false, []), 0));
        Assert.Equal(2, CoinSubSearch.Needed(new MoneyRules(false, true, []), 0));
        Assert.Equal(1, CoinSubSearch.Needed(new MoneyRules(true, false, []), 1));
        Assert.Equal(2, CoinSubSearch.Needed(new MoneyRules(true, false, []), 2));
        Assert.Equal(3, CoinSubSearch.Needed(new MoneyRules(true, true, []), 2));
        Assert.Equal(1, CoinSubSearch.Needed(null, 5));
    }

    [Fact]
    public void Drain_from_the_third_press_costs_a_second_coin()
    {
        // diff_type >= 7: the third charged press drains a coin first. $4: 1, 1, then 2 for the third = 4.
        var b = B("coinsub:5", 50, 4, drain: true);
        Assert.Equal(44, CoinSubSearch.After(41, 50, 3, b, 0)!.Start);
        Assert.Null(CoinSubSearch.After(41, 50, 3, B("coinsub:5", 50, 3, drain: true), 0));
    }

    // 5: boss rule 14
    [Fact]
    public void Rule_14_raises_the_goal_per_press()
    {
        var b = B("coinsub:5", 45, 5, step: true);
        var state = CoinSubSearch.After(41, 45, 2, b, 14)!;
        Assert.Equal(43, state.Start);
        Assert.Equal(47, state.Goal);
        Assert.Equal(47, state.Rest.Context!.Goal);
        var blind = CoinSubSearch.After(41, 45, 2, B("coinsub:5", 45, 5, step: true, blind: true), 14)!;
        Assert.Equal(46, blind.Goal);
        Assert.False(blind.Rest.Context!.Blind);
        var glass = CoinSubSearch.After(41, 45, 1, B("coinsub:1g", 45, 5, step: true), 14)!;
        Assert.Equal(45, glass.Goal);
    }

    // 6: the requirement examples
    [Fact]
    public void Press_then_division_finds_the_line()
    {
        long start = 39, goal = 48;
        var buttons = B("÷:1, 10:1, X8:1, coinsub:5, =", goal, 5);
        var none = CoinSubSearch.After(start, goal, 0, buttons, 0)!;
        Assert.Empty(ArithmeticSearch.Find(none.Start, none.Goal, none.Rest, 12));
        var noneLines = new List<DivisionShortcut.Line>();
        DivisionShortcut.Search(none.Start, none.Goal, none.Rest, 12, noneLines.Add, CancellationToken.None);
        Assert.Empty(noneLines);

        var state = CoinSubSearch.After(start, goal, 1, buttons, 0)!;
        var lines = new List<ResultLine>();
        DivisionShortcut.Search(state.Start, state.Goal, state.Rest, 12, l =>
        {
            if (CoinSubSearch.Wrap(ResultLine.From(l, state.Rest), state, start, buttons, null, 0) is { } wrapped)
                lines.Add(wrapped);
        }, CancellationToken.None);
        var line = lines.OrderBy(l => l.Cost).First();
        Assert.Equal("$++ (1 press) | / | 10 | = | X8", ButtonLabels.Line(line.Presses.Select(p => p.ToString())));
        Assert.Equal(4, line.Cost);  // = is free: the spec text counts it (5)
        Assert.Contains("(4 press", line.ToString());
    }

    [Fact]
    public void Alone_when_the_presses_reach_the_goal()
    {
        var buttons = B("coinsub:5, =", 44, 5);
        var state = CoinSubSearch.After(41, 44, 3, buttons, 0)!;
        var line = CoinSubSearch.Alone(state, 41, buttons, null)!;
        Assert.Equal("$++ (3 press)", ButtonLabels.Line(line.Presses.Select(p => p.ToString())));
        Assert.Equal(3, line.Cost);
        Assert.Null(CoinSubSearch.Alone(CoinSubSearch.After(41, 44, 2, buttons, 0)!, 41, buttons, null));
        Assert.Null(CoinSubSearch.Alone(state, 41, buttons, null, EngineeringCalculator.SendPlusOneRule));
        // Money for two presses only: three can't be made.
        Assert.Null(CoinSubSearch.After(41, 44, 3, B("coinsub:5, =", 44, 2), 0));
    }

    // 7: outside the 12-press cap
    [Fact]
    public void Rest_line_keeps_all_twelve_presses()
    {
        var buttons = B("coinsub:5, X++:20", 60, 5);
        var state = CoinSubSearch.After(41, 60, 2, buttons, 0)!;
        var presses = Enumerable.Repeat(Press.Of(LeftTransforms.Parse("X++")!), 12).ToList();
        var inner = ResultLine.From(new ArithmeticSearch.Line(null, "43", 0, 55, presses, 12, 0), state.Rest) with { Reaches = 55 };
        var wrapped = CoinSubSearch.Wrap(inner, state, 41, buttons, null, 0)!;
        Assert.Equal(14, wrapped.Cost);
        Assert.Equal(14, wrapped.Presses.Count);
        // A line that doesn't end where it says is rejected.
        Assert.Null(CoinSubSearch.Wrap(inner with { Reaches = 56 }, state, 41, buttons, null, 0));
        // Too many coinsub presses for the charges.
        Assert.Null(CoinSubSearch.Wrap(inner, state, 41, B("coinsub:1, X++:20", 60, 5), null, 0));
    }

    // 8: the line text
    [Fact]
    public void Line_groups_coinsub_runs()
    {
        Assert.Equal("$++ (2 press) | /", ButtonLabels.Line(["coinsub", "coinsub", "÷"]));
        Assert.Equal("$++ (1 press) | X8 | $++ (2 press)", ButtonLabels.Line(["coinsub", "X8", "coinsub", "coinsub"]));
        Assert.Equal("/ | 10 | =", ButtonLabels.Line(["÷", "10", "="]));
    }

    // 9: the near-one passive: a display within 1 of the goal after the presses wins with the bare =
    [Fact]
    public void Near1_bare_send_after_the_presses()
    {
        long start = 40, goal = 44;
        var buttons = B("coinsub:5, =", goal, 5);
        var state = CoinSubSearch.After(start, goal, 3, buttons, 0)!;
        Assert.Equal(43, state.Start);
        var lines = ArithmeticSearch.Find(state.Start, 43, state.Rest, 12, bareSend: false).ToList();
        Assert.Empty(lines);
        var bare = ArithmeticSearch.Find(state.Start, state.Start, state.Rest, 12, bareSend: true)
            .Select(l => CoinSubSearch.Wrap(ResultLine.From(l, state.Rest) with { Reaches = 43 }, state, start, buttons, null, 0))
            .Where(l => l != null).ToList();
        Assert.Single(bare);
        Assert.Equal("$++ (3 press) | =", ButtonLabels.Line(bare[0]!.Presses.Select(p => p.ToString())));
    }
}

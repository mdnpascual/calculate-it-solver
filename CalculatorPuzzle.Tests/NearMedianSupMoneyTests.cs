using CalcHook;

namespace CalculatorPuzzle.Tests;

/// <summary>Spec near-median-sup-money-equal-mw: near, median, sup, money, ==, mD (mw) and AX.</summary>
public class NearMedianSupMoneyTests
{
    static Buttons B(string text, long? goal = null, long? money = null, LineContext? context = null)
    {
        var (buttons, error) = Buttons.Parse(text, null, goal, money);
        Assert.Null(error);
        return context == null ? buttons! : buttons!.WithContext(context);
    }

    static Press D(int d) => new(Key.Digit, d);
    static readonly Press Eq = new(Key.Equals);
    static IReadOnlyList<Press> P(params Press[] presses) => presses;

    static string Last(long start, IReadOnlyList<Press> presses, Buttons buttons) =>
        EngineeringCalculator.Run(start, presses, buttons)![^1];

    static string Show(IReadOnlyList<Press> presses) => string.Join(' ', presses);

    // ---- Transforms ----

    [Theory]
    [InlineData("41", "48")]
    [InlineData("38", "48")]
    [InlineData("58", "48")]
    [InlineData("59", "69")]
    [InlineData("0", "10")]
    [InlineData("05", "15")]
    public void Near_applies(string display, string expected) =>
        Assert.Equal(expected, LeftTransforms.Near(48).Apply(display));

    [Fact]
    public void Near_preimages()
    {
        var near = LeftTransforms.Near(48);
        Assert.Equal(Enumerable.Range(38, 21).Select(v => v.ToString()), near.Preimages("48"));
        Assert.Equal(["20"], near.Preimages("30"));
        Assert.Empty(near.Preimages("60"));
    }

    [Fact]
    public void Near_does_not_clamp() =>
        Assert.Equal("1000005", LeftTransforms.Near(5).Apply("999995"));

    [Theory]
    [InlineData("47", "48")]
    [InlineData("46", "47")]
    [InlineData("49", "49")]
    [InlineData("50", "49")]
    [InlineData("0", "24")]
    public void Median_applies(string display, string expected) =>
        Assert.Equal(expected, LeftTransforms.Median(48).Apply(display));

    [Fact]
    public void Median_preimages()
    {
        var median = LeftTransforms.Median(48);
        Assert.Equal(["47", "48"], median.Preimages("48"));
        Assert.Equal(["49", "50"], median.Preimages("49"));
    }

    [Theory]
    [InlineData("941", "14")]
    [InlineData("99999", "45")]
    [InlineData("0099", "18")]
    [InlineData("123456", null)]
    [InlineData("012345", null)]
    public void Sup_applies(string display, string? expected) =>
        Assert.Equal(expected, LeftTransforms.Parse("sup")!.Apply(display));

    [Fact]
    public void Sup_preimages()
    {
        var sup = LeftTransforms.Parse("sup")!;
        Assert.Equal(["1", "10", "100", "1000", "10000"], sup.Preimages("1"));
        Assert.Empty(sup.Preimages("46"));
        Assert.Equal(["0"], sup.Preimages("0"));
        Assert.All(sup.Preimages("22"), u => Assert.Equal(22, u.Sum(c => c - '0')));
        Assert.Equal(sup.Preimages("22").Select(long.Parse).Order(), sup.Preimages("22").Select(long.Parse));
    }

    [Theory]
    [InlineData("36", "48")]
    [InlineData("0036", "48")]
    [InlineData("12345", null)]
    [InlineData("01234", null)]
    public void Money_applies(string display, string? expected) =>
        Assert.Equal(expected, LeftTransforms.Money(12).Apply(display));

    [Fact]
    public void Money_preimages()
    {
        var money = LeftTransforms.Money(12);
        Assert.Equal(["36"], money.Preimages("48"));
        Assert.Empty(money.Preimages("5"));
        Assert.Equal(["9999"], money.Preimages("10011"));
        Assert.Empty(money.Preimages("10012"));
    }

    [Fact]
    public void Bind_keeps_the_token_and_changes_the_value()
    {
        var near = LeftTransforms.Near(48);
        Assert.Equal(Reads.Goal, near.Reads);
        var rebound = near.Bind!(100);
        Assert.Equal("near", rebound.Token);
        Assert.Equal("100", rebound.Apply("100"));
        Assert.Equal("100", rebound.Apply("95"));
        Assert.Equal("70", rebound.Apply("60"));
        Assert.Equal(near, rebound);
        Assert.Equal(Reads.Money, LeftTransforms.Money(1).Reads);
        Assert.Equal(Reads.None, LeftTransforms.Parse("sup")!.Reads);
        Assert.Null(LeftTransforms.Parse("sup")!.Bind);
    }

    // ---- Parsing ----

    [Fact]
    public void Parse_reads_the_new_buttons()
    {
        var b = B("near:1, median:2, sup:3, money:2, ==:1, AX:1, m5:2", goal: 48, money: 12);
        Assert.Equal(["near", "median", "sup", "money"], b.Transforms.Select(t => t.Token));
        Assert.True(b.AllDigits);
        Assert.True(b.Complement);
        Assert.Equal(5, b.MemoryValue);
        Assert.Equal(1, b.Charges["=="]);
        Assert.Equal(1, b.Charges["AX"]);
        Assert.Equal(2, b.Charges["m"]);
        Assert.Equal(2, b.Charges["median"]);
        Assert.True(b.HasOperandKeys);
    }

    [Fact]
    public void Parse_is_case_insensitive_for_AX() => Assert.True(B("ax:1").Complement);

    [Fact]
    public void Near_median_and_money_need_their_values()
    {
        var b = B("near:1, median:1, money:1");
        Assert.Empty(b.Transforms);
        Assert.True(b.GoalIgnored);
        Assert.True(b.MoneyIgnored);
        Assert.Empty(b.Charges);
    }

    [Fact]
    public void Zero_charges_leave_the_button_out()
    {
        var b = B("near:0, money:0, m5:0, ==:0, AX:0", goal: 48, money: 12);
        Assert.Empty(b.Transforms);
        Assert.Null(b.MemoryValue);
        Assert.False(b.AllDigits || b.Complement);
    }

    [Fact]
    public void Only_one_mw_button_is_allowed() =>
        Assert.NotNull(Buttons.Parse("m5:1, m6:1").Error);

    [Theory]
    [InlineData("m05:1")]
    [InlineData("m1234567:1")]
    public void Bad_mw_numbers_are_errors(string text) => Assert.NotNull(Buttons.Parse(text).Error);

    [Fact]
    public void Tokens_and_operator_keys()
    {
        Assert.Equal("==", Buttons.Token("=="));
        Assert.Equal("AX", Buttons.Token("ax"));
        Assert.Equal("m", Buttons.Token("m5"));
        Assert.Equal("near", Buttons.Token("near"));
        Assert.Equal(Key.AllDigits, Buttons.OperatorKey("=="));
        Assert.Equal(Key.Complement, Buttons.OperatorKey("AX"));
    }

    [Fact]
    public void Equals_alone_is_still_ignored() => Assert.Empty(B("=").Charges);

    // ---- Simulator: == ----

    [Theory]
    [InlineData(483, 7, "777")]
    [InlineData(483, 0, "0")]
    [InlineData(52, 0, "0")]
    [InlineData(5, 8, "8")]
    public void AllDigits_replaces_every_digit(long start, int key, string expected) =>
        Assert.Equal(expected, Last(start, P(new(Key.AllDigits), D(key)), B($"==:1, {key}:1")));

    [Fact]
    public void AllDigits_with_10_uses_the_first_digit()
    {
        var b = B("==:1, 10:1");
        Assert.Equal("111", Last(483, P(new(Key.AllDigits), new(Key.Ten)), b));
    }

    [Fact]
    public void AllDigits_drops_leading_zeros_of_the_left_side() =>
        Assert.Equal("8", Last(0, P(new(Key.Append, 5), new(Key.AllDigits), D(8)), B("X5:1, ==:1, 8:1")));

    [Fact]
    public void AllDigits_needs_one_key_and_no_equals()
    {
        var b = B("==:1, 7:2");
        Assert.Null(EngineeringCalculator.Run(483, P(new(Key.AllDigits), Eq), b));
        Assert.Null(EngineeringCalculator.Run(483, P(new(Key.AllDigits), D(7), Eq), b));
        Assert.Null(EngineeringCalculator.Run(483, P(new(Key.AllDigits), D(7), D(7)), b));
        Assert.Null(EngineeringCalculator.Run(483, P(new(Key.AllDigits), new(Key.Add)), B("==:1, +:1")));
        Assert.Null(EngineeringCalculator.Run(483, P(new(Key.AllDigits), D(7)), B("7:1")));
    }

    // ---- Simulator: AX ----

    [Theory]
    [InlineData(41, 7, "741")]
    [InlineData(4, 7, "74")]
    [InlineData(0, 7, "70")]
    [InlineData(41, 0, "41")]
    public void Complement_prepends_the_key(long start, int key, string expected) =>
        Assert.Equal(expected, Last(start, P(new(Key.Complement), D(key)), B($"AX:1, {key}:1")));

    [Fact]
    public void Complement_prepends_the_whole_number_of_the_key()
    {
        Assert.Equal("1041", Last(41, P(new(Key.Complement), new(Key.Ten)), B("AX:1, 10:1")));
        Assert.Equal("1241", Last(41, P(new(Key.Complement), new(Key.Charge, 12)), B("AX:1, c12:1")));
        Assert.Equal("541", Last(41, P(new(Key.Complement), new(Key.Memory, 5)), B("AX:1, m5:1")));
    }

    [Fact]
    public void Complement_drops_the_leading_zero_of_the_display() =>
        Assert.Equal("74", Last(0, P(new(Key.Append, 4), new(Key.Complement), D(7)), B("X4:1, AX:1, 7:1")));

    [Fact]
    public void Complement_is_invalid_on_a_6_character_display_or_a_7_character_result()
    {
        var b = B("AX:1, 7:1, 10:1");
        Assert.Null(EngineeringCalculator.Run(123456, P(new(Key.Complement), D(7)), b));
        Assert.Null(EngineeringCalculator.Run(12345, P(new(Key.Complement), new(Key.Ten)), b));
        Assert.Equal("712345", Last(12345, P(new(Key.Complement), D(7)), b));
    }

    [Fact]
    public void Complement_needs_one_key_and_no_equals()
    {
        var b = B("AX:1, 7:2");
        Assert.Null(EngineeringCalculator.Run(41, P(new(Key.Complement), Eq), b));
        Assert.Null(EngineeringCalculator.Run(41, P(new(Key.Complement), D(7), Eq), b));
        Assert.Null(EngineeringCalculator.Run(41, P(new(Key.Complement), D(7), D(7)), b));
        Assert.Null(EngineeringCalculator.Run(41, P(new(Key.Add), new(Key.Complement), D(7)), B("AX:1, 7:1, +:1")));
        Assert.Null(EngineeringCalculator.Run(41, P(new(Key.Complement), D(7)), B("7:1")));
    }

    // ---- Simulator: mw ----

    [Fact]
    public void Mw_types_its_number_on_the_right_side()
    {
        var b = B("-:1, m5:2");
        Assert.Equal("48", Last(103, P(new(Key.Subtract), new(Key.Memory, 5), new(Key.Memory, 5), Eq), b));
        Assert.Equal("m5", Show(P(new Press(Key.Memory, 5))));
    }

    [Fact]
    public void Mw_is_invalid_on_the_left_side_or_with_another_number()
    {
        var b = B("-:1, m5:2");
        Assert.Null(EngineeringCalculator.Run(103, P(new Press(Key.Memory, 5)), b));
        Assert.Null(EngineeringCalculator.Run(103, P(new(Key.Subtract), new(Key.Memory, 4)), b));
    }

    [Fact]
    public void Mw_charges_are_its_own()
    {
        var b = B("-:1, m5:1, 5:1");
        Assert.False(b.Fits(P(new(Key.Subtract), new(Key.Memory, 5), new(Key.Memory, 5), Eq)));
        Assert.True(b.Fits(P(new(Key.Subtract), new(Key.Memory, 5), D(5), Eq)));
    }

    // ---- Simulator: sup, money, near, median ----

    [Fact]
    public void Sup_runs_only_on_a_short_display()
    {
        var b = B("sup:2");
        Assert.Equal("14", Last(941, P(Press.Of(LeftTransforms.Parse("sup")!)), b));
        Assert.Null(EngineeringCalculator.Run(123456, P(Press.Of(LeftTransforms.Parse("sup")!)), b));
    }

    // ---- Simulator: the game state a line runs in ----

    [Fact]
    public void Lucky_passive_adds_money_after_an_equals()
    {
        var rules = new MoneyRules(false, false, ["passive_lucky"]);
        var withPassive = B("+:1, 7:1, money:1", 27, 5, new LineContext(27, false, 5, rules));
        var without = B("+:1, 7:1, money:1", 27, 5, new LineContext(27, false, 5, new MoneyRules(false, false, [])));
        var money = Press.Of(LeftTransforms.Money(5));
        var line = P(new(Key.Add), D(7), Eq, money);
        Assert.Equal("27", Last(14, line, withPassive));
        Assert.Equal("26", Last(14, line, without));
    }

    [Fact]
    public void Boss_rule_10_money_pays_before_it_adds()
    {
        var rules = new MoneyRules(false, true, []);
        var b = B("X1:1, money:1", 46, 5, new LineContext(46, false, 5, rules));
        Assert.Equal("44", Last(4, P(new(Key.Append, 1), Press.Of(LeftTransforms.Money(5))), b));
    }

    [Fact]
    public void Difficulty_7_drains_money_from_the_third_charged_press()
    {
        var rules = new MoneyRules(true, false, []);
        var b = B("X1:1, X2:1, money:1", 500, 5, new LineContext(500, false, 5, rules));
        Assert.Equal("416", Last(4, P(new(Key.Append, 1), new(Key.Append, 2), Press.Of(LeftTransforms.Money(5))), b));
    }

    [Theory]
    [InlineData(39, "49")]
    [InlineData(30, "40")]
    public void Boss_rule_14_near_reads_the_raised_goal(long start, string expected)
    {
        var b = B("near:1", 48, null, new LineContext(48, true));
        Assert.Equal(expected, Last(start, P(Press.Of(LeftTransforms.Near(48))), b));
    }

    [Fact]
    public void Without_a_step_rule_near_reads_the_goal()
    {
        var b = B("near:1", 48, null, new LineContext(48));
        Assert.Equal("48", Last(39, P(Press.Of(LeftTransforms.Near(48))), b));
    }

    // ---- Searches ----

    static IReadOnlyList<ArithmeticSearch.Line> Find(long start, long goal, string text, long? money = null, int maxCost = 6)
    {
        var buttons = B(text, goal, money, new LineContext(goal, false, money ?? 0));
        return ArithmeticSearch.Find(start, goal, buttons, maxCost);
    }

    [Theory]
    [InlineData(41, 48, "near:1", null, "near")]
    [InlineData(20, 48, "near:3", null, "near near near")]
    [InlineData(47, 48, "median:1", null, "median")]
    [InlineData(46, 48, "median:2", null, "median median")]
    [InlineData(941, 14, "sup:1", null, "sup")]
    [InlineData(36, 48, "money:1", 12L, "money")]
    [InlineData(483, 777, "==:1, 7:1", null, "== 7")]
    [InlineData(483, 111, "==:1, 10:1", null, "== 10")]
    [InlineData(103, 48, "-:1, m5:2", null, "- m5 m5 =")]
    [InlineData(41, 741, "AX:1, 7:1", null, "AX 7")]
    [InlineData(41, 1041, "AX:1, 10:1", null, "AX 10")]
    [InlineData(4, 74, "AX:1, 7:1", null, "AX 7")]
    [InlineData(0, 70, "AX:1, 7:1", null, "AX 7")]
    public void Arithmetic_finds_the_cheapest_line(long start, long goal, string text, long? money, string expected)
    {
        var lines = Find(start, goal, text, money);
        Assert.NotEmpty(lines);
        Assert.Equal(expected, Show(lines[0].Presses));
    }

    [Theory]
    [InlineData(50, 48, "median:3", null)]
    [InlineData(123456, 21, "sup:1", null)]
    [InlineData(12345, 12357, "money:1", 12L)]
    [InlineData(123456, 7123456, "AX:1, 7:1", null)]
    [InlineData(12345, 1012345, "AX:1, 10:1", null)]
    public void Arithmetic_finds_no_line(long start, long goal, string text, long? money) =>
        Assert.Empty(Find(start, goal, text, money));

    [Fact]
    public void Two_operator_lines_use_the_one_key_buttons()
    {
        // 483 == 7 -> 777, then - 7 7 0 ... : 777 - 29 = 748 needs two segments.
        var buttons = B("==:1, 7:1, -:1, 2:1, 9:1", 748, null, new LineContext(748));
        var lines = TwoOperatorSearch.Find(483, 748, buttons, 8);
        Assert.NotEmpty(lines);
        Assert.Contains(lines, l => Show(l.Presses).StartsWith("== 7 - 2 9 ="));
        Assert.All(lines, l => Assert.Equal("748", Last(483, l.Presses, buttons)));
    }

    [Fact]
    public void Two_operator_lines_use_AX_as_the_second_operator()
    {
        // 41 + 9 = 50, then AX 7 -> 750.
        var buttons = B("+:1, 9:1, AX:1, 7:1", 750, null, new LineContext(750));
        var lines = TwoOperatorSearch.Find(41, 750, buttons, 6);
        Assert.Contains(lines, l => Show(l.Presses) == "+ 9 = AX 7");
    }

    [Fact]
    public void Division_types_the_divisor_with_the_mw_key()
    {
        var buttons = B("÷:1, m3:3", 33, null, new LineContext(33));
        var result = DivisionShortcut.Solve(99, 33, buttons, 6);
        Assert.Null(result.Error);
        Assert.Equal("÷ m3 =", Show(result.Lines[0].Presses));
    }

    [Fact]
    public void Two_operator_lines_type_operands_with_the_mw_key()
    {
        var buttons = B("+:2, m3:3", 46, null, new LineContext(46));
        var lines = TwoOperatorSearch.Find(10, 46, buttons, 8);
        Assert.Contains(lines, l => Show(l.Presses) == "+ m3 = + m3 m3 =");
    }

    [Fact]
    public void Goal_step_search_binds_near_to_goal_plus_k()
    {
        var buttons = B("near:1", 48, null, new LineContext(48, true));
        var lines = new List<ResultLine>();
        GoalStepSearch.Search(39, 48, buttons, RequiredPlan.For(null, false), null, l => lines.Add(l),
            CancellationToken.None, 1, 3);
        Assert.Contains(lines, l => Show(l.Presses) == "near" && l.Reaches == 49);
    }

    // ---- Game link ----

    [Theory]
    [InlineData("operator_near", "near")]
    [InlineData("operator_mediana", "median")]
    [InlineData("operator_sup", "sup")]
    [InlineData("operator_money", "money")]
    [InlineData("operator_equal", "==")]
    [InlineData("operator_complement", "AX")]
    public void MapButton_knows_the_new_ids(string id, string token)
    {
        Assert.Equal(token, GameLink.MapButton(id));
        Assert.Equal(ButtonLabels.Label(token), ButtonSymbols.Of(id));
    }

    static string Puzzle(string buttons, string extra = "") =>
        "{ \"version\": 1, \"seq\": 1, \"time\": \"t\", \"round\": 1, \"start\": 41, \"goal\": 48, " + extra
        + $"\"buttons\": [ {buttons} ] }}";

    [Fact]
    public void Parse_reads_a_stored_mw_number()
    {
        var p = GameLink.Parse(Puzzle("{ \"id\": \"number_mw\", \"uses\": 2, \"stored\": 5 }"))!;
        Assert.Equal("mem5:2", p.ButtonsText);
        Assert.Empty(p.Ignored);
    }

    [Fact]
    public void Parse_ignores_an_empty_mw()
    {
        var p = GameLink.Parse(Puzzle("{ \"id\": \"number_mw\", \"uses\": 2 }"))!;
        Assert.Equal("", p.ButtonsText);
        Assert.Equal(["mw"], p.Ignored);
    }

    [Fact]
    public void Parse_reads_coins_difficulty_and_passives()
    {
        var p = GameLink.Parse(Puzzle("{ \"id\": \"operator_money\", \"uses\": 1 }",
            "\"coins\": 12, \"diffType\": 7, \"coinPassives\": [\"passive_lucky\", \"passive_odd\"], \"bossRule\": 10, "))!;
        Assert.Equal("$:1", p.ButtonsText);
        Assert.Equal(12, p.Coins);
        Assert.Equal(7, p.DiffType);
        Assert.Equal(["passive_lucky", "passive_odd"], p.CoinPassives);
        var rules = p.MoneyRules;
        Assert.True(rules.DrainFromThird);
        Assert.True(rules.Rule10);
        Assert.Equal(["passive_lucky", "passive_odd"], rules.Passives);
    }

    [Fact]
    public void Parse_defaults_when_the_mod_is_older()
    {
        var p = GameLink.Parse(Puzzle("{ \"id\": \"operator_money\", \"uses\": 1 }"))!;
        Assert.Null(p.Coins);
        Assert.Equal(0, p.DiffType);
        Assert.Null(p.CoinPassives);
        Assert.False(p.MoneyRules.DrainFromThird);
        Assert.False(p.MoneyRules.Rule10);
        Assert.Empty(p.MoneyRules.Passives);
    }

    [Fact]
    public void Game_buttons_text_runs_through_Parse()
    {
        var p = GameLink.Parse(Puzzle(
            "{ \"id\": \"operator_near\", \"uses\": 1 }, { \"id\": \"operator_equal\", \"uses\": 1 }, { \"id\": \"number_mw\", \"uses\": 1, \"stored\": 7 }"))!;
        var b = Buttons.Parse(p.ButtonsText, null, 48, 0).Buttons!;
        Assert.Equal(7, b.MemoryValue);
        Assert.True(b.AllDigits);
        Assert.Single(b.Transforms);
    }

    // ---- Overlay symbols ----

    [Fact]
    public void Overlay_shows_the_stored_number_or_mw()
    {
        Assert.Equal("mem5", ButtonSymbols.Of("number_mw", 2, -1, 0, 5));
        Assert.Equal("mem", ButtonSymbols.Of("number_mw", 2, -1, 0, -1));
        Assert.Equal("+-10", ButtonSymbols.Of("operator_near", 1, -1, 0, -1));
    }
}

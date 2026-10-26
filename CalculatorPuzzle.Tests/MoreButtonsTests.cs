namespace CalculatorPuzzle.Tests;

public class MoreButtonsParseTests
{
    static Buttons B(string text, long? last = null) => TestButtons.Parse(text, last).Buttons!;

    [Fact]
    public void Charge_key_c_n_starts_at_n()
    {
        var b = B("c:12");
        Assert.Equal(12, b.ChargeStart);
        Assert.Equal(12, b.Charges["c"]);
    }

    [Fact]
    public void Charge_key_cV_n_has_a_value_and_a_count()
    {
        var b = B("c12:1");
        Assert.Equal(12, b.ChargeStart);
        Assert.Equal(1, b.Charges["c"]);
    }

    [Theory]
    [InlineData("c3:5")]
    [InlineData("c0:1")]
    [InlineData("q:4")]
    public void Bad_charge_and_quest_tokens_are_errors(string text) =>
        Assert.NotNull(Buttons.Parse(text).Error);

    [Fact]
    public void Quest_key_needs_its_digit() =>
        Assert.Contains("digit", Buttons.Parse("q:4").Error);

    [Fact]
    public void Quest_key_qD_n()
    {
        var b = B("unk8:4");
        Assert.Equal(8, b.QuestDigit);
        Assert.Equal(4, b.Charges["q"]);
    }

    [Fact]
    public void Percent_and_remorph_and_new_transforms()
    {
        var b = B("%:1, a->c:1, b->c:1, 100-x:1, 25|69:1");
        Assert.True(b.Percent);
        Assert.True(b.RemorphA);
        Assert.True(b.RemorphB);
        Assert.True(b.Has(Key.Percent));
        Assert.Equal(1, b.Charges["100-X"]);
        Assert.Equal(1, b.Charges["25|69"]);
        Assert.Equal(2, b.Transforms.Count);
    }

    [Fact]
    public void Last_with_a_value_adds_a_transform()
    {
        var b = B("last:1", 37);
        Assert.False(b.LastIgnored);
        Assert.Equal(1, b.Charges["last"]);
        Assert.Equal("37", b.Transforms.Single().Apply("41"));
    }

    [Fact]
    public void Last_without_a_value_is_ignored()
    {
        var b = B("last:1");
        Assert.True(b.LastIgnored);
        Assert.False(b.Charges.ContainsKey("last"));
        Assert.Empty(b.Transforms);
    }

    [Theory]
    [InlineData("volt:0")]
    [InlineData("q8:0")]
    [InlineData("%:0")]
    [InlineData("a->c:0")]
    [InlineData("b->c:0")]
    [InlineData("100-x:0")]
    [InlineData("25|69:0")]
    public void Zero_charges_leave_the_button_out(string text)
    {
        var b = B(text);
        Assert.Empty(b.Charges);
        Assert.Null(b.ChargeStart);
        Assert.Null(b.QuestDigit);
        Assert.False(b.Percent || b.RemorphA || b.RemorphB);
        Assert.Empty(b.Transforms);
    }

    [Theory]
    [InlineData("c", "c")]
    [InlineData("c12", "c")]
    [InlineData("q", "q")]
    [InlineData("q8", "q")]
    [InlineData("last", "last")]
    [InlineData("%", "%")]
    [InlineData("a->c", "a->c")]
    [InlineData("100-x", "100-X")]
    [InlineData("25|69", "25|69")]
    public void Token_names_the_button(string text, string token) => Assert.Equal(token, Buttons.Token(text));

    [Fact]
    public void Fits_counts_charge_presses_under_one_key()
    {
        IReadOnlyList<Press> line = [new(Key.Add), new(Key.Charge, 12), new(Key.Charge, 11), new(Key.Charge, 10), new(Key.Equals)];
        Assert.False(B("+:1, c:2").Fits(line));
        Assert.True(B("+:1, c:3").Fits(line));
    }

    [Fact]
    public void Drained_counts_the_charge_key_once()
    {
        IReadOnlyList<Press> line = [new(Key.Add), new(Key.Charge, 12), new(Key.Charge, 11), new(Key.Equals)];
        Assert.Equal(2, B("+:1, c:2").Drained(line));
    }
}

public class MoreButtonsSimulatorTests
{
    static IReadOnlyList<Press> P(params Press[] presses) => presses;
    static readonly Press Add = new(Key.Add), Eq = new(Key.Equals);
    static Press D(int d) => new(Key.Digit, d);
    static Press C(int v) => new(Key.Charge, v);
    static Press Q(int d) => new(Key.Quest, d);
    static Buttons B(string text, long? last = null) => TestButtons.Parse(text, last).Buttons!;

    [Fact]
    public void Charge_key_types_a_falling_value() =>
        Assert.Equal(["10", "12", "1211", "1221"], EngineeringCalculator.Run(10, P(Add, C(12), C(11), Eq), B("+:1, c:12")));

    [Fact]
    public void Charge_key_with_the_wrong_value_is_invalid()
    {
        Assert.Null(EngineeringCalculator.Run(10, P(Add, C(11), Eq), B("+:1, c:12")));
        Assert.Null(EngineeringCalculator.Run(10, P(Add, C(12), C(12), Eq), B("+:1, c:12")));
    }

    [Fact]
    public void More_charge_presses_than_charges_run_but_do_not_fit()
    {
        var presses = P(Add, C(12), C(11), C(10), Eq);
        var b = B("+:1, c12:2");
        Assert.NotNull(EngineeringCalculator.Run(10, presses, b));
        Assert.False(b.Fits(presses));
    }

    [Fact]
    public void Second_quest_press_is_invalid()
    {
        Assert.NotNull(EngineeringCalculator.Run(10, P(Add, Q(8), Eq), B("+:1, q8:3")));
        Assert.Null(EngineeringCalculator.Run(10, P(Add, Q(8), Q(8), Eq), B("+:1, q8:3")));
    }

    [Fact]
    public void Quest_key_needs_the_shown_digit() =>
        Assert.Null(EngineeringCalculator.Run(10, P(Add, Q(3), Eq), B("+:1, q8:3")));

    [Fact]
    public void Charge_and_quest_keys_are_right_side_only()
    {
        Assert.Null(EngineeringCalculator.Run(10, P(C(12)), B("+:1, c:12")));
        Assert.Null(EngineeringCalculator.Run(10, P(Q(8)), B("+:1, q8:1")));
    }

    [Fact]
    public void Remorph_a_replaces_the_tens_with_the_key()
    {
        Assert.Equal(["483", "103"], EngineeringCalculator.Run(483, P(new(Key.RemorphA), new(Key.Ten)), B("a->c:1, 10:1")));
        Assert.Equal("73", EngineeringCalculator.Run(483, P(new(Key.RemorphA), D(7)), B("a->c:1, 7:1"))![^1]);
        Assert.Equal("78", EngineeringCalculator.Run(48, P(new(Key.RemorphA), D(7)), B("a->c:1, 7:1"))![^1]);
    }

    [Fact]
    public void Remorph_b_replaces_the_last_digit()
    {
        Assert.Equal("487", EngineeringCalculator.Run(483, P(new(Key.RemorphB), D(7)), B("b->c:1, 7:1"))![^1]);
        Assert.Equal("490", EngineeringCalculator.Run(480, P(new(Key.RemorphB), new(Key.Ten)), B("b->c:1, 10:1"))![^1]);
    }

    [Fact]
    public void Remorph_with_the_charge_key() =>
        Assert.Equal("123", EngineeringCalculator.Run(483, P(new(Key.RemorphA), C(12)), B("a->c:1, c:12"))![^1]);

    [Fact]
    public void Remorph_needs_one_key_and_no_equals()
    {
        var b = B("a->c:1, 7:1");
        Assert.Null(EngineeringCalculator.Run(483, P(new(Key.RemorphA), Eq), b));
        Assert.Null(EngineeringCalculator.Run(483, P(new(Key.RemorphA), D(7), Eq), b));
        Assert.Null(EngineeringCalculator.Run(483, P(new(Key.RemorphA), D(7), D(7)), b));
    }

    [Fact]
    public void Remorph_is_left_side_only() =>
        Assert.Null(EngineeringCalculator.Run(5, P(Add, new(Key.RemorphA)), B("+:1, a->c:1")));

    [Fact]
    public void Percent_95_of_25() =>
        Assert.Equal(["95", "2", "25", "23"], EngineeringCalculator.Run(95, P(new(Key.Percent), D(2), D(5), Eq), B("%:1, 2:1, 5:1")));

    [Fact]
    public void Percent_result_above_2_to_the_24_is_invalid()
    {
        Assert.Null(EngineeringCalculator.Run(999_999, P(new(Key.Percent), D(9), D(9), D(9), D(9), Eq), B("%:1, 9:1")));
        Assert.NotNull(EngineeringCalculator.Run(1_000, P(new(Key.Percent), D(9), D(0), Eq), B("%:1, 9:1, 0:1")));
    }

    [Fact]
    public void Percent_needs_an_operand() =>
        Assert.Null(EngineeringCalculator.Run(95, P(new(Key.Percent), Eq), B("%:1")));

    [Fact]
    public void New_transforms_run_in_the_simulator()
    {
        Assert.Equal("59", EngineeringCalculator.Run(41, P(Press.Of(LeftTransforms.Parse("100-x")!)), B("100-x:1"))![^1]);
        Assert.Equal("5296", EngineeringCalculator.Run(2569, P(Press.Of(LeftTransforms.Parse("25|69")!)), B("25|69:1"))![^1]);
        Assert.Equal("37", EngineeringCalculator.Run(41, P(Press.Of(LeftTransforms.Last(37))), B("last:1", 37))![^1]);
    }

    [Fact]
    public void Transforms_on_the_right_side_are_invalid() =>
        Assert.Null(EngineeringCalculator.Run(41, P(Add, Press.Of(LeftTransforms.Parse("100-x")!)), B("+:1, 100-x:1")));
}

public class MoreTransformTests
{
    static string? Apply(string token, string display) => LeftTransforms.Parse(token)!.Apply(display);

    [Theory]
    [InlineData("41", "59")]
    [InlineData("100", "0")]
    [InlineData("150", "0")]
    [InlineData("05", "95")]
    public void Hundred_minus_x(string display, string expected) => Assert.Equal(expected, Apply("100-x", display));

    [Theory]
    [InlineData("2569", "5296")]
    [InlineData("120", "150")]
    [InlineData("41", "41")]
    public void Swap_25_69(string display, string expected) => Assert.Equal(expected, Apply("25|69", display));

    [Fact]
    public void Last_ignores_the_display() =>
        Assert.Equal("37", LeftTransforms.Last(37).Apply("anything"));

    [Fact]
    public void Hundred_minus_x_preimages()
    {
        var t = LeftTransforms.Parse("100-x")!;
        Assert.Equal(["59"], t.Preimages("41"));
        Assert.Equal(["100", "101", "102"], t.Preimages("0").Take(3));
        Assert.Equal(["0"], t.Preimages("100"));
        Assert.Empty(t.Preimages("101"));
    }

    [Theory]
    [InlineData("25|69", "5296")]
    [InlineData("25|69", "41")]
    [InlineData("100-x", "41")]
    [InlineData("100-x", "0")]
    public void Preimages_map_back(string token, string target)
    {
        var t = LeftTransforms.Parse(token)!;
        foreach (var pre in t.Preimages(target).Take(5))
            Assert.Equal(target, t.Apply(pre));
    }
}

public class MoreButtonsOperandTests
{
    static Buttons B(string text) => TestButtons.Parse(text).Buttons!;
    static string Text(IReadOnlyList<Press>? p) => p == null ? "null" : string.Join(' ', p);

    [Fact]
    public void Charge_keys_spell_a_number() =>
        Assert.Equal("c12 c11", Text(DivisionShortcut.TypeOperand("1211", B("c:12"))));

    [Fact]
    public void Charge_key_runs_out() =>
        Assert.Null(DivisionShortcut.TypeOperand("1211", B("c12:1")));

    [Fact]
    public void Quest_key_types_its_digit_once()
    {
        Assert.Equal("q8", Text(DivisionShortcut.TypeOperand("8", B("q8:1"))));
        Assert.Null(DivisionShortcut.TypeOperand("88", B("q8:1")));
    }

    [Fact]
    public void Cheapest_divisor_can_use_the_charge_key() =>
        Assert.Equal("c5", Text(DivisionShortcut.CheapestDivisor(5, 5, B("c:5"), 3)!.Value.Presses));

    [Fact]
    public void Minimum_allows_one()
    {
        Assert.Null(DivisionShortcut.CheapestDivisor(1, 1, B("1"), 3));
        Assert.Equal(1, DivisionShortcut.CheapestDivisor(1, 1, B("1"), 3, minimum: 1)!.Value.Divisor);
    }

    [Fact]
    public void Division_by_the_charge_key()
    {
        var lines = DivisionShortcut.Solve(50, 10, B("÷:1, c:5"), 6).Lines;
        Assert.Equal("÷ c5 =", string.Join(' ', lines[0].Presses));
    }

    [Fact]
    public void Division_by_the_quest_key()
    {
        var lines = DivisionShortcut.Solve(41, 8, B("÷:1, q5:3"), 6).Lines;
        Assert.Equal("÷ q5 =", string.Join(' ', lines[0].Presses));
    }
}

public class MoreButtonsSearchTests
{
    static Buttons B(string text, long? last = null) => TestButtons.Parse(text, last).Buttons!;
    static string First(long s, long g, Buttons b) =>
        string.Join(' ', ArithmeticSearch.Find(s, g, b, 12)[0].Presses);

    [Fact]
    public void Percent_95_to_23() => Assert.Equal("% 2 5 =", First(95, 23, B("%:1, 2:1, 5:1")));

    [Fact]
    public void Remorph_a_483_to_73() => Assert.Equal("a->c 7", First(483, 73, B("a->c:1, 7:1")));

    [Fact]
    public void Remorph_b_483_to_487() => Assert.Equal("b->c 7", First(483, 487, B("b->c:1, 7:1")));

    [Fact]
    public void Charge_key_in_an_addition() => Assert.Equal("+ c12 c11 =", First(10, 1221, B("+:1, c:12")));

    [Fact]
    public void Hundred_minus_x_41_to_59() => Assert.Equal("100-X", First(41, 59, B("100-x:1")));

    [Fact]
    public void Last_41_to_37() => Assert.Equal("last", First(41, 37, B("last:1", 37)));

    [Fact]
    public void Swap_2569() => Assert.Equal("25|69", First(2569, 5296, B("25|69:1")));

    [Fact]
    public void Quest_key_in_an_addition() => Assert.Equal("+ q8 =", First(10, 18, B("+:1, q8:2")));

    [Fact]
    public void Lines_respect_the_quest_key_limit() =>
        Assert.DoesNotContain(ArithmeticSearch.Find(10, 98, B("+:1, q8:3"), 12),
            l => l.Presses.Count(p => p.Key == Key.Quest) > 1);

    [Fact]
    public void Percent_lines_are_checked_in_the_simulator()
    {
        var b = B("%:1, 2:1, 5:1, 0:1");
        foreach (var l in ArithmeticSearch.Find(95, 23, b, 8))
            Assert.Equal("23", EngineeringCalculator.Run(95, l.Presses, b)![^1]);
    }

    [Fact]
    public void Modulo_search_can_type_with_the_charge_key()
    {
        var b = B("mod:1, 4X:1, 3X:1, c:9");
        foreach (var l in ModuloSearch.Find(41, 8, b, 8).Lines)
            Assert.Equal("8", EngineeringCalculator.Run(41, l.Presses, b)![^1]);
    }
}

public class MoreButtonsGameLinkTests
{
    static string Json(string buttons, string extra = "") =>
        "{ \"version\": 1, \"seq\": 1, \"time\": \"\", \"round\": 1, \"start\": 41, \"goal\": 48, " + extra
        + $"\"buttons\": [ {buttons} ] }}";

    static GamePuzzle Parse(string buttons, string extra = "") => GameLink.Parse(Json(buttons, extra))!;

    [Fact]
    public void Charge_key_carries_its_value() =>
        Assert.Equal("volt12:12", Parse("{ \"id\": \"number_charge\", \"uses\": 12 }").ButtonsText);

    [Fact]
    public void Glass_charge_key_has_one_press() =>
        Assert.Equal("volt12:1g", Parse("{ \"id\": \"number_charge\", \"uses\": 12, \"glass\": true }").ButtonsText);

    [Fact]
    public void Empty_charge_key_is_left_out() =>
        Assert.Equal("volt:0", Parse("{ \"id\": \"number_charge\", \"uses\": 0 }").ButtonsText);

    [Fact]
    public void Quest_key_carries_its_digit() =>
        Assert.Equal("unk8:4", Parse("{ \"id\": \"number_quest\", \"uses\": 4, \"digit\": 8 }").ButtonsText);

    [Fact]
    public void Quest_key_without_a_digit_is_ignored()
    {
        var p = Parse("{ \"id\": \"number_quest\", \"uses\": 4 }");
        Assert.Equal("", p.ButtonsText);
        Assert.Equal(["quest"], p.Ignored);
    }

    [Fact]
    public void Other_new_ids_map()
    {
        var p = Parse("{ \"id\": \"operator%\", \"uses\": 1 }, { \"id\": \"operator_remorph_a\", \"uses\": 2 }, "
            + "{ \"id\": \"operator_remorph_b\", \"uses\": 3 }, { \"id\": \"operator_100-x\", \"uses\": 4 }, "
            + "{ \"id\": \"operator_flip\", \"uses\": 5 }, { \"id\": \"operator_last\", \"uses\": 6 }");
        Assert.Equal("%:1, a->c:2, b->c:3, 100-X:4, mirror:5, last:6", p.ButtonsText);
        Assert.Null(Buttons.Parse(p.ButtonsText, 7).Error);
    }

    [Fact]
    public void Last_is_read_when_present()
    {
        Assert.Equal(37, Parse("{ \"id\": \"operator/\", \"uses\": 1 }", "\"last\": 37, ").Last);
        Assert.Null(Parse("{ \"id\": \"operator/\", \"uses\": 1 }").Last);
    }

    [Fact]
    public void Required_charge_and_quest_keys()
    {
        Assert.Equal("c", Parse("{ \"id\": \"operator/\", \"uses\": 1 }", "\"required\": \"number_charge\", ").Required);
        Assert.Equal("q", Parse("{ \"id\": \"operator/\", \"uses\": 1 }", "\"required\": \"number_quest\", ").Required);
    }
}

public class MoreButtonsRequiredTests
{
    [Fact]
    public void A_line_with_charge_presses_uses_c()
    {
        IReadOnlyList<Press> line = [new(Key.Add), new(Key.Charge, 12), new(Key.Charge, 11), new(Key.Equals)];
        Assert.True(Buttons.Uses(line, "c"));
        Assert.False(Buttons.Uses(line, "q"));
    }

    [Fact]
    public void Last_can_be_required()
    {
        IReadOnlyList<Press> line = [Press.Of(LeftTransforms.Last(37))];
        Assert.True(Buttons.Uses(line, "last"));
    }
}

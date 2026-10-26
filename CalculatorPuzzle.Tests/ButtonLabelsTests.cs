using System.Text.Json;
using CalcHook;
using CalculatorPuzzle;

namespace CalculatorPuzzle.Tests;

public class ButtonLabelsTests
{
    [Theory]
    [InlineData("money", "$")]
    [InlineData("flip", "reverse")]
    [InlineData("round", "->)")]
    [InlineData("sup", "combine")]
    [InlineData("sqr", "X²")]
    [InlineData("median", "_2_")]
    [InlineData("25|69", "mirror")]
    [InlineData("==", "equalize")]
    [InlineData("<<", "X<<")]
    [InlineData(">>", "X>>")]
    [InlineData("near", "+-10")]
    [InlineData("reset", "x->0")]
    [InlineData("to25", "x->25")]
    [InlineData("c", "volt")]
    [InlineData("c12", "volt12")]
    [InlineData("q", "unk")]
    [InlineData("q8", "unk8")]
    [InlineData("m5", "mem5")]
    [InlineData("mw", "mem")]
    [InlineData("÷", "÷")]
    [InlineData("p", "p")]
    [InlineData("X4", "X4")]
    public void Label_follows_the_table(string token, string label) => Assert.Equal(label, ButtonLabels.Label(token));

    static readonly string[] Ids = Enumerable.Range(0, 11).Select(i => "number" + i)
        .Concat(new[]
        {
            "number_charge", "number_mw", "number_quest", "operator+", "operator-", "operator*", "operator/", "operator%", "operator_mod",
            "operator_3x", "operator_x4", "operator_0to8", "operator_+16", "operator_-4", "operator_++", "operator_--", "operator_*2",
            "operator_/2", "operator_sqr", "operator_sqrt", "operator_100-x", "operator_reverse", "operator_flip", "operator_<",
            "operator_>", "operator_swap", "operator_last", "operator_round", "operator_near", "operator_complement",
            "operator_mediana", "operator_prime", "operator_sup", "operator_to0", "operator_to25", "operator_remorph_a",
            "operator_remorph_b", "operator_money", "operator_equal",
        }).ToArray();

    [Fact]
    public void Token_reverses_Label_for_every_mapped_token_and_live_form()
    {
        var tokens = Ids.Select(GameLink.MapButton).Where(t => t != null).Select(t => t!)
            .Concat(new[] { "c12", "q8", "m5", "c", "q" }).ToList();
        foreach (var t in tokens)
            Assert.Equal(t, ButtonLabels.Token(ButtonLabels.Label(t)));
    }

    [Fact]
    public void Token_ignores_case_and_leaves_tokens_and_unknown_text()
    {
        Assert.Equal("c12", ButtonLabels.Token("VOLT12"));
        Assert.Equal("sqr", ButtonLabels.Token("x²"));
        Assert.Equal("sqr", ButtonLabels.Token("X²"));
        Assert.Equal("flip", ButtonLabels.Token("flip"));
        Assert.Equal("c12", ButtonLabels.Token("c12"));
        Assert.Equal("banana", ButtonLabels.Token("banana"));
    }

    [Fact]
    public void No_label_equals_a_different_buttons_token()
    {
        var tokens = Ids.Select(GameLink.MapButton).Where(t => t != null).Select(t => t!).ToHashSet();
        foreach (var t in tokens)
        {
            var label = ButtonLabels.Label(t);
            if (label != t)
                Assert.DoesNotContain(label, tokens);
        }
    }

    [Fact]
    public void Buttons_Parse_takes_labels_like_tokens()
    {
        var labels = Buttons.Parse("volt12:12, x²:3, $:1, reverse:2, mirror:1, equalize:1, x<<:1, x>>:1, +-10:1, x->25:1, x->0:1, ->):1, combine:1, _2_:1, unk8:1, mem5:1",
            last: null, goal: 50, money: 7);
        var tokens = Buttons.Parse("c12:12, sqr:3, money:1, flip:2, 25|69:1, ==:1, <<:1, >>:1, near:1, to25:1, reset:1, round:1, sup:1, median:1, q8:1, m5:1",
            last: null, goal: 50, money: 7);
        Assert.Null(labels.Error);
        Assert.Null(tokens.Error);
        Assert.Equal(tokens.Buttons!.Charges.OrderBy(p => p.Key), labels.Buttons!.Charges.OrderBy(p => p.Key));
    }

    [Fact]
    public void Buttons_Token_takes_labels()
    {
        Assert.Equal("flip", Buttons.Token("reverse"));
        Assert.Equal("c", Buttons.Token("volt"));
        Assert.Equal("sqr", Buttons.Token("X²"));
    }

    [Fact]
    public void Line_shows_labels_and_slash()
    {
        Assert.Equal("volt12 | volt11 | / | =", ButtonLabels.Line(new[] { "c12", "c11", "÷", "=" }));
        Assert.Equal("8X | / | 10 | = | reverse", ButtonLabels.Line(new[] { "8X", "÷", "10", "=", "flip" }));
    }

    [Fact]
    public void ResultLine_ToString_shows_labels()
    {
        var presses = new[] { new Press(Key.Flip), new Press(Key.Divide) };
        var line = new ResultLine(presses, 2, 0, 0, null, null);
        Assert.StartsWith("reverse | /   (2 press", line.ToString());
    }

    [Fact]
    public void ResultLine_ToString_short_words()
    {
        var presses = new[] { new Press(Key.Flip), new Press(Key.Divide) };
        var line = new ResultLine(presses, 2, 1, 7, null, null, Missing: true, Required: "X8", Reaches: 49);
        Assert.EndsWith("(2 press, goal 49, spare 7, empty 1, no X8)", line.ToString());
        Assert.DoesNotContain("empty", (line with { Drained = 0 }).ToString());
    }

    [Fact]
    public void Parse_errors_use_labels()
    {
        Assert.Equal("Unknown key needs its digit (write unkD:n)", Buttons.Parse("q:1").Error);
        Assert.StartsWith("Volt key", Buttons.Parse("c0:1").Error);
        Assert.Equal("Volt key volt2 can't have more than 2 presses", Buttons.Parse("c2:3").Error);
        Assert.Equal("Only one mem button (memD) is allowed", Buttons.Parse("m5:1, m6:1").Error);
    }

    [Fact]
    public void GameLink_writes_labels_and_the_text_parses_back()
    {
        const string json = """
            {"version":1,"seq":1,"time":"t","round":1,"start":41,"goal":48,"buttons":[
              {"id":"number_charge","uses":12},{"id":"operator_sqr","uses":3},{"id":"operator_money","uses":2},
              {"id":"number_quest","uses":1,"digit":8},{"id":"number_mw","uses":1,"stored":5},{"id":"operator_reverse","uses":2}]}
            """;
        var puzzle = GameLink.Parse(json)!;
        Assert.Equal("volt12:12, X²:3, $:2, unk8:1, mem5:1, reverse:2", puzzle.ButtonsText);
        var (buttons, error) = Buttons.Parse(puzzle.ButtonsText, null, 48, 10);
        Assert.Null(error);
        Assert.Equal(12, buttons!.Charges["c"]);
        Assert.Equal(3, buttons.Charges["sqr"]);
        Assert.Equal(2, buttons.Charges["money"]);
        Assert.Equal(2, buttons.Charges["flip"]);
    }
}

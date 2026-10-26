namespace CalculatorPuzzle.Tests;

public class GameLinkTests
{
    [Theory]
    [InlineData("operator_5x", "5X")]
    [InlineData("operator_0x", "0X")]
    [InlineData("operator_x1", "X1")]
    [InlineData("operator_x9", "X9")]
    [InlineData("number10", "10")]
    [InlineData("number0", "0")]
    [InlineData("number7", "7")]
    [InlineData("operator/", "÷")]
    [InlineData("operator_mod", "mod")]
    [InlineData("operator_reverse", "flip")]
    [InlineData("operator_to0", "reset")]
    [InlineData("operator_prime", "p")]
    [InlineData("operator_random", null)]
    [InlineData("operator_near", "near")]
    [InlineData("operator_mediana", "median")]
    [InlineData("operator_sup", "sup")]
    [InlineData("operator_money", "money")]
    [InlineData("operator_equal", "==")]
    [InlineData("operator_complement", "AX")]
    [InlineData("number_mw", "m")]
    [InlineData("number11", null)]
    [InlineData("", null)]
    public void MapButton_follows_the_table(string id, string? token) =>
        Assert.Equal(token, GameLink.MapButton(id));

    const string Sample = """
        {
          "version": 1, "seq": 7, "time": "2026-10-22T15:14:49.0200000+02:00",
          "round": 3, "start": 64, "goal": 37,
          "calculator": "Calculator_Engineering", "scene": "Game",
          "buttons": [
            { "id": "operator_5x", "label": "5X", "uses": 2 },
            { "id": "operator_x1", "label": "X1", "uses": 2 },
            { "id": "number10", "label": "10|13", "uses": 13, "glass": false, "legendary": false },
            { "id": "operator/", "label": "÷", "uses": 6 },
            { "id": "operator_random", "label": "P", "uses": 1 },
            { "id": "operator_mod", "label": "mod", "uses": 8 }
          ]
        }
        """;

    [Fact]
    public void Parse_reads_the_sample()
    {
        var p = GameLink.Parse(Sample)!;

        Assert.Equal(7, p.Seq);
        Assert.Equal(3, p.Round);
        Assert.Equal(64, p.Start);
        Assert.Equal(37, p.Goal);
        Assert.Equal("5X:2, X1:2, 10:13, ÷:6, mod:8", p.ButtonsText);
        Assert.Equal(["random"], p.Ignored);
        Assert.Null(Buttons.Parse(p.ButtonsText).Error);
    }

    static string WithRequired(string requiredJson) =>
        "{ \"version\": 1, \"seq\": 1, \"time\": \"\", \"round\": 1, \"start\": 41, \"goal\": 48, "
        + $"\"challenge\": 8, \"required\": {requiredJson}, \"buttons\": [ {{ \"id\": \"operator/\", \"uses\": 3 }} ] }}";

    [Theory]
    [InlineData("\"operator/\"", "÷")]
    [InlineData("\"operator_reverse\"", "flip")]
    [InlineData("\"operator_near\"", "near")]
    [InlineData("null", null)]
    [InlineData("\"\"", null)]
    [InlineData("7", null)]
    public void Parse_reads_required(string requiredJson, string? expected) =>
        Assert.Equal(expected, GameLink.Parse(WithRequired(requiredJson))!.Required);

    static string WithWarp(string warpJson) =>
        Sample.Replace("\"round\": 3,", $"\"warp\": {warpJson}, \"round\": 3,");

    [Fact]
    public void Parse_reads_warp() => Assert.Equal(2, GameLink.Parse(WithWarp("2"))!.Warp);

    [Fact]
    public void Parse_without_warp_gives_0() => Assert.Equal(0, GameLink.Parse(Sample)!.Warp);

    [Theory]
    [InlineData("-1")]
    [InlineData("\"x\"")]
    public void Parse_bad_warp_gives_0(string warpJson) => Assert.Equal(0, GameLink.Parse(WithWarp(warpJson))!.Warp);

    [Fact]
    public void Parse_without_required_field() =>
        Assert.Null(GameLink.Parse(Sample)!.Required);

    static string One(string id, int uses, string? glass) =>
        "{ \"version\": 1, \"seq\": 1, \"time\": \"\", \"round\": 1, \"start\": 41, \"goal\": 48, \"buttons\": [ "
        + $"{{ \"id\": \"{id}\", \"uses\": {uses}{(glass == null ? "" : $", \"glass\": {glass}")} }} ] }}";

    [Fact]
    public void Parse_clamps_glass_to_one_charge()
    {
        var p = GameLink.Parse(One("operator_x1", 4, "true"))!;
        Assert.Equal("X1:1g", p.ButtonsText);
        Assert.Equal(["X1"], p.Glass);
    }

    [Fact]
    public void Parse_keeps_normal_charges()
    {
        var p = GameLink.Parse(One("operator_x1", 4, "false"))!;
        Assert.Equal("X1:4", p.ButtonsText);
        Assert.Empty(p.Glass);
    }

    [Fact]
    public void Parse_without_glass_field_is_not_glass() =>
        Assert.Equal("X1:4", GameLink.Parse(One("operator_x1", 4, null))!.ButtonsText);

    [Fact]
    public void Parse_keeps_zero_for_glass() =>
        Assert.Equal("X1:0", GameLink.Parse(One("operator_x1", 0, "true"))!.ButtonsText);

    [Fact]
    public void Parse_ignores_unmapped_glass()
    {
        var p = GameLink.Parse(One("operator_random", 2, "true"))!;
        Assert.Equal(["random"], p.Ignored);
        Assert.Empty(p.Glass);
    }

    [Fact]
    public void Glass_division_fits_only_once()
    {
        var p = GameLink.Parse(One("operator/", 3, "true"))!;
        var buttons = Buttons.Parse(p.ButtonsText).Buttons!;
        Assert.Equal("÷:1g", p.ButtonsText);
        Assert.True(buttons.Fits([new Press(Key.Divide)]));
        Assert.False(buttons.Fits([new Press(Key.Divide), new Press(Key.Divide)]));
    }

    [Theory]
    [InlineData("{ \"version\": 2, \"seq\": 1, \"time\": \"\", \"round\": 1, \"start\": 1, \"goal\": 2, \"buttons\": [] }")]
    [InlineData("{ not json")]
    [InlineData("{ \"version\": 1 }")]
    [InlineData("")]
    public void Parse_returns_null_for_bad_input(string json) =>
        Assert.Null(GameLink.Parse(json));
}

public class ArithmeticMapButtonTests
{
    [Theory]
    [InlineData("operator+", "+")]
    [InlineData("operator-", "-")]
    [InlineData("operator*", "×")]
    public void Maps_operators(string id, string token) => Assert.Equal(token, GameLink.MapButton(id));
}

public class TransformIdTests
{
    [Theory]
    [InlineData("operator_++", "X++")]
    [InlineData("operator_--", "X--")]
    [InlineData("operator_*2", "X*2")]
    [InlineData("operator_/2", "X/2")]
    [InlineData("operator_<", "<<")]
    [InlineData("operator_>", ">>")]
    [InlineData("operator_sqr", "sqr")]
    [InlineData("operator_sqrt", "sqrt")]
    [InlineData("operator_to25", "to25")]
    [InlineData("operator_round", "round")]
    [InlineData("operator_+10", "X+10")]
    [InlineData("operator_-64", "X-64")]
    [InlineData("operator_0to8", "0->8")]
    [InlineData("operator_last", "last")]
    [InlineData("operator_100-x", "100-X")]
    [InlineData("operator%", "%")]
    [InlineData("number_charge", "c")]
    [InlineData("number_quest", "q")]
    [InlineData("operator_remorph_a", "a->c")]
    [InlineData("operator_remorph_b", "b->c")]
    [InlineData("operator_flip", "25|69")]
    public void Ids_map(string id, string? token) => Assert.Equal(token, GameLink.MapButton(id));
}

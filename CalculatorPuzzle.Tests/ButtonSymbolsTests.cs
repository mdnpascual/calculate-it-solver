using System.Text.RegularExpressions;
using CalcHook;
using CalculatorPuzzle;

namespace CalculatorPuzzle.Tests;

public class ButtonSymbolsTests
{
    // Every id in docs/button-id.md (first table).
    static readonly string[] Ids =
    {
        "number0", "number1", "number2", "number3", "number4", "number5", "number6", "number7", "number8", "number9",
        "number10", "number11", "number15", "number32", "number99", "number_charge", "number_mw", "number_quest", "number_test",
        "operator+", "operator-", "operator*", "operator/", "operator%", "operator_mod",
        "operator_3x", "operator_4x", "operator_5x", "operator_6x", "operator_8x",
        "operator_x1", "operator_x2", "operator_x4", "operator_x7", "operator_x9",
        "operator_+2", "operator_+4", "operator_+8", "operator_+10", "operator_+16", "operator_+32", "operator_+64",
        "operator_-4", "operator_-8", "operator_-10", "operator_-16", "operator_-32", "operator_-64",
        "operator_++", "operator_--", "operator_*2", "operator_/2", "operator_sqr", "operator_sqrt", "operator_100-x",
        "operator_0to8", "operator_1to5", "operator_2to4", "operator_3to0", "operator_4to7",
        "operator_5to6", "operator_6to2", "operator_7to3", "operator_8to9", "operator_9to1",
        "operator_reverse", "operator_flip", "operator_<", "operator_>", "operator_cut", "operator_swap", "operator_last",
        "operator_round", "operator_near", "operator_complement", "operator_mediana", "operator_prime", "operator_sup",
        "operator_to0", "operator_to25", "operator_random", "operator_energy", "operator_resurrect",
        "operator_remorph_a", "operator_remorph_b", "operator_reroll", "operator_bomb", "operator_money", "operator_credit",
        "operator_coinadd", "operator_coinsub", "operator_send", "operator_warp", "operator_secret", "operator_equal", "operator_tag",
    };

    [Fact]
    public void Doc_list_matches_the_ids_in_button_id_md()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "docs", "button-id.md")))
            dir = Path.GetDirectoryName(dir)!;
        Assert.NotNull(dir);
        string text = File.ReadAllText(Path.Combine(dir, "docs", "button-id.md"));
        var docIds = new List<string>();
        foreach (Match m in Regex.Matches(text, @"^\| `((?:number|operator)[^`]*)` \|", RegexOptions.Multiline))
            if (!docIds.Contains(m.Groups[1].Value)) docIds.Add(m.Groups[1].Value);
        Assert.Equal(docIds.OrderBy(x => x, StringComparer.Ordinal), Ids.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void Of_matches_MapButton_or_names_the_unmapped_id()
    {
        foreach (string id in Ids)
        {
            string? token = GameLink.MapButton(id);
            if (token != null) Assert.Equal(ButtonLabels.Label(token), ButtonSymbols.Of(id));
            else Assert.StartsWith("(", ButtonSymbols.Of(id));
        }
    }

    [Theory]
    [InlineData("operator/", "÷")]
    [InlineData("operator*", "×")]
    [InlineData("operator_x4", "X4")]
    [InlineData("operator_4x", "4X")]
    [InlineData("operator_3to0", "3->0")]
    [InlineData("operator_+16", "X+16")]
    [InlineData("operator_reverse", "reverse")]
    [InlineData("number10", "10")]
    [InlineData("number11", "(11)")]
    [InlineData("operator_energy", "(energy)")]
    [InlineData("operator%", "%")]
    [InlineData("number_charge", "volt")]
    [InlineData("number_quest", "unk")]
    [InlineData("operator_last", "last")]
    [InlineData("operator_remorph_a", "a->c")]
    [InlineData("operator_remorph_b", "b->c")]
    [InlineData("operator_100-x", "100-X")]
    [InlineData("operator_flip", "mirror")]
    [InlineData("operator_prime", "p")]
    [InlineData("number_mw", "mem")]
    [InlineData("operator_equal", "equalize")]
    [InlineData("operator_near", "+-10")]
    [InlineData("operator_complement", "AX")]
    [InlineData("operator_swap", "swap")]
    public void Of_spot_checks(string id, string expected) => Assert.Equal(expected, ButtonSymbols.Of(id));

    [Theory]
    [InlineData("number_charge", 12, -1, "volt12")]
    [InlineData("number_charge", 0, -1, "volt")]
    [InlineData("number_quest", 4, 8, "unk8")]
    [InlineData("number_quest", 4, -1, "unk")]
    [InlineData("operator%", 3, -1, "%")]
    public void Of_with_live_values(string id, int uses, int digit, string expected) =>
        Assert.Equal(expected, ButtonSymbols.Of(id, uses, digit));

    [Fact]
    public void Of_shows_the_nearest_prime_for_the_prime_key()
    {
        Assert.Equal("p8081", ButtonSymbols.Of("operator_prime", 1, -1, 8084));
        Assert.Equal("volt12", ButtonSymbols.Of("number_charge", 12, -1, 8084));
    }

    [Fact]
    public void Label_copy_equals_ButtonLabels_Label()
    {
        var tokens = Ids.Select(GameLink.MapButton).Where(t => t != null).Select(t => t!)
            .Concat(new[] { "c", "c12", "q", "q8", "m", "m5", "mw", "p8081" });
        foreach (var t in tokens)
            Assert.Equal(ButtonLabels.Label(t), ButtonSymbols.Label(t));
    }

    [Fact]
    public void Of_with_live_values_for_the_mw_key()
    {
        Assert.Equal("mem5", ButtonSymbols.Of("number_mw", 2, -1, 0, 5));
        Assert.Equal("mem", ButtonSymbols.Of("number_mw", 2, -1, 0, -1));
    }

    [Fact]
    public void NearestPrime_equals_Primes_Nearest()
    {
        for (long v = 0; v <= 20_000; v++)
            Assert.Equal(Primes.Nearest(v), ButtonSymbols.NearestPrime(v));
        foreach (long v in new[] { 999_999, 1_000_000_000_000, 100_000_000_000_000_000, 4_000_000_000_000_000_000 })
            Assert.Equal(Primes.Nearest(v), ButtonSymbols.NearestPrime(v));
    }
}

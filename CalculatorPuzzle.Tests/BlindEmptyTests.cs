namespace CalculatorPuzzle.Tests;

public class BlindEmptyTests
{
    static Buttons B(string text, bool blind, long money = 0)
    {
        var buttons = Buttons.Parse(text, goal: 46, money: money).Buttons!;
        return buttons.WithContext(new LineContext(46, false, money, null, blind));
    }

    static Press Plus() => Press.Of(LeftTransforms.Parse("X++")!);

    static ResultLine Line(Buttons b, params Press[] presses) =>
        ResultLine.From(new ArithmeticSearch.Line(null, "41", 0, 41, presses, presses.Length, 0), b);

    // 1
    [Fact]
    public void Blind_off_counts_every_press()
    {
        Assert.Equal(1, B("X++:1", false).Drained([Plus()]));
    }

    // 2
    [Fact]
    public void Blind_on_first_press_is_free()
    {
        Assert.Equal(0, B("X++:1", true).Drained([Plus()]));
    }

    // 3
    [Fact]
    public void Blind_on_free_press_is_one_of_the_uses()
    {
        Assert.Equal(0, B("X++:2", true).Drained([Plus(), Plus()]));
        Assert.Equal(1, B("X++:1, X8:1", true).Drained([Plus(), new Press(Key.Append, 8)]));
    }

    // 4
    [Fact]
    public void Glass_first_press_leaves_the_free_press_to_the_next()
    {
        var b = B("X1:1g, X8:1", true);
        Assert.Equal(1, b.Drained([new Press(Key.Append, 1), new Press(Key.Append, 8)]));
    }

    // 5
    [Fact]
    public void Ranking_prefers_the_line_that_empties_nothing_with_blind()
    {
        var b = B("X++:1, X8:1", true);
        var free = Line(b, Plus());
        var real = Line(b, new Press(Key.Append, 8), Plus()) with { Cost = 1 };
        Assert.Equal(0, free.Drained);
        Assert.Equal(1, real.Drained);
        Assert.True(ResultLine.Ranking.Compare(free, real) < 0);

        var off = B("X++:1, X8:1", false);
        Assert.Equal(1, Line(off, Plus()).Drained);
        Assert.Equal(2, Line(off, new Press(Key.Append, 8), Plus()).Drained);
    }

    // 6
    [Fact]
    public void Swap_wrap_counts_swap_as_the_free_press()
    {
        var b = B("swap:1, X++:1", true);
        var inner = Line(b.Without("swap"), Plus());
        var wrapped = SwapSearch.Wrap(inner, 41, b, null)!;
        Assert.Equal(1, wrapped.Drained);
    }

    // 7
    [Fact]
    public void Coinsub_wrap_counts_the_first_coinsub_as_the_free_press()
    {
        var b = B("coinsub:1, X++:1", true, money: 5);
        var state = CoinSubSearch.After(41, 43, 1, b, 0)!;
        var inner = ResultLine.From(new ArithmeticSearch.Line(null, "42", 0, 43, [Plus()], 1, 0), state.Rest);
        var wrapped = CoinSubSearch.Wrap(inner, state, 41, b, null, 0)!;
        Assert.Equal(1, wrapped.Drained);
        Assert.Equal(2, CoinSubSearch.Wrap(inner, state, 41, B("coinsub:1, X++:1", false, money: 5), null, 0)!.Drained);
    }

    // 8
    [Fact]
    public void ToString_has_no_empty_for_the_free_press()
    {
        Assert.DoesNotContain("empty", Line(B("X++:1", true), Plus()).ToString());
        Assert.Contains("empty 1", Line(B("X++:1", false), Plus()).ToString());
    }
}

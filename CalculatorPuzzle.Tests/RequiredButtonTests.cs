namespace CalculatorPuzzle.Tests;

public class RequiredButtonTests
{
    [Theory]
    [InlineData("/", "÷")]
    [InlineData("÷", "÷")]
    [InlineData("x1", "X1")]
    [InlineData(" 8x ", "8X")]
    [InlineData("flip", "flip")]
    [InlineData("mod", "mod")]
    [InlineData("=", null)]
    [InlineData("foo", null)]
    [InlineData("", null)]
    [InlineData("÷, flip", null)]
    [InlineData("flip:2", null)]
    public void Token_gives_the_canonical_spelling(string text, string? expected) =>
        Assert.Equal(expected, Buttons.Token(text));

    static readonly IReadOnlyList<Press> EightXDivideFlip =
        [new(Key.Prepend, 8), new(Key.Divide), new(Key.Ten), new(Key.Equals), new(Key.Flip)];

    [Fact]
    public void Uses_finds_a_pressed_button()
    {
        Assert.True(Buttons.Uses(EightXDivideFlip, "flip"));
        Assert.True(Buttons.Uses(EightXDivideFlip, "÷"));
        Assert.False(Buttons.Uses(EightXDivideFlip, "X8"));
        Assert.False(Buttons.Uses(EightXDivideFlip, "="));
    }

    static ResultLine Line(int cost, bool missing, string? required = "flip", params Press[] presses) =>
        new(presses, cost, 0, 0, null, null, null, missing, required);

    [Fact]
    public void Ranking_puts_lines_with_the_required_button_first()
    {
        var withFlip = Line(4, false);
        var cheaper = Line(3, true);
        var lines = new List<ResultLine> { cheaper, withFlip };
        lines.Sort(ResultLine.Ranking);
        Assert.Same(withFlip, lines[0]);
    }

    [Fact]
    public void Ranking_without_a_required_button_is_by_cost()
    {
        var four = Line(4, false, null);
        var three = Line(3, false, null);
        var lines = new List<ResultLine> { four, three };
        lines.Sort(ResultLine.Ranking);
        Assert.Same(three, lines[0]);
    }

    [Fact]
    public void ToString_marks_a_missing_line()
    {
        Assert.EndsWith(", no reverse)", Line(3, true, "flip", new Press(Key.Divide)).ToString());
        Assert.EndsWith(", no /)", Line(3, true, "÷", new Press(Key.Flip)).ToString());
        Assert.DoesNotContain("no ", Line(3, false, "flip", new Press(Key.Flip)).ToString());
    }

    [Fact]
    public void Find_ranks_the_flip_line_first_for_41_to_48()
    {
        var (buttons, error) = Buttons.Parse("÷:3, 10:13, X8:2, 8X:2, flip:2");
        Assert.Null(error);
        const string required = "flip";

        var lines = new List<ResultLine>();
        lines.AddRange(ArithmeticSearch.Find(41, 48, buttons!, 12).Select(l => ResultLine.From(l, buttons!, required)));
        lines.AddRange(DivisionShortcut.Solve(41, 48, buttons!, 12).Lines.Select(l => ResultLine.From(l, buttons!, required)));
        lines.Sort(ResultLine.Ranking);

        Assert.False(lines[0].Missing);
        Assert.Contains(lines, l => string.Join(' ', l.Presses) == "8X ÷ 10 = flip");
        var plain = lines.Single(l => string.Join(' ', l.Presses) == "÷ 10 = X8");
        Assert.True(plain.Missing);
        Assert.True(lines.IndexOf(plain) > lines.FindLastIndex(l => !l.Missing));
        Assert.EndsWith("no reverse)", plain.ToString());
    }
}

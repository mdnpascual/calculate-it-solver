namespace CalculatorPuzzle.Tests;

public class ButtonsParseTests
{
    [Fact]
    public void Parses_a_simple_list()
    {
        var (b, error) = TestButtons.Parse("4X, 8X, X4, 10, ÷");

        Assert.Null(error);
        Assert.Equal([4, 8], b!.PrependDigits.Order());
        Assert.Equal([4], b.AppendDigits.Order());
        Assert.True(b.Ten);
        Assert.True(b.Divide);
        Assert.False(b.Reset);
        Assert.False(b.Mod);
        Assert.False(b.Flip);
    }

    [Fact]
    public void Ignores_case_and_spaces_and_accepts_slash()
    {
        var (b, error) = TestButtons.Parse("4x,x4 , /, FLIP");

        Assert.Null(error);
        Assert.Equal([4], b!.PrependDigits);
        Assert.Equal([4], b.AppendDigits);
        Assert.True(b.Divide);
        Assert.True(b.Flip);
    }

    [Fact]
    public void Accepts_duplicates_and_equals()
    {
        var (b, error) = TestButtons.Parse("4X, 4X, =");

        Assert.Null(error);
        Assert.Equal([4], b!.PrependDigits);
    }

    [Fact]
    public void Default_text_parses()
    {
        var (b, error) = TestButtons.Parse("5X, X1, X2, X7, X4, 4X, 6X, 3X, 8X, 10, /, X9, flip, mod, =, reset");

        Assert.Null(error);
        Assert.Equal([3, 4, 5, 6, 8], b!.PrependDigits.Order());
        Assert.Equal([1, 2, 4, 7, 9], b.AppendDigits.Order());
        Assert.True(b.Ten && b.Divide && b.Mod && b.Flip && b.Reset);
    }

    [Theory]
    [InlineData("Y4")]
    [InlineData("44X")]
    public void Unknown_token_is_named_in_the_error(string token)
    {
        var (b, error) = Buttons.Parse($"10:9, {token}");

        Assert.Null(b);
        Assert.Contains($"\"{token}\"", error);
    }

    [Fact]
    public void Empty_text_gives_an_empty_button_set()
    {
        var (b, error) = TestButtons.Parse("");

        Assert.Null(error);
        Assert.Empty(b!.PrependDigits);
        Assert.Empty(b.AppendDigits);
        Assert.False(b.Ten);
        Assert.False(b.Divide);
    }

    [Fact]
    public void Parses_digit_keys_apart_from_prepend_and_append()
    {
        var (b, error) = TestButtons.Parse("3, 7, 10, ÷");

        Assert.Null(error);
        Assert.Equal([3, 7], b!.DigitKeys.Order());
        Assert.True(b.Ten);
        Assert.True(b.Divide);
        Assert.Empty(b.PrependDigits);
        Assert.Empty(b.AppendDigits);

        var (x3, _) = TestButtons.Parse("X3");
        Assert.Equal([3], x3!.AppendDigits);
        Assert.Empty(x3.DigitKeys);
    }

    [Theory]
    [InlineData("25")]
    [InlineData("100")]
    public void Multi_digit_strings_other_than_10_are_unknown(string token)
    {
        var (b, error) = Buttons.Parse($"10:9, {token}");

        Assert.Null(b);
        Assert.Contains($"\"{token}\"", error);
    }

    [Fact]
    public void Default_text_has_no_digit_keys()
    {
        var (b, error) = TestButtons.Parse("5X, X1, 10, /, flip, mod, =, reset");

        Assert.Null(error);
        Assert.Empty(b!.DigitKeys);
    }
}

public class EngineeringCalculatorTests
{
    static Press P(int d) => new(Key.Prepend, d);
    static Press A(int d) => new(Key.Append, d);
    static readonly Press Ten = new(Key.Ten);
    static readonly Press Div = new(Key.Divide);
    static readonly Press Eq = new(Key.Equals);
    static readonly Press Reset = new(Key.Reset);
    static readonly Press Flip = new(Key.Flip);

    static string Last(long start, params Press[] presses) => EngineeringCalculator.Run(start, presses)![^1];

    [Fact]
    public void Worked_example_41_to_48()
    {
        var log = EngineeringCalculator.Run(41, [A(1), P(8), Div, Ten, Ten, Eq, P(4)]);

        Assert.Equal(["411", "8411", "8411", "10", "1010", "8", "48"], log);
    }

    [Fact]
    public void Append_keeps_leading_zeros_and_prepend_drops_them()
    {
        Assert.Equal("04", Last(41, Reset, A(4)));
        Assert.Equal("041", Last(41, Reset, A(4), A(1)));
        Assert.Equal("14", Last(41, Reset, A(4), P(1)));
        Assert.Equal("10", Last(41, Reset, P(1)));
    }

    [Fact]
    public void Flip_reverses_the_number()
    {
        Assert.Equal("14", Last(41, Flip));
        Assert.Equal("321", Last(123, Flip));
        Assert.Equal("21", Last(120, Flip));
        Assert.Equal("121", Last(120, Flip, P(1)));
        Assert.Equal("2", Last(120, Flip, Div, Ten, Eq));
        Assert.Equal("4", Last(41, Reset, A(4), Flip));
    }

    [Fact]
    public void Division_drops_the_fraction()
    {
        Assert.Equal("8", Last(8411, Div, Ten, Ten, Eq));
        Assert.Equal("4", Last(41, Div, Ten, Eq));
    }

    [Theory]
    [InlineData(Key.Prepend)]
    [InlineData(Key.Append)]
    [InlineData(Key.Reset)]
    [InlineData(Key.Flip)]
    [InlineData(Key.Divide)]
    public void Left_side_buttons_are_invalid_after_divide(Key key)
    {
        Assert.Null(EngineeringCalculator.Run(41, [Div, new Press(key, 4)]));
    }

    [Fact]
    public void Ten_is_valid_after_divide()
    {
        Assert.NotNull(EngineeringCalculator.Run(41, [Div, Ten]));
    }

    [Fact]
    public void Edits_work_again_after_equals()
    {
        Assert.Equal("48", Last(41, Div, Ten, Eq, A(8)));
        Assert.Equal("48", Last(85, Div, Ten, Eq, P(4)));
        Assert.Equal("48", Last(841, Div, Ten, Eq, Flip));
    }

    [Fact]
    public void Equals_with_no_operand_is_invalid()
    {
        Assert.Null(EngineeringCalculator.Run(41, [Div, Eq]));
        Assert.Null(EngineeringCalculator.Run(41, [Eq]));
    }

    [Fact]
    public void A_missing_button_makes_Run_return_null()
    {
        var (buttons, _) = TestButtons.Parse("4X, 10, ÷");

        Assert.NotNull(EngineeringCalculator.Run(41, [P(4)], buttons));
        Assert.Null(EngineeringCalculator.Run(41, [P(8)], buttons));
        Assert.Null(EngineeringCalculator.Run(41, [Flip], buttons));
    }

    [Fact]
    public void Cost_does_not_count_equals()
    {
        Assert.Equal(3, EngineeringCalculator.Cost([Div, Ten, Eq, A(8)]));
    }

    [Fact]
    public void Digit_key_is_invalid_on_the_left()
    {
        Assert.Null(EngineeringCalculator.Run(41, [new Press(Key.Digit, 3)]));
    }

    [Fact]
    public void Ten_is_invalid_on_the_left()
    {
        Assert.Null(EngineeringCalculator.Run(41, [new Press(Key.Ten)]));
    }

    [Fact]
    public void Digit_keys_are_invalid_after_equals()
    {
        Assert.Null(EngineeringCalculator.Run(41, [new Press(Key.Divide), new Press(Key.Ten), new Press(Key.Equals), new Press(Key.Digit, 8)]));
        Assert.Null(EngineeringCalculator.Run(41, [new Press(Key.Divide), new Press(Key.Ten), new Press(Key.Equals), new Press(Key.Ten)]));
    }

    [Fact]
    public void Digit_keys_build_the_operand()
    {
        var presses = new[] { new Press(Key.Divide), new Press(Key.Digit, 4), new Press(Key.Digit, 5), new Press(Key.Equals) };

        Assert.Equal(["531", "4", "45", "11"], EngineeringCalculator.Run(531, presses));
    }

    [Fact]
    public void Ten_and_digit_keys_mix_in_the_operand()
    {
        var presses = new[] { new Press(Key.Divide), new Press(Key.Ten), new Press(Key.Digit, 1) };

        Assert.Equal(["531", "10", "101"], EngineeringCalculator.Run(531, presses));
    }

    [Fact]
    public void Missing_digit_key_is_invalid()
    {
        var (buttons, _) = TestButtons.Parse("4, 10, ÷");

        Assert.Null(EngineeringCalculator.Run(41, [new Press(Key.Divide), new Press(Key.Digit, 3)], buttons));
        Assert.NotNull(EngineeringCalculator.Run(41, [new Press(Key.Divide), new Press(Key.Digit, 4)], buttons));
    }

    [Fact]
    public void Digit_press_prints_as_the_bare_digit()
    {
        Assert.Equal("3", new Press(Key.Digit, 3).ToString());
    }
}

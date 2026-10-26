namespace CalculatorPuzzle.Tests;

public class LeftEditsTests
{
    static Buttons B(string text) => TestButtons.Parse(text).Buttons!;

    static string Show(IReadOnlyList<Press>? plan) => plan == null ? "none" : string.Join(' ', plan);

    static void AssertPlanReaches(string from, string to, Buttons buttons)
    {
        var plan = LeftEdits.Plan(from, to, buttons);
        Assert.NotNull(plan);
        var log = EngineeringCalculator.Run(long.Parse(from), plan, buttons);
        Assert.NotNull(log);
        Assert.Equal(to, log.Count == 0 ? from : log[^1]);
    }

    [Fact]
    public void Without_flip_41_to_8411_takes_two_presses()
    {
        var buttons = B("8X, X1, 10");
        var plan = LeftEdits.Plan("41", "8411", buttons);

        Assert.Equal(2, plan!.Count);
        AssertPlanReaches("41", "8411", buttons);
    }

    [Fact]
    public void Without_flip_41_to_48_is_impossible_when_no_button_makes_an_8()
    {
        Assert.Null(LeftEdits.Plan("41", "48", B("4X, X4, 10")));
    }

    [Fact]
    public void Flip_lets_a_prepend_stand_in_for_an_append()
    {
        var buttons = B("8X, flip");

        Assert.Equal("8X flip", Show(LeftEdits.Plan("4", "48", buttons)));
        AssertPlanReaches("4", "48", buttons);
    }

    [Fact]
    public void Flip_lets_an_append_stand_in_for_a_prepend()
    {
        Assert.Equal("X2 flip", Show(LeftEdits.Plan("7", "27", B("X2, flip"))));
    }

    [Fact]
    public void A_run_of_prepends_costs_two_extra_presses_with_flip()
    {
        var buttons = B("X2, X3, flip");

        Assert.Equal("flip X2 X3 flip", Show(LeftEdits.Plan("45", "3245", buttons)));
        AssertPlanReaches("45", "3245", buttons);
    }

    [Fact]
    public void A_single_digit_core_is_its_own_reverse()
    {
        var buttons = B("X2, X3, flip");

        Assert.Equal(3, LeftEdits.Plan("1", "321", buttons)!.Count);
        AssertPlanReaches("1", "321", buttons);
    }

    [Fact]
    public void Zeros_are_padded_with_flip_not_with_0X()
    {
        Assert.Equal("flip X0 X4 flip", Show(LeftEdits.Plan("31", "4031", B("flip, X0, X4"))));
        Assert.Null(LeftEdits.Plan("31", "4031", B("0X, 4X")));
        Assert.Null(LeftEdits.Plan("41", "1041", B("0X, 1X")));
        Assert.Equal("flip X0", Show(LeftEdits.Plan("12", "210", B("flip, X0"))));
    }

    [Fact]
    public void Same_string_needs_no_presses()
    {
        Assert.Empty(LeftEdits.Plan("48", "48", B(""))!);
    }

    [Theory]
    [InlineData("41", "548541")]
    [InlineData("120", "10210")]
    [InlineData("531", "11531")]
    [InlineData("7", "2507")]
    public void Every_plan_runs_to_its_target(string from, string to)
    {
        var buttons = Buttons.All;
        AssertPlanReaches(from, to, buttons);
    }

    [Fact]
    public void Digit_key_is_not_a_left_edit()
    {
        Assert.Equal("none", Show(LeftEdits.Plan("4", "48", B("8"))));
    }

    [Fact]
    public void Ten_is_not_a_left_edit()
    {
        Assert.Equal("X1 X0", Show(LeftEdits.Plan("41", "4110", B("X1, X0, 10"))));
        Assert.Equal("none", Show(LeftEdits.Plan("41", "4110", B("10"))));
    }

    [Fact]
    public void Digit_key_is_ignored_when_Xd_exists()
    {
        Assert.Equal("X8", Show(LeftEdits.Plan("4", "48", B("X8, 8"))));
    }
}

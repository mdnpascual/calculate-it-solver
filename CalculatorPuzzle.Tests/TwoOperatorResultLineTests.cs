namespace CalculatorPuzzle.Tests;

public class TwoOperatorResultLineTests
{
    static readonly IReadOnlyList<Press> Presses = [new(Key.Add), new(Key.Digit, 1), new(Key.Equals)];

    static ResultLine Two(int order = 0) =>
        new(Presses, 3, 0, 5, null, null, null, TwoOperator: new TwoOperatorSearch.Line(Key.Add, 1, 2, Key.Add, 1, 3, Presses, 3, order));

    static ResultLine Arithmetic() =>
        new(Presses, 3, 0, 5, null, null, new ArithmeticSearch.Line(Key.Add, "1", 1, 1, Presses, 3, 0));

    static ResultLine Division() => new(Presses, 3, 0, 5, new DivisionShortcut.Line("1", 1, 10, 1, Presses, 3), null);

    static ResultLine Modulo() => new(Presses, 3, 0, 5, null, new ModuloSearch.Line(1, 1, 1, 1, Presses, 3, 0));

    [Fact]
    public void Ranking_puts_arithmetic_before_two_operator_before_division_before_modulo()
    {
        var sorted = new[] { Modulo(), Division(), Two(), Arithmetic() }.Order(ResultLine.Ranking).ToList();
        Assert.NotNull(sorted[0].Arithmetic);
        Assert.NotNull(sorted[1].TwoOperator);
        Assert.NotNull(sorted[2].Division);
        Assert.NotNull(sorted[3].Modulo);
    }

    [Fact]
    public void Two_operator_rows_keep_their_own_order()
    {
        var sorted = new[] { Two(2), Two(0), Two(1) }.Order(ResultLine.Ranking).Select(l => l.TwoOperator!.Order).ToList();
        Assert.Equal([0, 1, 2], sorted);
    }
}

namespace CalculatorPuzzle;

/// <summary>One row of the Lines box: an arithmetic, two-operator, division or modulo line, with its drained count and spare.</summary>
public record ResultLine(IReadOnlyList<Press> Presses, int Cost, int Drained, int Spare,
                         DivisionShortcut.Line? Division, ModuloSearch.Line? Modulo,
                         ArithmeticSearch.Line? Arithmetic = null,
                         bool Missing = false, string? Required = null, long? Reaches = null,
                         TwoOperatorSearch.Line? TwoOperator = null,
                         SchoolSearch.Line? School = null, string? Credit = null, bool Swapped = false)
{
    // School: a School mathematics line. Credit: the operators_used entry a Just a one line sets ("÷", "digit", ...); it orders the lines but isn't printed.
    // Reaches: the goal the line ends at when it differs from G (boss rule 14, the near-one passive, or a swap line, which ends at S).
    // Swapped: a flipped-leg line; its first press is swap (SwapSearch.Wrap).
    // Missing: a required button is set and the line never presses it (Market Demand: round reward $0).
    public static ResultLine From(ArithmeticSearch.Line line, Buttons buttons, string? required = null) =>
        new(line.Presses, line.Cost, buttons.Drained(line.Presses), buttons.Spare(line.Presses), null, null, line,
            Lacks(line.Presses, required), required);

    public static ResultLine From(DivisionShortcut.Line line, Buttons buttons, string? required = null) =>
        new(line.Presses, line.Cost, buttons.Drained(line.Presses), buttons.Spare(line.Presses), line, null, null,
            Lacks(line.Presses, required), required);

    public static ResultLine From(ModuloSearch.Line line, Buttons buttons, string? required = null) =>
        new(line.Presses, line.Cost, buttons.Drained(line.Presses), buttons.Spare(line.Presses), null, line, null,
            Lacks(line.Presses, required), required);

    public static ResultLine From(TwoOperatorSearch.Line line, Buttons buttons, string? required = null) =>
        new(line.Presses, line.Cost, buttons.Drained(line.Presses), buttons.Spare(line.Presses), null, null, null,
            Lacks(line.Presses, required), required, TwoOperator: line);

    public static ResultLine From(SchoolSearch.Line line, Buttons buttons) =>
        new(line.Presses, line.Cost, buttons.Drained(line.Presses), buttons.Spare(line.Presses), null, null, School: line);

    static bool Lacks(IReadOnlyList<Press> presses, string? required) =>
        required != null && !Buttons.Uses(presses, required);

    /// <summary>Lines that press the required button first, then cost (low first), drained (few first), spare (high first), arithmetic before two-operator before division before modulo, then each kind's own order.</summary>
    public static IComparer<ResultLine> Ranking { get; } = Comparer<ResultLine>.Create((x, y) =>
    {
        int c = x.Missing.CompareTo(y.Missing);
        if (c != 0) return c;
        c = x.Cost.CompareTo(y.Cost);
        if (c != 0) return c;
        c = x.Drained.CompareTo(y.Drained);
        if (c != 0) return c;
        c = y.Spare.CompareTo(x.Spare);
        if (c != 0) return c;
        c = Kind(x).CompareTo(Kind(y));
        if (c != 0) return c;
        if (x.Arithmetic != null)
            return x.Arithmetic.Order.CompareTo(y.Arithmetic!.Order);
        if (x.TwoOperator != null)
            return x.TwoOperator.Order.CompareTo(y.TwoOperator!.Order);
        if (x.Division != null && y.Division != null)
            return DivisionShortcut.Ranking.Compare(x.Division, y.Division);
        if (x.School != null)
            return 0;
        return x.Modulo!.Order.CompareTo(y.Modulo!.Order);
    });

    static int Kind(ResultLine l) => l.Arithmetic != null ? 0 : l.TwoOperator != null ? 1 : l.Division != null ? 2 : l.School != null ? 4 : 3;

    public override string ToString() =>
        $"{ButtonLabels.Line(Presses.Select(p => p.ToString()))}   ({Cost} press{(Reaches != null ? $", goal {Reaches}" : "")}, spare {Spare}{(Drained > 0 ? $", empty {Drained}" : "")}{(Missing ? $", no {ButtonLabels.Label(Required ?? "")}" : "")})".Replace('÷', '/');
}
